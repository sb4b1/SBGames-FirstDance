using System;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// ScriptableObject defining authorable configuration for a Trap in the dungeon.
    /// Extends <see cref="UnitPlacementDefinitionSO"/> to leverage existing placement, grid, and shop systems.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrapData", menuName = "SBGames/Placement/Trap Data", order = 11)]
    public class TrapDataSO : UnitPlacementDefinitionSO
    {
        [Header("Trap Identity & Classification")]
        [SerializeField] private EntityCategory entityCategory = EntityCategory.Trap;
        [SerializeField] private TrapCategoryType trapType = TrapCategoryType.MechanicalHazard;
        [SerializeField] private TrapTriggerType triggerType = TrapTriggerType.InstantBurst;
        [SerializeField] private TrapTargetFilter targetFilter = TrapTargetFilter.AdventurersOnly;
        [SerializeField] private Sprite icon;

        [Header("Economy & Refund")]
        [Tooltip("Energy/Gold refunded to DM when sold during Planning phase. If 0 or negative, defaults to BaseCost.")]
        [SerializeField] private int sellRefundAmount = 0;

        [Header("Combat & Payload")]
        [Tooltip("Base damage delivered to entities upon triggering.")]
        [SerializeField, Min(0f)] private float damageAmount = 25f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private bool canCrit = false;

        [Header("Trigger Dynamics & Recharge")]
        [Tooltip("Cooldown in seconds between consecutive triggers.")]
        [SerializeField, Min(0f)] private float cooldown = 1.0f;

        [Tooltip("Maximum trigger charges per combat round. Set to 0 for unlimited charges.")]
        [SerializeField, Min(0)] private int maxChargesPerRound = 0;

        [Tooltip("Windup delay in seconds before detonation when TriggerType is DelayedDetonation.")]
        [SerializeField, Min(0f)] private float triggerDelay = 0.5f;

        [Tooltip("Interval in seconds between damage ticks when TriggerType is PeriodicTick.")]
        [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;

        [Header("NavMesh & Living Trap Properties")]
        [Tooltip("If true, attaches or enables a NavMeshObstacle to influence pathfinding.")]
        [SerializeField] private bool hasNavMeshObstacle = false;

        [Tooltip("Max health for LivingUnit traps (Mimics, Totems, Barricades). Ignored for MechanicalHazard.")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        // Public Accessors
        public EntityCategory Category => entityCategory;
        public TrapCategoryType TrapType => trapType;
        public TrapTriggerType TriggerType => triggerType;
        public TrapTargetFilter TargetFilter => targetFilter;
        public Sprite Icon => icon;
        public int SellRefundAmount => sellRefundAmount > 0 ? sellRefundAmount : BaseCost;
        public float DamageAmount => damageAmount;
        public DamageType DamageType => damageType;
        public bool CanCrit => canCrit;
        public float Cooldown => cooldown;
        public int MaxChargesPerRound => maxChargesPerRound;
        public float TriggerDelay => triggerDelay;
        public float TickInterval => tickInterval;
        public bool HasNavMeshObstacle => hasNavMeshObstacle;
        public float MaxHealth => maxHealth;

        /// <summary>
        /// Creates a ready-to-use DamageData payload struct.
        /// </summary>
        public DamageData CreateDamagePayload(object source = null)
        {
            return new DamageData(damageAmount, damageType, source, canCrit);
        }

        private void OnValidate()
        {
            if (entityCategory != EntityCategory.Trap)
            {
                entityCategory = EntityCategory.Trap;
            }

            if (sellRefundAmount < 0)
            {
                sellRefundAmount = BaseCost;
            }

            if (tickInterval < 0.05f)
            {
                tickInterval = 0.05f;
            }
        }
    }
}
