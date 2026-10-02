namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Result status code for inventory operations.
    /// </summary>
    public enum InventoryOperationStatus
    {
        Success = 0,
        InventoryFull = 1,
        ItemNotFound = 2,
        InsufficientQuantity = 3,
        InvalidSlot = 4,
        LockedInCombat = 5,
        InvalidOperation = 6,
        SocketFull = 7,
        IncompatibleItem = 8
    }

    /// <summary>
    /// Structured result containing success status, detailed enum code, and user-facing error message.
    /// </summary>
    public readonly struct InventoryOperationResult
    {
        public bool IsSuccess { get; }
        public InventoryOperationStatus Status { get; }
        public string Message { get; }
        public int AffectedQuantity { get; }
        public int QuantityAffected => AffectedQuantity;

        public InventoryOperationResult(bool isSuccess, InventoryOperationStatus status, string message = "", int affectedQuantity = 0)
        {
            IsSuccess = isSuccess;
            Status = status;
            Message = message ?? string.Empty;
            AffectedQuantity = affectedQuantity;
        }

        public static InventoryOperationResult Success(string message = "", int affectedQuantity = 0)
            => new(true, InventoryOperationStatus.Success, message, affectedQuantity);

        public static InventoryOperationResult Failure(InventoryOperationStatus status, string message)
            => new(false, status, message, 0);
    }
}
