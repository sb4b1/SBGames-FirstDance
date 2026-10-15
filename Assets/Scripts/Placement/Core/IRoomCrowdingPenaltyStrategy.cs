using System;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Strategy interface for calculating energy cost surcharges when placing units into a room that exceeds soft capacity.
    /// </summary>
    public interface IRoomCrowdingPenaltyStrategy
    {
        /// <summary>
        /// Calculates the additional energy cost surcharge incurred due to room crowding.
        /// </summary>
        /// <param name="currentRoomCapacity">Current capacity consumed by units already placed in the room.</param>
        /// <param name="softCapacity">Soft capacity limit before crowding penalties apply.</param>
        /// <param name="maxCapacity">Hard capacity limit of the room.</param>
        /// <param name="baseCost">Base energy cost of the unit being placed.</param>
        /// <returns>Non-negative integer energy cost surcharge to be added to the unit's base cost.</returns>
        int CalculateCrowdingCostSurcharge(int currentRoomCapacity, int softCapacity, int maxCapacity, int baseCost);
    }

    /// <summary>
    /// Linear crowding penalty strategy: each unit/capacity point beyond soft capacity adds a fixed percentage surcharge to base energy cost.
    /// Default: +25% base cost per point over soft capacity.
    /// </summary>
    public class LinearRoomCrowdingPenaltyStrategy : IRoomCrowdingPenaltyStrategy
    {
        private readonly float _surchargePercentPerPoint;

        public float SurchargePercentPerPoint => _surchargePercentPerPoint;

        public LinearRoomCrowdingPenaltyStrategy(float surchargePercentPerPoint = 0.25f)
        {
            _surchargePercentPerPoint = Mathf.Max(0f, surchargePercentPerPoint);
        }

        public int CalculateCrowdingCostSurcharge(int currentRoomCapacity, int softCapacity, int maxCapacity, int baseCost)
        {
            if (currentRoomCapacity < softCapacity || baseCost <= 0)
            {
                return 0;
            }

            int excessPoints = (currentRoomCapacity - softCapacity) + 1;
            float totalSurcharge = baseCost * (excessPoints * _surchargePercentPerPoint);
            return Mathf.Max(1, Mathf.CeilToInt(totalSurcharge));
        }
    }
}
