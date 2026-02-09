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

            return (int)data.ThreatLevel;
        });

        this.AddLocalWorldSensor<FearLevel>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();

            return (int) data.FearLevel;
        });

        this.AddLocalWorldSensor<AgentLife>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();

            return (int) data.AgentLife;
        });

        this.AddLocalTargetSensor<SelfTarget>((agent, references, target) =>
        {
            return new TransformTarget(agent.Transform);
        });
    }

}