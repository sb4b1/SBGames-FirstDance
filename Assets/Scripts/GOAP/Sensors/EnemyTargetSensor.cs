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
        float bestSqrDist = float.MaxValue;
        Vector3 agentPos = agentTransform.position;

        // 1. Direct Visual Targets
        if (brain.EnemyTargets != null && brain.EnemyTargets.Count > 0)
        {
            for (int i = 0; i < brain.EnemyTargets.Count; i++)
            {
                var t = brain.EnemyTargets[i];
                if (t == null) continue;

                float sqrDist = (t.position - agentPos).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestTarget = t;
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

                float sqrDist = (record.Target.position - agentPos).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestTarget = record.Target;
                }
            }
        }

        if (bestTarget == null)
        {
            if (brain.CurrentTarget != null) brain.SetTarget(null);
            return null;
        }

        brain.SetTarget(bestTarget, Mathf.Sqrt(bestSqrDist));
        
        if (existingTarget is TransformTarget transformTarget)
        {
            return transformTarget.SetTransform(bestTarget);
        }

        return new TransformTarget(bestTarget);
    }
}