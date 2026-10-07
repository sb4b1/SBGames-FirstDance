using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class MageAgentTypeFactory : AgentTypeFactoryBase
{
    public override IAgentTypeConfig Create()
        {
            var factory = CreateBuilder("MageAgent");
            
            factory.AddCapability<WanderCapabilityFactory>();
            factory.AddCapability<SkillCapabilityFactory>();
            factory.AddCapability<BasicCapabilityFactory>();

            return factory.Build();
        }
}