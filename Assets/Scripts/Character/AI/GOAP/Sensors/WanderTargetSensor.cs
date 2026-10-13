using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using SB4b1.Logging;
using UnityEngine;
using UnityEngine.AI;

public class WanderTargetSensor : LocalTargetSensorBase
{
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
        var random = this.GetRandomPosition(agent, references);

        // If the existing target is a `PositionTarget`, we can reuse it and just update the position.
        if (existingTarget is PositionTarget positionTarget)
        {
            return positionTarget.SetPosition(random);
        }
        return new PositionTarget(random);  
    }

    private Vector3 GetRandomPosition(IActionReceiver agent, IComponentReference references)
    {
        var ai = references?.GetCachedComponent<BaseAI>();
        Vector3 protectPoint = (ai != null && ai.SpawnPosition != Vector3.zero) ? ai.SpawnPosition : agent.Transform.position;

        int territoryArea = NavMesh.GetAreaFromName("Territory");
        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        int areaMask = 0;
        if (territoryArea >= 0) areaMask |= (1 << territoryArea);
        if (walkableArea >= 0) areaMask |= (1 << walkableArea);
        if (areaMask == 0) areaMask = NavMesh.AllAreas;

        // Try sampling around agent in territory / walkable area
        for (int i = 0; i < 5; i++)
        {
            Vector2 random = Random.insideUnitCircle * 3f;
            Vector3 position = agent.Transform.position + new Vector3(random.x, random.y, 0f);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, areaMask))
            {
                if (ai != null && ai.AIMemory != null && ai.AIMemory.IsPositionHazardous(hit.position, 1.2f))
                {
                    continue;
                }
                return hit.position;
            }
        }

        // Try sampling around protect point in territory / walkable area
        for (int i = 0; i < 5; i++)
        {
            Vector2 random = Random.insideUnitCircle * 2f;
            Vector3 position = protectPoint + new Vector3(random.x, random.y, 0f);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, areaMask))
            {
                if (ai != null && ai.AIMemory != null && ai.AIMemory.IsPositionHazardous(hit.position, 1.2f))
                {
                    continue;
                }
                return hit.position;
            }
        }

        // Fallback: sample any area around agent
        for (int i = 0; i < 5; i++)
        {
            Vector2 random = Random.insideUnitCircle * 3f;
            Vector3 position = agent.Transform.position + new Vector3(random.x, random.y, 0f);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                if (ai != null && ai.AIMemory != null && ai.AIMemory.IsPositionHazardous(hit.position, 1.2f))
                {
                    continue;
                }
                return hit.position;
            }
        }

        return agent.Transform.position;
    }
}