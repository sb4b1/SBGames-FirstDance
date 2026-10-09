using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class FocusFireTargetSensor : LocalTargetSensorBase
{
    public override void Created() { }
    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var ai = references.GetCachedComponent<BaseAI>();
        if (ai == null) return null;

        var focusTarget = ai.FocusFireTarget;
        if (focusTarget == null) return null;

        if (existingTarget is TransformTarget tt)
        {
            return tt.SetTransform(focusTarget);
        }

        return new TransformTarget(focusTarget);
    }
}
