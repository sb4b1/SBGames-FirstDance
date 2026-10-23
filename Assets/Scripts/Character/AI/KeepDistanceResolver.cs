using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Character
{
    /// <summary>
    /// Pure C# calculation for Keep Distance (kiting) resolution and comfort distance boundaries.
    /// Zero-allocation, immutable-friendly, testable without Unity scene.
    /// </summary>
    public static class KeepDistanceResolver
    {
        /// <summary>
        /// Resolves the reference engagement range for a unit.
        /// If basic attack mode is None or basic attack range is small (<= 1.5) and longest skill cast range exists,
        /// derives range from the longest usable skill cast range.
        /// </summary>
        public static float ResolveReferenceRange(float basicAttackRange, BasicAttackMode basicAttackMode, float longestSkillRange)
        {
            if (basicAttackMode == BasicAttackMode.None)
            {
                return longestSkillRange > 0f ? longestSkillRange : basicAttackRange;
            }

            // Ranged basic attack
            if (basicAttackRange > 1.5f)
            {
                return basicAttackRange;
            }

            // Melee basic attack with long skill
            if (longestSkillRange > basicAttackRange)
            {
                return longestSkillRange;
            }

            return basicAttackRange;
        }

        /// <summary>
        /// Determines whether Keep Distance behavior resolves to enabled.
        /// Auto: enabled if reference range >= autoEnableRangeThreshold.
        /// On: always enabled.
        /// Off: always disabled.
        /// </summary>
        public static bool ResolveIsEnabled(KeepDistanceMode mode, float referenceRange, float autoEnableThreshold = 3.0f)
        {
            switch (mode)
            {
                case KeepDistanceMode.On:
                    return true;
                case KeepDistanceMode.Off:
                    return false;
                case KeepDistanceMode.Auto:
                default:
                    return referenceRange >= autoEnableThreshold;
            }
        }

        /// <summary>
        /// Computes the start and stop retreat distance thresholds.
        /// If comfortDistanceOverride > 0, start is based on the override; otherwise startFraction * referenceRange.
        /// stopDistance is stopFraction * referenceRange (or override * (stopFraction / startFraction)).
        /// </summary>
        public static void CalculateHysteresisDistances(
            float referenceRange,
            float comfortDistanceOverride,
            KeepDistanceTuningSO tuning,
            out float startDistance,
            out float stopDistance)
        {
            float startFrac = tuning != null ? tuning.StartFraction : 0.6f;
            float stopFrac = tuning != null ? tuning.StopFraction : 0.9f;

            if (comfortDistanceOverride > 0f)
            {
                startDistance = comfortDistanceOverride;
                stopDistance = startFrac > 0.001f ? comfortDistanceOverride * (stopFrac / startFrac) : comfortDistanceOverride * 1.5f;
            }
            else
            {
                startDistance = referenceRange * startFrac;
                stopDistance = referenceRange * stopFrac;
            }

            if (stopDistance <= startDistance)
            {
                stopDistance = startDistance + 1f;
            }
        }
    }
}
