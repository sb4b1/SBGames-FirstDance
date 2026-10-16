using UnityEngine;

public enum LeaderAssessmentAccuracy
{
    Optimistic = 0,
    NearTrue = 1,
    Accurate = 2
}

public enum LeaderSafetyMargin
{
    Thin = 0,
    Medium = 1,
    Thick = 2
}

public enum ItemUseQuality
{
    LateWasteful = 0,
    Adequate = 1,
    WellTimed = 2
}

public enum TargetPriorityMode
{
    Nearest = 0,
    LowestHp = 1,
    SupportFirstThenLowestHp = 2
}

public enum LeaderlessBehavior
{
    Dissolve = 0,
    StayLoose = 1
}

public enum HealerPriorityMode
{
    LowestHp = 0,
    TankFirstThenLowestHp = 1
}

/// <summary>
/// Data-driven baseline decision parameters per adventurer rank.
/// Higher ranks (B, A, S) can be added as additional assets without code changes.
/// </summary>
[CreateAssetMenu(fileName = "Profile_Rank_E", menuName = "SBGames/AI/Adventurer AI Profile")]
public class AdventurerAIProfile : ScriptableObject
{
    [Header("Rank Identity")]
    [SerializeField] private CharacterRank rank = CharacterRank.E;

    [Header("Perception & Reaction")]
    [Tooltip("Strategic Utility/GOAP decision evaluation interval in seconds.")]
    [SerializeField, Range(0.1f, 1.0f)] private float decisionInterval = 0.5f;

    [Tooltip("Perception sensor detection radius in world units.")]
    [SerializeField, Range(3f, 15f)] private float visionRadius = 5f;

    [Header("Survival & Tactical Staging")]
    [Tooltip("Baseline HP fraction [0.1, 0.8] below which the adventurer flees, before personality adjustment.")]
    [SerializeField, Range(0.1f, 0.8f)] private float individualFleeHpThreshold = 0.40f;

    [Tooltip("Pause duration in seconds when staging at a doorway or blind corner.")]
    [SerializeField, Range(0.1f, 1.5f)] private float doorwayHesitation = 0.6f;

    [Tooltip("Duration in seconds that triggered/witnessed trap hazards are retained in spatial memory.")]
    [SerializeField, Range(5f, 120f)] private float hazardMemoryDuration = 10f;

    [Header("Target Selection")]
    [Tooltip("Target selection priority mode (Nearest, LowestHp, SupportFirstThenLowestHp).")]
    [SerializeField] private TargetPriorityMode targetPriorityMode = TargetPriorityMode.Nearest;

    [Header("Squad & Item Heuristics")]
    [Tooltip("Probability/weight [0.1, 1.0] of complying with squad leader tactical orders.")]
    [SerializeField, Range(0.1f, 1.0f)] private float squadOrderCompliance = 0.50f;

    [SerializeField] private LeaderAssessmentAccuracy leaderAssessmentAccuracy = LeaderAssessmentAccuracy.Optimistic;
    [SerializeField] private LeaderSafetyMargin leaderSafetyMargin = LeaderSafetyMargin.Thin;
    [SerializeField] private ItemUseQuality itemUseQuality = ItemUseQuality.LateWasteful;

    [Header("Leader Checkpoint & Succession (Phase 4)")]
    [Tooltip("Greed/reward weight multiplying expected potential reward when evaluating whether to advance.")]
    [SerializeField, Range(0.1f, 3.0f)] private float rewardWeight = 1.60f;

    [Tooltip("Reaction delay in seconds after the party leader dies before survivors evaluate succession.")]
    [SerializeField, Range(0.0f, 5.0f)] private float successionReactionDelay = 1.80f;

    [Tooltip("Minimum candidate score [0.0, 1.0] required for a survivor to step up as the new party leader.")]
    [SerializeField, Range(0.0f, 1.0f)] private float stepUpThreshold = 0.70f;

    [Tooltip("Fallback squad behavior when no survivor meets the stepUpThreshold after leader death.")]
    [SerializeField] private LeaderlessBehavior leaderlessBehavior = LeaderlessBehavior.Dissolve;

    [Header("Item Kit (Phase 5)")]
    [Tooltip("Baseline consumable item kit granted to adventurers of this rank at spawn.")]
    [SerializeField] private AdventurerItemKit startingItemKit;

    [Header("Room Loop Tuning (Phase 6)")]
    [Tooltip("Room loop state machine timings, weights, and boss door policies for this rank.")]
    [SerializeField] private SBGames.Characters.AI.RoomLoopTuningSO roomLoopTuning;

    [Header("Healer Heuristics")]
    [Tooltip("Ally HP fraction below which healer triggers heal goal.")]
    [SerializeField, Range(0.2f, 0.9f)] private float healHpThreshold = 0.50f;

    [Tooltip("Healer target prioritization mode.")]
    [SerializeField] private HealerPriorityMode healPriorityMode = HealerPriorityMode.LowestHp;

    [Tooltip("Reaction delay or interval for healing decisions in seconds.")]
    [SerializeField, Range(0.0f, 2.0f)] private float healReactionDelay = 0.3f;

    public CharacterRank Rank => rank;
    public float DecisionInterval => decisionInterval;
    public float VisionRadius => visionRadius;
    public float IndividualFleeHpThreshold => individualFleeHpThreshold;
    public float DoorwayHesitation => doorwayHesitation;
    public float HazardMemoryDuration => hazardMemoryDuration;
    public TargetPriorityMode TargetPriorityMode => targetPriorityMode;
    public float SquadOrderCompliance => squadOrderCompliance;
    public LeaderAssessmentAccuracy LeaderAssessmentAccuracy => leaderAssessmentAccuracy;
    public LeaderSafetyMargin LeaderSafetyMargin => leaderSafetyMargin;
    public ItemUseQuality ItemUseQuality => itemUseQuality;
    public float RewardWeight => rewardWeight;
    public float SuccessionReactionDelay => successionReactionDelay;
    public float StepUpThreshold => stepUpThreshold;
    public LeaderlessBehavior LeaderlessBehavior => leaderlessBehavior;
    public AdventurerItemKit StartingItemKit => startingItemKit;
    public SBGames.Characters.AI.RoomLoopTuningSO RoomLoopTuning => roomLoopTuning;

    public float HealHpThreshold => healHpThreshold > 0f ? healHpThreshold : (rank switch
    {
        CharacterRank.E => 0.35f,
        CharacterRank.D => 0.50f,
        CharacterRank.C => 0.70f,
        _ => 0.50f
    });

    public HealerPriorityMode HealPriorityMode => rank == CharacterRank.C ? HealerPriorityMode.TankFirstThenLowestHp : healPriorityMode;

    public float HealReactionDelay => rank switch
    {
        CharacterRank.E => 0.8f,
        CharacterRank.D => 0.25f,
        CharacterRank.C => 0.05f,
        _ => healReactionDelay
    };

    /// <summary>
    /// Computes the individual flee HP threshold adjusted by a personality multiplier.
    /// Clamps the personality multiplier to [0.85, 1.15] (+/-15% max shift) and clamps the result to [0, 1].
    /// Strictly zero managed allocations.
    /// </summary>
    public float GetAdjustedFleeThreshold(float personalityMultiplier)
    {
        float clampedMultiplier = Mathf.Clamp(personalityMultiplier, 0.85f, 1.15f);
        return Mathf.Clamp01(individualFleeHpThreshold * clampedMultiplier);
    }
}
