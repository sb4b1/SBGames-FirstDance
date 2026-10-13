using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

// Common attack capability factory
public class AttackCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("AttackCapability");

        builder.AddGoal<KillEnemyGoal>()
            .AddCondition<ThreatLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddAction<AttackAction>()
            .SetTarget<EnemyTarget>()
            .AddEffect<ThreatLevel>(EffectType.Decrease)
            .SetBaseCost(2);

        return builder.Build();
    }
}
