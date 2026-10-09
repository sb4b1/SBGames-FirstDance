using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class SurvivalCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("SurvivalCapability");

        builder.AddGoal<SurviveGoal>()
            .AddCondition<FearLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddGoal<KeepDistanceGoal>()
            .AddCondition<FearLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddAction<FleeAction>()
            .SetTarget<KeepDistanceTarget>()
            .AddEffect<FearLevel>(EffectType.Decrease)
            .SetBaseCost(1);

        return builder.Build();
    }
}
