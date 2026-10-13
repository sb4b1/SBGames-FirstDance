using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

[GoapId("Warp-7a1e4c39-2d5f-4a81-b631-92e1f4a8b7c3")]
public class WarpAction : GoapActionBase<WarpAction.Data>
{
    public override void Created() { }

    public override bool IsValid(IActionReceiver agent, Data data)
    {
        if (data.AI == null || data.Kit == null) return false;
        return (data.Kit.IsCastingItem && data.Kit.CastingCategory == ConsumableItemCategory.Warp) || data.AI.CanWarpNow();
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
        // Return true so Perform manages standing in place vs. stepping out of melee before casting
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

        // 1. If already casting warp item, hold position until cast completes or is interrupted
        if (data.Kit.IsCastingItem && data.Kit.CastingCategory == ConsumableItemCategory.Warp)
        {
            data.AI.IsAgentStop(true);
            return ActionRunState.Continue;
        }

        // 2. Check if we still can and should warp
        if (!data.AI.CanWarpNow())
        {
            data.AI.IsAgentStop(false);
            return ActionRunState.Completed;
        }

        // 3. Check if positioned safely (stepped out of melee if required)
        if (!data.AI.IsReadyToWarpAtCurrentPosition())
        {
            data.AI.IsAgentStop(false);
            Vector3 desiredPos = data.AI.GetDesiredWarpPosition();
            if (data.Target is PositionTarget posTarget)
            {
                posTarget.SetPosition(desiredPos);
            }
            data.AI.AgentDestination(desiredPos);
            return ActionRunState.Continue;
        }

        // 4. In position: stop moving and start the interruptible warp item cast
        data.AI.IsAgentStop(true);
        if (data.Kit.TryStartUse(ConsumableItemCategory.Warp))
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
        if (data.Kit != null && data.Kit.IsCastingItem && data.Kit.CastingCategory == ConsumableItemCategory.Warp)
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
