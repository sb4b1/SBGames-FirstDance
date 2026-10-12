using System;
using UnityEngine;

[Serializable]
public struct ItemUseQualityTuningEntry
{
    [SerializeField] private ItemUseQuality quality;
    [Tooltip("HP fraction [0, 1] below which the adventurer triggers HP potion recovery.")]
    [SerializeField, Range(0.05f, 0.95f)] private float hpTriggerFraction;
    [Tooltip("Mana fraction [0, 1] below which the adventurer triggers Mana potion recovery.")]
    [SerializeField, Range(0.05f, 0.95f)] private float manaTriggerFraction;
    [Tooltip("If true, the adventurer drinks immediately where it stands even while under melee attack.")]
    [SerializeField] private bool panicDrinkUnderAttack;
    [Tooltip("If true, the adventurer steps back out of enemy melee range before starting the drink cast.")]
    [SerializeField] private bool backOutOfMeleeFirst;
    [Tooltip("If true, the adventurer moves to the party rear or behind the Tank before drinking.")]
    [SerializeField] private bool seekPartyRearOrBehindTank;
    [Tooltip("Distance in world units from the nearest enemy considered safe to drink when backing out.")]
    [SerializeField, Min(0f)] private float safeEnemyDistance;
    [Tooltip("Maximum number of consumable items usable per room (-1 = unlimited).")]
    [SerializeField] private int perRoomItemQuota;

    public ItemUseQuality Quality => quality;
    public float HpTriggerFraction => hpTriggerFraction;
    public float ManaTriggerFraction => manaTriggerFraction;
    public bool PanicDrinkUnderAttack => panicDrinkUnderAttack;
    public bool BackOutOfMeleeFirst => backOutOfMeleeFirst;
    public bool SeekPartyRearOrBehindTank => seekPartyRearOrBehindTank;
    public float SafeEnemyDistance => safeEnemyDistance;
    public int PerRoomItemQuota => perRoomItemQuota;

    public ItemUseQualityTuningEntry(
        ItemUseQuality quality,
        float hpTriggerFraction,
        float manaTriggerFraction,
        bool panicDrinkUnderAttack,
        bool backOutOfMeleeFirst,
        bool seekPartyRearOrBehindTank,
        float safeEnemyDistance,
        int perRoomItemQuota)
    {
        this.quality = quality;
        this.hpTriggerFraction = Mathf.Clamp01(hpTriggerFraction);
        this.manaTriggerFraction = Mathf.Clamp01(manaTriggerFraction);
        this.panicDrinkUnderAttack = panicDrinkUnderAttack;
        this.backOutOfMeleeFirst = backOutOfMeleeFirst;
        this.seekPartyRearOrBehindTank = seekPartyRearOrBehindTank;
        this.safeEnemyDistance = Mathf.Max(0f, safeEnemyDistance);
        this.perRoomItemQuota = perRoomItemQuota;
    }
}

/// <summary>
/// Data-driven tuning table indexed by <see cref="ItemUseQuality"/> (LateWasteful=0, Adequate=1, WellTimed=2).
/// Uses O(1) enum-indexed array lookups with no switch statements.
/// </summary>
[CreateAssetMenu(fileName = "ItemUseQualityTuning", menuName = "SBGames/AI/Item Use Quality Tuning")]
public class ItemUseQualityTuningSO : ScriptableObject
{
    public const int QualityCount = 3;

    private static readonly ItemUseQualityTuningEntry[] DefaultQualityTable = CreateDefaultQualityArray();

    [Header("Quality Entries (Indexed by (int)ItemUseQuality: LateWasteful=0, Adequate=1, WellTimed=2)")]
    [SerializeField] private ItemUseQualityTuningEntry[] qualityEntries = CreateDefaultQualityArray();

    [Header("Squad Recover Order Thresholds")]
    [Tooltip("Target HP fraction [0, 1] members recover up to while SquadOrderType.Recover is active.")]
    [SerializeField, Range(0.50f, 1.0f)] private float squadRecoverTargetHpFraction = 0.85f;
    [Tooltip("Target Mana fraction [0, 1] members recover up to while SquadOrderType.Recover is active.")]
    [SerializeField, Range(0.50f, 1.0f)] private float squadRecoverTargetManaFraction = 0.75f;

    public float SquadRecoverTargetHpFraction => squadRecoverTargetHpFraction;
    public float SquadRecoverTargetManaFraction => squadRecoverTargetManaFraction;

    public static ItemUseQualityTuningEntry[] CreateDefaultQualityArray()
    {
        return new ItemUseQualityTuningEntry[]
        {
            // LateWasteful (0, Rank E): drinks late in place while being hit, no retreat positioning, unlimited per-room quota
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.LateWasteful,
                hpTriggerFraction: 0.28f,
                manaTriggerFraction: 0.15f,
                panicDrinkUnderAttack: true,
                backOutOfMeleeFirst: false,
                seekPartyRearOrBehindTank: false,
                safeEnemyDistance: 0f,
                perRoomItemQuota: -1),

            // Adequate (1, Rank D): steps back out of melee first, moderate trigger thresholds, unlimited per-room quota
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.Adequate,
                hpTriggerFraction: 0.45f,
                manaTriggerFraction: 0.30f,
                panicDrinkUnderAttack: false,
                backOutOfMeleeFirst: true,
                seekPartyRearOrBehindTank: false,
                safeEnemyDistance: 3.5f,
                perRoomItemQuota: -1),

            // WellTimed (2, Rank C): moves behind Tank/party rear, drinks when safe, respects per-room item quota
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.WellTimed,
                hpTriggerFraction: 0.65f,
                manaTriggerFraction: 0.45f,
                panicDrinkUnderAttack: false,
                backOutOfMeleeFirst: true,
                seekPartyRearOrBehindTank: true,
                safeEnemyDistance: 5.0f,
                perRoomItemQuota: 2)
        };
    }

    public ItemUseQualityTuningEntry GetEntry(ItemUseQuality quality)
    {
        int index = Mathf.Clamp((int)quality, 0, QualityCount - 1);
        if (qualityEntries != null && index < qualityEntries.Length)
        {
            return qualityEntries[index];
        }
        return DefaultQualityTable[index];
    }

    public static ItemUseQualityTuningEntry GetEntryOrDefault(ItemUseQualityTuningSO tuning, ItemUseQuality quality)
    {
        if (tuning != null)
        {
            return tuning.GetEntry(quality);
        }
        int index = Mathf.Clamp((int)quality, 0, DefaultQualityTable.Length - 1);
        return DefaultQualityTable[index];
    }

    private void OnValidate()
    {
        if (qualityEntries == null || qualityEntries.Length < QualityCount)
        {
            var defaults = CreateDefaultQualityArray();
            if (qualityEntries != null)
            {
                for (int i = 0; i < qualityEntries.Length && i < defaults.Length; i++)
                {
                    defaults[i] = qualityEntries[i];
                }
            }
            qualityEntries = defaults;
        }
    }
}
