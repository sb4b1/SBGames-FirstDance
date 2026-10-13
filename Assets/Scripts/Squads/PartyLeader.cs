using System.Collections.Generic;
using SB4b1.Logging;
using SB4b1.Utils;
using SBGames.Characters.AI;
using SBGames.Dungeon.Markers;
using SBGames.Personality;
using UnityEngine;

public enum CheckpointDecision
{
    Advance = 0,
    Recover = 1,
    Regroup = 2,
    Retreat = 3
}

public enum CheckpointReason
{
    None = 0,
    FavorableMargin = 1,
    NeedsRecoveryBeforeAdvance = 2,
    PartyScattered = 3,
    SafetyMarginNotMet = 4,
    KeyRoleLost = 5,
    RetreatBlockedFallback = 6
}

[DisallowMultipleComponent]
public class PartyLeader : MonoBehaviour
{
    private static readonly SquadOrderType[] DecisionToOrderTable = new SquadOrderType[]
    {
        SquadOrderType.FollowLeader, // Advance (0) -> upgraded to ScoutAhead if CanScout
        SquadOrderType.Recover,      // Recover (1) -> maps to SquadOrderType.Recover
        SquadOrderType.Standby,      // Regroup (2) -> maps to Standby
        SquadOrderType.FallBack      // Retreat (3) -> maps to FallBack
    };

    [Header("Squad Configuration")]
    [SerializeField] private bool allowSuccession = true;
    [SerializeField] private float leashDistance = 8f;
    [SerializeField] private float fallBackHealthThreshold = 0.3f;

    [Header("Formation Configuration")]
    [SerializeField] private SquadFormationConfigSO formationConfig;
    [SerializeField] private FunctionFormationSlot[] formationSlots = SquadFormationConfigSO.CreateDefaultSlotsArray();
    [SerializeField] private Vector3 marchingForward = Vector3.up;

    [Header("Checkpoint & Succession (Phase 4)")]
    [SerializeField] private LeaderCheckpointTuningSO checkpointTuning;
    [SerializeField] private StandInPartyRewardProvider standInRewardProvider = new();
    [SerializeField, Min(0)] private int recoveryItemsRemaining = 1;

    [Header("Runtime State")]
    [SerializeField] private SquadBlackboard blackboard = new();
    [SerializeField] private List<SquadMember> members = new(8);

    private CharacterStatsManager statsManager;
    private BaseAI leaderBaseAI;
    private SquadMember activeScout;
    private SquadMember scoutMember;
    private Coroutine scoutPanicCoroutine;
    private Coroutine successionCoroutine;
    private IPartyRewardProvider customRewardProvider;
    private bool hadTankInParty;
    private bool hadHealerInParty;
    private bool hasActiveCheckpointOrder;
    private bool isLeaderDeathSubscribed;
    private bool isSuccessionPending;
    private PartyLeader lastPromotedLeader;

    public SquadBlackboard Blackboard => blackboard;
    public List<SquadMember> Members => members;
    public float LeashDistance => leashDistance;
    public float FallBackHealthThreshold => fallBackHealthThreshold;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;
    public bool IsEscaped { get; set; }
    public SquadMember ActiveScout => activeScout;
    public SquadFormationConfigSO FormationConfig { get => formationConfig; set => formationConfig = value; }
    public LeaderCheckpointTuningSO CheckpointTuning { get => checkpointTuning; set => checkpointTuning = value; }
    public IPartyRewardProvider RewardProvider
    {
        get => customRewardProvider ?? standInRewardProvider;
        set => customRewardProvider = value;
    }
    public StandInPartyRewardProvider StandInRewardProvider => standInRewardProvider;
    public int RecoveryItemsRemaining
    {
        get => recoveryItemsRemaining;
        set => recoveryItemsRemaining = Mathf.Max(0, value);
    }
    public bool RetreatBlocked
    {
        get => blackboard != null && blackboard.RetreatBlocked;
        set
        {
            if (blackboard != null)
            {
                blackboard.RetreatBlocked = value;
            }
        }
    }
    public bool HasActiveCheckpointOrder
    {
        get => hasActiveCheckpointOrder;
        set => hasActiveCheckpointOrder = value;
    }
    public CheckpointDecision LastCheckpointDecision { get; private set; } = CheckpointDecision.Advance;
    public CheckpointReason LastCheckpointReason { get; private set; } = CheckpointReason.None;
    public bool IsSuccessionPending => isSuccessionPending || successionCoroutine != null;
    public PartyLeader LastPromotedLeader => lastPromotedLeader;

    public AdventurerAIProfile LeaderProfile
    {
        get
        {
            if (leaderBaseAI == null)
            {
                leaderBaseAI = GetComponentInChildren<BaseAI>();
            }
            return leaderBaseAI != null ? leaderBaseAI.AdventurerProfile : null;
        }
    }

    public AdventurerFunction EffectiveLeaderFunction
    {
        get
        {
            if (statsManager == null)
            {
                statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
            }
            return statsManager != null ? statsManager.EffectiveFunction : AdventurerFunction.DPS;
        }
    }

    public Vector3 MarchingForward
    {
        get => marchingForward.sqrMagnitude > 0.001f ? marchingForward.normalized : transform.up;
        set => marchingForward = value.sqrMagnitude > 0.001f ? value.normalized : Vector3.up;
    }

    /// <summary>
    /// The living squad member whose <see cref="AdventurerFunction"/> is <see cref="AdventurerFunction.Scout"/>, or null.
    /// Automatically updated when members join, leave, or die.
    /// </summary>
    public SquadMember ScoutMember
    {
        get
        {
            if (scoutMember != null && scoutMember.IsAlive && scoutMember.CharacterFunction == AdventurerFunction.Scout && members.Contains(scoutMember))
            {
                return scoutMember;
            }

            RefreshScoutMember();
            return scoutMember;
        }
    }

    /// <summary>
    /// True when the party has a living member whose function is <see cref="AdventurerFunction.Scout"/>.
    /// </summary>
    public bool CanScout => ScoutMember != null;

    [Header("Room Loop (Phase 6)")]
    [SerializeField] private RoomLoopState currentRoomLoopState = RoomLoopState.None;
    [SerializeField] private RoomVolume currentRoom;
    [SerializeField] private DoorMarker targetDoor;
    [SerializeField] private float stateTimer = 0f;
    private DoorwayStagingController doorwayStagingController;

    public RoomLoopState CurrentRoomLoopState => currentRoomLoopState;
    public RoomVolume CurrentRoom => currentRoom;
    public DoorMarker TargetDoor => targetDoor;
    public RoomLoopTuningSO EffectiveRoomLoopTuning => LeaderProfile != null ? LeaderProfile.RoomLoopTuning : null;

    public event System.Action<SquadMember> OnScoutDispatched;
    public event System.Action<SquadMember> OnScoutDebriefed;
    public event System.Action<SquadMember> OnScoutEliminated;
    public event System.Action<PartyLeader> OnLeaderDied;
    public event System.Action<PartyLeader, PartyLeader> OnLeaderChanged;
    public event System.Action<CheckpointDecision, CheckpointReason> OnCheckpointDecision;
    public event System.Action<RoomVolume> OnRoomEntered;
    public event System.Action<RoomVolume> OnRoomCleared;
    public event System.Action<RoomLoopState> OnRoomLoopStateChanged;

    private void Awake()
    {
        if (formationSlots == null || formationSlots.Length == 0)
        {
            formationSlots = SquadFormationConfigSO.CreateDefaultSlotsArray();
        }
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        if (statsManager != null && isLeaderDeathSubscribed)
        {
            statsManager.OnCharacterDeath -= HandleLeaderDeath;
            isLeaderDeathSubscribed = false;
        }
    }

    private void OnDestroy()
    {
        DisbandSquad();
    }

    private void EnsureInitialized()
    {
        if (blackboard.LeaderTransform == null)
        {
            blackboard.LeaderTransform = transform;
        }
        if (statsManager == null)
        {
            statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        }
        if (leaderBaseAI == null)
        {
            leaderBaseAI = GetComponentInChildren<BaseAI>();
        }
        if (statsManager != null && !isLeaderDeathSubscribed)
        {
            statsManager.OnCharacterDeath -= HandleLeaderDeath;
            statsManager.OnCharacterDeath += HandleLeaderDeath;
            isLeaderDeathSubscribed = true;
        }
        RecordRoleIfKey(EffectiveLeaderFunction);
    }

    public void RecordRoleIfKey(AdventurerFunction function)
    {
        if (function == AdventurerFunction.Tank) hadTankInParty = true;
        if (function == AdventurerFunction.Healer) hadHealerInParty = true;
    }

    public void RegisterMember(SquadMember member)
    {
        EnsureInitialized();
        if (member == null || members.Contains(member)) return;
        if (member.Leader != null && member.Leader != this)
        {
            member.LeaveSquad();
        }
        members.Add(member);
        member.AssignLeader(this);

        if (member.StatsManager != null)
        {
            member.StatsManager.OnCharacterDeath += HandleMemberDeath;
        }

        RecordRoleIfKey(member.EffectiveFunction);
        RecordRoleIfKey(EffectiveLeaderFunction);
        RefreshScoutMember();
    }

    public void UnregisterMember(SquadMember member)
    {
        if (member == null) return;
        if (member.StatsManager != null)
        {
            member.StatsManager.OnCharacterDeath -= HandleMemberDeath;
        }
        if (activeScout == member)
        {
            activeScout = null;
        }
        members.Remove(member);
        RefreshScoutMember();
    }

    public void RefreshScoutMember()
    {
        scoutMember = null;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.CharacterFunction == AdventurerFunction.Scout)
            {
                scoutMember = m;
                return;
            }
        }
    }

    public SquadMember FindScout()
    {
        var functionScout = ScoutMember;
        if (functionScout != null)
        {
            return functionScout;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.IsScout)
            {
                return m;
            }
        }
        return null;
    }

    public SquadMember FindTank()
    {
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.EffectiveFunction == AdventurerFunction.Tank)
            {
                return m;
            }
        }
        return null;
    }

    private void Update()
    {
        UpdateRoomLoop(Time.deltaTime);
    }

    /// <summary>
    /// Starts or resumes the room loop at the current position.
    /// </summary>
    public void StartRoomLoop()
    {
        if (currentRoom == null)
        {
            currentRoom = DungeonRoomRegistry.GetRoomAt(transform.position);
        }
        if (!blackboard.HasEntrancePosition)
        {
            blackboard.EntrancePosition = DungeonRoomRegistry.HasEntrance ? DungeonRoomRegistry.EntrancePosition : transform.position;
        }

        if (currentRoom != null)
        {
            blackboard.RecordRoomVisited(currentRoom.RoomId, currentRoom.transform.position);
            RecordRoomExits(currentRoom);

            if (currentRoom.IsSafeRoom)
            {
                SetRoomLoopState(RoomLoopState.SafeRoomStop);
                return;
            }
        }

        SetRoomLoopState(RoomLoopState.Approach);
    }

    public void NotifyCombatCleared()
    {
        if (currentRoomLoopState == RoomLoopState.Fight)
        {
            SetRoomLoopState(RoomLoopState.Regroup);
        }
    }

    public void SetRoomLoopState(RoomLoopState newState)
    {
        if (currentRoomLoopState == newState) return;

        currentRoomLoopState = newState;
        stateTimer = 0f;

        var tuning = EffectiveRoomLoopTuning;
        var profile = LeaderProfile;

        switch (newState)
        {
            case RoomLoopState.Approach:
                // Pick target door
                targetDoor = SelectNextExit(currentRoom);
                if (targetDoor == null)
                {
                    // No unexplored exits found anywhere -> backtrack/retreat
                    blackboard.RetreatBlocked = false;
                    OrderFallBack(blackboard.EntrancePosition);
                }
                else
                {
                    OrderFollowLeader();
                }
                break;

            case RoomLoopState.Staging:
                // Arrived at doorway threshold
                float hesitation = profile != null ? profile.DoorwayHesitation : 0.6f;
                if (doorwayStagingController != null)
                {
                    doorwayStagingController.SetHesitationDuration(hesitation);
                    doorwayStagingController.StartStaging();
                }
                OrderStandby();

                // Check boss door recognition
                if (targetDoor != null && targetDoor.IsBossDoor)
                {
                    blackboard.SetBossDoorRecognized(targetDoor.Position);
                    bool needPreBossRecover = tuning != null && tuning.PreBossRecover;
                    bool needPreBossCheckpoint = tuning != null && tuning.PreBossCheckpoint;

                    if (needPreBossRecover)
                    {
                        OrderRecover();
                    }
                    else if (needPreBossCheckpoint)
                    {
                        RequestCheckpoint();
                    }
                }
                break;

            case RoomLoopState.ScoutPeek:
                if (CanScout && targetDoor != null)
                {
                    var scout = ScoutMember;
                    if (scout != null)
                    {
                        activeScout = scout;
                        scout.IsScouting = true;
                        blackboard.SetOrder(SquadOrderType.ScoutAhead, null, targetDoor.Position);
                        OnScoutDispatched?.Invoke(scout);
                    }
                    else
                    {
                        SetRoomLoopState(RoomLoopState.TankEnters);
                    }
                }
                else
                {
                    SetRoomLoopState(RoomLoopState.TankEnters);
                }
                break;

            case RoomLoopState.TankEnters:
                // Advance into next room, with Tank leading if present
                if (targetDoor != null)
                {
                    targetDoor.MarkExplored();
                    blackboard.MarkDoorTraversed(targetDoor.DoorId);
                    Vector3 marchDir = (targetDoor.Position - transform.position);
                    if (marchDir.sqrMagnitude > 0.01f)
                    {
                        MarchingForward = marchDir.normalized;
                    }
                }
                OrderFollowLeader();
                break;

            case RoomLoopState.Fight:
                // Tactical evaluator handles combat focus fire
                break;

            case RoomLoopState.Regroup:
                OrderStandby();
                break;

            case RoomLoopState.Checkpoint:
                if (currentRoom != null)
                {
                    OnRoomCleared?.Invoke(currentRoom);
                    AdventurerEventHub.RaiseRoomCleared(this, currentRoom, currentRoom.RoomId);
                }
                var decision = RequestCheckpoint();
                if (decision == CheckpointDecision.Advance)
                {
                    SetRoomLoopState(RoomLoopState.Approach);
                }
                break;

            case RoomLoopState.SafeRoomStop:
                OrderRecover();
                break;
        }

        OnRoomLoopStateChanged?.Invoke(currentRoomLoopState);
    }

    private void UpdateRoomLoop(float dt)
    {
        if (currentRoomLoopState == RoomLoopState.None) return;

        stateTimer += dt;
        var tuning = EffectiveRoomLoopTuning;

        // Auto-detect current room changes
        var detectedRoom = DungeonRoomRegistry.GetRoomAt(transform.position);
        if (detectedRoom != null && detectedRoom != currentRoom)
        {
            currentRoom = detectedRoom;
            blackboard.RecordRoomVisited(currentRoom.RoomId, currentRoom.transform.position);
            RecordRoomExits(currentRoom);
            OnRoomEntered?.Invoke(currentRoom);
            AdventurerEventHub.RaiseRoomEntered(this, currentRoom, currentRoom.RoomId);

            if (currentRoom.IsBossRoom)
            {
                AdventurerEventHub.RaiseBossFightStarted(this, currentRoom, targetDoor);
            }

            if (currentRoom.IsSafeRoom)
            {
                SetRoomLoopState(RoomLoopState.SafeRoomStop);
                return;
            }
        }

        // Combat transition takes priority
        if (IsInCombat())
        {
            if (currentRoomLoopState != RoomLoopState.Fight)
            {
                SetRoomLoopState(RoomLoopState.Fight);
            }
            return;
        }

        switch (currentRoomLoopState)
        {
            case RoomLoopState.Approach:
                if (targetDoor == null)
                {
                    targetDoor = SelectNextExit(currentRoom);
                    if (targetDoor == null) return;
                }
                // Check if leader arrived near doorway
                float distSq = (transform.position - targetDoor.Position).sqrMagnitude;
                if (distSq <= 2.25f) // within 1.5m
                {
                    SetRoomLoopState(RoomLoopState.Staging);
                }
                break;

            case RoomLoopState.Staging:
                // If the target door is Sealed by DM, the party must wait in Standby indefinitely until the door opens
                if (targetDoor != null && targetDoor.State == DoorState.Sealed)
                {
                    // Remain in Standby, exempt from timeouts
                    OrderStandby();
                    break;
                }

                float maxStagingWait = tuning != null ? tuning.StagingHesitationTimeout : 1.5f;
                float maxStragglerWait = tuning != null ? tuning.StragglerTimeout : 5.0f;
                bool stragglersCaughtUp = !AreMembersStraggling();

                if ((stateTimer >= maxStagingWait && stragglersCaughtUp) || stateTimer >= maxStragglerWait)
                {
                    if (doorwayStagingController != null && doorwayStagingController.IsStaging)
                    {
                        doorwayStagingController.EndStaging();
                    }
                    if (CanScout)
                    {
                        SetRoomLoopState(RoomLoopState.ScoutPeek);
                    }
                    else
                    {
                        SetRoomLoopState(RoomLoopState.TankEnters);
                    }
                }
                break;

            case RoomLoopState.ScoutPeek:
                float scoutTimeout = 4.0f;
                if (activeScout == null || !activeScout.IsAlive || stateTimer >= scoutTimeout)
                {
                    if (activeScout != null)
                    {
                        SynchronizeScoutIntel(activeScout);
                    }
                    SetRoomLoopState(RoomLoopState.TankEnters);
                }
                break;

            case RoomLoopState.TankEnters:
                if (stateTimer >= 2.0f || (currentRoom != null && currentRoom.Contains(transform.position)))
                {
                    if (IsInCombat())
                    {
                        SetRoomLoopState(RoomLoopState.Fight);
                    }
                    else
                    {
                        SetRoomLoopState(RoomLoopState.Regroup);
                    }
                }
                break;

            case RoomLoopState.Fight:
                // Managed by NotifyCombatCleared or combat ending
                break;

            case RoomLoopState.Regroup:
                float regroupWait = tuning != null ? tuning.RegroupTimeout : 4.0f;
                if (!AreMembersStraggling() || stateTimer >= regroupWait)
                {
                    SetRoomLoopState(RoomLoopState.Checkpoint);
                }
                break;

            case RoomLoopState.Checkpoint:
                // RequestCheckpoint was executed upon entering state.
                // If it chose Recover or Regroup, wait for completion then resume Approach
                if (LastCheckpointDecision == CheckpointDecision.Recover)
                {
                    if (IsPartyRecoveryComplete() || stateTimer >= 5.0f)
                    {
                        SetRoomLoopState(RoomLoopState.Approach);
                    }
                }
                else if (LastCheckpointDecision == CheckpointDecision.Regroup)
                {
                    if (!AreMembersStraggling() || stateTimer >= 4.0f)
                    {
                        SetRoomLoopState(RoomLoopState.Approach);
                    }
                }
                else if (LastCheckpointDecision == CheckpointDecision.Retreat)
                {
                    // Stay retreating towards entrance
                }
                break;

            case RoomLoopState.SafeRoomStop:
                float safeDuration = tuning != null ? tuning.SafeRoomDuration : 10.0f;
                if (stateTimer >= safeDuration || IsPartyRecoveryComplete())
                {
                    SetRoomLoopState(RoomLoopState.Approach);
                }
                break;
        }
    }

    private bool AreMembersStraggling()
    {
        float sqrThreshold = (leashDistance * 0.75f) * (leashDistance * 0.75f);
        Vector3 leaderPos = transform.position;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || !m.IsAlive) continue;
            if ((m.transform.position - leaderPos).sqrMagnitude > sqrThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private void RecordRoomExits(RoomVolume room)
    {
        if (room == null || room.Exits == null) return;
        var exits = room.Exits;
        for (int i = 0; i < exits.Count; i++)
        {
            var exit = exits[i];
            if (exit != null)
            {
                var opposite = exit.GetOppositeRoom(room);
                int oppId = opposite != null ? opposite.RoomId : -1;
                blackboard.RecordDoorSeen(exit.DoorId, exit.Position, exit.IsBossDoor, oppId);
            }
        }
    }

    /// <summary>
    /// Deterministic fork rule: selects the best exit without map knowledge.
    /// Never loops into a fully explored branch. Zero allocations.
    /// </summary>
    public DoorMarker SelectNextExit(RoomVolume room)
    {
        if (room == null)
        {
            room = DungeonRoomRegistry.GetRoomAt(transform.position);
            if (room == null) return null;
        }

        var exits = room.Exits;
        if (exits == null || exits.Count == 0)
        {
            blackboard.MarkRoomDeadEnd(room.RoomId);
            return null;
        }

        var tuning = EffectiveRoomLoopTuning;
        float distWeight = tuning != null ? tuning.NearestExitWeight : 1.0f;
        float hazardWeight = tuning != null ? tuning.HazardAvoidanceWeight : 0.0f;
        float scoutWeight = tuning != null ? tuning.ScoutInfoWeight : 0.0f;

        DoorMarker bestDoor = null;
        float bestScore = float.MaxValue;
        Vector3 myPos = transform.position;

        // 1. Evaluate untraversed exits from current room
        for (int i = 0; i < exits.Count; i++)
        {
            var door = exits[i];
            if (door == null || door.State == DoorState.Sealed) continue;

            var targetRm = door.GetOppositeRoom(room);
            if (targetRm != null && blackboard.IsRoomFullyExplored(targetRm.RoomId))
            {
                continue; // Skip fully explored branches
            }

            if (blackboard.IsDoorTraversed(door.DoorId))
            {
                continue;
            }

            float dist = Vector3.Distance(myPos, door.Position);
            float score = dist * distWeight;

            // Penalty if door or target room is hazardous
            if (targetRm != null && (targetRm.IsHazardous || blackboard.IsRoomDangerous(targetRm.RoomId)))
            {
                score += (hazardWeight * 50f);
            }
            if (blackboard.IsPositionHazardous(door.Position, 2f))
            {
                score += (hazardWeight * 25f);
            }

            // Tie breaker: lowest DoorId
            if (score < bestScore || (Mathf.Approximately(score, bestScore) && (bestDoor == null || door.DoorId < bestDoor.DoorId)))
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        if (bestDoor != null)
        {
            return bestDoor;
        }

        // All local exits from current room traversed/explored -> current room is fully explored
        blackboard.MarkRoomFullyExplored(room.RoomId);

        // 2. Backtrack to find closest untraversed door in the known graph
        var allDoors = DungeonRoomRegistry.Doors;
        for (int i = 0; i < allDoors.Count; i++)
        {
            var door = allDoors[i];
            if (door == null || door.State == DoorState.Sealed) continue;
            if (blackboard.IsDoorTraversed(door.DoorId)) continue;

            // Don't head into fully explored rooms
            if (door.RoomA != null && blackboard.IsRoomFullyExplored(door.RoomA.RoomId) &&
                door.RoomB != null && blackboard.IsRoomFullyExplored(door.RoomB.RoomId))
            {
                continue;
            }

            float dist = Vector3.Distance(myPos, door.Position);
            float score = dist * distWeight;

            if (score < bestScore || (Mathf.Approximately(score, bestScore) && (bestDoor == null || door.DoorId < bestDoor.DoorId)))
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    public bool IsInCombat()
    {
        if (blackboard != null && (blackboard.HasKnownEnemies || blackboard.CurrentOrder == SquadOrderType.FocusFire))
        {
            return true;
        }

        if (leaderBaseAI == null)
        {
            leaderBaseAI = GetComponentInChildren<BaseAI>();
        }

        if (leaderBaseAI != null && (leaderBaseAI.ThreatLevel > 0 || (leaderBaseAI.EnemyTargets != null && leaderBaseAI.EnemyTargets.Count > 0)))
        {
            return true;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.BaseAI != null && m.BaseAI.ThreatLevel > 0)
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetFormationSlotOffset(AdventurerFunction function, bool inCombat)
    {
        if (formationConfig != null)
        {
            return formationConfig.GetSlotOffset(function, inCombat);
        }

        AdventurerFunction effective = function != AdventurerFunction.None ? function : AdventurerFunction.DPS;
        if (formationSlots != null)
        {
            for (int i = 0; i < formationSlots.Length; i++)
            {
                if (formationSlots[i].Function == effective)
                {
                    return inCombat ? formationSlots[i].CombatOffset : formationSlots[i].TravelOffset;
                }
            }
        }

        return new Vector2(0f, -1.0f);
    }

    public Vector3 GetFormationWorldPosition(SquadMember member)
    {
        if (member == null) return transform.position;
        if (member.IsScouting) return transform.position;

        bool inCombat = IsInCombat();
        Vector2 slotOffset = GetFormationSlotOffset(member.EffectiveFunction, inCombat);

        Vector3 forward = MarchingForward;
        forward.z = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.up;
        }
        else
        {
            forward.Normalize();
        }

        // 2D right vector perpendicular to forward (clockwise 90 degrees: (y, -x))
        Vector3 right = new Vector3(forward.y, -forward.x, 0f);
        return transform.position + (right * slotOffset.x) + (forward * slotOffset.y);
    }

    public bool DispatchScout(SquadMember targetScout = null)
    {
        var scout = targetScout != null ? targetScout : FindScout();
        if (scout == null || !scout.IsAlive) return false;

        activeScout = scout;
        scout.IsScouting = true;

        // Squad hold state: fellow squad members enter Standby
        OrderStandby();

        OnScoutDispatched?.Invoke(scout);
        return true;
    }

    public void SynchronizeScoutIntel(SquadMember scout)
    {
        if (scout == null) return;

        // 1. Transfer hazard records from Scout local memory into SquadBlackboard
        var memory = scout.GetComponentInChildren<CharacterAIMemory>() ?? scout.GetComponentInParent<CharacterAIMemory>();
        if (memory != null)
        {
            var hazards = memory.GetHazardRecords();
            int count = memory.HazardCount;
            float currentTime = Time.time;
            for (int i = 0; i < count; i++)
            {
                ref readonly var record = ref hazards[i];
                if (record.ExpirationTime > currentTime)
                {
                    float remainingDuration = record.ExpirationTime - currentTime;
                    blackboard.ReportHazard(record.GridPos, record.WorldPos, record.DangerLevel, remainingDuration);
                }
            }
        }

        // 2. Transfer local known enemies into SquadBlackboard
        var scoutAI = scout.BaseAI;
        if (scoutAI != null)
        {
            var localEnemies = scoutAI.LocalKnownEnemies;
            float currentTime = Time.time;
            for (int i = 0; i < localEnemies.Count; i++)
            {
                var record = localEnemies[i];
                if (record.IsValid(currentTime))
                {
                    blackboard.ReportEnemy(record.Target, record.LastKnownPosition, currentTime);
                }
            }
        }

        scout.IsScouting = false;

        if (activeScout == scout)
        {
            activeScout = null;
        }

        OnScoutDebriefed?.Invoke(scout);

        // Resume March: once debriefed, resume navigation
        OrderFollowLeader();
    }

    private void HandleMemberDeath(CharacterStatsManager memberStats)
    {
        if (memberStats == null) return;
        var member = memberStats.GetComponent<SquadMember>() ?? memberStats.GetComponentInChildren<SquadMember>();
        if (member == null) return;

        RecordRoleIfKey(member.EffectiveFunction);
        RefreshScoutMember();

        // Check if the dying member is the active scout
        if (activeScout == member)
        {
            activeScout = null;
            OnScoutEliminated?.Invoke(member);

            // Fear surge on squad members
            ApplySquadFearSurge(35);

            // 3-second panic delay before resuming march blindly
            if (scoutPanicCoroutine != null) StopCoroutine(scoutPanicCoroutine);
            scoutPanicCoroutine = StartCoroutine(ScoutDeathPanicRoutine());
        }
    }

    private System.Collections.IEnumerator ScoutDeathPanicRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        scoutPanicCoroutine = null;

        // If still in Standby from scout dispatch, resume march
        if (blackboard.CurrentOrder == SquadOrderType.Standby)
        {
            OrderFollowLeader();
        }
    }

    public void CompleteScoutPanicForTesting()
    {
        if (scoutPanicCoroutine != null)
        {
            StopCoroutine(scoutPanicCoroutine);
            scoutPanicCoroutine = null;
        }

        if (blackboard.CurrentOrder == SquadOrderType.Standby)
        {
            OrderFollowLeader();
        }
    }

    private void ApplySquadFearSurge(int fearAmount)
    {
        var leaderAI = GetComponentInChildren<BaseAI>();
        if (leaderAI != null)
        {
            leaderAI.SetFearLevel(Mathf.Clamp(leaderAI.FearLevel + fearAmount, 0, 100));
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.BaseAI != null)
            {
                m.BaseAI.SetFearLevel(Mathf.Clamp(m.BaseAI.FearLevel + fearAmount, 0, 100));
            }
        }
    }

    public void OrderFocusFire(Transform target)
    {
        if (target == null)
        {
            ClearOrder();
            return;
        }
        hasActiveCheckpointOrder = false;
        blackboard.SetOrder(SquadOrderType.FocusFire, target);
    }

    public void OrderFollowLeader()
    {
        blackboard.SetOrder(SquadOrderType.FollowLeader, null, transform.position);
    }

    public void OrderFallBack(Vector3 rallyPosition = default)
    {
        Vector3 rally = rallyPosition != default ? rallyPosition : transform.position;
        blackboard.SetOrder(SquadOrderType.FallBack, null, rally);
    }

    public void OrderStandby()
    {
        blackboard.SetOrder(SquadOrderType.Standby, null, transform.position);
    }

    public void OrderRecover()
    {
        blackboard.SetOrder(SquadOrderType.Recover, null, transform.position);
    }

    public void ClearOrder()
    {
        hasActiveCheckpointOrder = false;
        blackboard.ClearOrder();
    }

    /// <summary>
    /// Sums the real remaining item counts across the leader's and living members' <see cref="AdventurerItemKitController"/>s.
    /// Zero allocations.
    /// </summary>
    public int GetPartyRemainingItemCount()
    {
        return GetPartyRemainingItemCount(out _, out _);
    }

    /// <summary>
    /// Sums the real remaining item counts across the party's <see cref="AdventurerItemKitController"/>s,
    /// outputting whether any member has a kit controller attached and whether any member can still recover with their kit.
    /// Zero allocations.
    /// </summary>
    public int GetPartyRemainingItemCount(out bool anyKitAttached, out bool anyMemberCanRecoverWithKit)
    {
        anyKitAttached = false;
        anyMemberCanRecoverWithKit = false;
        int totalRemaining = 0;

        if (IsAlive)
        {
            var leaderKit = GetComponent<AdventurerItemKitController>() ?? GetComponentInChildren<AdventurerItemKitController>();
            if (leaderKit != null)
            {
                anyKitAttached = true;
                totalRemaining += leaderKit.TotalRemainingCount;
                if (CanMemberStillRecoverWithKit(gameObject, statsManager, leaderBaseAI, leaderKit))
                {
                    anyMemberCanRecoverWithKit = true;
                }
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || !m.IsAlive) continue;

            var mKit = m.GetComponent<AdventurerItemKitController>() ?? m.GetComponentInChildren<AdventurerItemKitController>();
            if (mKit != null)
            {
                anyKitAttached = true;
                totalRemaining += mKit.TotalRemainingCount;
                if (CanMemberStillRecoverWithKit(m.gameObject, m.StatsManager, m.BaseAI, mKit))
                {
                    anyMemberCanRecoverWithKit = true;
                }
            }
        }

        return totalRemaining;
    }

    /// <summary>
    /// Returns true when all living party members have either reached their squad recovery target HP/Mana
    /// or have no remaining applicable recovery items/casts in progress.
    /// Zero allocations.
    /// </summary>
    public bool IsPartyRecoveryComplete()
    {
        if (IsAlive)
        {
            var leaderKit = GetComponent<AdventurerItemKitController>() ?? GetComponentInChildren<AdventurerItemKitController>();
            if (leaderKit != null)
            {
                if (leaderKit.IsCastingItem || CanMemberStillRecoverWithKit(gameObject, statsManager, leaderBaseAI, leaderKit))
                {
                    return false;
                }
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || !m.IsAlive) continue;

            var mKit = m.GetComponent<AdventurerItemKitController>() ?? m.GetComponentInChildren<AdventurerItemKitController>();
            if (mKit != null)
            {
                if (mKit.IsCastingItem || CanMemberStillRecoverWithKit(m.gameObject, m.StatsManager, m.BaseAI, mKit))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// If the current squad order is <see cref="SquadOrderType.Recover"/> and all living members have finished recovering
    /// (reached target HP/Mana or exhausted usable items), re-runs <see cref="RequestCheckpoint()"/> and returns true.
    /// </summary>
    public bool CheckAndCompleteRecoverOrder()
    {
        if (blackboard == null || blackboard.CurrentOrder != SquadOrderType.Recover)
        {
            return false;
        }

        if (!IsPartyRecoveryComplete())
        {
            return false;
        }

        RequestCheckpoint();
        return true;
    }

    private static bool CanMemberStillRecoverWithKit(
        GameObject memberObj,
        CharacterStatsManager memberStats,
        BaseAI memberAI,
        AdventurerItemKitController kit)
    {
        if (kit == null || kit.TotalRemainingCount <= 0) return false;
        if (memberStats != null && memberStats.Life <= 0f) return false;

        float targetHp = 0.85f;
        float targetMana = 0.75f;
        int quota = -1;

        if (memberAI != null)
        {
            var tuning = memberAI.ItemUseQualityTuning != null ? memberAI.ItemUseQualityTuning : kit.QualityTuning;
            ItemUseQualityTuningEntry entry = ItemUseQualityTuningSO.GetEntryOrDefault(tuning, memberAI.ItemUseQuality);
            quota = entry.PerRoomItemQuota;
            if (tuning != null)
            {
                targetHp = Mathf.Max(entry.HpTriggerFraction, tuning.SquadRecoverTargetHpFraction);
                targetMana = Mathf.Max(entry.ManaTriggerFraction, tuning.SquadRecoverTargetManaFraction);
            }
            else
            {
                targetHp = Mathf.Max(entry.HpTriggerFraction, 0.85f);
                targetMana = Mathf.Max(entry.ManaTriggerFraction, 0.75f);
            }
        }

        if (quota >= 0 && kit.ItemsUsedInCurrentRoom >= quota)
        {
            return false;
        }

        if (memberStats != null && memberStats.MaxLife != null && memberStats.MaxLife.Value > 0f)
        {
            float hpFrac = memberStats.Life / memberStats.MaxLife.Value;
            if (hpFrac < targetHp && kit.GetRemainingCount(ConsumableItemCategory.HealthPotion) > 0)
            {
                return true;
            }
        }

        if (memberObj != null)
        {
            var statusMgr = memberObj.GetComponent<StatusEffectManager>() ?? memberObj.GetComponentInChildren<StatusEffectManager>();
            if (statusMgr != null && statusMgr.HasHarmfulEffect() && kit.GetRemainingCount(ConsumableItemCategory.StatusCure) > 0)
            {
                return true;
            }
        }

        if (memberStats != null && memberStats.MaxMana != null && memberStats.MaxMana.Value > 0f)
        {
            float manaFrac = memberStats.Mana / memberStats.MaxMana.Value;
            if (manaFrac < targetMana && kit.GetRemainingCount(ConsumableItemCategory.ManaPotion) > 0)
            {
                return true;
            }
        }

        return false;
    }

    public void DisbandSquad()
    {
        if (successionCoroutine != null)
        {
            StopCoroutine(successionCoroutine);
            successionCoroutine = null;
        }

        for (int i = members.Count - 1; i >= 0; i--)
        {
            var m = members[i];
            if (m != null)
            {
                m.AssignLeader(null);
            }
        }
        members.Clear();
        scoutMember = null;
        hasActiveCheckpointOrder = false;
        blackboard.ClearOrder();
    }

    /// <summary>
    /// Evaluates party condition and reward/risk tradeoff at a checkpoint, emits <see cref="OnCheckpointDecision"/>,
    /// maps the decision to <see cref="SquadOrderType"/>, and logs the decision and reason via <see cref="ELogger"/>.
    /// Zero heap allocations in the decision path.
    /// </summary>
    public CheckpointDecision RequestCheckpoint()
    {
        return RequestCheckpoint(out _);
    }

    /// <summary>
    /// Evaluates party condition and reward/risk tradeoff at a checkpoint, returning the decision and reason code.
    /// </summary>
    public CheckpointDecision RequestCheckpoint(out CheckpointReason reason)
    {
        float hpWeight = checkpointTuning != null ? checkpointTuning.HpConditionWeight : 0.65f;
        float manaWeight = checkpointTuning != null ? checkpointTuning.ManaConditionWeight : 0.15f;
        float survivalWeight = checkpointTuning != null ? checkpointTuning.SurvivalRatioWeight : 0.20f;
        float recoveryThreshold = checkpointTuning != null ? checkpointTuning.RecoveryHpThreshold : 0.65f;
        float healerMinMana = checkpointTuning != null ? checkpointTuning.HealerMinManaFraction : 0.25f;

        int livingCount = 0;
        int totalTrackedCount = 0;
        float sumHpFrac = 0f;
        float sumManaFrac = 0f;
        float minHpFrac = 1f;
        bool anyMemberScattered = false;
        bool hasLivingHealerWithMana = false;
        bool hasLivingTank = false;
        bool hasLivingHealer = false;
        bool hasDeadTank = false;
        bool hasDeadHealer = false;

        float sqrLeash = leashDistance * leashDistance;
        Vector3 leaderPos = transform.position;

        if (statsManager == null)
        {
            statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        }

        AdventurerFunction leaderFn = EffectiveLeaderFunction;
        RecordRoleIfKey(leaderFn);

        if (IsAlive)
        {
            totalTrackedCount++;
            livingCount++;
            float lHp = GetNormalizedLife(statsManager);
            float lMana = GetNormalizedMana(statsManager);
            sumHpFrac += lHp;
            sumManaFrac += lMana;
            if (lHp < minHpFrac) minHpFrac = lHp;

            if (leaderFn == AdventurerFunction.Tank) hasLivingTank = true;
            if (leaderFn == AdventurerFunction.Healer)
            {
                hasLivingHealer = true;
                if (lMana >= healerMinMana) hasLivingHealerWithMana = true;
            }
        }
        else
        {
            totalTrackedCount++;
            if (leaderFn == AdventurerFunction.Tank) hasDeadTank = true;
            if (leaderFn == AdventurerFunction.Healer) hasDeadHealer = true;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;

            totalTrackedCount++;
            AdventurerFunction mFn = m.EffectiveFunction;
            RecordRoleIfKey(mFn);

            if (!m.IsAlive)
            {
                if (mFn == AdventurerFunction.Tank) hasDeadTank = true;
                if (mFn == AdventurerFunction.Healer) hasDeadHealer = true;
                continue;
            }

            livingCount++;
            float mHp = GetNormalizedLife(m.StatsManager);
            float mMana = GetNormalizedMana(m.StatsManager);
            sumHpFrac += mHp;
            sumManaFrac += mMana;
            if (mHp < minHpFrac) minHpFrac = mHp;

            if (mFn == AdventurerFunction.Tank) hasLivingTank = true;
            if (mFn == AdventurerFunction.Healer)
            {
                hasLivingHealer = true;
                if (mMana >= healerMinMana) hasLivingHealerWithMana = true;
            }

            if ((m.transform.position - leaderPos).sqrMagnitude > sqrLeash)
            {
                anyMemberScattered = true;
            }
        }

        float avgHp = livingCount > 0 ? (sumHpFrac / livingCount) : 0f;
        float avgMana = livingCount > 0 ? (sumManaFrac / livingCount) : 0f;
        float survivalRatio = totalTrackedCount > 0 ? ((float)livingCount / totalTrackedCount) : 1f;
        float weightSum = Mathf.Max(0.001f, hpWeight + manaWeight + survivalWeight);
        float trueCondition = Mathf.Clamp01((avgHp * hpWeight + avgMana * manaWeight + survivalRatio * survivalWeight) / weightSum);

        var profile = LeaderProfile;
        LeaderAssessmentAccuracy accuracy = profile != null ? profile.LeaderAssessmentAccuracy : LeaderAssessmentAccuracy.NearTrue;
        LeaderSafetyMargin safetyMarginEnum = profile != null ? profile.LeaderSafetyMargin : LeaderSafetyMargin.Medium;
        float rewardWeight = profile != null ? profile.RewardWeight : 1.0f;

        AccuracyTuningEntry accEntry = LeaderCheckpointTuningSO.GetAccuracyTuningOrDefault(checkpointTuning, accuracy);
        float noise = accEntry.NoiseAmplitude > 0f ? RandomUtil.Range(-accEntry.NoiseAmplitude, accEntry.NoiseAmplitude) : 0f;
        float perceivedSuccessChance = Mathf.Clamp01(trueCondition + accEntry.OptimismBias + noise);
        float failureChance = 1f - perceivedSuccessChance;

        var provider = RewardProvider;
        float carriedValue = provider != null ? Mathf.Max(0f, provider.GetPartyCarriedValue(this)) : 0f;
        float knownPotential = provider != null ? Mathf.Max(0f, provider.GetKnownPotentialAhead(this)) : 0f;
        float unknownExpectation = provider != null ? Mathf.Max(0f, provider.GetUnknownExpectationPerRoom(this)) : 0f;
        float totalPotentialAhead = knownPotential + unknownExpectation;

        float advanceValue = perceivedSuccessChance * totalPotentialAhead * rewardWeight;
        float expectedDeathLoss = carriedValue * failureChance;
        float totalStake = Mathf.Max(1f, carriedValue + totalPotentialAhead);
        float netNormalizedAdvantage = (advanceValue - expectedDeathLoss) / totalStake;

        float requiredMargin = LeaderCheckpointTuningSO.GetRequiredSafetyMarginOrDefault(checkpointTuning, safetyMarginEnum);
        bool marginMet = netNormalizedAdvantage >= requiredMargin;

        bool tankLostNoReplacement = (hadTankInParty || hasDeadTank) && !hasLivingTank;
        bool healerLostNoReplacement = (hadHealerInParty || hasDeadHealer) && !hasLivingHealer;
        bool keyRoleLost = tankLostNoReplacement || healerLostNoReplacement;

        int realKitItems = GetPartyRemainingItemCount(out bool anyKitAttached, out bool anyMemberCanRecoverWithKit);
        bool hasKitRecoveryMeans = anyKitAttached ? (realKitItems > 0 && anyMemberCanRecoverWithKit) : (recoveryItemsRemaining > 0);

        bool needsRecovery = avgHp < recoveryThreshold || minHpFrac < (recoveryThreshold * 0.85f) || (anyKitAttached && anyMemberCanRecoverWithKit && avgHp < 0.85f);
        bool hasRecoveryMeans = hasKitRecoveryMeans || hasLivingHealerWithMana;

        CheckpointDecision decision;
        if (keyRoleLost || !marginMet)
        {
            if (!RetreatBlocked)
            {
                decision = CheckpointDecision.Retreat;
                reason = keyRoleLost ? CheckpointReason.KeyRoleLost : CheckpointReason.SafetyMarginNotMet;
            }
            else
            {
                reason = CheckpointReason.RetreatBlockedFallback;
                AdventurerEventHub.RaiseLeaderRetreatBlocked(this, reason, transform.position);
                if (anyMemberScattered)
                {
                    decision = CheckpointDecision.Regroup;
                }
                else if (needsRecovery && hasRecoveryMeans)
                {
                    decision = CheckpointDecision.Recover;
                }
                else
                {
                    decision = CheckpointDecision.Advance;
                }
            }
        }
        else
        {
            if (anyMemberScattered)
            {
                decision = CheckpointDecision.Regroup;
                reason = CheckpointReason.PartyScattered;
            }
            else if (needsRecovery && hasRecoveryMeans)
            {
                decision = CheckpointDecision.Recover;
                reason = CheckpointReason.NeedsRecoveryBeforeAdvance;
            }
            else
            {
                decision = CheckpointDecision.Advance;
                reason = CheckpointReason.FavorableMargin;
            }
        }

        ApplyCheckpointOrder(decision);
        LastCheckpointDecision = decision;
        LastCheckpointReason = reason;
        hasActiveCheckpointOrder = true;

        ELogger.Log("Checkpoint", this, LastCheckpointReason);
        OnCheckpointDecision?.Invoke(decision, reason);
        AdventurerEventHub.RaiseCheckpointDecision(this, decision, reason);
        return decision;
    }

    private void ApplyCheckpointOrder(CheckpointDecision decision)
    {
        int index = Mathf.Clamp((int)decision, 0, DecisionToOrderTable.Length - 1);
        SquadOrderType mappedOrder = DecisionToOrderTable[index];

        if (decision == CheckpointDecision.Advance && CanScout)
        {
            var scout = ScoutMember;
            if (scout != null)
            {
                activeScout = scout;
                scout.IsScouting = true;
                blackboard.SetOrder(SquadOrderType.ScoutAhead, null, transform.position);
                OnScoutDispatched?.Invoke(scout);
                return;
            }
        }

        if (activeScout != null && decision != CheckpointDecision.Advance)
        {
            activeScout.IsScouting = false;
            activeScout = null;
        }

        blackboard.SetOrder(mappedOrder, null, transform.position);
    }

    private static float GetNormalizedLife(CharacterStatsManager stats)
    {
        if (stats == null || stats.MaxLife.Value <= 0f) return 1f;
        return Mathf.Clamp01(stats.Life / stats.MaxLife.Value);
    }

    private static float GetNormalizedMana(CharacterStatsManager stats)
    {
        if (stats == null) return 1f;
        if (stats.MaxMana.Value <= 0f) return 1f;
        return Mathf.Clamp01(stats.Mana / stats.MaxMana.Value);
    }

    private void HandleLeaderDeath(CharacterStatsManager stats)
    {
        RecordRoleIfKey(EffectiveLeaderFunction);
        OnLeaderDied?.Invoke(this);
        AdventurerEventHub.RaiseLeaderDied(this, transform.position);

        if (!allowSuccession)
        {
            ApplyLeaderlessFallbackToSurvivors();
            OnLeaderChanged?.Invoke(this, null);
            AdventurerEventHub.RaiseLeaderChanged(this, null);
            return;
        }

        bool anyLivingCandidate = false;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.gameObject != gameObject)
            {
                anyLivingCandidate = true;
                break;
            }
        }

        if (!anyLivingCandidate)
        {
            DisbandSquad();
            OnLeaderChanged?.Invoke(this, null);
            return;
        }

        float delay = GetSuccessionReactionDelay();
        if (delay > 0f)
        {
            isSuccessionPending = true;
            if (Application.isPlaying && isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                if (successionCoroutine != null)
                {
                    StopCoroutine(successionCoroutine);
                }
                successionCoroutine = StartCoroutine(SuccessionReactionRoutine(delay));
            }
        }
        else
        {
            isSuccessionPending = false;
            ExecuteSuccessionEvaluation();
        }
    }

    /// <summary>
    /// Triggered when the leader departs the dungeon alive (e.g. by using a Warp Escape item).
    /// Runs emergent succession as a leader loss to promote the next living non-escaped candidate.
    /// </summary>
    public void HandleLeaderDeparted(string reason = "Warped")
    {
        RecordRoleIfKey(EffectiveLeaderFunction);
        IsEscaped = true;

        if (!allowSuccession)
        {
            ApplyLeaderlessFallbackToSurvivors();
            OnLeaderChanged?.Invoke(this, null);
            AdventurerEventHub.RaiseLeaderChanged(this, null);
            return;
        }

        bool anyLivingCandidate = false;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && !m.IsEscaped && m.gameObject != gameObject)
            {
                anyLivingCandidate = true;
                break;
            }
        }

        if (!anyLivingCandidate)
        {
            DisbandSquad();
            OnLeaderChanged?.Invoke(this, null);
            AdventurerEventHub.RaiseLeaderChanged(this, null);
            return;
        }

        isSuccessionPending = false;
        if (successionCoroutine != null)
        {
            StopCoroutine(successionCoroutine);
            successionCoroutine = null;
        }
        ExecuteSuccessionEvaluation();
    }

    private float GetSuccessionReactionDelay()
    {
        var profile = LeaderProfile;
        if (profile != null)
        {
            return profile.SuccessionReactionDelay;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m.BaseAI != null && m.BaseAI.AdventurerProfile != null)
            {
                return m.BaseAI.AdventurerProfile.SuccessionReactionDelay;
            }
        }

        return 0f;
    }

    private System.Collections.IEnumerator SuccessionReactionRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        successionCoroutine = null;
        isSuccessionPending = false;
        ExecuteSuccessionEvaluation();
    }

    /// <summary>
    /// Immediately completes any pending succession reaction window (or triggers evaluation if not yet run)
    /// and returns the newly promoted <see cref="PartyLeader"/>, or null if no survivor stepped up.
    /// </summary>
    public PartyLeader CompleteSuccessionForTesting()
    {
        if (successionCoroutine != null)
        {
            StopCoroutine(successionCoroutine);
            successionCoroutine = null;
            isSuccessionPending = false;
            return ExecuteSuccessionEvaluation();
        }

        if (isSuccessionPending)
        {
            isSuccessionPending = false;
            return ExecuteSuccessionEvaluation();
        }

        if (lastPromotedLeader != null)
        {
            return lastPromotedLeader;
        }

        return ExecuteSuccessionEvaluation();
    }

    /// <summary>
    /// Computes the "will I lead?" score [0, 1] for a surviving squad member from:
    /// - <see cref="AdventurerFunction"/> weight from <see cref="LeaderCheckpointTuningSO"/> (Tank highest, DPS/None mid, Healer low, Scout lowest)
    /// - Courage/fear from <see cref="CharacterPersonality.Snapshot"/> and runtime fear
    /// - Current HP fraction
    /// - Nearby enemy count hesitation penalty
    /// Zero allocations.
    /// </summary>
    public float ComputeCandidateStepUpScore(SquadMember candidate)
    {
        if (candidate == null || !candidate.IsAlive) return 0f;

        float fnWeight = LeaderCheckpointTuningSO.GetFunctionStepUpWeightOrDefault(checkpointTuning, candidate.EffectiveFunction);
        float fnContrib = checkpointTuning != null ? checkpointTuning.FunctionWeightContribution : 0.60f;
        float hpContrib = checkpointTuning != null ? checkpointTuning.HpFractionContribution : 0.40f;
        float enemyPenaltyPerTarget = checkpointTuning != null ? checkpointTuning.EnemyHesitationPerTarget : 0.10f;

        float hpFraction = GetNormalizedLife(candidate.StatsManager);

        float courageMultiplier = 1.0f;
        var personality = candidate.GetComponent<CharacterPersonality>() ?? candidate.GetComponentInChildren<CharacterPersonality>();
        if (personality != null)
        {
            ref readonly var snap = ref personality.Snapshot;
            courageMultiplier = Mathf.Clamp(2.0f - snap.FearMultiplier, 0.5f, 1.5f);
            if (snap.IgnoreSurvivalGoal)
            {
                courageMultiplier += 0.15f;
            }
        }

        float runtimeFearPenalty = 0f;
        int nearbyEnemies = 0;
        var cAI = candidate.BaseAI;
        if (cAI != null)
        {
            runtimeFearPenalty = (cAI.FearLevel / 100f) * 0.25f;
            if (cAI.EnemyTargets != null && cAI.EnemyTargets.Count > 0)
            {
                nearbyEnemies = cAI.EnemyTargets.Count;
            }
        }

        if (nearbyEnemies == 0 && blackboard != null && blackboard.KnownEnemies != null)
        {
            nearbyEnemies = blackboard.KnownEnemies.Count;
        }

        float enemyHesitation = Mathf.Clamp01(nearbyEnemies * enemyPenaltyPerTarget);
        float baseScore = (fnWeight * fnContrib) + (hpFraction * hpContrib);
        float finalScore = (baseScore * courageMultiplier) - enemyHesitation - runtimeFearPenalty;
        return Mathf.Clamp01(finalScore);
    }

    private PartyLeader ExecuteSuccessionEvaluation()
    {
        SquadMember bestCandidate = null;
        float bestScore = -1f;

        for (int i = 0; i < members.Count; i++)
        {
            var candidate = members[i];
            if (candidate == null || !candidate.IsAlive || candidate.IsEscaped || candidate.gameObject == gameObject)
            {
                continue;
            }

            float score = ComputeCandidateStepUpScore(candidate);
            var candidateProfile = (candidate.BaseAI != null && candidate.BaseAI.AdventurerProfile != null)
                ? candidate.BaseAI.AdventurerProfile
                : LeaderProfile;

            float threshold = candidateProfile != null ? candidateProfile.StepUpThreshold : 0f;
            if (score >= threshold && score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null)
        {
            lastPromotedLeader = PromoteToLeader(bestCandidate);
            return lastPromotedLeader;
        }

        lastPromotedLeader = null;
        ApplyLeaderlessFallbackToSurvivors();
        OnLeaderChanged?.Invoke(this, null);
        return null;
    }

    private void ApplyLeaderlessFallbackToSurvivors()
    {
        LeaderlessBehavior behavior = LeaderlessBehavior.Dissolve;
        var profile = LeaderProfile;
        if (profile != null)
        {
            behavior = profile.LeaderlessBehavior;
        }
        else
        {
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m != null && m.IsAlive && m.BaseAI != null && m.BaseAI.AdventurerProfile != null)
                {
                    behavior = m.BaseAI.AdventurerProfile.LeaderlessBehavior;
                    break;
                }
            }
        }

        bool stayLoose = LeaderCheckpointTuningSO.ShouldStayLooseWhenLeaderlessOrDefault(checkpointTuning, behavior);

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;

            if (m.StatsManager != null)
            {
                m.StatsManager.OnCharacterDeath -= HandleMemberDeath;
            }

            if (m.IsAlive && stayLoose)
            {
                m.EnterStayLooseMode(members);
            }
            else
            {
                m.DissolveFromSquad();
            }
        }

        members.Clear();
        activeScout = null;
        scoutMember = null;
        hasActiveCheckpointOrder = false;
        blackboard.ClearOrder();
    }

    private PartyLeader PromoteToLeader(SquadMember successor)
    {
        if (successor == null)
        {
            ApplyLeaderlessFallbackToSurvivors();
            OnLeaderChanged?.Invoke(this, null);
            return null;
        }

        // Unregister successor from old squad and clear its member status
        members.Remove(successor);
        successor.LeaveSquad();

        // Attach PartyLeader component to successor if not present
        if (!successor.TryGetComponent<PartyLeader>(out var newLeader))
        {
            newLeader = successor.gameObject.AddComponent<PartyLeader>();
        }

        // Inherit tactical & checkpoint parameters
        newLeader.leashDistance = this.leashDistance;
        newLeader.fallBackHealthThreshold = this.fallBackHealthThreshold;
        newLeader.allowSuccession = this.allowSuccession;
        newLeader.formationConfig = this.formationConfig;
        newLeader.formationSlots = this.formationSlots;
        newLeader.marchingForward = this.marchingForward;
        newLeader.checkpointTuning = this.checkpointTuning;
        newLeader.standInRewardProvider = this.standInRewardProvider;
        newLeader.customRewardProvider = this.customRewardProvider;
        newLeader.recoveryItemsRemaining = this.recoveryItemsRemaining;
        newLeader.hadTankInParty = this.hadTankInParty;
        newLeader.hadHealerInParty = this.hadHealerInParty;
        newLeader.RecordRoleIfKey(this.EffectiveLeaderFunction);
        newLeader.RecordRoleIfKey(newLeader.EffectiveLeaderFunction);

        // Inherit shared SquadBlackboard state (known enemies, shared hazards, RetreatBlocked, etc.)
        newLeader.Blackboard.CopyStateFrom(this.blackboard);
        newLeader.Blackboard.LeaderTransform = newLeader.transform;

        // If current leader had SquadTacticalEvaluator, preserve autonomous AI on successor
        if (TryGetComponent<SquadTacticalEvaluator>(out var oldEvaluator))
        {
            if (!successor.TryGetComponent<SquadTacticalEvaluator>(out var newEvaluator))
            {
                newEvaluator = successor.gameObject.AddComponent<SquadTacticalEvaluator>();
            }
            newEvaluator.AutonomousAI = oldEvaluator.AutonomousAI;
        }

        // Inherit dungeon room loop state
        newLeader.currentRoom = this.currentRoom;
        newLeader.targetDoor = this.targetDoor;
        newLeader.currentRoomLoopState = this.currentRoomLoopState;

        // Remaining living members accept the new leader with probability squadOrderCompliance; refusers act alone
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;

            if (m.StatsManager != null)
            {
                m.StatsManager.OnCharacterDeath -= HandleMemberDeath;
            }

            if (!m.IsAlive || m == successor)
            {
                m.DissolveFromSquad();
                continue;
            }

            var memberProfile = (m.BaseAI != null && m.BaseAI.AdventurerProfile != null)
                ? m.BaseAI.AdventurerProfile
                : LeaderProfile;

            float compliance = memberProfile != null ? Mathf.Clamp01(memberProfile.SquadOrderCompliance) : 1.0f;
            float roll = compliance >= 1.0f ? 0f : RandomUtil.Range(0f, 1f);

            if (roll <= compliance)
            {
                newLeader.RegisterMember(m);
            }
            else
            {
                // Refuser leaves the squad and only retains its own CharacterAIMemory
                m.DissolveFromSquad();
            }
        }

        members.Clear();
        activeScout = null;
        scoutMember = null;

        OnLeaderChanged?.Invoke(this, newLeader);
        AdventurerEventHub.RaiseLeaderChanged(this, newLeader);

        // New leader immediately evaluates a checkpoint decision
        newLeader.RequestCheckpoint();
        return newLeader;
    }
}
