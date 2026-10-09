using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyLeader))]
public class SquadTacticalEvaluator : MonoBehaviour
{
    [Header("Evaluation Settings")]
    [SerializeField] private bool autonomousAI = true;
    [SerializeField] private float evaluationInterval = 0.5f;

    private PartyLeader leader;
    private BaseAI baseAI;
    private CharacterStatsManager statsManager;
    private float timer;

    public bool AutonomousAI
    {
        get => autonomousAI;
        set => autonomousAI = value;
    }

    private void Awake()
    {
        leader = GetComponent<PartyLeader>();
        statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        baseAI = GetComponentInChildren<BaseAI>();
    }

    private void Update()
    {
        if (!autonomousAI || leader == null || !leader.IsAlive) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = evaluationInterval;

        EvaluateTactics();
    }

    /// <summary>
    /// Evaluates squad metrics and battlefield state, issuing zero-alloc tactical directives.
    /// </summary>
    public void EvaluateTactics()
    {
        if (baseAI == null) baseAI = GetComponentInChildren<BaseAI>();
        if (statsManager == null) statsManager = GetComponent<CharacterStatsManager>();

        var members = leader.Members;

        // 1. Compute Squad Health Metrics
        float totalHealthRatio = 0f;
        int activeCount = 0;

        if (statsManager != null && statsManager.MaxLife.Value > 0f)
        {
            totalHealthRatio += Mathf.Clamp01(statsManager.Life / statsManager.MaxLife.Value);
            activeCount++;
        }

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || !m.IsAlive) continue;

            if (m.TryGetComponent<CharacterStatsManager>(out var mStats) && mStats.MaxLife.Value > 0f)
            {
                totalHealthRatio += Mathf.Clamp01(mStats.Life / mStats.MaxLife.Value);
                activeCount++;
            }
        }

        float avgHealth = activeCount > 0 ? (totalHealthRatio / activeCount) : 0f;
        leader.Blackboard.UpdateSquadMetrics(avgHealth, activeCount);

        bool hasEnemies = baseAI != null && baseAI.EnemyTargets != null && baseAI.EnemyTargets.Count > 0;

        // 2. Fall Back if average health is critically low and enemies are threatening
        if (hasEnemies && avgHealth < leader.FallBackHealthThreshold)
        {
            if (leader.Blackboard.CurrentOrder != SquadOrderType.FallBack)
            {
                leader.OrderFallBack();
            }
            return;
        }

        // 3. Focus Fire if leader has enemies
        if (hasEnemies)
        {
            Transform bestTarget = baseAI.CurrentTarget;
            if (bestTarget == null)
            {
                // Select first valid enemy in list
                for (int i = 0; i < baseAI.EnemyTargets.Count; i++)
                {
                    var t = baseAI.EnemyTargets[i];
                    if (t != null)
                    {
                        bestTarget = t;
                        break;
                    }
                }
            }

            if (bestTarget != null)
            {
                if (leader.Blackboard.CurrentOrder != SquadOrderType.FocusFire || leader.Blackboard.FocusFireTarget != bestTarget)
                {
                    leader.OrderFocusFire(bestTarget);
                }
                return;
            }
        }

        // 4. Rally / Leash Check: check if any squad member is too far
        float sqrLeash = leader.LeashDistance * leader.LeashDistance;
        Vector3 leaderPos = transform.position;
        bool memberTooFar = false;

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || !m.IsAlive) continue;

            if ((m.transform.position - leaderPos).sqrMagnitude > sqrLeash)
            {
                memberTooFar = true;
                break;
            }
        }

        if (memberTooFar)
        {
            if (leader.Blackboard.CurrentOrder != SquadOrderType.FollowLeader)
            {
                leader.OrderFollowLeader();
            }
            return;
        }

        // 5. Default: Clear order if no active tactical condition applies
        if (leader.Blackboard.CurrentOrder != SquadOrderType.None)
        {
            leader.ClearOrder();
        }
    }
}
