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
            if (data.CurrentTarget == null) return 999;

            return (int)Vector3.Distance(agent.Transform.position, data.CurrentTarget.position);
        });

        this.AddLocalTargetSensor<KeepDistanceTarget>((agent, references, target) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data.CurrentTarget == null) return null;
            // find opposite direction of target
            Vector3 a = agent.Transform.position - (data.CurrentTarget.transform.position - agent.Transform.position);

            // find perpendicular vector from vector a
            var b = -a.x / a.y;

            // get random position fron perpendicular vector
            //Vector3 position = new Vector3(1, b, 0).normalized;

            int count = 0;
            while (count < 5)
            {

                if (NavMesh.SamplePosition(a * RandomUtil.Range(-2, 2), out NavMeshHit hit, 1, NavMesh.AllAreas))
                {
                    if (target is PositionTarget positionTarget)
                    {
                        return positionTarget.SetPosition(hit.position);
                    }

                    return new PositionTarget(hit.position);
                }

                count++;
            }

            return new PositionTarget(agent.Transform.position);
        });
    }

}