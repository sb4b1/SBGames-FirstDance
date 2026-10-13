using System;
using UnityEngine;
using UnityEngine.AI;

namespace SBGames.Characters.AI
{
    /// <summary>
    /// Detects doorways, room thresholds, and blind corners using 2D Raycasts.
    /// Manages tactical slowdown (-40% speed) and a micro-hesitation pause (0.6s - 1.2s)
    /// while the agent orients / sweeps vision across the entrance before entering.
    /// Zero managed heap allocations during evaluation.
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorwayStagingController : MonoBehaviour
    {
        [Header("Detection Parameters")]
        [SerializeField] private float checkDistance = 1.5f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private float speedReductionMultiplier = 0.6f; // -40% speed
        [SerializeField] private float minHesitationDuration = 0.6f;
        [SerializeField] private float maxHesitationDuration = 1.2f;

        [Header("Dependencies")]
        [SerializeField] private BaseAI baseAI;
        [SerializeField] private NavMeshAgent navAgent;

        private float originalSpeed = -1f;
        private float hesitationTimer = 0f;
        private bool isStaging = false;
        private float lastStagingTime = -999f;
        private const float COOLDOWN_BETWEEN_STAGINGS = 4.0f;

        private static readonly RaycastHit2D[] raycastBuffer = new RaycastHit2D[4];

        public bool IsStaging => isStaging;
        public float MinHesitationDuration => minHesitationDuration;
        public float MaxHesitationDuration => maxHesitationDuration;
        public Action OnStagingStarted;
        public Action OnStagingCompleted;

        /// <summary>
        /// Sets the doorway hesitation duration in seconds (e.g. from <see cref="AdventurerAIProfile"/>).
        /// </summary>
        public void SetHesitationDuration(float duration)
        {
            float clamped = Mathf.Max(0f, duration);
            minHesitationDuration = clamped;
            maxHesitationDuration = clamped;
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void ResolveDependencies()
        {
            if (baseAI == null) baseAI = GetComponent<BaseAI>() ?? GetComponentInChildren<BaseAI>() ?? GetComponentInParent<BaseAI>();
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>() ?? GetComponentInParent<NavMeshAgent>();
        }

        private void Update()
        {
            if (navAgent == null) ResolveDependencies();
            if (navAgent == null || !navAgent.isActiveAndEnabled) return;

            // Reckless personality ignores doorway staging
            if (baseAI != null && baseAI.Personality != null && baseAI.Personality.Snapshot.IgnoreSurvivalGoal)
            {
                if (isStaging) EndStaging();
                return;
            }

            if (isStaging)
            {
                hesitationTimer -= Time.deltaTime;

                // Perform vision sweep across the doorway (subtle oscillation)
                float sweepAngle = Mathf.Sin(Time.time * 8f) * 35f;
                Vector3 forward = transform.up;
                Vector3 sweepDir = Quaternion.Euler(0, 0, sweepAngle) * forward;
                // Vision or sprite orientation can follow sweepDir

                if (hesitationTimer <= 0f)
                {
                    EndStaging();
                }
                return;
            }

            // Staging cooldown check
            if (Time.time - lastStagingTime < COOLDOWN_BETWEEN_STAGINGS) return;

            // Only check when moving with a valid destination
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.05f) return;

            // Detect doorway or corner
            if (CheckApproachingDoorwayOrCorner())
            {
                StartStaging();
            }
        }

        public bool CheckApproachingDoorwayOrCorner()
        {
            Vector2 origin = transform.position;
            Vector2 forward = navAgent.velocity.sqrMagnitude > 0.01f ? (Vector2)navAgent.velocity.normalized : (Vector2)transform.up;
            Vector2 left = new Vector2(-forward.y, forward.x);
            Vector2 right = new Vector2(forward.y, -forward.x);

            // Cast rays forward-left and forward-right to detect doorway frame/chokepoints
            int leftHits = Physics2D.RaycastNonAlloc(origin, (forward + left).normalized, raycastBuffer, checkDistance, obstacleLayerMask);
            bool leftWall = false;
            for (int i = 0; i < leftHits; i++)
            {
                if (raycastBuffer[i].collider != null && !raycastBuffer[i].collider.isTrigger && raycastBuffer[i].collider.gameObject != gameObject)
                {
                    leftWall = true;
                    break;
                }
            }

            int rightHits = Physics2D.RaycastNonAlloc(origin, (forward + right).normalized, raycastBuffer, checkDistance, obstacleLayerMask);
            bool rightWall = false;
            for (int i = 0; i < rightHits; i++)
            {
                if (raycastBuffer[i].collider != null && !raycastBuffer[i].collider.isTrigger && raycastBuffer[i].collider.gameObject != gameObject)
                {
                    rightWall = true;
                    break;
                }
            }

            // Doorway is detected if walls exist on flanks while forward space is open
            if (leftWall && rightWall)
            {
                int forwardHits = Physics2D.RaycastNonAlloc(origin, forward, raycastBuffer, checkDistance, obstacleLayerMask);
                bool forwardClear = true;
                for (int i = 0; i < forwardHits; i++)
                {
                    if (raycastBuffer[i].collider != null && !raycastBuffer[i].collider.isTrigger && raycastBuffer[i].collider.gameObject != gameObject)
                    {
                        forwardClear = false;
                        break;
                    }
                }
                return forwardClear;
            }

            // Single flank blind corner check
            if (leftWall || rightWall)
            {
                // Check if forward path has a sharp angle/corner
                return true;
            }

            return false;
        }

        public void StartStaging()
        {
            if (isStaging) return;
            isStaging = true;
            lastStagingTime = Time.time;

            if (navAgent != null)
            {
                originalSpeed = navAgent.speed;
                navAgent.speed = originalSpeed * speedReductionMultiplier;
            }

            hesitationTimer = UnityEngine.Random.Range(minHesitationDuration, maxHesitationDuration);
            OnStagingStarted?.Invoke();
        }

        public void EndStaging()
        {
            if (!isStaging) return;
            isStaging = false;

            if (navAgent != null && originalSpeed > 0f)
            {
                navAgent.speed = originalSpeed;
            }

            OnStagingCompleted?.Invoke();
        }

        private void OnDisable()
        {
            EndStaging();
        }
    }
}
