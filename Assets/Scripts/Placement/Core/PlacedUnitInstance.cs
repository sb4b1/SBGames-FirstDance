using System;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Represents an active, committed unit placed on the dungeon grid.
    /// Retains exact snapshots of the cost paid and capacity consumed for accurate refunds/reclamation.
    /// </summary>
    public class PlacedUnitInstance
    {
        public string InstanceId { get; }
        public IUnitPlacementDefinition Definition { get; }
        public Vector2Int GridPosition { get; internal set; }
        public float CombatPowerFactor { get; }
        public int CostPaid { get; }
        public int CapacityConsumed { get; }
        public GameObject GameObjectInstance { get; set; }

        public PlacedUnitInstance(
            string instanceId,
            IUnitPlacementDefinition definition,
            Vector2Int gridPosition,
            float combatPowerFactor,
            int costPaid,
            int capacityConsumed,
            GameObject gameObjectInstance = null)
        {
            InstanceId = string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString() : instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            GridPosition = gridPosition;
            CombatPowerFactor = combatPowerFactor;
            CostPaid = costPaid;
            CapacityConsumed = capacityConsumed;
            GameObjectInstance = gameObjectInstance;
        }

        public override string ToString()
        {
            return $"PlacedUnit [{Definition.DisplayName} (ID: {Definition.UnitId})] at {GridPosition} | CP Factor: {CombatPowerFactor:F2} | Cost: {CostPaid} | Capacity: {CapacityConsumed}";
        }
    }
}
