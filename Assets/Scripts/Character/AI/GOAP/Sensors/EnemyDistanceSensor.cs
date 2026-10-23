using CrashKonijn.Goap.Runtime;
using SB4b1.Utils;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDistanceSensor : MultiSensorBase
{
    private static readonly System.Collections.Generic.Dictionary<int, CachedRetreatPoint> retreatCache = new(32);

    private struct CachedRetreatPoint
    {
        public Vector3 Position;
        public float Timestamp;
    }

    public override void Created() { }

    public override void Update() { }

    public EnemyDistanceSensor()
    {
        this.AddLocalWorldSensor<EnemyDistance>((agent, references) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null || data.CurrentTarget == null) return 999;

            return (int)data.TargetDistance;
        });

        this.AddLocalTargetSensor<KeepDistanceTarget>((agent, references, target) =>
        {
            var data = references?.GetCachedComponent<BaseAI>();
            if (data == null) return null;

            Vector3 agentPos = agent.Transform.position;

            // If cornered or cannot gain distance, fight from where it stands
            if (data.IsCornered)
            {
                if (target is PositionTarget corneredTarget)
                {
                    return corneredTarget.SetPosition(agentPos);
                }
                return new PositionTarget(agentPos);
            }

            Transform threat = data.CurrentTarget != null ? data.CurrentTarget : data.GetNearestThreatTransform(out _);
            if (threat == null)
            {
                if (target is PositionTarget noThreatTarget)
                {
                    return noThreatTarget.SetPosition(agentPos);
                }
                return new PositionTarget(agentPos);
            }

            // Rate-limiting / Throttling: recompute on the Engaged perception tier (or if no cache exists)
            int instanceId = data.GetEntityId().GetHashCode();
            float now = Time.time;
            bool isEngagedTier = data.Vision != null && data.Vision.CurrentTier == PerceptionTier.Engaged;
            float maxCacheLife = isEngagedTier ? 0.35f : 0.75f;

            if (retreatCache.TryGetValue(instanceId, out var cached) && (now - cached.Timestamp < maxCacheLife))
            {
                if (target is PositionTarget cachedTarget)
                {
                    return cachedTarget.SetPosition(cached.Position);
                }
                return new PositionTarget(cached.Position);
            }

            Vector3 toTarget = threat.position - agentPos;
            toTarget.z = 0f;
            Vector3 awayDir = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : Vector3.up;

            float minRetreat = 2.5f;
            float maxRetreat = 4.5f;
            if (data.StatsManager != null)
            {
                float baseRange = data.BasicAttackMode == BasicAttackMode.None
                    ? data.GetMainSkillRange()
                    : (data.StatsManager.AttackRange != null ? data.StatsManager.AttackRange.Value : 1f);
                float preferredDist = data.GetPreferredStandingDistance(baseRange);
                minRetreat = Mathf.Max(1.0f, preferredDist * 0.85f);
                maxRetreat = Mathf.Max(minRetreat + 0.5f, preferredDist * 1.15f);
            }

            // Retreat direction should favor the squad (toward the tank or leader) over simply away from the enemy
            Vector3 retreatDirection = awayDir;
            Transform squadAnchor = null;
            if (data.HasSquadLeader && data.SquadMember != null && data.SquadMember.Leader != null)
            {
                var leader = data.SquadMember.Leader;
                squadAnchor = data.FindLivingPartyTankTransform(leader);
                if (squadAnchor == null && leader.IsAlive && leader.gameObject != data.gameObject)
                {
                    squadAnchor = leader.transform;
                }
            }

            if (squadAnchor != null)
            {
                Vector3 toSquad = squadAnchor.position - agentPos;
                toSquad.z = 0f;
                if (toSquad.sqrMagnitude > 0.1f)
                {
                    // Blend 60% towards squad anchor and 40% directly away from enemy
                    retreatDirection = (toSquad.normalized * 0.6f + awayDir * 0.4f).normalized;
                }
            }

            Vector3 perpDir = new Vector3(-retreatDirection.y, retreatDirection.x, 0f);
            Vector3 chosenPosition = agentPos;
            bool foundNavMesh = false;

            for (int i = 0; i < 4; i++)
            {
                float perpFactor = (i == 0) ? 0f : ((i % 2 == 1 ? 1f : -1f) * 1.0f);
                float retreatDist = Mathf.Lerp(minRetreat, maxRetreat, (i + 1) * 0.25f);
                Vector3 candidatePos = agentPos + (retreatDirection * retreatDist) + (perpDir * perpFactor);

                if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    chosenPosition = hit.position;
                    foundNavMesh = true;
                    break;
                }
            }

            // Graceful fallback: if no sampled position in directional arc, try agent position sample or current position
            if (!foundNavMesh)
            {
                if (NavMesh.SamplePosition(agentPos, out NavMeshHit hitFallback, 2.5f, NavMesh.AllAreas))
                {
                    chosenPosition = hitFallback.position;
                }
                else
                {
                    chosenPosition = agentPos;
                }
            }

            retreatCache[instanceId] = new CachedRetreatPoint { Position = chosenPosition, Timestamp = now };

            if (target is PositionTarget posTarget)
            {
                return posTarget.SetPosition(chosenPosition);
            }

            return new PositionTarget(chosenPosition);
        });
    }
}
