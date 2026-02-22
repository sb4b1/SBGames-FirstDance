using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

// Basic capability factory
public class BasicCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("BasicCapability");

        builder.AddTargetSensor<EnemyTargetSensor>()
            .SetTarget<EnemyTarget>();
        builder.AddMultiSensor<AwarenessSensor>();

        return builder.Build();
    }
}
