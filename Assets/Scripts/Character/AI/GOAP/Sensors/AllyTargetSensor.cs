using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class AllyTargetSensor : LocalTargetSensorBase
{
    public override void Created() {}

    public override void Update() {}

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var brain = references.GetCachedComponent<BaseAI>();
        if (brain == null || brain.AllyTargets == null || brain.AllyTargets.Count == 0) return null;

        Transform bestTarget = null;
        float bestSqrDist = float.MaxValue;
        Vector3 agentPos = agent.Transform.position;

        for (int i = 0; i < brain.AllyTargets.Count; i++)
        {
            var t = brain.AllyTargets[i];
            if (t == null) continue;

            float sqrDist = (t.position - agentPos).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                bestTarget = t;
            }
        }

        if (bestTarget == null) return null;

        if (existingTarget is TransformTarget transformTarget)
        {
            return transformTarget.SetTransform(bestTarget);
        }

        return new TransformTarget(bestTarget);
    }

}