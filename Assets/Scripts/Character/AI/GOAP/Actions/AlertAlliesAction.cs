using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class AlertAlliesAction : GoapActionBase<AlertAlliesAction.Data>
{
    private const float SHOUT_DURATION = 0.5f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.Timer = SHOUT_DURATION;
        data.Shouted = false;
        if (data.AI == null && agent != null)
        {
            data.AI = agent.Transform.GetComponent<BaseAI>() ?? agent.Transform.GetComponentInParent<BaseAI>();
        }
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (!data.Shouted)
        {
            data.Shouted = true;
            if (data.AI == null && agent != null)
            {
                data.AI = agent.Transform.GetComponent<BaseAI>() ?? agent.Transform.GetComponentInParent<BaseAI>();
            }

            if (data.AI != null)
            {
                data.AI.PerformAlertShout();
            }
        }

        data.Timer -= context.DeltaTime;
        if (data.Timer <= 0f)
        {
            return ActionRunState.Completed;
        }

        return ActionRunState.Continue;
    }

    public override void End(IMonoAgent agent, Data data)
    {
        data.Shouted = false;
    }

    public override void Stop(IMonoAgent agent, Data data)
    {
        data.Shouted = false;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public float Timer { get; set; }
        public bool Shouted { get; set; }

        public BaseAI AI { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }
    }
}
