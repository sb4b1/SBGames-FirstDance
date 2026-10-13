using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

// Common attack capability factory
public class WanderCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("WanderCapability");

        builder.AddGoal<IdleGoal>()
            .AddCondition<IsIdle>(Comparison.GreaterThanOrEqual, 1)
            .SetBaseCost(1);

        builder.AddAction<WanderAction>()
            .SetTarget<WanderTarget>()
            .AddEffect<IsIdle>(EffectType.Increase);

        builder.AddTargetSensor<WanderTargetSensor>()
            .SetTarget<WanderTarget>();

        return builder.Build();
    }
}
