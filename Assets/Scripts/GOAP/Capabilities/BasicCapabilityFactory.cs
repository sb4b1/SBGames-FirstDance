using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

// Basic capability factory
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, null, "BasicCapabiliityFactory")]
public class BasicCapabilityFactory : CapabilityFactoryBase
{
    public override ICapabilityConfig Create()
    {
        var builder = new CapabilityBuilder("BasicCapability");

        builder.AddTargetSensor<EnemyTargetSensor>()
            .SetTarget<EnemyTarget>();
        builder.AddMultiSensor<AwarenessSensor>();
        builder.AddMultiSensor<CombatStatusSensor>();
        builder.AddMultiSensor<EnemyDistanceSensor>();

        return builder.Build();
    }
}
