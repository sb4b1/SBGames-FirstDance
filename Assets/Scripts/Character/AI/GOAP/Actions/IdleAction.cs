using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class IdleAction : GoapActionBase<IdleAction.Data>
{
    private const float MIN_IDLE_DURATION = 1.0f;
    private const float MAX_IDLE_DURATION = 3.0f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.Timer = Random.Range(MIN_IDLE_DURATION, MAX_IDLE_DURATION);
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        // Zero-alloc orientation while holding ground / idling
        if (data.FlipRotator != null && data.AI != null)
        {
            var enemies = data.AI.EnemyTargets;
            if (enemies != null && enemies.Count > 0)
            {
                Transform nearest = null;
                float minSqrDist = float.MaxValue;
                Vector3 currentPos = agent.Transform.position;

                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null) continue;

                    float sqrDist = (enemy.position - currentPos).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        nearest = enemy;
                    }
                }

                if (nearest != null)
                {
                    float dx = nearest.position.x - currentPos.x;
                    if (Mathf.Abs(dx) > 0.05f)
                    {
                        data.FlipRotator.ApplyFlip(dx < 0f);
                    }
                }
            }
            else if (data.AI.HasSquadLeader && data.AI.LeaderTransform != null)
            {
                float dx = data.AI.LeaderTransform.position.x - agent.Transform.position.x;
                if (Mathf.Abs(dx) > 0.05f)
                {
                    data.FlipRotator.ApplyFlip(dx < 0f);
                }
            }
        }

        data.Timer -= context.DeltaTime;
        return data.Timer <= 0f ? ActionRunState.Completed : ActionRunState.Continue;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        // When idling (waiting in place or for party/underling orders), agent is considered in range
        return true;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public float Timer { get; set; }

        [GetComponent]
        public BaseAI AI { get; set; }

        [GetComponentInParent]
        public CharacterFlipRotator FlipRotator { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}
