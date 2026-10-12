using CrashKonijn.Goap.Runtime;
using SB4b1.Utils;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDistanceSensor : MultiSensorBase
{
    public override void Created() { }

    public override void Update() { }

    public EnemyDistanceSensor()
    {
        this.AddLocalWorldSensor<EnemyDistance>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null || data.CurrentTarget == null) return 999;

            return (int)data.TargetDistance;
        });

        this.AddLocalTargetSensor<KeepDistanceTarget>((agent, references, target) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null || data.CurrentTarget == null) return null;

            Vector3 agentPos = agent.Transform.position;
            Vector3 toTarget = data.CurrentTarget.position - agentPos;
            toTarget.z = 0f;
            Vector3 awayDir = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : Vector3.up;
            Vector3 perpDir = new Vector3(-awayDir.y, awayDir.x, 0f);

            float minRetreat = 2.5f;
            float maxRetreat = 4.5f;
            if (data.ClassStyleTable != null && data.StatsManager != null && data.StatsManager.AttackRange != null)
            {
                float preferredDist = data.GetPreferredStandingDistance(data.StatsManager.AttackRange.Value);
                minRetreat = Mathf.Max(1.0f, preferredDist * 0.85f);
                maxRetreat = Mathf.Max(minRetreat + 0.5f, preferredDist * 1.15f);
            }

            for (int i = 0; i < 5; i++)
            {
                float perpFactor = RandomUtil.Range(-1.5f, 1.5f);
                float retreatDist = RandomUtil.Range(minRetreat, maxRetreat);
                Vector3 candidatePos = agentPos + (awayDir * retreatDist) + (perpDir * perpFactor);

                if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    if (target is PositionTarget positionTarget)
                    {
                        return positionTarget.SetPosition(hit.position);
                    }

                    return new PositionTarget(hit.position);
                }
            }

            if (target is PositionTarget fallbackTarget)
            {
                return fallbackTarget.SetPosition(agentPos);
            }

            return new PositionTarget(agentPos);
        });
    }

}