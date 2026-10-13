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

    [Header("Warp Item Tuning (Phase 8)")]
    [Tooltip("HP fraction [0, 1] below which the adventurer considers using a single-user warp escape item.")]
    [SerializeField, Range(0.05f, 0.95f)] private float warpTriggerHpFraction;
    [Tooltip("If true, carrier must have zero usable health potions remaining before triggering warp.")]
    [SerializeField] private bool requireNoPotions;
    [Tooltip("If true, carrier steps back out of enemy melee range before starting the warp cast.")]
    [SerializeField] private bool requireOutOfMelee;
    [Tooltip("Duration in seconds of the interruptible warp item cast.")]
    [SerializeField, Range(1.0f, 10.0f)] private float warpCastDuration;
    [Tooltip("Cooldown in seconds before retrying warp if interrupted by stun, knockback, etc.")]
    [SerializeField, Range(0.5f, 15.0f)] private float warpInterruptCooldown;

    public ItemUseQuality Quality => quality;
    public float HpTriggerFraction => hpTriggerFraction;
    public float ManaTriggerFraction => manaTriggerFraction;
    public bool PanicDrinkUnderAttack => panicDrinkUnderAttack;
    public bool BackOutOfMeleeFirst => backOutOfMeleeFirst;
    public bool SeekPartyRearOrBehindTank => seekPartyRearOrBehindTank;
    public float SafeEnemyDistance => safeEnemyDistance;
    public int PerRoomItemQuota => perRoomItemQuota;
    public float WarpTriggerHpFraction => warpTriggerHpFraction;
    public bool RequireNoPotions => requireNoPotions;
    public bool RequireOutOfMelee => requireOutOfMelee;
    public float WarpCastDuration => warpCastDuration > 0f ? warpCastDuration : 3.5f;
    public float WarpInterruptCooldown => warpInterruptCooldown > 0f ? warpInterruptCooldown : 5.0f;

    public ItemUseQualityTuningEntry(
        ItemUseQuality quality,
        float hpTriggerFraction,
        float manaTriggerFraction,
        bool panicDrinkUnderAttack,
        bool backOutOfMeleeFirst,
        bool seekPartyRearOrBehindTank,
        float safeEnemyDistance,
        int perRoomItemQuota,
        float warpTriggerHpFraction = 0.20f,
        bool requireNoPotions = true,
        bool requireOutOfMelee = false,
        float warpCastDuration = 3.5f,
        float warpInterruptCooldown = 5.0f)
    {
        this.quality = quality;
        this.hpTriggerFraction = Mathf.Clamp01(hpTriggerFraction);
        this.manaTriggerFraction = Mathf.Clamp01(manaTriggerFraction);
        this.panicDrinkUnderAttack = panicDrinkUnderAttack;
        this.backOutOfMeleeFirst = backOutOfMeleeFirst;
        this.seekPartyRearOrBehindTank = seekPartyRearOrBehindTank;
        this.safeEnemyDistance = Mathf.Max(0f, safeEnemyDistance);
        this.perRoomItemQuota = perRoomItemQuota;
        this.warpTriggerHpFraction = Mathf.Clamp01(warpTriggerHpFraction);
        this.requireNoPotions = requireNoPotions;
        this.requireOutOfMelee = requireOutOfMelee;
        this.warpCastDuration = Mathf.Max(0.5f, warpCastDuration);
        this.warpInterruptCooldown = Mathf.Max(0f, warpInterruptCooldown);
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
            // LateWasteful (0, Rank E): drinks late in place while being hit, warp too late (0.15), doesn't wait for zero potions, in melee, long cast
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.LateWasteful,
                hpTriggerFraction: 0.28f,
                manaTriggerFraction: 0.15f,
                panicDrinkUnderAttack: true,
                backOutOfMeleeFirst: false,
                seekPartyRearOrBehindTank: false,
                safeEnemyDistance: 0f,
                perRoomItemQuota: -1,
                warpTriggerHpFraction: 0.15f,
                requireNoPotions: false,
                requireOutOfMelee: false,
                warpCastDuration: 4.5f,
                warpInterruptCooldown: 5.0f),

            // Adequate (1, Rank D): steps back out of melee first, warp below flee threshold (0.25) with no potions left
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.Adequate,
                hpTriggerFraction: 0.45f,
                manaTriggerFraction: 0.30f,
                panicDrinkUnderAttack: false,
                backOutOfMeleeFirst: true,
                seekPartyRearOrBehindTank: false,
                safeEnemyDistance: 3.5f,
                perRoomItemQuota: -1,
                warpTriggerHpFraction: 0.25f,
                requireNoPotions: true,
                requireOutOfMelee: false,
                warpCastDuration: 3.5f,
                warpInterruptCooldown: 4.0f),

            // WellTimed (2, Rank C): moves behind Tank/party rear, steps out of melee first, warps earlier (0.35) so cast finishes
            new ItemUseQualityTuningEntry(
                quality: ItemUseQuality.WellTimed,
                hpTriggerFraction: 0.65f,
                manaTriggerFraction: 0.45f,
                panicDrinkUnderAttack: false,
                backOutOfMeleeFirst: true,
                seekPartyRearOrBehindTank: true,
                safeEnemyDistance: 5.0f,
                perRoomItemQuota: 2,
                warpTriggerHpFraction: 0.35f,
                requireNoPotions: false,
                requireOutOfMelee: true,
                warpCastDuration: 3.0f,
                warpInterruptCooldown: 3.0f)
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
