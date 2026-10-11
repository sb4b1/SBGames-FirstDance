using System;
using System.Collections;
using System.Collections.Generic;
using SBGames.Loop;
using SBGames.Placement;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Characters.Traps
{
    /// <summary>
    /// Runtime controller attached to Trap GameObjects in the dungeon.
    /// Handles 2D trigger interactions, target validation, damage payload delivery,
    /// cooldown/charge management, living unit HP (IDamageable), and phase synchronization.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class TrapController : MonoBehaviour, IDamageable
    {
        [Header("Trap Configuration")]
        [SerializeField] private TrapDataSO trapData;

        [Header("Target Filtering")]
        [Tooltip("Physics layers that can trigger this trap.")]
        [SerializeField] private LayerMask targetLayerMask = ~0;

        [Tooltip("Optional tags considered valid targets (e.g. 'Adventurer', 'Hero'). If empty, checks any entity with IDamageable on target layers.")]
        [SerializeField] private string[] targetTags = new string[] { "Adventurer", "Hero" };

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool isArmed = false;
        [SerializeField] private int remainingCharges = 0;
        [SerializeField] private float currentCooldownTimer = 0f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Unity Events for Audio & VFX")]
        public UnityEvent<Vector3> onTriggerVisualEffect = new();
        public UnityEvent<float, float> onHealthChanged = new(); // (current, max)
        public UnityEvent onTrapArmed = new();
        public UnityEvent onTrapDisarmed = new();
        public UnityEvent onTrapDepleted = new();
        public UnityEvent onTrapDestroyed = new();

        // C# Events for decoupled system listeners
        public static event Action<TrapController, IDamageable, DamageData> OnAnyTrapTriggered;
        public event Action<TrapController, IDamageable, DamageData> OnTrapTriggered;
        public event Action<TrapController> OnArmedStateChanged;
        public event Action<TrapController> OnDestroyed;

        // Internal State
        private static readonly Collider2D[] _witnessBuffer = new Collider2D[16];
        private Collider2D _triggerCollider;
        private Coroutine _delayedDetonationCoroutine;
        private readonly HashSet<IDamageable> _occupyingDamageables = new();
        private readonly List<IDamageable> _tickCleanupBuffer = new();
        private float _tickTimer = 0f;
        private CoreGameLoop _coreGameLoop;
        private DungeonPlacementManager _placementManager;

        public Vector2Int GridPosition
        {
            get
            {
                if (DungeonPlacementManager.Instance != null)
                {
                    return DungeonPlacementManager.Instance.WorldToGridPosition(transform.position);
                }
                return new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
            }
        }

        public TrapDataSO Data => trapData;
        public bool IsArmed => isArmed;
        public int RemainingCharges => remainingCharges;
        public bool IsCoolingDown => currentCooldownTimer > 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => trapData != null ? trapData.MaxHealth : 100f;
        public bool IsLivingTrap => trapData != null && trapData.TrapType == TrapCategoryType.LivingUnit;
        public bool IsDepleted => trapData != null && trapData.MaxChargesPerRound > 0 && remainingCharges <= 0;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider2D>();
            if (_triggerCollider != null && !_triggerCollider.isTrigger)
            {
                _triggerCollider.isTrigger = true;
            }

            if (trapData != null)
            {
                Initialize(trapData);
            }
        }

        private void Start()
        {
            FindAndSubscribePhaseEvents();
        }

        private void OnEnable()
        {
            FindAndSubscribePhaseEvents();
        }

        private void OnDisable()
        {
            UnsubscribePhaseEvents();
            StopAllCoroutines();
            _occupyingDamageables.Clear();
        }

        private void OnDestroy()
        {
            UnsubscribePhaseEvents();
            OnDestroyed?.Invoke(this);
        }

        private void Update()
        {
            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer -= Time.deltaTime;
                if (currentCooldownTimer < 0f)
                {
                    currentCooldownTimer = 0f;
                }
            }

            if (isArmed && trapData != null && trapData.TriggerType == TrapTriggerType.PeriodicTick && _occupyingDamageables.Count > 0)
            {
                ProcessPeriodicTick();
            }
        }

        /// <summary>
        /// Initializes the trap with configuration data.
        /// </summary>
        public void Initialize(TrapDataSO data)
        {
            trapData = data ?? throw new ArgumentNullException(nameof(data));
            currentHealth = trapData.MaxHealth;
            ResetForPlanningPhase();
            UpdateObstacleState();
        }

        #region Phase Synchronization

        private void FindAndSubscribePhaseEvents()
        {
            if (_coreGameLoop == null)
            {
                _coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            }

            if (_coreGameLoop != null)
            {
                _coreGameLoop.OnPhaseChanged -= HandleGameLoopPhaseChanged;
                _coreGameLoop.OnPhaseChanged += HandleGameLoopPhaseChanged;
                SyncArmedStateWithPhase(_coreGameLoop.CurrentPhase == GamePhase.Battle);
            }
            else
            {
                if (_placementManager == null)
                {
                    _placementManager = FindAnyObjectByType<DungeonPlacementManager>();
                }

                if (_placementManager != null)
                {
                    _placementManager.onPhaseChanged.RemoveListener(HandlePlacementPhaseChanged);
                    _placementManager.onPhaseChanged.AddListener(HandlePlacementPhaseChanged);
                    SyncArmedStateWithPhase(_placementManager.PhaseManager != null && _placementManager.PhaseManager.CurrentPhase == DungeonPhase.CombatPhase);
                }
            }
        }

        private void UnsubscribePhaseEvents()
        {
            if (_coreGameLoop != null)
            {
                _coreGameLoop.OnPhaseChanged -= HandleGameLoopPhaseChanged;
            }

            if (_placementManager != null)
            {
                _placementManager.onPhaseChanged.RemoveListener(HandlePlacementPhaseChanged);
            }
        }

        private void HandleGameLoopPhaseChanged(GamePhase newPhase)
        {
            if (newPhase == GamePhase.Planning)
            {
                ResetForPlanningPhase();
            }
            else if (newPhase == GamePhase.Battle)
            {
                ArmForCombatPhase();
            }
            else if (newPhase == GamePhase.GameOver)
            {
                Disarm();
            }
        }

        private void HandlePlacementPhaseChanged(DungeonPhase phase)
        {
            if (phase == DungeonPhase.BuildPhase)
            {
                ResetForPlanningPhase();
            }
            else if (phase == DungeonPhase.CombatPhase)
            {
                ArmForCombatPhase();
            }
        }

        private void SyncArmedStateWithPhase(bool isCombatActive)
        {
            if (isCombatActive)
            {
                ArmForCombatPhase();
            }
            else
            {
                ResetForPlanningPhase();
            }
        }

        /// <summary>
        /// Rearms and recharges the trap when entering Planning / Prep phase.
        /// </summary>
        public void ResetForPlanningPhase()
        {
            isArmed = false;
            currentCooldownTimer = 0f;
            _tickTimer = 0f;

            if (trapData != null)
            {
                remainingCharges = trapData.MaxChargesPerRound;
                if (IsLivingTrap)
                {
                    currentHealth = trapData.MaxHealth;
                    onHealthChanged?.Invoke(currentHealth, trapData.MaxHealth);
                }
            }

            if (_delayedDetonationCoroutine != null)
            {
                StopCoroutine(_delayedDetonationCoroutine);
                _delayedDetonationCoroutine = null;
            }

            _occupyingDamageables.Clear();
            UpdateObstacleState();

            onTrapDisarmed?.Invoke();
            OnArmedStateChanged?.Invoke(this);
        }

        /// <summary>
        /// Arms the trap when entering the Combat / Battle phase.
        /// </summary>
        public void ArmForCombatPhase()
        {
            if (trapData != null && trapData.MaxChargesPerRound > 0 && remainingCharges <= 0)
            {
                isArmed = false;
                return;
            }

            isArmed = true;
            currentCooldownTimer = 0f;
            _tickTimer = 0f;
            UpdateObstacleState();

            onTrapArmed?.Invoke();
            OnArmedStateChanged?.Invoke(this);
        }

        public void Disarm()
        {
            isArmed = false;
            UpdateObstacleState();
            onTrapDisarmed?.Invoke();
            OnArmedStateChanged?.Invoke(this);
        }

        private void UpdateObstacleState()
        {
            if (trapData == null) return;

            // Check if NavMeshObstacle or custom 2D obstacle is attached
            var obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.enabled = isArmed && trapData.HasNavMeshObstacle;
            }
        }

        #endregion

        #region Physics Trigger Handling

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isArmed || trapData == null) return;
            if (IsDepleted || IsCoolingDown) return;

            if (!IsValidTarget(other, out var damageable)) return;

            switch (trapData.TriggerType)
            {
                case TrapTriggerType.InstantBurst:
                    ExecutePayload(damageable);
                    break;

                case TrapTriggerType.DelayedDetonation:
                    if (_delayedDetonationCoroutine == null)
                    {
                        _delayedDetonationCoroutine = StartCoroutine(DelayedDetonationRoutine());
                    }
                    break;

                case TrapTriggerType.PeriodicTick:
                    if (damageable != null && !_occupyingDamageables.Contains(damageable))
                    {
                        _occupyingDamageables.Add(damageable);
                    }
                    break;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isArmed || trapData == null) return;
            if (trapData.TriggerType != TrapTriggerType.PeriodicTick) return;

            if (IsValidTarget(other, out var damageable))
            {
                if (damageable != null && !_occupyingDamageables.Contains(damageable))
                {
                    _occupyingDamageables.Add(damageable);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                _occupyingDamageables.Remove(damageable);
            }
            else
            {
                var parentDamageable = other.GetComponentInParent<IDamageable>();
                if (parentDamageable != null)
                {
                    _occupyingDamageables.Remove(parentDamageable);
                }
            }
        }

        private bool IsValidTarget(Collider2D col, out IDamageable damageable)
        {
            damageable = null;
            if (col == null) return false;

            // 1. Layer filtering
            if ((targetLayerMask.value & (1 << col.gameObject.layer)) == 0)
            {
                return false;
            }

            // 2. Tag filtering (if tags are specified)
            if (targetTags != null && targetTags.Length > 0)
            {
                bool tagMatches = false;
                for (int i = 0; i < targetTags.Length; i++)
                {
                    if (col.CompareTag(targetTags[i]))
                    {
                        tagMatches = true;
                        break;
                    }
                }

                if (!tagMatches)
                {
                    // Check parent for tag if collider is a child hit-box
                    Transform parent = col.transform.parent;
                    while (parent != null)
                    {
                        for (int i = 0; i < targetTags.Length; i++)
                        {
                            if (parent.CompareTag(targetTags[i]))
                            {
                                tagMatches = true;
                                break;
                            }
                        }
                        if (tagMatches) break;
                        parent = parent.parent;
                    }

                    if (!tagMatches) return false;
                }
            }

            // 3. Obtain IDamageable interface
            if (!col.TryGetComponent(out damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            return damageable != null;
        }

        #endregion

        #region Execution & Payloads

        /// <summary>
        /// Applies the trap's damage payload directly to the specified target.
        /// </summary>
        public void ExecutePayload(IDamageable target)
        {
            if (trapData == null) return;

            var payload = trapData.CreateDamagePayload(this);
            target?.TakeDamage(in payload);

            ConsumeChargeAndTriggerVisuals(target, payload);
        }

        private void ConsumeChargeAndTriggerVisuals(IDamageable target, DamageData payload)
        {
            currentCooldownTimer = trapData.Cooldown;

            if (trapData.MaxChargesPerRound > 0)
            {
                remainingCharges--;
                if (remainingCharges <= 0)
                {
                    remainingCharges = 0;
                    isArmed = false;
                    onTrapDepleted?.Invoke();
                }
            }

            onTriggerVisualEffect?.Invoke(transform.position);
            OnTrapTriggered?.Invoke(this, target, payload);
            OnAnyTrapTriggered?.Invoke(this, target, payload);

            // Notify victim directly
            Vector2Int gridPos = GridPosition;
            float danger = trapData != null ? trapData.DamageAmount : 50f;
            if (target is Component comp)
            {
                var victimMem = comp.GetComponentInChildren<CharacterAIMemory>() ?? comp.GetComponentInParent<CharacterAIMemory>();
                victimMem?.RecordHazard(gridPos, transform.position, danger);
            }

            // Notify nearby witnesses without allocations
            int witnessCount = Physics2D.OverlapCircleNonAlloc(transform.position, 8f, _witnessBuffer, targetLayerMask);
            for (int i = 0; i < witnessCount; i++)
            {
                var col = _witnessBuffer[i];
                if (col == null) continue;
                var witnessMem = col.GetComponentInChildren<CharacterAIMemory>() ?? col.GetComponentInParent<CharacterAIMemory>();
                witnessMem?.RecordHazard(gridPos, transform.position, danger);
            }
        }

        private IEnumerator DelayedDetonationRoutine()
        {
            yield return new WaitForSeconds(trapData.TriggerDelay);
            _delayedDetonationCoroutine = null;

            if (!isArmed || IsDepleted) yield break;

            // Detonate on all valid overlapping entities
            int hitCount = 0;
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayerMask,
                useTriggers = true
            };

            List<Collider2D> overlaps = new List<Collider2D>();
            if (_triggerCollider != null)
            {
                _triggerCollider.Overlap(filter, overlaps);
            }

            var payload = trapData.CreateDamagePayload(this);

            for (int i = 0; i < overlaps.Count; i++)
            {
                var col = overlaps[i];
                if (IsValidTarget(col, out var target) && target != null)
                {
                    target.TakeDamage(in payload);
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                ConsumeChargeAndTriggerVisuals(null, payload);
            }
            else
            {
                // Trigger fired even if targets moved away
                ConsumeChargeAndTriggerVisuals(null, payload);
            }
        }

        private void ProcessPeriodicTick()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= trapData.TickInterval)
            {
                _tickTimer = 0f;

                if (_occupyingDamageables.Count == 0) return;

                var payload = trapData.CreateDamagePayload(this);
                _tickCleanupBuffer.Clear();

                foreach (var target in _occupyingDamageables)
                {
                    if (target == null || (target is Component comp && comp == null))
                    {
                        _tickCleanupBuffer.Add(target);
                        continue;
                    }

                    target.TakeDamage(in payload);
                }

                for (int i = 0; i < _tickCleanupBuffer.Count; i++)
                {
                    _occupyingDamageables.Remove(_tickCleanupBuffer[i]);
                }
                _tickCleanupBuffer.Clear();

                onTriggerVisualEffect?.Invoke(transform.position);
                OnTrapTriggered?.Invoke(this, null, payload);

                if (trapData.MaxChargesPerRound > 0)
                {
                    remainingCharges--;
                    if (remainingCharges <= 0)
                    {
                        remainingCharges = 0;
                        isArmed = false;
                        onTrapDepleted?.Invoke();
                    }
                }
            }
        }

        #endregion

        #region IDamageable Implementation (Living / Destructible Traps)

        public bool TakeDamage(in DamageData damage)
        {
            if (!IsLivingTrap)
            {
                // Mechanical hazard traps are immune to attacks
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage.Amount);
            onHealthChanged?.Invoke(currentHealth, MaxHealth);

            if (currentHealth <= 0f)
            {
                Die();
                return true;
            }

            return false;
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(new DamageData(damage, DamageType.Physical));
        }

        public void ForceDeath()
        {
            if (!IsLivingTrap) return;
            currentHealth = 0f;
            onHealthChanged?.Invoke(currentHealth, MaxHealth);
            Die();
        }

        private void Die()
        {
            isArmed = false;
            onTrapDestroyed?.Invoke();
            OnDestroyed?.Invoke(this);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        #endregion
    }
}
