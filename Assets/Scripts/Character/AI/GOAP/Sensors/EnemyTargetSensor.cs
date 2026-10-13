using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class EnemyTargetSensor : LocalTargetSensorBase
{

    public override void Created() { }

    public override void Update() { }

    public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
    {
        Transform agentTransform = null;
        if (agent != null)
        {
            agentTransform = agent.Transform != null ? agent.Transform : (agent is Component comp ? comp.transform : null);
        }

        var brain = references?.GetCachedComponent<BaseAI>();
        if (brain == null && agentTransform != null)
        {
            brain = agentTransform.GetComponent<BaseAI>() ?? agentTransform.GetComponentInParent<BaseAI>() ?? agentTransform.GetComponentInChildren<BaseAI>();
        }

        if (brain == null || agentTransform == null)
        {
            return null;
        }

        Transform bestTarget = null;
        float bestScore = -1f;
        float bestDist = 0f;
        Vector3 agentPos = agentTransform.position;

        // 1. Direct Visual Targets (weighted by distance, low health, and memory grudge)
        if (brain.EnemyTargets != null && brain.EnemyTargets.Count > 0)
        {
            for (int i = 0; i < brain.EnemyTargets.Count; i++)
            {
                var t = brain.EnemyTargets[i];
                if (t == null) continue;

                float dist = (t.position - agentPos).magnitude;
                float score = brain.GetTraitWeightedTargetScore(t, dist);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = t;
                    bestDist = dist;
                }
            }
        }

        // 2. Known Enemies Fallback (shared via squad or proximity alert)
        if (bestTarget == null && brain.HasKnownEnemies)
        {
            var known = brain.KnownEnemies;
            float currentTime = Time.time;
            for (int i = 0; i < known.Count; i++)
            {
                var record = known[i];
                if (!record.IsValid(currentTime) || record.Target == null) continue;

                float dist = (record.Target.position - agentPos).magnitude;
                float score = brain.GetTraitWeightedTargetScore(record.Target, dist);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = record.Target;
                    bestDist = dist;
                }
            }
        }

        // 3. Fallback to Dual-Layer AI Memory: Last Known Target / Position
        if (bestTarget == null)
        {
            if (brain.AIMemory != null && brain.AIMemory.HasLastKnownTarget)
            {
                Vector3 lastPos = brain.AIMemory.LastKnownPosition;
                brain.SetTarget(brain.AIMemory.LastKnownTarget, (lastPos - agentPos).magnitude);

                if (existingTarget is PositionTarget positionTarget)
                {
                    return positionTarget.SetPosition(lastPos);
                }
                return new PositionTarget(lastPos);
            }

            if (brain.CurrentTarget != null) brain.SetTarget(null);
            return null;
        }

        brain.SetTarget(bestTarget, bestDist);
        
        if (existingTarget is TransformTarget transformTarget)
        {
            return transformTarget.SetTransform(bestTarget);
        }

        return new TransformTarget(bestTarget);
    }
}