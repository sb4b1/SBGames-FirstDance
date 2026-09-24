using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class SkillCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("SkillCapability");

        builder.AddAction<CastSkillAction>()
            .SetTarget<EnemyTarget>()
            .AddCondition<IsSkillReady>(Comparison.GreaterThanOrEqual, 1)
            .AddCondition<AgentMana>(Comparison.GreaterThanOrEqual, 5)
            .AddEffect<ThreatLevel>(EffectType.Decrease)
            .SetBaseCost(1);

        return builder.Build();
    }
}
