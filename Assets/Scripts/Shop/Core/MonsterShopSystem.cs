using System;
using System.Collections.Generic;
using System.Text;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Shop.Data;
using UnityEngine;

namespace SBGames.Shop.Core
{
    /// <summary>
    /// Core production-grade monster shop engine for the auto-battler.
    /// Manages round-based inventory generation, rank/level probability weighting, slot locking,
    /// atomic purchasing with currency validation, and phase restriction checks.
    /// Designed with zero side-effect pre-validation and deterministic execution.
    /// </summary>
    public class MonsterShopSystem
    {
        private readonly List<ShopSlot> _slots;
        private readonly IRandomProvider _rng;

        public IMonsterCatalog Catalog { get; }
        public IResourceWallet Wallet { get; }
        public ShopConfig Config { get; set; }
        public IDungeonPhaseManager PhaseManager { get; set; }

        public IReadOnlyList<ShopSlot> Slots => _slots;
        public int CurrentRound { get; private set; } = 1;
        public DungeonRank CurrentPlayerRank { get; private set; } = DungeonRank.E;

        // Events
        public event Action<IReadOnlyList<ShopSlot>, bool> OnShopRefreshed; // (slots, isManualReroll)
        public event Action<int, MonsterShopItem> OnItemPurchased; // (slotIndex, item)
        public event Action<int, bool> OnSlotLockToggled; // (slotIndex, isLocked)
        public event Action<ShopValidationResult> OnTransactionRejected; // (validationFailure)

        public MonsterShopSystem(
            IMonsterCatalog catalog,
            IResourceWallet wallet,
            ShopConfig config = null,
            IRandomProvider rng = null,
            IDungeonPhaseManager phaseManager = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Config = config ?? new ShopConfig();
            _rng = rng ?? new SystemRandomProvider();
            PhaseManager = phaseManager;

            _slots = new List<ShopSlot>(Config.SlotCount);
            for (int i = 0; i < Config.SlotCount; i++)
            {
                _slots.Add(new ShopSlot(i));
            }
        }

        #region Validation Layer

        /// <summary>
        /// Pure validation of a refresh request without mutating shop state or deducting currency.
        /// </summary>
        /// <param name="isManualReroll">True if this is a player-requested reroll costing currency.</param>
        /// <returns>Validation result indicating whether the refresh can proceed.</returns>
        public ShopValidationResult ValidateRefresh(bool isManualReroll)
        {
            int currentFunds = Wallet != null ? Wallet.CurrentResources : 0;

            // 1. Phase restrictions (Build / Planning Phase vs Combat Phase)
            if (PhaseManager != null && !PhaseManager.CanPlaceOrModifyUnits)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.PhaseLocked,
                    $"Shop refresh locked during {PhaseManager.CurrentPhase}. Only available during Planning/Build phase.",
                    costRequired: isManualReroll ? Config.ManualRerollCost : 0,
                    availableCurrency: currentFunds
                );
            }

            // 2. Currency check for manual rerolls
            int cost = isManualReroll ? Config.ManualRerollCost : 0;
            if (isManualReroll && cost > 0 && !Wallet.CanAfford(cost))
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.InsufficientCurrency,
                    $"Insufficient currency for manual reroll. Required: {cost}, Available: {currentFunds}.",
                    costRequired: cost,
                    availableCurrency: currentFunds
                );
            }

            // 3. Catalog integrity check
            if (Catalog.TotalArchetypeCount == 0)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.InvalidRequest,
                    "Monster catalog is empty. Cannot generate shop inventory.",
                    costRequired: cost,
                    availableCurrency: currentFunds
                );
            }

            return ShopValidationResult.SuccessResult(
                costRequired: cost,
                availableCurrency: currentFunds
            );
        }

        /// <summary>
        /// Pure validation of a slot purchase attempt without mutating state or deducting currency.
        /// </summary>
        /// <param name="slotIndex">0-indexed shop slot.</param>
        /// <returns>Validation outcome detailing success or precise failure reason.</returns>
        public ShopValidationResult ValidatePurchase(int slotIndex)
        {
            int currentFunds = Wallet != null ? Wallet.CurrentResources : 0;

            // 1. Slot bounds
            if (slotIndex < 0 || slotIndex >= _slots.Count)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.InvalidSlot,
                    $"Slot index {slotIndex} is out of shop range (0 to {_slots.Count - 1}).",
                    slotIndex: slotIndex,
                    costRequired: 0,
                    availableCurrency: currentFunds
                );
            }

            // 2. Phase restrictions
            if (PhaseManager != null && !PhaseManager.CanPlaceOrModifyUnits)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.PhaseLocked,
                    $"Shop purchasing locked during {PhaseManager.CurrentPhase}. Only available during Planning/Build phase.",
                    slotIndex: slotIndex,
                    costRequired: 0,
                    availableCurrency: currentFunds
                );
            }

            var slot = _slots[slotIndex];

            // 3. Slot occupancy & purchase status
            if (slot.IsPurchased)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.SlotAlreadyPurchased,
                    $"Slot {slotIndex} has already been purchased this round.",
                    slotIndex: slotIndex,
                    costRequired: 0,
                    availableCurrency: currentFunds
                );
            }

            if (slot.CurrentItem == null)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.SlotEmpty,
                    $"Slot {slotIndex} is empty.",
                    slotIndex: slotIndex,
                    costRequired: 0,
                    availableCurrency: currentFunds
                );
            }

            int requiredCost = slot.CurrentItem.Cost;

            // 4. Currency check
            if (!Wallet.CanAfford(requiredCost))
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.InsufficientCurrency,
                    $"Insufficient currency to purchase {slot.CurrentItem.DisplayName}. Required: {requiredCost}, Available: {currentFunds}.",
                    slotIndex: slotIndex,
                    costRequired: requiredCost,
                    availableCurrency: currentFunds
                );
            }

            return ShopValidationResult.SuccessResult(
                slotIndex: slotIndex,
                costRequired: requiredCost,
                availableCurrency: currentFunds
            );
        }

        /// <summary>
        /// Pure validation for toggling the lock flag on a shop slot.
        /// </summary>
        public ShopValidationResult ValidateToggleLock(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.InvalidSlot,
                    $"Slot index {slotIndex} is out of shop range."
                );
            }

            var slot = _slots[slotIndex];
            if (slot.IsEmpty)
            {
                return ShopValidationResult.FailureResult(
                    ShopStatus.SlotEmpty,
                    $"Cannot lock/unlock empty or already purchased slot {slotIndex}."
                );
            }

            return ShopValidationResult.SuccessResult(slotIndex: slotIndex);
        }

        #endregion

        #region Execution Layer

        /// <summary>
        /// Executes a shop refresh with new randomized monster offers.
        /// Preserves locked slots containing unpurchased items while re-rolling all other slots.
        /// </summary>
        /// <param name="roundNumber">Current round number for scaling odds.</param>
        /// <param name="playerRank">Current progression rank tier.</param>
        /// <param name="isManualReroll">True if this is a manual reroll costing currency.</param>
        /// <param name="validationResult">Validation outcome.</param>
        /// <returns>True if the shop successfully refreshed; false if validation or payment failed.</returns>
        public bool TryRefresh(
            int roundNumber,
            DungeonRank playerRank,
            bool isManualReroll,
            out ShopValidationResult validationResult)
        {
            CurrentRound = Mathf.Max(1, roundNumber);
            CurrentPlayerRank = playerRank;

            validationResult = ValidateRefresh(isManualReroll);
            if (!validationResult.IsValid)
            {
                OnTransactionRejected?.Invoke(validationResult);
                return false;
            }

            // Deduct reroll cost if manual reroll
            if (isManualReroll && Config.ManualRerollCost > 0)
            {
                bool deducted = Wallet.TryDeduct(Config.ManualRerollCost);
                if (!deducted)
                {
                    validationResult = ShopValidationResult.FailureResult(
                        ShopStatus.InsufficientCurrency,
                        "Failed to deduct manual reroll cost from wallet.",
                        costRequired: Config.ManualRerollCost,
                        availableCurrency: Wallet.CurrentResources
                    );
                    OnTransactionRejected?.Invoke(validationResult);
                    return false;
                }
            }

            // Generate items for unlocked slots
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                // Preserve locked slots with valid offers
                if (slot.IsLocked && slot.HasAvailableItem)
                {
                    continue;
                }

                MonsterShopItem rolledItem = GenerateRandomShopItem(CurrentRound, CurrentPlayerRank);
                slot.Populate(rolledItem);
            }

            OnShopRefreshed?.Invoke(_slots, isManualReroll);
            return true;
        }

        /// <summary>
        /// Attempts to purchase the monster in the specified slot.
        /// Atomically deducts currency and marks the slot as purchased upon success.
        /// </summary>
        /// <param name="slotIndex">0-indexed shop slot.</param>
        /// <param name="purchasedItem">The purchased monster item instance, or null on failure.</param>
        /// <param name="validationResult">Validation outcome detailing success or failure reason.</param>
        /// <returns>True if the purchase succeeded, false otherwise.</returns>
        public bool TryPurchase(
            int slotIndex,
            out MonsterShopItem purchasedItem,
            out ShopValidationResult validationResult)
        {
            validationResult = ValidatePurchase(slotIndex);
            if (!validationResult.IsValid)
            {
                purchasedItem = null;
                OnTransactionRejected?.Invoke(validationResult);
                return false;
            }

            var slot = _slots[slotIndex];
            int cost = slot.CurrentItem.Cost;

            // Atomically apply currency deduction
            bool deducted = Wallet.TryDeduct(cost);
            if (!deducted)
            {
                purchasedItem = null;
                validationResult = ShopValidationResult.FailureResult(
                    ShopStatus.InsufficientCurrency,
                    "Failed to deduct currency during purchase execution.",
                    slotIndex: slotIndex,
                    costRequired: cost,
                    availableCurrency: Wallet.CurrentResources
                );
                OnTransactionRejected?.Invoke(validationResult);
                return false;
            }

            purchasedItem = slot.CurrentItem;
            slot.MarkPurchased();

            OnItemPurchased?.Invoke(slotIndex, purchasedItem);
            return true;
        }

        /// <summary>
        /// Toggles the lock state of a slot so it stays frozen across refreshes.
        /// </summary>
        public bool TryToggleLock(int slotIndex, out bool isLocked, out ShopValidationResult validationResult)
        {
            validationResult = ValidateToggleLock(slotIndex);
            if (!validationResult.IsValid)
            {
                isLocked = false;
                OnTransactionRejected?.Invoke(validationResult);
                return false;
            }

            var slot = _slots[slotIndex];
            slot.SetLocked(!slot.IsLocked);
            isLocked = slot.IsLocked;

            OnSlotLockToggled?.Invoke(slotIndex, isLocked);
            return true;
        }

        #endregion

        #region Generation Logic

        /// <summary>
        /// Generates a single randomized monster offer based on configured rank & level probability curves.
        /// </summary>
        public MonsterShopItem GenerateRandomShopItem(int roundNumber, DungeonRank playerRank)
        {
            DungeonRank chosenRank = RollRank(roundNumber, playerRank);
            var templates = Catalog.GetTemplatesForRank(chosenRank);

            // Fallback if catalog has no templates for rolled rank
            if (templates == null || templates.Count == 0)
            {
                // Fallback to lowest rank available
                chosenRank = DungeonRank.E;
                templates = Catalog.GetTemplatesForRank(chosenRank);

                if (templates == null || templates.Count == 0)
                {
                    throw new InvalidOperationException($"Catalog contains no templates for rank {chosenRank} or fallback rank E.");
                }
            }

            int templateIndex = _rng.Next(templates.Count);
            MonsterTemplate selectedTemplate = templates[templateIndex];

            int rolledLevel = RollLevel(roundNumber, playerRank);

            return MonsterShopItem.CreateFromTemplate(
                selectedTemplate,
                rolledLevel,
                Config.LevelCostMultiplier
            );
        }

        /// <summary>
        /// Selects a rank (E through S) via weighted random lottery.
        /// </summary>
        public DungeonRank RollRank(int roundNumber, DungeonRank playerRank)
        {
            var probabilities = Config.GetRankProbabilities(roundNumber, playerRank);
            float totalWeight = 0f;
            foreach (var kvp in probabilities)
            {
                if (kvp.Value > 0f && Catalog.HasTemplatesForRank(kvp.Key))
                {
                    totalWeight += kvp.Value;
                }
            }

            if (totalWeight <= 0f)
            {
                return DungeonRank.E;
            }

            float roll = _rng.NextFloat() * totalWeight;
            float cumulative = 0f;

            foreach (var kvp in probabilities)
            {
                if (kvp.Value <= 0f || !Catalog.HasTemplatesForRank(kvp.Key))
                    continue;

                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    return kvp.Key;
                }
            }

            return DungeonRank.E;
        }

        /// <summary>
        /// Selects a spawn level via weighted random lottery.
        /// </summary>
        public int RollLevel(int roundNumber, DungeonRank playerRank)
        {
            var probabilities = Config.GetLevelProbabilities(roundNumber, playerRank);
            float totalWeight = 0f;
            foreach (var kvp in probabilities)
            {
                if (kvp.Value > 0f)
                {
                    totalWeight += kvp.Value;
                }
            }

            if (totalWeight <= 0f)
            {
                return 1;
            }

            float roll = _rng.NextFloat() * totalWeight;
            float cumulative = 0f;

            foreach (var kvp in probabilities)
            {
                if (kvp.Value <= 0f)
                    continue;

                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    return kvp.Key;
                }
            }

            return 1;
        }

        #endregion

        #region Query Helpers

        /// <summary>
        /// Returns the total count of unpurchased monster offers available in the shop.
        /// </summary>
        public int GetAvailableOfferCount()
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].HasAvailableItem) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the count of currently locked slots.
        /// </summary>
        public int GetLockedSlotCount()
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsLocked) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns a formatted multi-line string inspecting the shop inventory and configuration.
        /// </summary>
        public string GetShopInspectionSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== MONSTER SHOP (Round {CurrentRound} | Tier: {CurrentPlayerRank} | Funds: {Wallet?.CurrentResources ?? 0}) ===");
            for (int i = 0; i < _slots.Count; i++)
            {
                sb.AppendLine($"  {_slots[i]}");
            }
            return sb.ToString();
        }

        #endregion
    }
}
