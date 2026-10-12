using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class RecoveryCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("RecoveryCapability");

        builder.AddGoal<RecoverGoal>()
            .AddCondition<NeedsRecovery>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddAction<RecoverAction>()
            .SetTarget<RecoverTarget>()
            .AddEffect<NeedsRecovery>(EffectType.Decrease)
            .SetBaseCost(1)
            .SetStoppingDistance(0.5f);

        builder.AddMultiSensor<RecoverSensor>();

        return builder.Build();
    }
}
