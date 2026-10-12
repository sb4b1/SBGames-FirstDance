using System;
using SB4b1.Logging;
using SBGames.Inventory.Data;
using UnityEngine;

[DisallowMultipleComponent]
public class AdventurerItemKitController : MonoBehaviour, IPoolable
{
    public const int MaxKitSlots = 8;
    public const int CategoryCount = 4; // None=0, HealthPotion=1, ManaPotion=2, StatusCure=3

    [Serializable]
    public struct RuntimeKitSlot
    {
        public ItemDataSO Item;
        public ConsumableItemCategory Category;
        public int InitialCount;
        public int RemainingCount;
        public float CooldownReadyTime;
    }

    [Header("Kit Configuration")]
    [SerializeField] private AdventurerItemKit overrideStartingKit;
    [SerializeField] private ItemKitModifiers kitModifiers;
    [SerializeField] private ItemUseQualityTuningSO qualityTuning;

    [Header("Cast & Cooldown Timing")]
    [Tooltip("Duration in seconds of the interruptible item use cast before consumption completes.")]
    [SerializeField, Range(0.1f, 2.0f)] private float castDuration = 0.75f;
    [Tooltip("Cooldown in seconds per item slot after an item is consumed.")]
    [SerializeField, Range(0.5f, 15.0f)] private float itemCooldown = 6.0f;

    private readonly RuntimeKitSlot[] slots = new RuntimeKitSlot[MaxKitSlots];
    private int activeSlotCount;
    private int totalInitialCount;
    private int totalRemainingCount;
    private int itemsUsedInCurrentRoom;

    private bool isCastingItem;
    private int castingSlotIndex = -1;
    private float castRemainingTime;
    private float currentClockTime;

    private CharacterBrain brain;
    private CharacterStatsManager statsManager;
    private StatusEffectManager statusEffectManager;
    private BaseAI baseAI;
    private bool isDeathSubscribed;
    private bool isKitResolved;

    // Preallocated buffers for zero-alloc kit resolution indexed by ConsumableItemCategory (0..3)
    private readonly ItemDataSO[] resolvedCategoryItems = new ItemDataSO[CategoryCount];
    private readonly int[] resolvedCategoryCounts = new int[CategoryCount];

    public event Action<AdventurerItemKitController, ItemDataSO> OnPotionConsumed;
    public event Action<AdventurerItemKitController, ItemDataSO> OnItemCastStarted;
    public event Action<AdventurerItemKitController, ItemDataSO> OnItemCastInterrupted;

    public AdventurerItemKit OverrideStartingKit { get => overrideStartingKit; set => overrideStartingKit = value; }
    public ItemKitModifiers KitModifiers { get => kitModifiers; set => kitModifiers = value; }
    public ItemUseQualityTuningSO QualityTuning { get => qualityTuning; set => qualityTuning = value; }
    public float CastDuration { get => castDuration; set => castDuration = Mathf.Max(0.05f, value); }
    public float ItemCooldown { get => itemCooldown; set => itemCooldown = Mathf.Max(0f, value); }

    public bool IsCastingItem => isCastingItem;
    public float CastRemainingTime => isCastingItem ? castRemainingTime : 0f;
    public ItemDataSO CastingItem => (isCastingItem && castingSlotIndex >= 0 && castingSlotIndex < activeSlotCount)
        ? slots[castingSlotIndex].Item
        : null;

    public int ActiveSlotCount => activeSlotCount;
    public int TotalInitialCount => totalInitialCount;
    public int TotalRemainingCount => totalRemainingCount;
    public float RemainingFraction => totalInitialCount > 0 ? Mathf.Clamp01((float)totalRemainingCount / totalInitialCount) : 0f;
    public int ItemsUsedInCurrentRoom => itemsUsedInCurrentRoom;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
        SubscribeDeath();
    }

    private void Start()
    {
        if (!isKitResolved)
        {
            ResolveRuntimeKit();
        }
    }

    private void OnDisable()
    {
        UnsubscribeDeath();
        CancelCast("Disabled");
    }

    private void Update()
    {
        currentClockTime = Time.time;
        if (isCastingItem)
        {
            AdvanceCast(Time.deltaTime);
        }
    }

    public void OnSpawn()
    {
        CacheComponents();
        SubscribeDeath();
        CancelCast("SpawnReset");
        ResolveRuntimeKit();
    }

    public void OnDespawn()
    {
        CancelCast("Despawn");
        UnsubscribeDeath();
        isKitResolved = false;
    }

    private void CacheComponents()
    {
        if (brain == null)
        {
            brain = GetComponent<CharacterBrain>() ?? GetComponentInParent<CharacterBrain>() ?? GetComponentInChildren<CharacterBrain>();
        }
        if (statsManager == null)
        {
            statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        }
        if (statusEffectManager == null)
        {
            statusEffectManager = GetComponent<StatusEffectManager>() ?? GetComponentInParent<StatusEffectManager>() ?? GetComponentInChildren<StatusEffectManager>();
        }
        if (baseAI == null)
        {
            baseAI = GetComponent<BaseAI>() ?? GetComponentInChildren<BaseAI>() ?? GetComponentInParent<BaseAI>();
        }
    }

    private void SubscribeDeath()
    {
        if (statsManager != null && !isDeathSubscribed)
        {
            statsManager.OnCharacterDeath -= HandleCharacterDeath;
            statsManager.OnCharacterDeath += HandleCharacterDeath;
            isDeathSubscribed = true;
        }
    }

    private void UnsubscribeDeath()
    {
        if (statsManager != null && isDeathSubscribed)
        {
            statsManager.OnCharacterDeath -= HandleCharacterDeath;
            isDeathSubscribed = false;
        }
    }

    private void HandleCharacterDeath(CharacterStatsManager deadStats)
    {
        CancelCast("Death");
    }

    /// <summary>
    /// Resets the per-room item usage counter (e.g. when entering a new room).
    /// </summary>
    public void ResetRoomQuota()
    {
        itemsUsedInCurrentRoom = 0;
    }

    /// <summary>
    /// Resolves the final runtime item kit from (rank kit + function modifier + wealth modifier).
    /// Mana potions are only granted if the character has MaxMana &gt; 0.
    /// Zero managed allocations.
    /// </summary>
    public void ResolveRuntimeKit()
    {
        CacheComponents();

        var profile = baseAI != null ? baseAI.AdventurerProfile : null;
        AdventurerItemKit baseKit = overrideStartingKit != null
            ? overrideStartingKit
            : (profile != null ? profile.StartingItemKit : null);

        AdventurerFunction function = statsManager != null ? statsManager.CharacterFunction : AdventurerFunction.None;
        AdventurerWealth wealth = statsManager != null ? statsManager.CharacterWealth : AdventurerWealth.Average;
        bool hasManaPool = statsManager != null && statsManager.MaxMana != null && statsManager.MaxMana.Value > 0f;

        ResolveRuntimeKit(baseKit, kitModifiers, function, wealth, hasManaPool);
    }

    /// <summary>
    /// Explicit overload for resolving the runtime kit from given inputs. Zero managed allocations.
    /// </summary>
    public void ResolveRuntimeKit(
        AdventurerItemKit baseKit,
        ItemKitModifiers modifiers,
        AdventurerFunction function,
        AdventurerWealth wealth,
        bool hasManaPool)
    {
        CancelCast("ResolveKit");

        for (int c = 0; c < CategoryCount; c++)
        {
            resolvedCategoryItems[c] = null;
            resolvedCategoryCounts[c] = 0;
        }

        if (modifiers != null)
        {
            resolvedCategoryItems[(int)ConsumableItemCategory.HealthPotion] = modifiers.DefaultHpPotionItem;
            resolvedCategoryItems[(int)ConsumableItemCategory.ManaPotion] = modifiers.DefaultManaPotionItem;
            resolvedCategoryItems[(int)ConsumableItemCategory.StatusCure] = modifiers.DefaultStatusCureItem;
        }

        activeSlotCount = 0;
        totalInitialCount = 0;
        totalRemainingCount = 0;
        itemsUsedInCurrentRoom = 0;
        currentClockTime = Application.isPlaying ? Time.time : 0f;

        // 1. Base rank kit entries
        if (baseKit != null && baseKit.Entries != null)
        {
            var entries = baseKit.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Item == null || entry.Count <= 0) continue;

                ConsumableItemCategory cat = AdventurerItemKit.ClassifyItem(entry.Item);
                if (cat != ConsumableItemCategory.None)
                {
                    int catIdx = (int)cat;
                    if (resolvedCategoryItems[catIdx] == null)
                    {
                        resolvedCategoryItems[catIdx] = entry.Item;
                    }
                    resolvedCategoryCounts[catIdx] += entry.Count;
                }
                else if (activeSlotCount < MaxKitSlots)
                {
                    slots[activeSlotCount] = new RuntimeKitSlot
                    {
                        Item = entry.Item,
                        Category = ConsumableItemCategory.None,
                        InitialCount = entry.Count,
                        RemainingCount = entry.Count,
                        CooldownReadyTime = 0f
                    };
                    activeSlotCount++;
                    totalInitialCount += entry.Count;
                    totalRemainingCount += entry.Count;
                }
            }
        }

        // 2. Function modifier lookup (indexed by (int)AdventurerFunction, no switch)
        FunctionKitModifierEntry fnMod = ItemKitModifiers.GetFunctionModifierOrDefault(modifiers, function);
        resolvedCategoryCounts[(int)ConsumableItemCategory.HealthPotion] += fnMod.HpPotionDelta;
        resolvedCategoryCounts[(int)ConsumableItemCategory.ManaPotion] += fnMod.ManaPotionDelta;
        resolvedCategoryCounts[(int)ConsumableItemCategory.StatusCure] += fnMod.StatusCureDelta;

        // 3. Wealth modifier lookup (indexed by (int)AdventurerWealth, no switch)
        WealthKitModifierEntry wealthMod = ItemKitModifiers.GetWealthModifierOrDefault(modifiers, wealth);
        resolvedCategoryCounts[(int)ConsumableItemCategory.HealthPotion] += wealthMod.HpPotionDelta;
        resolvedCategoryCounts[(int)ConsumableItemCategory.ManaPotion] += wealthMod.ManaPotionDelta;
        resolvedCategoryCounts[(int)ConsumableItemCategory.StatusCure] += wealthMod.StatusCureDelta;

        if (wealthMod.OptionalReplacementItem != null)
        {
            ConsumableItemCategory repCat = AdventurerItemKit.ClassifyItem(wealthMod.OptionalReplacementItem);
            if (repCat != ConsumableItemCategory.None)
            {
                resolvedCategoryItems[(int)repCat] = wealthMod.OptionalReplacementItem;
            }
            else
            {
                resolvedCategoryItems[(int)ConsumableItemCategory.HealthPotion] = wealthMod.OptionalReplacementItem;
            }
        }

        // 4. Mana Potion gate: only granted to members with MaxMana > 0
        if (!hasManaPool)
        {
            resolvedCategoryCounts[(int)ConsumableItemCategory.ManaPotion] = 0;
        }

        // 5. Commit category slots (floored at 0)
        for (int c = 1; c < CategoryCount; c++)
        {
            int finalCount = Mathf.Max(0, resolvedCategoryCounts[c]);
            ItemDataSO item = resolvedCategoryItems[c];
            if (finalCount <= 0 || item == null || activeSlotCount >= MaxKitSlots)
            {
                continue;
            }

            slots[activeSlotCount] = new RuntimeKitSlot
            {
                Item = item,
                Category = (ConsumableItemCategory)c,
                InitialCount = finalCount,
                RemainingCount = finalCount,
                CooldownReadyTime = 0f
            };
            activeSlotCount++;
            totalInitialCount += finalCount;
            totalRemainingCount += finalCount;
        }

        isKitResolved = true;
    }

    public int GetRemainingCount(ConsumableItemCategory category)
    {
        int sum = 0;
        for (int i = 0; i < activeSlotCount; i++)
        {
            if (slots[i].Category == category)
            {
                sum += slots[i].RemainingCount;
            }
        }
        return sum;
    }

    public int GetRemainingCount(ItemDataSO item)
    {
        if (item == null) return 0;
        int sum = 0;
        for (int i = 0; i < activeSlotCount; i++)
        {
            if (slots[i].Item == item)
            {
                sum += slots[i].RemainingCount;
            }
        }
        return sum;
    }

    public bool IsSlotOffCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= activeSlotCount) return false;
        float now = Application.isPlaying ? Time.time : currentClockTime;
        return now >= slots[slotIndex].CooldownReadyTime;
    }

    public bool CanUse(ConsumableItemCategory category, int perRoomQuota = -1)
    {
        if (!CanCharacterUseItems(perRoomQuota)) return false;

        int slotIdx = FindUsableSlotByCategory(category);
        return slotIdx >= 0;
    }

    public bool CanUse(ItemDataSO item, int perRoomQuota = -1)
    {
        if (item == null || !CanCharacterUseItems(perRoomQuota)) return false;

        int slotIdx = FindUsableSlotByItem(item);
        return slotIdx >= 0;
    }

    /// <summary>
    /// Returns true if the adventurer has any usable recovery item whose effect can currently be applied
    /// (or is already mid-cast on an item).
    /// </summary>
    public bool HasAnyApplicableItemNow(float hpTriggerFrac = 1.0f, float manaTriggerFrac = 1.0f, int perRoomQuota = -1)
    {
        if (isCastingItem) return true;
        if (!CanCharacterUseItems(perRoomQuota)) return false;

        return FindBestNeededSlotIndex(hpTriggerFrac, manaTriggerFrac) >= 0;
    }

    public bool TryStartUse(ConsumableItemCategory category, int perRoomQuota = -1)
    {
        if (!CanCharacterUseItems(perRoomQuota)) return false;

        int slotIdx = FindUsableSlotByCategory(category);
        if (slotIdx < 0) return false;

        BeginSlotCast(slotIdx);
        return true;
    }

    public bool TryStartUse(ItemDataSO item, int perRoomQuota = -1)
    {
        if (item == null || !CanCharacterUseItems(perRoomQuota)) return false;

        int slotIdx = FindUsableSlotByItem(item);
        if (slotIdx < 0) return false;

        BeginSlotCast(slotIdx);
        return true;
    }

    /// <summary>
    /// Evaluates needed recovery categories in priority order (HealthPotion -> StatusCure -> ManaPotion)
    /// against the supplied trigger thresholds and starts casting the first usable item.
    /// </summary>
    public bool TryStartBestNeededItem(float hpTriggerFrac, float manaTriggerFrac, int perRoomQuota = -1)
    {
        if (!CanCharacterUseItems(perRoomQuota)) return false;

        int slotIdx = FindBestNeededSlotIndex(hpTriggerFrac, manaTriggerFrac);
        if (slotIdx < 0) return false;

        BeginSlotCast(slotIdx);
        return true;
    }

    /// <summary>
    /// Advances the active item cast timer by <paramref name="deltaTime"/> seconds.
    /// Consumes the item and applies its <see cref="SBGames.Inventory.Core.ConsumableEffect"/> ONLY when the cast completes.
    /// </summary>
    public bool AdvanceCast(float deltaTime)
    {
        currentClockTime += Mathf.Max(0f, deltaTime);
        if (!isCastingItem) return false;

        if (statsManager != null && statsManager.Life <= 0f)
        {
            CancelCast("Dead");
            return false;
        }

        castRemainingTime -= Mathf.Max(0f, deltaTime);
        if (castRemainingTime > 0f)
        {
            return false;
        }

        return CompleteCast();
    }

    /// <summary>
    /// Cancels any in-progress item cast without consuming the item.
    /// Called automatically when the character is stunned, knocked back, killed, or interrupted via <see cref="CharacterBrain.InterruptAllActions"/>.
    /// </summary>
    public void CancelCast(string reason = "Interrupted")
    {
        if (!isCastingItem) return;

        ItemDataSO interruptedItem = (castingSlotIndex >= 0 && castingSlotIndex < activeSlotCount)
            ? slots[castingSlotIndex].Item
            : null;

        isCastingItem = false;
        castingSlotIndex = -1;
        castRemainingTime = 0f;

        if (interruptedItem != null)
        {
            ELogger.Log("ItemCastInterrupted", this, reason);
            OnItemCastInterrupted?.Invoke(this, interruptedItem);
        }
    }

    private void BeginSlotCast(int slotIndex)
    {
        isCastingItem = true;
        castingSlotIndex = slotIndex;
        castRemainingTime = castDuration;

        ItemDataSO item = slots[slotIndex].Item;
        ELogger.Log("ItemCastStart", this, item != null ? item.ItemID : string.Empty);
        OnItemCastStarted?.Invoke(this, item);
    }

    private bool CompleteCast()
    {
        int slotIdx = castingSlotIndex;
        isCastingItem = false;
        castingSlotIndex = -1;
        castRemainingTime = 0f;

        if (slotIdx < 0 || slotIdx >= activeSlotCount) return false;

        ref RuntimeKitSlot slot = ref slots[slotIdx];
        if (slot.RemainingCount <= 0 || slot.Item == null) return false;

        CacheComponents();
        if (statsManager != null && statsManager.Life <= 0f) return false;

        var effect = slot.Item.ConsumableEffect;
        bool applied = false;
        if (effect != null)
        {
            applied = effect.Apply(gameObject, statsManager);
        }

        if (!applied)
        {
            return false;
        }

        slot.RemainingCount--;
        totalRemainingCount = Mathf.Max(0, totalRemainingCount - 1);
        float now = Application.isPlaying ? Time.time : currentClockTime;
        slot.CooldownReadyTime = now + itemCooldown;
        itemsUsedInCurrentRoom++;

        ELogger.Log("PotionConsumed", this, slot.Item.ItemID);
        OnPotionConsumed?.Invoke(this, slot.Item);
        return true;
    }

    private bool CanCharacterUseItems(int perRoomQuota)
    {
        CacheComponents();
        if (isCastingItem) return false;
        if (totalRemainingCount <= 0) return false;
        if (statsManager != null && statsManager.Life <= 0f) return false;
        if (brain != null && brain.IsDashing) return false;
        if (perRoomQuota >= 0 && itemsUsedInCurrentRoom >= perRoomQuota) return false;
        return true;
    }

    private int FindUsableSlotByCategory(ConsumableItemCategory category)
    {
        float now = Application.isPlaying ? Time.time : currentClockTime;
        for (int i = 0; i < activeSlotCount; i++)
        {
            ref readonly RuntimeKitSlot slot = ref slots[i];
            if (slot.Category != category || slot.RemainingCount <= 0 || slot.Item == null) continue;
            if (now < slot.CooldownReadyTime) continue;

            var effect = slot.Item.ConsumableEffect;
            if (effect != null && !effect.CanApply(gameObject, statsManager, out _))
            {
                continue;
            }
            return i;
        }
        return -1;
    }

    private int FindUsableSlotByItem(ItemDataSO item)
    {
        float now = Application.isPlaying ? Time.time : currentClockTime;
        for (int i = 0; i < activeSlotCount; i++)
        {
            ref readonly RuntimeKitSlot slot = ref slots[i];
            if (slot.Item != item || slot.RemainingCount <= 0) continue;
            if (now < slot.CooldownReadyTime) continue;

            var effect = slot.Item.ConsumableEffect;
            if (effect != null && !effect.CanApply(gameObject, statsManager, out _))
            {
                continue;
            }
            return i;
        }
        return -1;
    }

    private int FindBestNeededSlotIndex(float hpTriggerFrac, float manaTriggerFrac)
    {
        CacheComponents();

        // 1. Health Potion if HP fraction is below trigger
        if (statsManager != null && statsManager.MaxLife != null && statsManager.MaxLife.Value > 0f)
        {
            float hpFrac = statsManager.Life / statsManager.MaxLife.Value;
            if (hpFrac <= hpTriggerFrac && statsManager.Life < statsManager.MaxLife.Value)
            {
                int hpSlot = FindUsableSlotByCategory(ConsumableItemCategory.HealthPotion);
                if (hpSlot >= 0) return hpSlot;
            }
        }

        // 2. Status Cure if a harmful status effect is active
        if (statusEffectManager != null && statusEffectManager.HasHarmfulEffect())
        {
            int cureSlot = FindUsableSlotByCategory(ConsumableItemCategory.StatusCure);
            if (cureSlot >= 0) return cureSlot;
        }

        // 3. Mana Potion if MaxMana > 0 and Mana fraction is below trigger
        if (statsManager != null && statsManager.MaxMana != null && statsManager.MaxMana.Value > 0f)
        {
            float manaFrac = statsManager.Mana / statsManager.MaxMana.Value;
            if (manaFrac <= manaTriggerFrac && statsManager.Mana < statsManager.MaxMana.Value)
            {
                int manaSlot = FindUsableSlotByCategory(ConsumableItemCategory.ManaPotion);
                if (manaSlot >= 0) return manaSlot;
            }
        }

        return -1;
    }
}
