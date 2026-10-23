using UnityEngine;

namespace SBGames.Character.AI
{
    /// <summary>
    /// Pure C# calculator for evaluating a unit's tactical Fight Outlook without allocations.
    /// Outlook is positive when winning/even and negative when losing.
    /// </summary>
    public static class FightOutlookCalculator
    {
        public struct CalculationInput
        {
            public float SelfLife;
            public float SelfMaxLife;
            public float SelfDps;
            public float TargetLife;
            public float TargetMaxLife;
            public float NearbyAlliesDps;
            public float IncomingEnemiesDps;
            public float CourageBias; // -1 (cowardly) to +1 (reckless)
            public float FunctionTolerance; // e.g. 1.5 for Tank, 0.65 for Healer
            public float RankTtkNoiseFactor; // -error to +error (Rank E wide, D narrow, C ~0)
            public float RankTtdNoiseFactor; // -error to +error
            public bool IsHardSafetyTrigger; // HP critical or imminent lethal
        }

        public struct CalculationResult
        {
            public float EstimatedTtk;
            public float EstimatedTtd;
            public float RawOutlook;       // Positive: winning/even, Negative: losing
            public float NormalizedOutlook;// -1 (imminent loss) to +1 (guaranteed win)
            public bool IsWinningOrEven;
            public bool IsLosing;
        }

        public static CalculationResult Calculate(in CalculationInput input, FightOutlookTuningSO tuning = null)
        {
            float courageWeight = tuning != null ? tuning.CourageWeight : 0.35f;

            // 1. Total offensive DPS vs current target (self + nearby allies)
            float totalOffensiveDps = Mathf.Max(0.5f, input.SelfDps + input.NearbyAlliesDps);
            float targetLife = Mathf.Max(1f, input.TargetLife);
            float baseTtk = targetLife / totalOffensiveDps;

            // 2. Total defensive incoming DPS vs self
            float incomingDps = Mathf.Max(0.5f, input.IncomingEnemiesDps);
            float selfLife = Mathf.Max(1f, input.SelfLife);
            float baseTtd = selfLife / incomingDps;

            // 3. Apply rank noise unless hard safety trigger active
            float effectiveTtk = baseTtk;
            float effectiveTtd = baseTtd;

            if (!input.IsHardSafetyTrigger)
            {
                effectiveTtk = Mathf.Max(0.1f, baseTtk * (1f + input.RankTtkNoiseFactor));
                effectiveTtd = Mathf.Max(0.1f, baseTtd * (1f + input.RankTtdNoiseFactor));
            }

            // 4. Role/Function tolerance & Courage bias
            // Function tolerance adjusts perceived TTD (Tanks tolerate worse outlook by feeling higher effective TTD)
            float adjustedTtd = effectiveTtd * Mathf.Max(0.2f, input.FunctionTolerance);

            // Raw outlook: difference ratio (adjustedTtd - effectiveTtk) / max(effectiveTtd, effectiveTtk)
            float maxTime = Mathf.Max(effectiveTtk, adjustedTtd);
            float rawOutlook = maxTime > 0.001f ? (adjustedTtd - effectiveTtk) / maxTime : 0f;

            // Modulate with personality courage trait
            float normalizedOutlook = Mathf.Clamp(rawOutlook + (input.CourageBias * courageWeight), -1f, 1f);

            bool winningOrEven = normalizedOutlook >= -0.05f;

            return new CalculationResult
            {
                EstimatedTtk = effectiveTtk,
                EstimatedTtd = effectiveTtd,
                RawOutlook = rawOutlook,
                NormalizedOutlook = normalizedOutlook,
                IsWinningOrEven = winningOrEven,
                IsLosing = !winningOrEven
            };
        }
    }
}
