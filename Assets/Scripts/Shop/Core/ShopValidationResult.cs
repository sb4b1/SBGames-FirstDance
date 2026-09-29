using System;

namespace SBGames.Shop.Core
{
    /// <summary>
    /// Status code indicating the outcome of a monster shop operation (purchase, refresh, lock, etc.).
    /// </summary>
    public enum ShopStatus
    {
        Success = 0,
        InvalidSlot = 1,
        SlotEmpty = 2,
        SlotAlreadyPurchased = 3,
        InsufficientCurrency = 4,
        PhaseLocked = 5,
        InvalidRequest = 6,
        CatalogEmptyForRank = 7,
        WalletUnavailable = 8
    }

    /// <summary>
    /// Readonly validation structure returned by shop validation queries.
    /// Follows the zero-mutation result pattern established by DungeonPlacementSystem.
    /// </summary>
    public readonly struct ShopValidationResult
    {
        public bool IsValid => Status == ShopStatus.Success;
        public ShopStatus Status { get; }
        public string Message { get; }
        public int SlotIndex { get; }
        public int CostRequired { get; }
        public int AvailableCurrency { get; }

        public ShopValidationResult(
            ShopStatus status,
            string message,
            int slotIndex = -1,
            int costRequired = 0,
            int availableCurrency = 0)
        {
            Status = status;
            Message = message ?? string.Empty;
            SlotIndex = slotIndex;
            CostRequired = costRequired;
            AvailableCurrency = availableCurrency;
        }

        public static ShopValidationResult SuccessResult(int slotIndex = -1, int costRequired = 0, int availableCurrency = 0)
        {
            return new ShopValidationResult(
                ShopStatus.Success,
                "Operation validated successfully.",
                slotIndex,
                costRequired,
                availableCurrency
            );
        }

        public static ShopValidationResult FailureResult(
            ShopStatus status,
            string message,
            int slotIndex = -1,
            int costRequired = 0,
            int availableCurrency = 0)
        {
            if (status == ShopStatus.Success)
            {
                throw new ArgumentException("Cannot create a failure result with ShopStatus.Success.", nameof(status));
            }

            return new ShopValidationResult(
                status,
                message,
                slotIndex,
                costRequired,
                availableCurrency
            );
        }

        public override string ToString()
        {
            return IsValid
                ? $"[ShopValidation: Success] Slot: {SlotIndex}, Cost: {CostRequired}, Available: {AvailableCurrency}"
                : $"[ShopValidation: Failed ({Status})] {Message} (Slot: {SlotIndex}, Cost: {CostRequired}, Available: {AvailableCurrency})";
        }
    }
}
