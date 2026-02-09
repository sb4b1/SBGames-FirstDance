using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class EnemyTargetSensor : LocalTargetSensorBase
{

    public override void Created() { }

    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var brain = references.GetCachedComponent<BaseAI>();
        if (brain.EnemyTargets == null) return null;
        Transform target = null;
        foreach (var transform in brain.EnemyTargets)
        {
            if (target == null)
            {
                target = transform;
                continue;
            }

            target = Vector3.Distance(transform.position, agent.Transform.position) < Vector3.Distance(target.position, agent.Transform.position)
                ? target : transform;
        }

        brain.SetTarget(target);
        
        if (existingTarget is TransformTarget transformTarget)
        {
            return transformTarget.SetTransform(target);
        }

        return new TransformTarget(target);
    }
}