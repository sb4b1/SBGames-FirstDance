using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class LeaderTargetSensor : LocalTargetSensorBase
{
    public override void Created() { }
    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var ai = references.GetCachedComponent<BaseAI>();
        if (ai == null) return null;

        var leaderTransform = ai.LeaderTransform;
        if (leaderTransform == null) return null;

        if (existingTarget is TransformTarget tt)
        {
            return tt.SetTransform(leaderTransform);
        }

        return new TransformTarget(leaderTransform);
    }
}
