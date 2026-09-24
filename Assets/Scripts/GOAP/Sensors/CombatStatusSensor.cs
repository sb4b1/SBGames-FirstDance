using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class CombatStatusSensor : MultiSensorBase
{
    public override void Created() {}

    public override void Update() {}

    public CombatStatusSensor()
    {
        this.AddLocalWorldSensor<IsSkillReady>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return 0;

            return -1;
        });
    }

}