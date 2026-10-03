using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Skill/Configurable Skill")]
public class ConfigurableUtilitySkill : UtilitySkill
{
    [Header("Skill Identifier")]
    [SerializeField] private string skillID = "SkillID";

    [Header("Priority & Base Weight")]
    [SerializeField, Range(0.1f, 10f)] private float baseWeight = 1.0f;
    [SerializeField] private float minManaCost = 0f;

    [Header("Distance Consideration")]
    [SerializeField] private bool useDistanceConsideration = true;
    [Tooltip("Maximum range for distance normalization.")]
    [SerializeField] private float maxEvaluationRange = 10f;
    [Tooltip("Curve evaluated from 0 (at melee contact) to 1 (at maxEvaluationRange).")]
    [SerializeField] private AnimationCurve distanceCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);

    [Header("Cluster / AoE Consideration")]
    [SerializeField] private bool useClusterConsideration = false;
    [SerializeField] private float clusterRadius = 3f;
    [Tooltip("Curve evaluated using count of enemies in clusterRadius (e.g. X: 1 to 5 enemies, Y: multiplier).")]
    [SerializeField] private AnimationCurve clusterCurve = AnimationCurve.Linear(1f, 0.5f, 4f, 1.5f);

    [Header("Health Threshold Gate (Optional)")]
    [SerializeField] private bool checkHealthThreshold = false;
    [SerializeField, Range(0f, 100f)] private float minHealthPercent = 0f;
    [SerializeField, Range(0f, 100f)] private float maxHealthPercent = 100f;

    public override string GetID() => skillID;

    public override float Evaluate(BaseAI context)
    {
        if (context == null)
            return 0f;

        // 1. Mana check
        if (minManaCost > 0f && context.AgentMana < minManaCost)
            return 0f;

        // 2. Cooldown check via SkillController if available
        if (context.SkillController != null)
        {
            var skills = context.SkillController.Skills;
            if (skills != null)
            {
                bool foundSkill = false;
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null && (skills[i].UtilityData == this || skills[i].ID == skillID))
                    {
                        foundSkill = true;
                        if (!skills[i].IsReady)
                            return 0f; // on cooldown
                        break;
                    }
                }
                // If not found in character skills, do not score
                if (!foundSkill && skills.Count > 0)
                    return 0f;
            }
        }

        // 3. Health threshold gate (e.g. defensive skills or execution skills)
        if (checkHealthThreshold)
        {
            float maxHp = 100f;
            if (context.TryGetComponent<CharacterStatsManager>(out var stats) && stats.MaxLife != null && stats.MaxLife.Value > 0f)
            {
                maxHp = stats.MaxLife.Value;
            }
            float currentHpPercent = (context.AgentLife / maxHp) * 100f;
            if (currentHpPercent < minHealthPercent || currentHpPercent > maxHealthPercent)
                return 0f;
        }

        float score = baseWeight;

        // 4. Distance evaluation
        if (useDistanceConsideration)
        {
            if (context.CurrentTarget == null)
                return 0f;

            Vector3 diff = context.CurrentTarget.position - context.transform.position;
            float sqrDist = diff.sqrMagnitude;
            float maxRange = maxEvaluationRange > 0f ? maxEvaluationRange : 10f;
            float normDist = Mathf.Clamp01(Mathf.Sqrt(sqrDist) / maxRange);

            float distFactor = distanceCurve.Evaluate(normDist);
            if (distFactor <= 0f)
                return 0f;

            score *= distFactor;
        }

        // 5. Cluster / AoE evaluation
        if (useClusterConsideration)
        {
            int enemiesInCluster = context.EnemiesInSkillRange(clusterRadius);
            if (enemiesInCluster <= 0)
                return 0f;

            float clusterFactor = clusterCurve.Evaluate(enemiesInCluster);
            score *= clusterFactor;
        }

        return Mathf.Max(0f, score);
    }
}
