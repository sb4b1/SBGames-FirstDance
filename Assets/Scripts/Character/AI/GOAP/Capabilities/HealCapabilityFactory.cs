using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class HealCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("HealCapability");

        builder.AddGoal<HealAllyGoal>()
            .AddCondition<AlliesNeedHealing>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddAction<HealAllyAction>()
            .SetTarget<WoundedAllyTarget>()
            .AddCondition<IsSkillReady>(Comparison.GreaterThanOrEqual, 1)
            .AddCondition<AgentMana>(Comparison.GreaterThanOrEqual, 5)
            .AddEffect<AlliesNeedHealing>(EffectType.Decrease)
            .SetBaseCost(1)
            .SetValidateConditions(false)
            .SetValidateTarget(false);

        builder.AddTargetSensor<WoundedAllyTargetSensor>()
            .SetTarget<WoundedAllyTarget>();

        return builder.Build();
    }
}
