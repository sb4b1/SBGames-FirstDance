using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class WarpSensor : MultiSensorBase
{
    public override void Created() { }

    public override void Update() { }

    public WarpSensor()
    {
        this.AddLocalWorldSensor<CanWarp>((agent, references) =>
        {
            var ai = references?.GetCachedComponent<BaseAI>();
            if (ai == null) return 0;
            return ai.CanWarpNow() ? 1 : 0;
        });

        this.AddLocalTargetSensor<WarpTarget>((agent, references, target) =>
        {
            var ai = references?.GetCachedComponent<BaseAI>();
            Vector3 desiredPos = ai != null ? ai.GetDesiredWarpPosition() : agent.Transform.position;

            if (target is PositionTarget positionTarget)
            {
                return positionTarget.SetPosition(desiredPos);
            }

            return new PositionTarget(desiredPos);
        });
    }
}
