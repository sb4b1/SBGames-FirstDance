using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PartyLeader : MonoBehaviour
{
    [Header("Squad Configuration")]
    [SerializeField] private bool allowSuccession = true;
    [SerializeField] private float leashDistance = 8f;
    [SerializeField] private float fallBackHealthThreshold = 0.3f;

    [Header("Runtime State")]
    [SerializeField] private SquadBlackboard blackboard = new();
    [SerializeField] private List<SquadMember> members = new(8);

    private CharacterStatsManager statsManager;
    private SquadMember activeScout;
    private Coroutine scoutPanicCoroutine;

    public SquadBlackboard Blackboard => blackboard;
    public List<SquadMember> Members => members;
    public float LeashDistance => leashDistance;
    public float FallBackHealthThreshold => fallBackHealthThreshold;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;
    public SquadMember ActiveScout => activeScout;

    public event System.Action<SquadMember> OnScoutDispatched;
    public event System.Action<SquadMember> OnScoutDebriefed;
    public event System.Action<SquadMember> OnScoutEliminated;

    private void Awake()
    {
        blackboard.LeaderTransform = transform;
        statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
    }

    private void OnEnable()
    {
        if (statsManager != null)
        {
            statsManager.OnCharacterDeath += HandleLeaderDeath;
        }
    }

    private void OnDisable()
    {
        if (statsManager != null)
        {
            statsManager.OnCharacterDeath -= HandleLeaderDeath;
        }
    }

    private void OnDestroy()
    {
        DisbandSquad();
    }

    public void RegisterMember(SquadMember member)
    {
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
    }

    public SquadMember FindScout()
    {
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

    public void ClearOrder()
    {
        blackboard.ClearOrder();
    }

    public void DisbandSquad()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            var m = members[i];
            if (m != null)
            {
                m.AssignLeader(null);
            }
        }
        members.Clear();
        blackboard.ClearOrder();
    }

    private void HandleLeaderDeath(CharacterStatsManager stats)
    {
        if (allowSuccession)
        {
            // Find first living successor
            SquadMember successor = null;
            for (int i = 0; i < members.Count; i++)
            {
                var candidate = members[i];
                if (candidate != null && candidate.IsAlive && candidate.gameObject != gameObject)
                {
                    successor = candidate;
                    break;
                }
            }

            if (successor != null)
            {
                PromoteToLeader(successor);
                return;
            }
        }

        DisbandSquad();
    }

    private void PromoteToLeader(SquadMember successor)
    {
        if (successor == null)
        {
            DisbandSquad();
            return;
        }

        // Unregister successor from old squad and clear its member status
        members.Remove(successor);
        successor.LeaveSquad();

        // Attach PartyLeader component to successor if not present
        if (!successor.TryGetComponent<PartyLeader>(out var newLeader))
        {
            newLeader = successor.gameObject.AddComponent<PartyLeader>();
        }

        // Inherit tactical parameters
        newLeader.leashDistance = this.leashDistance;
        newLeader.fallBackHealthThreshold = this.fallBackHealthThreshold;
        newLeader.allowSuccession = this.allowSuccession;

        // If current leader had SquadTacticalEvaluator, preserve autonomous AI on successor
        if (TryGetComponent<SquadTacticalEvaluator>(out var oldEvaluator))
        {
            if (!successor.TryGetComponent<SquadTacticalEvaluator>(out var newEvaluator))
            {
                newEvaluator = successor.gameObject.AddComponent<SquadTacticalEvaluator>();
            }
            newEvaluator.AutonomousAI = oldEvaluator.AutonomousAI;
        }

        // Migrate remaining living members to new leader
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null && m.IsAlive && m != successor)
            {
                newLeader.RegisterMember(m);
            }
        }

        members.Clear();
    }
}
