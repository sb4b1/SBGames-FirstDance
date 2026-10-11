using System;
using SBGames.Loop;
using UnityEngine;

/// <summary>
/// Pre-allocated, zero-allocation dual-layer AI character memory component.
/// Maintains Tactical Combat Memory (short-to-mid term) and Spatial/Dungeon Knowledge Memory (mid-to-long term).
/// Governed by PersonalityModifierSnapshot biases and synchronized with Squad dynamics and AIDecisionThrottler.
/// </summary>
[DisallowMultipleComponent]
public class CharacterAIMemory : MonoBehaviour
{
    // =========================================================
    // Memory Data Models
    // =========================================================

    [Serializable]
    public struct DamageRecord
    {
        public int SourceId;
        public float Amount;
        public float Timestamp;

        public DamageRecord(int sourceId, float amount, float timestamp)
        {
            SourceId = sourceId;
            Amount = amount;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct GrudgeRecord
    {
        public int EntityId;
        public Transform TargetTransform;
        public float GrudgeScore;
        public float LastUpdatedTime;

        public GrudgeRecord(int entityId, Transform targetTransform, float grudgeScore, float lastUpdatedTime)
        {
            EntityId = entityId;
            TargetTransform = targetTransform;
            GrudgeScore = grudgeScore;
            LastUpdatedTime = lastUpdatedTime;
        }
    }

    [Serializable]
    public struct LastKnownTargetInfo
    {
        public Transform Target;
        public Vector3 Position;
        public float FadeTimer;
        public float Duration;
        public bool HasValue;

        public LastKnownTargetInfo(Transform target, Vector3 position, float fadeTimer, float duration)
        {
            Target = target;
            Position = position;
            FadeTimer = fadeTimer;
            Duration = duration;
            HasValue = target != null && fadeTimer > 0f;
        }
    }

    [Serializable]
    public struct HazardMemoryRecord
    {
        public Vector2Int GridPos;
        public Vector3 WorldPos;
        public float DangerLevel;
        public float ExpirationTime;

        public HazardMemoryRecord(Vector2Int gridPos, Vector3 worldPos, float dangerLevel, float expirationTime)
        {
            GridPos = gridPos;
            WorldPos = worldPos;
            DangerLevel = dangerLevel;
            ExpirationTime = expirationTime;
        }
    }

    // =========================================================
    // Component References
    // =========================================================

    [Header("Dependencies")]
    [SerializeField] private BaseAI baseAI;
    [SerializeField] private CharacterStatsManager statsManager;
    [SerializeField] private CharacterBrain characterBrain;

    public BaseAI BaseAI => baseAI != null ? baseAI : (baseAI = GetComponent<BaseAI>() ?? GetComponentInChildren<BaseAI>() ?? GetComponentInParent<BaseAI>());
    public CharacterStatsManager StatsManager => statsManager != null ? statsManager : (statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>());
    public CharacterBrain CharacterBrain => characterBrain != null ? characterBrain : (characterBrain = GetComponent<CharacterBrain>() ?? GetComponentInParent<CharacterBrain>() ?? GetComponentInChildren<CharacterBrain>());

    // =========================================================
    // Tactical Combat Memory (Short-to-Mid Term)
    // =========================================================

    [Header("Last Attacker & Retaliation")]
    [SerializeField] private int lastAttackerId;
    [SerializeField] private Transform lastAttackerTransform;
    [SerializeField] private float retaliationTimer;
    [SerializeField] private float baseRetaliationDuration = 4.0f;

    [Header("Damage History Ring Buffer")]
    private const int DAMAGE_HISTORY_CAPACITY = 6;
    [SerializeField] private DamageRecord[] damageHistory = new DamageRecord[DAMAGE_HISTORY_CAPACITY];
    [SerializeField] private int damageHistoryHead;
    [SerializeField] private int damageHistoryCount;

    [Header("Grudge / Threat Tracking")]
    private const int MAX_GRUDGES = 8;
    [SerializeField] private GrudgeRecord[] grudgeRecords = new GrudgeRecord[MAX_GRUDGES];
    [SerializeField] private int grudgeCount;
    [SerializeField] private float grudgeDecayRate = 2.5f;

    [Header("Last Known Target")]
    [SerializeField] private LastKnownTargetInfo lastKnownTarget;

    // =========================================================
    // Spatial & Dungeon Knowledge Memory (Mid-to-Long Term)
    // =========================================================

    [Header("Hazard & Trap Memory")]
    private const int MAX_HAZARDS = 16;
    [SerializeField] private HazardMemoryRecord[] hazardRecords = new HazardMemoryRecord[MAX_HAZARDS];
    [SerializeField] private int hazardCount;
    [SerializeField] private float defaultHazardDuration = 30f;

    private CoreGameLoop coreGameLoop;

    // =========================================================
    // Public Properties
    // =========================================================

    public int LastAttackerId => lastAttackerId;
    public Transform LastAttackerTransform => lastAttackerTransform;
    public float RetaliationTimer => retaliationTimer;
    public bool HasRecentAttacker => retaliationTimer > 0f && lastAttackerTransform != null;

    public bool HasLastKnownTarget => lastKnownTarget.HasValue && lastKnownTarget.FadeTimer > 0f && lastKnownTarget.Target != null;
    public Vector3 LastKnownPosition => lastKnownTarget.Position;
    public Transform LastKnownTarget => lastKnownTarget.Target;

    public int GrudgeCount => grudgeCount;
    public int HazardCount => hazardCount;
    public int DamageRecordCount => damageHistoryCount;

    // =========================================================
    // Lifecycle & Subscriptions
    // =========================================================

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (statsManager != null)
        {
            statsManager.OnDamaged -= HandleOnDamaged;
            statsManager.OnDamaged += HandleOnDamaged;
        }

        SubscribeToGameLoop();
    }

    private void OnDisable()
    {
        if (statsManager != null)
        {
            statsManager.OnDamaged -= HandleOnDamaged;
        }

        UnsubscribeFromGameLoop();
    }

    private void ResolveDependencies()
    {
        if (baseAI == null) baseAI = GetComponent<BaseAI>() ?? GetComponentInChildren<BaseAI>() ?? GetComponentInParent<BaseAI>();
        if (statsManager == null) statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        if (characterBrain == null) characterBrain = GetComponent<CharacterBrain>() ?? GetComponentInParent<CharacterBrain>() ?? GetComponentInChildren<CharacterBrain>();
    }

    private void SubscribeToGameLoop()
    {
        if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
        if (coreGameLoop != null)
        {
            coreGameLoop.OnPhaseChanged -= HandleGameLoopPhaseChanged;
            coreGameLoop.OnPhaseChanged += HandleGameLoopPhaseChanged;
        }
    }

    private void UnsubscribeFromGameLoop()
    {
        if (coreGameLoop != null)
        {
            coreGameLoop.OnPhaseChanged -= HandleGameLoopPhaseChanged;
        }
    }

    private void HandleGameLoopPhaseChanged(GamePhase newPhase)
    {
        // When transitioning between Battle and Planning phases, clear tactical combat memory
        // while persisting known dungeon hazard grid data for surviving units
        if (newPhase == GamePhase.Planning || newPhase == GamePhase.GameOver)
        {
            ResetCombatMemory();
        }
    }

    private void HandleOnDamaged(DamageData damage)
    {
        GameObject attackerGo = null;
        if (damage.Source is GameObject go)
        {
            attackerGo = go;
        }
        else if (damage.Source is Component comp)
        {
            attackerGo = comp.gameObject;
        }

        RecordDamageIntake(in damage, attackerGo);
    }

    // =========================================================
    // Tactical Combat Memory API
    // =========================================================

    /// <summary>
    /// Records a received damage instance into the fixed ring buffer and updates grudge & retaliation windows.
    /// Strictly 0 B runtime allocations.
    /// </summary>
    public void RecordDamageIntake(in DamageData damage, GameObject attacker)
    {
        int sourceId = attacker != null ? attacker.GetEntityId().GetHashCode() : 0;
        float currentTime = Time.time;

        // 1. Ring buffer insert
        damageHistory[damageHistoryHead] = new DamageRecord(sourceId, damage.Amount, currentTime);
        damageHistoryHead = (damageHistoryHead + 1) % DAMAGE_HISTORY_CAPACITY;
        if (damageHistoryCount < DAMAGE_HISTORY_CAPACITY)
        {
            damageHistoryCount++;
        }

        if (attacker == null) return;

        // 2. Retaliation window
        lastAttackerId = sourceId;
        lastAttackerTransform = attacker.transform;
        retaliationTimer = baseRetaliationDuration;

        // 3. Grudge score weighting (scaled by Personality)
        float grudgeWeight = (BaseAI != null && BaseAI.Personality != null)
            ? BaseAI.Personality.Snapshot.GrudgeWeightMultiplier
            : 1.0f;

        float grudgeIncrement = damage.Amount * grudgeWeight;
        AddGrudge(sourceId, attacker.transform, grudgeIncrement, currentTime);
    }

    private void AddGrudge(int entityId, Transform targetTransform, float amount, float currentTime)
    {
        // Search existing grudge record
        for (int i = 0; i < grudgeCount; i++)
        {
            if (grudgeRecords[i].EntityId == entityId)
            {
                grudgeRecords[i].GrudgeScore += amount;
                grudgeRecords[i].TargetTransform = targetTransform;
                grudgeRecords[i].LastUpdatedTime = currentTime;
                return;
            }
        }

        // New record: append if space available
        if (grudgeCount < MAX_GRUDGES)
        {
            grudgeRecords[grudgeCount] = new GrudgeRecord(entityId, targetTransform, amount, currentTime);
            grudgeCount++;
            return;
        }

        // Buffer full: replace entry with lowest score
        int lowestIndex = 0;
        float lowestScore = float.MaxValue;
        for (int i = 0; i < MAX_GRUDGES; i++)
        {
            if (grudgeRecords[i].GrudgeScore < lowestScore)
            {
                lowestScore = grudgeRecords[i].GrudgeScore;
                lowestIndex = i;
            }
        }

        grudgeRecords[lowestIndex] = new GrudgeRecord(entityId, targetTransform, amount, currentTime);
    }

    /// <summary>
    /// Gets the current accumulated grudge score for the specified entity ID without managed allocations.
    /// </summary>
    public float GetGrudgeScore(int entityId)
    {
        if (entityId == 0) return 0f;
        for (int i = 0; i < grudgeCount; i++)
        {
            if (grudgeRecords[i].EntityId == entityId)
            {
                return grudgeRecords[i].GrudgeScore;
            }
        }
        return 0f;
    }

    /// <summary>
    /// Calculates total damage intake over the specified rolling time window (seconds).
    /// Used to calculate burst damage velocity (HP deficit velocity) for survival panic.
    /// </summary>
    public float GetRecentDamage(float windowSeconds)
    {
        float cutoff = Time.time - windowSeconds;
        float total = 0f;

        for (int i = 0; i < damageHistoryCount; i++)
        {
            ref readonly var record = ref damageHistory[i];
            if (record.Timestamp >= cutoff)
            {
                total += record.Amount;
            }
        }

        return total;
    }

    /// <summary>
    /// Records the last known position of a target before it breaks line-of-sight.
    /// </summary>
    public void RecordLastKnownPosition(Transform target, Vector3 position, float duration)
    {
        if (target == null) return;

        float pruneMultiplier = (BaseAI != null && BaseAI.Personality != null)
            ? BaseAI.Personality.Snapshot.TargetPruneDelayMultiplier
            : 1.0f;

        float effectiveDuration = Mathf.Max(0.5f, duration * pruneMultiplier);
        lastKnownTarget = new LastKnownTargetInfo(target, position, effectiveDuration, effectiveDuration);
    }

    /// <summary>
    /// For Pragmatic / Cautious units: immediately clears grudge and target tracking for an unreachable or immune entity.
    /// </summary>
    public void PruneUnreachableTarget(int entityId)
    {
        for (int i = 0; i < grudgeCount; i++)
        {
            if (grudgeRecords[i].EntityId == entityId)
            {
                grudgeRecords[i] = grudgeRecords[grudgeCount - 1];
                grudgeRecords[grudgeCount - 1] = default;
                grudgeCount--;
                break;
            }
        }

        if (lastAttackerId == entityId)
        {
            lastAttackerId = 0;
            lastAttackerTransform = null;
            retaliationTimer = 0f;
        }

        if (lastKnownTarget.HasValue && lastKnownTarget.Target != null && lastKnownTarget.Target.gameObject.GetEntityId().GetHashCode() == entityId)
        {
            lastKnownTarget = default;
        }
    }

    // =========================================================
    // Spatial & Dungeon Knowledge Memory API
    // =========================================================

    /// <summary>
    /// Caches grid coordinates and world position of a triggered or witnessed trap hazard.
    /// Immediately synchronizes with SquadBlackboard if this character belongs to a squad.
    /// </summary>
    public void RecordHazard(Vector2Int gridCoord, Vector3 worldPos, float dangerLevel, float duration = 30f)
    {
        float durationMultiplier = (BaseAI != null && BaseAI.Personality != null)
            ? BaseAI.Personality.Snapshot.HazardMemoryDurationMultiplier
            : 1.0f;

        float effectiveDuration = duration * durationMultiplier;
        float expirationTime = Time.time + effectiveDuration;

        // Check if hazard coordinate is already cached locally
        for (int i = 0; i < hazardCount; i++)
        {
            if (hazardRecords[i].GridPos == gridCoord || (hazardRecords[i].WorldPos - worldPos).sqrMagnitude < 0.25f)
            {
                hazardRecords[i].ExpirationTime = Mathf.Max(hazardRecords[i].ExpirationTime, expirationTime);
                hazardRecords[i].DangerLevel = Mathf.Max(hazardRecords[i].DangerLevel, dangerLevel);
                SyncHazardToSquad(gridCoord, worldPos, dangerLevel, effectiveDuration);
                return;
            }
        }

        // Insert new hazard locally
        if (hazardCount < MAX_HAZARDS)
        {
            hazardRecords[hazardCount] = new HazardMemoryRecord(gridCoord, worldPos, dangerLevel, expirationTime);
            hazardCount++;
        }
        else
        {
            // Replace entry with earliest expiration time
            int earliestIndex = 0;
            float earliestTime = float.MaxValue;
            for (int i = 0; i < MAX_HAZARDS; i++)
            {
                if (hazardRecords[i].ExpirationTime < earliestTime)
                {
                    earliestTime = hazardRecords[i].ExpirationTime;
                    earliestIndex = i;
                }
            }
            hazardRecords[earliestIndex] = new HazardMemoryRecord(gridCoord, worldPos, dangerLevel, expirationTime);
        }

        SyncHazardToSquad(gridCoord, worldPos, dangerLevel, effectiveDuration);
    }

    private void SyncHazardToSquad(Vector2Int gridCoord, Vector3 worldPos, float dangerLevel, float duration)
    {
        if (BaseAI != null && BaseAI.HasSquadLeader && BaseAI.SquadMember.Leader.Blackboard != null)
        {
            BaseAI.SquadMember.Leader.Blackboard.ReportHazard(gridCoord, worldPos, dangerLevel, duration);
        }
    }

    /// <summary>
    /// Checks whether a given position falls within the danger radius of any locally remembered or squad-shared hazards.
    /// Takes into account personality "Tunnel Vision" (diminished trap hazard awareness during active combat).
    /// Strictly 0 B runtime allocations.
    /// </summary>
    public bool IsPositionHazardous(Vector3 worldPos, float safetyRadius = 1.2f)
    {
        // 1. Tunnel Vision Check: Reckless / Bloodthirsty units ignore or downscale hazard checks in combat
        if (BaseAI != null && BaseAI.ThreatLevel > 0 && BaseAI.Personality != null && BaseAI.Personality.Snapshot.HasTunnelVision)
        {
            return false;
        }

        float sqrRadius = safetyRadius * safetyRadius;
        float currentTime = Time.time;

        // 2. Local Hazard Records Check
        for (int i = 0; i < hazardCount; i++)
        {
            ref readonly var hazard = ref hazardRecords[i];
            if (hazard.ExpirationTime < currentTime) continue;

            if ((hazard.WorldPos - worldPos).sqrMagnitude <= sqrRadius)
            {
                return true;
            }
        }

        // 3. Squad Shared Hazards Check (inherits knowledge from squad without needing to step on it)
        if (BaseAI != null && BaseAI.HasSquadLeader && BaseAI.SquadMember.Leader.Blackboard != null)
        {
            if (BaseAI.SquadMember.Leader.Blackboard.IsPositionHazardous(worldPos, safetyRadius))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // Throttled Decay & Reset Operations
    // =========================================================

    /// <summary>
    /// Executes periodic decay of Grudge scores, retaliation timer, last known target position, and hazard memories.
    /// Must be invoked by <see cref="AIDecisionThrottler"/> (every 0.2s - 0.4s) instead of Update().
    /// Strictly 0 B runtime allocations.
    /// </summary>
    public void TickMemoryDecay(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 1. Retaliation Window Decay
        if (retaliationTimer > 0f)
        {
            retaliationTimer -= deltaTime;
            if (retaliationTimer <= 0f)
            {
                retaliationTimer = 0f;
                lastAttackerId = 0;
                lastAttackerTransform = null;
            }
        }

        // 2. Last Known Target Fade Decay
        if (lastKnownTarget.HasValue)
        {
            lastKnownTarget.FadeTimer -= deltaTime;
            if (lastKnownTarget.FadeTimer <= 0f || lastKnownTarget.Target == null)
            {
                lastKnownTarget = default;
            }
        }

        // 3. Grudge Decay
        float decayAmount = grudgeDecayRate * deltaTime;
        for (int i = grudgeCount - 1; i >= 0; i--)
        {
            grudgeRecords[i].GrudgeScore -= decayAmount;
            if (grudgeRecords[i].GrudgeScore <= 0f || grudgeRecords[i].TargetTransform == null)
            {
                // Swap-remove with last element (zero alloc)
                grudgeRecords[i] = grudgeRecords[grudgeCount - 1];
                grudgeRecords[grudgeCount - 1] = default;
                grudgeCount--;
            }
        }

        // 4. Hazard Expiration Decay
        float currentTime = Time.time;
        for (int i = hazardCount - 1; i >= 0; i--)
        {
            if (hazardRecords[i].ExpirationTime <= currentTime)
            {
                hazardRecords[i] = hazardRecords[hazardCount - 1];
                hazardRecords[hazardCount - 1] = default;
                hazardCount--;
            }
        }
    }

    /// <summary>
    /// Clears tactical combat memory (Last Attacker, Grudge, Damage history, Last known target)
    /// while preserving dungeon hazard spatial memory. Called on round phase change (Battle -> Planning).
    /// </summary>
    public void ResetCombatMemory()
    {
        lastAttackerId = 0;
        lastAttackerTransform = null;
        retaliationTimer = 0f;
        lastKnownTarget = default;

        grudgeCount = 0;
        for (int i = 0; i < MAX_GRUDGES; i++)
        {
            grudgeRecords[i] = default;
        }

        damageHistoryHead = 0;
        damageHistoryCount = 0;
        for (int i = 0; i < DAMAGE_HISTORY_CAPACITY; i++)
        {
            damageHistory[i] = default;
        }
    }

    /// <summary>
    /// Clears remembered dungeon hazard tiles.
    /// </summary>
    public void ResetSpatialMemory()
    {
        hazardCount = 0;
        for (int i = 0; i < MAX_HAZARDS; i++)
        {
            hazardRecords[i] = default;
        }
    }

    /// <summary>
    /// Clears both tactical combat and spatial hazard memories completely.
    /// </summary>
    public void ResetAllMemory()
    {
        ResetCombatMemory();
        ResetSpatialMemory();
    }
}
