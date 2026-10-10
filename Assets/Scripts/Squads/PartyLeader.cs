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

    public SquadBlackboard Blackboard => blackboard;
    public List<SquadMember> Members => members;
    public float LeashDistance => leashDistance;
    public float FallBackHealthThreshold => fallBackHealthThreshold;
    public bool IsAlive => statsManager == null || statsManager.Life > 0;

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
    }

    public void UnregisterMember(SquadMember member)
    {
        if (member == null) return;
        members.Remove(member);
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
