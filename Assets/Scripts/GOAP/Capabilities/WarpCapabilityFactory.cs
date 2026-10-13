using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class WarpCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("WarpCapability");

        builder.AddGoal<WarpGoal>()
            .AddCondition<CanWarp>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddAction<WarpAction>()
            .SetTarget<WarpTarget>()
            .AddEffect<CanWarp>(EffectType.Decrease)
            .SetBaseCost(1)
            .SetStoppingDistance(0.5f);

        builder.AddMultiSensor<WarpSensor>();

        return builder.Build();
    }
}
