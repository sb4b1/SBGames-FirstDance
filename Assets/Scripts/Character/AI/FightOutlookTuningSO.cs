using UnityEngine;

namespace SBGames.Character.AI
{
    [CreateAssetMenu(fileName = "FightOutlookTuning_Default", menuName = "SBGames/AI/Fight Outlook Tuning SO")]
    public class FightOutlookTuningSO : ScriptableObject
    {
        [Header("Rank Misjudgment Noise (Per-Decision Error Factor)")]
        [Tooltip("Max error factor for Rank E (e.g. 0.40 means ±40% random noise).")]
        [SerializeField, Range(0.1f, 0.8f)] private float rankEErrorFactor = 0.40f;

        [Tooltip("Max error factor for Rank D (e.g. 0.15 means ±15% random noise).")]
        [SerializeField, Range(0.05f, 0.3f)] private float rankDErrorFactor = 0.15f;

        [Tooltip("Max error factor for Rank C (near zero, e.g. 0.02 means ±2%).")]
        [SerializeField, Range(0.0f, 0.1f)] private float rankCErrorFactor = 0.02f;

        [Tooltip("Interval in seconds between re-rolling rank estimation noise (1-2s).")]
        [SerializeField, Range(0.5f, 3.0f)] private float noiseRerollInterval = 1.5f;

        [Header("Role / Function Tolerance Multipliers")]
        [Tooltip("Tank tolerates worse outlook (multiplies TTK or reduces outlook pessimism, default 1.5x).")]
        [SerializeField, Range(1.0f, 2.5f)] private float tankOutlookTolerance = 1.5f;

        [Tooltip("Healer disengages earlier (amplifies outlook pessimism, default 0.65x).")]
        [SerializeField, Range(0.3f, 1.0f)] private float healerOutlookTolerance = 0.65f;

        [Tooltip("Default DPS tolerance multiplier.")]
        [SerializeField, Range(0.8f, 1.2f)] private float dpsOutlookTolerance = 1.0f;

        [Header("Courage Weight")]
        [Tooltip("Weight applied to personality courage trait bias in outlook.")]
        [SerializeField, Range(0.0f, 1.0f)] private float courageWeight = 0.35f;

        [Header("Commitment & Sharp Change Thresholds")]
        [Tooltip("Minimum hold duration in seconds for a chosen goal before allowing switch, unless outlook changes sharply.")]
        [SerializeField, Range(0.5f, 5.0f)] private float minimumGoalHoldTime = 1.5f;

        [Tooltip("Sharp outlook delta threshold that bypasses minimum hold time.")]
        [SerializeField, Range(0.2f, 1.0f)] private float sharpOutlookChangeThreshold = 0.45f;

        public float RankEErrorFactor => rankEErrorFactor;
        public float RankDErrorFactor => rankDErrorFactor;
        public float RankCErrorFactor => rankCErrorFactor;
        public float NoiseRerollInterval => noiseRerollInterval;
        public float TankOutlookTolerance => tankOutlookTolerance;
        public float HealerOutlookTolerance => healerOutlookTolerance;
        public float DpsOutlookTolerance => dpsOutlookTolerance;
        public float CourageWeight => courageWeight;
        public float MinimumGoalHoldTime => minimumGoalHoldTime;
        public float SharpOutlookChangeThreshold => sharpOutlookChangeThreshold;

        public float GetRankErrorFactor(CharacterRank rank)
        {
            return rank switch
            {
                CharacterRank.E => rankEErrorFactor,
                CharacterRank.D => rankDErrorFactor,
                CharacterRank.C => rankCErrorFactor,
                _ => 0f
            };
        }

        public float GetFunctionTolerance(AdventurerFunction function)
        {
            if ((function & AdventurerFunction.Tank) != 0) return tankOutlookTolerance;
            if ((function & AdventurerFunction.Healer) != 0) return healerOutlookTolerance;
            return dpsOutlookTolerance;
        }

        public static FightOutlookTuningSO CreateRuntimeInstance()
        {
            var inst = CreateInstance<FightOutlookTuningSO>();
            return inst;
        }
    }
}
