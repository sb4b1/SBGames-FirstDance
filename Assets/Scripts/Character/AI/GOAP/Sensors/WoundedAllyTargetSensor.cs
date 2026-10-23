using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class WoundedAllyTargetSensor : LocalTargetSensorBase
{
    public override void Created() { }

    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var brain = references.GetCachedComponent<BaseAI>();
        if (brain == null) return null;

        var rule = brain.GetBestReadyHealSkillSelfTargetRule();
        if (brain.GetBestWoundedAllyTarget(out Transform target, out _, rule))
        {
            if (existingTarget is TransformTarget transformTarget)
            {
                return transformTarget.SetTransform(target);
            }
            return new TransformTarget(target);
        }

        return null;
    }
}
