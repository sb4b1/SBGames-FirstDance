using CrashKonijn.Goap.Runtime;

public class SquadOrderSensor : MultiSensorBase
{
    public override void Created() { }
    public override void Update() { }

    public SquadOrderSensor()
    {
        this.AddLocalWorldSensor<HasSquadOrder>((agent, references) =>
        {
            var ai = references.GetCachedComponent<BaseAI>();
            if (ai == null) return 0;
            return (ai.SquadOrder != SquadOrderType.None) ? 1 : 0;
        });

        this.AddLocalWorldSensor<SquadOrderTypeKey>((agent, references) =>
        {
            var ai = references.GetCachedComponent<BaseAI>();
            if (ai == null) return 0;
            return (int)ai.SquadOrder;
        });
    }
}
