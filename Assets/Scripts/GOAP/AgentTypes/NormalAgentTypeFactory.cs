using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;

public class NormalAgentTypeFactory : AgentTypeFactoryBase
{
    public override IAgentTypeConfig Create()
        {
            var factory = CreateBuilder("NormalAgent");
            
            factory.AddCapability<WanderCapabilityFactory>();

            return factory.Build();
        }
}