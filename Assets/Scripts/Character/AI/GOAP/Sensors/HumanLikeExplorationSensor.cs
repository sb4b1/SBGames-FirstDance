using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using SBGames.Characters.AI;
using SBGames.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace SBGames.GOAP.Sensors
{
    /// <summary>
    /// Evaluates candidate exploration points ahead of the party/scout using tactical heuristics:
    /// Score(P) = w_fog * UnexploredFactor(P) - w_dist * Distance(P) - w_hazard * HazardPenalty(P) + w_interest * PointOfInterest(P)
    /// Incorporates wall-hugging heuristics (0.5m - 0.8m wall offset) and personality modifier snapshot biases.
    /// Zero managed heap allocations during evaluation.
    /// </summary>
    public class HumanLikeExplorationSensor : LocalTargetSensorBase
    {
        private static readonly Collider2D[] poiBuffer = new Collider2D[8];

        public override void Created() { }
        public override void Update() { }

        public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
        {
            var bestPosition = EvaluateBestFrontierPoint(agent, references);

            if (existingTarget is PositionTarget posTarget)
            {
                return posTarget.SetPosition(bestPosition);
            }
            return new PositionTarget(bestPosition);
        }

        public Vector3 EvaluateBestFrontierPoint(IActionReceiver agent, IComponentReference references)
        {
            var ai = references?.GetCachedComponent<BaseAI>();
            Vector3 origin = agent.Transform.position;

            Vector3 forwardDir = agent.Transform.up;
            if (ai != null && ai.LeaderTransform != null)
            {
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

            // Personality Weights
            float w_fog = 25f;
            float w_dist = 1.0f;
            float w_hazard = 50f;
            float w_interest = 15f;

            if (ai != null && ai.Personality != null)
            {
                var snap = ai.Personality.Snapshot;
                // Reckless ignores hazards
                if (snap.IgnoreSurvivalGoal)
                {
                    w_hazard = 0f;
                }
                // Cautious / Cowardly
                if (snap.FearMultiplier > 1.2f)
                {
                    w_hazard *= 2.0f;
                    w_dist *= 2.5f; // Strongly avoids wandering far
                }
            }

            int territoryArea = NavMesh.GetAreaFromName("Territory");
            int walkableArea = NavMesh.GetAreaFromName("Walkable");
            int areaMask = 0;
            if (territoryArea >= 0) areaMask |= (1 << territoryArea);
            if (walkableArea >= 0) areaMask |= (1 << walkableArea);
            if (areaMask == 0) areaMask = NavMesh.AllAreas;

            Vector3 bestCandidate = origin;
            float highestScore = float.MinValue;

            bool isScoutOrRogue = ai != null && ai.SquadMember != null && ai.SquadMember.IsScout;

            // Sample candidate points in forward fan
            for (int i = 0; i < 6; i++)
            {
                float angle = -45f + (i * 18f);
                Vector3 sampleDir = Quaternion.Euler(0, 0, angle) * forwardDir;
                float distance = 6f + (i % 3) * 3f; // 6m - 12m ahead

                Vector3 candidate = origin + (sampleDir * distance);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.5f, areaMask))
                {
                    Vector3 testPos = hit.position;

                    // 1. Hazard Penalty Check
                    bool isHazard = ai != null && ai.AIMemory != null && ai.AIMemory.IsPositionHazardous(testPos, 1.2f);
                    if (isHazard && w_hazard > 0f)
                    {
                        continue; // Reject coordinate with known trap
                    }

                    // 2. Unexplored Factor
                    float unexplored = (ai != null && ai.AIMemory != null) ? ai.AIMemory.GetUnexploredFactor(testPos) : 1.0f;

                    // 3. Distance Penalty
                    float dist = Vector3.Distance(origin, testPos);

                    // 4. Point of Interest Check (Chests, crates, destructibles)
                    float poiBonus = 0f;
                    int hitCount = Physics2D.OverlapCircleNonAlloc(testPos, 2.5f, poiBuffer);
                    for (int p = 0; p < hitCount; p++)
                    {
                        var col = poiBuffer[p];
                        if (col != null && col.TryGetComponent<DestructiblePropController>(out var prop) && !prop.IsDestroyed)
                        {
                            poiBonus += 1.0f;
                        }
                    }

                    // 5. Wall-Hugging Affinity Heuristic for Scout / Rogue (0.5m - 0.8m wall offset)
                    float wallAffinity = 0f;
                    if (isScoutOrRogue && NavMesh.FindClosestEdge(testPos, out NavMeshHit edgeHit, areaMask))
                    {
                        float distToWall = edgeHit.distance;
                        if (distToWall >= 0.45f && distToWall <= 0.85f)
                        {
                            wallAffinity = 15f; // Optimal wall-hugging corridor
                        }
                        else if (distToWall < 0.3f)
                        {
                            wallAffinity = -5f; // Too close, clipping into wall
                        }
                        else
                        {
                            wallAffinity = -5f; // Open room center penalty
                        }
                    }

                    // Heuristic calculation
                    float score = (w_fog * unexplored) - (w_dist * dist) - (isHazard ? w_hazard : 0f) + (w_interest * poiBonus) + wallAffinity;

                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestCandidate = testPos;
                    }
                }
            }

            // Record best candidate into local spatial visitation memory
            if (ai != null && ai.AIMemory != null && bestCandidate != origin)
            {
                ai.AIMemory.RecordVisitedPosition(bestCandidate);
            }

            return bestCandidate;
        }
    }
}
