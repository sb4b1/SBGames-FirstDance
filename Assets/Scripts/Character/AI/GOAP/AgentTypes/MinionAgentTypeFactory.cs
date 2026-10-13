using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class MinionAgentTypeFactory : AgentTypeFactoryBase
{
    public override IAgentTypeConfig Create()
    {
        var factory = CreateBuilder("MinionAgent");

        factory.AddCapability<WanderCapabilityFactory>();
        factory.AddCapability<AttackCapabilityFactory>();
        factory.AddCapability<MinionBasicCapabilityFactory>();
        factory.AddCapability<SquadCapabilityFactory>();
        factory.AddCapability<SurvivalCapabilityFactory>();

        return factory.Build();
    }
}
