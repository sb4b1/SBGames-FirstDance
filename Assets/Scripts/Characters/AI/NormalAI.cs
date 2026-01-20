public class NormalAI : BaseAI
{
    protected override void Awake()
    {
        base.Awake();

        if (this.provider.AgentTypeBehaviour == null)
            this.provider.AgentType = this.goap.GetAgentType("NormalAgent");
    }


    protected override void SetGoal()
    {
        this.provider.RequestGoal<WanderGoal>();
    }
}