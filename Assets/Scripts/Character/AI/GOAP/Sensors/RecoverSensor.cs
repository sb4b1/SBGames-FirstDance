using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class RecoverSensor : MultiSensorBase
{
    public override void Created() { }

    public override void Update() { }

    public RecoverSensor()
    {
        this.AddLocalWorldSensor<NeedsRecovery>((agent, references) =>
        {
            var ai = references?.GetCachedComponent<BaseAI>();
            if (ai == null) return 0;
            return ai.CanRecoverWithItemNow() || ai.HasReadySelfOnlyHealSkill() ? 1 : 0;
        });

        this.AddLocalTargetSensor<RecoverTarget>((agent, references, target) =>
        {
            var ai = references?.GetCachedComponent<BaseAI>();
            Vector3 desiredPos = ai != null ? ai.GetDesiredRecoveryPosition() : agent.Transform.position;

            if (target is PositionTarget positionTarget)
            {
                return positionTarget.SetPosition(desiredPos);
            }

            return new PositionTarget(desiredPos);
        });
    }
}
