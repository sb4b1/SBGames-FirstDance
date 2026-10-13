using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class AwarenessSensor : MultiSensorBase
{
    public override void Created() {}

    public override void Update() {}

    public AwarenessSensor()
    {
        this.AddLocalWorldSensor<ThreatLevel>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 0;
            return (int)data.ThreatLevel;
        });

        this.AddLocalWorldSensor<FearLevel>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 0;
            return (int)data.FearLevel;
        });

        this.AddLocalWorldSensor<AgentLife>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 0;
            return (int)data.AgentLife;
        });

        this.AddLocalWorldSensor<AgentMana>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 0;
            return (int)data.AgentMana;
        });

        this.AddLocalWorldSensor<HasAlertedAllies>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 1;
            return data.HasAlertedAllies ? 1 : 0;
        });

        this.AddLocalTargetSensor<SelfTarget>((agent, references, target) =>
        {
            if (target is TransformTarget transformTarget)
            {
                return transformTarget.SetTransform(agent.Transform);
            }
            return new TransformTarget(agent.Transform);
        });
    }

}