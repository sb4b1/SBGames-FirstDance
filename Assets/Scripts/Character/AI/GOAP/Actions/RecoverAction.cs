using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

[GoapId("Recover-8f4d2b19-6c3a-4e51-9b82-71c4a9d0e3f2")]
public class RecoverAction : GoapActionBase<RecoverAction.Data>
{
    public override void Created() { }

    public override bool IsValid(IActionReceiver agent, Data data)
    {
        if (data.AI == null || data.Kit == null) return false;
        return data.Kit.IsCastingItem || data.AI.CanRecoverWithItemNow();
    }

    public override void Start(IMonoAgent agent, Data data)
    {
        if (data.AI == null && agent != null && agent.Transform != null)
        {
            data.AI = agent.Transform.GetComponent<BaseAI>() ?? agent.Transform.GetComponentInParent<BaseAI>();
        }
        data.Kit = data.AI != null ? data.AI.ItemKitController : null;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        // Return true so Perform can manage standing in place vs. stepping back / moving behind Tank before casting
        return true;
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (data.AI == null && agent != null && agent.Transform != null)
        {
            data.AI = agent.Transform.GetComponent<BaseAI>() ?? agent.Transform.GetComponentInParent<BaseAI>();
        }
        if (data.Kit == null && data.AI != null)
        {
            data.Kit = data.AI.ItemKitController;
        }
        if (data.AI == null || data.Kit == null)
        {
            return ActionRunState.Stop;
        }

        // 1. If already casting an item, hold position until cast completes or is interrupted
        if (data.Kit.IsCastingItem)
        {
            data.AI.IsAgentStop(true);
            return ActionRunState.Continue;
        }

        // 2. Check if we still have a usable recovery item that matches current triggers & quota
        if (!data.AI.CanRecoverWithItemNow())
        {
            data.AI.IsAgentStop(false);
            return ActionRunState.Completed;
        }

        // 3. Check if we are at our quality-determined recovery position (in place, stepped back, or behind Tank)
        if (!data.AI.IsReadyToDrinkAtCurrentPosition())
        {
            data.AI.IsAgentStop(false);
            Vector3 desiredPos = data.AI.GetDesiredRecoveryPosition();
            if (data.Target is PositionTarget posTarget)
            {
                posTarget.SetPosition(desiredPos);
            }
            data.AI.AgentDestination(desiredPos);
            return ActionRunState.Continue;
        }

        // 4. In position: stop moving and start the interruptible item cast
        data.AI.IsAgentStop(true);
        data.AI.GetRecoveryThresholdsAndQuota(out float hpTrigger, out float manaTrigger, out int quota);

        if (data.Kit.TryStartBestNeededItem(hpTrigger, manaTrigger, quota))
        {
            return ActionRunState.Continue;
        }

        data.AI.IsAgentStop(false);
        return ActionRunState.Completed;
    }

    public override void Complete(IMonoAgent agent, Data data)
    {
        if (data.AI != null)
        {
            data.AI.IsAgentStop(false);
        }
    }

    public override void Stop(IMonoAgent agent, Data data)
    {
        if (data.Kit != null && data.Kit.IsCastingItem)
        {
            data.Kit.CancelCast("ActionStopped");
        }
        if (data.AI != null)
        {
            data.AI.IsAgentStop(false);
        }
    }

    public override void End(IMonoAgent agent, Data data)
    {
        if (data.AI != null)
        {
            data.AI.IsAgentStop(false);
        }
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }

        [GetComponent]
        public BaseAI AI { get; set; }

        public AdventurerItemKitController Kit { get; set; }
    }
}
