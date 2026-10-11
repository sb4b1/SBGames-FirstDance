using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;
using UnityEngine.AI;

public class ScoutReconTargetSensor : LocalTargetSensorBase
{
    private readonly SBGames.GOAP.Sensors.HumanLikeExplorationSensor explorationSensor = new();

    public override void Created() { }
    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        var targetPos = explorationSensor.EvaluateBestFrontierPoint(agent, references);

        if (existingTarget is PositionTarget posTarget)
        {
            return posTarget.SetPosition(targetPos);
        }
        return new PositionTarget(targetPos);
    }

    private Vector3 GetReconPosition(IActionReceiver agent, IComponentReference references)
    {
        var ai = references?.GetCachedComponent<BaseAI>();
        Vector3 origin = agent.Transform.position;
        Vector3 forwardDir = agent.Transform.up; // 2D top-down forward

        if (ai != null && ai.LeaderTransform != null)
        {
            // Forward heading from party leader outward
            Vector3 fromLeader = (origin - ai.LeaderTransform.position);
            if (fromLeader.sqrMagnitude > 0.1f)
            {
                forwardDir = fromLeader.normalized;
            }
            else
            {
                forwardDir = ai.LeaderTransform.up;
            }
        }

        int territoryArea = NavMesh.GetAreaFromName("Territory");
        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        int areaMask = 0;
        if (territoryArea >= 0) areaMask |= (1 << territoryArea);
        if (walkableArea >= 0) areaMask |= (1 << walkableArea);
        if (areaMask == 0) areaMask = NavMesh.AllAreas;

        // Sample forward exploration points (8 to 14 meters forward with small lateral deviation)
        for (int i = 0; i < 4; i++)
        {
            float forwardDistance = 8f + (i * 2f);
            Vector2 lateralJitter = Random.insideUnitCircle * 2.5f;
            Vector3 testPos = origin + (forwardDir * forwardDistance) + new Vector3(lateralJitter.x, lateralJitter.y, 0f);

            if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 3f, areaMask))
            {
                if (ai != null && ai.AIMemory != null && ai.AIMemory.IsPositionHazardous(hit.position, 1.2f))
                {
                    continue;
                }
                return hit.position;
            }
        }

        // Fallback: wider sample
        if (NavMesh.SamplePosition(origin + (forwardDir * 6f), out NavMeshHit fallbackHit, 5f, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        return origin;
    }
}
