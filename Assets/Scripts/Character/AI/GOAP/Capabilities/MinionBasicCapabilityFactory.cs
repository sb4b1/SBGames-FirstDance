using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class MinionBasicCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("MinionBasicCapability");

        builder.AddTargetSensor<EnemyTargetSensor>()
            .SetTarget<EnemyTarget>();
        builder.AddMultiSensor<AwarenessSensor>();
        builder.AddMultiSensor<EnemyDistanceSensor>();

        return builder.Build();
    }
}
