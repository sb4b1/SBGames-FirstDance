using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

// Basic capability factory
public class BasicCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("BasicCapability");

        builder.AddGoal<KillEnemyGoal>()
            .AddCondition<ThreatLevel>(Comparison.SmallerThanOrEqual, 0)
            .SetBaseCost(1);

        builder.AddTargetSensor<EnemyTargetSensor>()
            .SetTarget<EnemyTarget>();
        builder.AddMultiSensor<AwarenessSensor>();
        builder.AddMultiSensor<CombatStatusSensor>();
        builder.AddMultiSensor<EnemyDistanceSensor>();

        return builder.Build();
    }
}
