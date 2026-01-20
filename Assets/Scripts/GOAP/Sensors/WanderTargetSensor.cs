using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using SB4b1.Logging;
using UnityEngine;
using UnityEngine.AI;

public class WanderTargetSensor : LocalTargetSensorBase
{
private Vector3 protectPoint = Vector3.zero;
    // Is called when this script is initialzed
    public override void Created()
    {
        
    }

    // Is called every frame that an agent of an `AgentType` that uses this sensor needs it.
    // This can be used to 'cache' data that is used in the `Sense` method.
    // Eg look up all the trees in the scene, and then find the closest one in the Sense method.
    public override void Update()
    {

    }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var random = this.GetRandomPosition(agent);

        // If the existing target is a `PositionTarget`, we can reuse it and just update the position.
        if (existingTarget is PositionTarget positionTarget)
        {
            return positionTarget.SetPosition(random);
        }
        return new PositionTarget(random);  
    }

    private Vector3 GetRandomPosition(IActionReceiver agent)
    {
        if (protectPoint == Vector3.zero) protectPoint = agent.Transform.position;
        int count = 0;
        while (count < 5)
        {
            var random = Random.insideUnitCircle * 3;
            var position = agent.Transform.position + (Vector3)random;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1, 1 << NavMesh.GetAreaFromName("Territory")))
            {

                return hit.position;
            }

            count++;
        }

        count = 0;
        while (count < 5)
        {
            var random = Random.insideUnitCircle * 2;
            var position = protectPoint + (Vector3)random;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1, 1 << NavMesh.GetAreaFromName("Territory")))
            {
                return hit.position;
            }

            count++;
        }
        return agent.Transform.position;
    }
}