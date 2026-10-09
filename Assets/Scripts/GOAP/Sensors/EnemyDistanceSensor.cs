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

            for (int i = 0; i < 5; i++)
            {
                float perpFactor = RandomUtil.Range(-1.5f, 1.5f);
                float retreatDist = RandomUtil.Range(2.5f, 4.5f);
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