using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyLeader))]
public class SquadTacticalEvaluator : MonoBehaviour
{
    [Header("Evaluation Settings")]
    [SerializeField] private bool autonomousAI = true;
    [SerializeField] private float evaluationInterval = 0.5f;
    [Tooltip("Quiet time in seconds with no known enemies after combat ends before triggering a leader checkpoint.")]
    [SerializeField, Min(0f)] private float postCombatCheckpointDelay = 1.5f;

    private PartyLeader leader;
    private BaseAI baseAI;
    private CharacterStatsManager statsManager;
    private float timer;
    private bool wasInCombat;
    private float postCombatQuietTimer;

    public bool AutonomousAI
    {
        get => autonomousAI;
        set => autonomousAI = value;
    }

    public float PostCombatCheckpointDelay
    {
        get => postCombatCheckpointDelay;
        set => postCombatCheckpointDelay = Mathf.Max(0f, value);
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

        EvaluateTactics(evaluationInterval);
    }

    /// <summary>
    /// Evaluates squad metrics and battlefield state, issuing zero-alloc tactical directives.
    /// </summary>
    public void EvaluateTactics()
    {
        EvaluateTactics(evaluationInterval);
    }

    /// <summary>
    /// Evaluates squad metrics and battlefield state, advancing post-combat quiet timer by <paramref name="elapsedSeconds"/>.
    /// </summary>
    public void EvaluateTactics(float elapsedSeconds)
    {
        if (leader == null) leader = GetComponent<PartyLeader>();
        if (leader == null) return;
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

            var mStats = m.StatsManager;
            if (mStats != null && mStats.MaxLife.Value > 0f)
            {
                totalHealthRatio += Mathf.Clamp01(mStats.Life / mStats.MaxLife.Value);
                activeCount++;
            }
        }

        float avgHealth = activeCount > 0 ? (totalHealthRatio / activeCount) : 1f;
        leader.Blackboard.UpdateSquadMetrics(avgHealth, activeCount);

        leader.Blackboard.PruneExpiredEnemies(Time.time);
        leader.Blackboard.PruneExpiredHazards(Time.time);

        bool hasDirectEnemies = baseAI != null && baseAI.EnemyTargets != null && baseAI.EnemyTargets.Count > 0;
        bool hasKnownEnemies = leader.Blackboard.HasKnownEnemies;
        bool hasEnemies = hasDirectEnemies || hasKnownEnemies;

        if (hasEnemies)
        {
            wasInCombat = true;
            postCombatQuietTimer = 0f;
            leader.HasActiveCheckpointOrder = false;
        }
        else if (wasInCombat)
        {
            postCombatQuietTimer += Mathf.Max(0f, elapsedSeconds);
            if (postCombatQuietTimer >= postCombatCheckpointDelay)
            {
                wasInCombat = false;
                postCombatQuietTimer = 0f;
                // If room loop is active, notify it of room/combat clear; otherwise fallback to checkpoint
                if (leader.CurrentRoomLoopState == SBGames.Characters.AI.RoomLoopState.Fight)
                {
                    leader.NotifyCombatCleared();
                }
                else
                {
                    leader.RequestCheckpoint();
                }
                return;
            }
        }

        // 2. Fall Back if average health is critically low and enemies are threatening
        if (hasEnemies && avgHealth < leader.FallBackHealthThreshold)
        {
            if (leader.Blackboard.CurrentOrder != SquadOrderType.FallBack)
            {
                leader.OrderFallBack();
            }
            return;
        }

        // 3. Focus Fire if leader or squad knows of enemies
        if (hasEnemies)
        {
            Transform bestTarget = baseAI != null ? baseAI.CurrentTarget : null;
            if (bestTarget == null && hasDirectEnemies)
            {
                float bestDirectScore = float.MinValue;
                Vector3 myPos = transform.position;
                for (int i = 0; i < baseAI.EnemyTargets.Count; i++)
                {
                    var t = baseAI.EnemyTargets[i];
                    if (t == null) continue;

                    float dist = Vector3.Distance(myPos, t.position);
                    float score = baseAI.GetTraitWeightedTargetScore(t, dist);
                    if (score > bestDirectScore)
                    {
                        bestDirectScore = score;
                        bestTarget = t;
                    }
                }
            }

            // If no direct target, select best target from known enemies using trait & proximity weighting
            if (bestTarget == null && hasKnownEnemies)
            {
                float bestScore = float.MinValue;
                var known = leader.Blackboard.KnownEnemies;
                Vector3 myPos = transform.position;

                for (int i = 0; i < known.Count; i++)
                {
                    var record = known[i];
                    if (!record.IsValid(Time.time)) continue;

                    float dist = Vector3.Distance(myPos, record.Target.position);
                    float score = baseAI != null ? baseAI.GetTraitWeightedTargetScore(record.Target, dist) : (100f / (1f + dist));

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = record.Target;
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

        // If SquadOrderType.Recover is active out of combat, re-run RequestCheckpoint once all members finish recovering
        if (leader.Blackboard.CurrentOrder == SquadOrderType.Recover)
        {
            leader.CheckAndCompleteRecoverOrder();
            return;
        }

        // Preserve active checkpoint order or active scout recon when out of combat
        if (leader.HasActiveCheckpointOrder || leader.ActiveScout != null)
        {
            return;
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
