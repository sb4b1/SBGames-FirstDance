using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class LeaderTargetSensor : LocalTargetSensorBase
{
    public override void Created() { }
    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var ai = references?.GetCachedComponent<BaseAI>();
        if (ai == null && agent != null)
        {
            var agentTransform = agent.Transform != null ? agent.Transform : (agent is Component comp ? comp.transform : null);
            if (agentTransform != null)
            {
                ai = agentTransform.GetComponent<BaseAI>() ?? agentTransform.GetComponentInParent<BaseAI>() ?? agentTransform.GetComponentInChildren<BaseAI>();
            }
        }

        if (ai == null) return null;

        var leaderTransform = ai.LeaderTransform;
        if (leaderTransform == null) return null;

        Vector3 targetPos = ai.GetFormationTargetPosition();

        if (existingTarget is PositionTarget posTarget)
        {
            return posTarget.SetPosition(targetPos);
        }

        return new PositionTarget(targetPos);
    }
}
