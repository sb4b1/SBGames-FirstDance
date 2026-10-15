namespace SBGames.Placement
{
    /// <summary>
    /// Status codes indicating the outcome of a placement validation or attempt.
    /// </summary>
    public enum PlacementStatus
    {
        /// <summary>Placement is valid or successfully completed.</summary>
        Success = 0,

        /// <summary>The placement request was null or contains an invalid unit definition.</summary>
        InvalidRequest = 1,

        /// <summary>Target coordinates are outside the dungeon grid boundaries.</summary>
        OutOfBounds = 2,

        /// <summary>Target cell already contains another placed unit or obstacle.</summary>
        CellOccupied = 3,

        /// <summary>Player does not have enough resources or energy to pay the placement cost.</summary>
        InsufficientResources = 4,
        InsufficientEnergy = 4,

        /// <summary>Dungeon does not have enough remaining capacity for the scaled unit.</summary>
        ExceedsCapacityLimit = 5,

        /// <summary>Placement is disallowed during active combat or non-build phases.</summary>
        PhaseLocked = 6,

        /// <summary>Target cell is in a non-buildable zone (e.g. Entrance room or void).</summary>
        NonBuildableCell = 7,

        /// <summary>Target room has reached its maximum monster capacity limit.</summary>
        ExceedsRoomCapacity = 8
    }

    /// <summary>
    /// Pure, side-effect-free result of a placement validation check.
    /// Contains comprehensive diagnostics on cost, capacity, and failure reasons.
    /// </summary>
    public readonly struct PlacementValidationResult
    {
        public readonly bool IsValid;
        public readonly PlacementStatus Status;
        public readonly string Message;
        public readonly int CostRequired;
        public readonly int CapacityRequired;
        public readonly int AvailableResources;
        public readonly int AvailableCapacity;

        public int EnergyRequired => CostRequired;
        public int AvailableEnergy => AvailableResources;

        public PlacementValidationResult(
            bool isValid,
            PlacementStatus status,
            string message,
            int costRequired,
            int capacityRequired,
            int availableResources,
            int availableCapacity)
        {
            IsValid = isValid;
            Status = status;
            Message = message;
            CostRequired = costRequired;
            CapacityRequired = capacityRequired;
            AvailableResources = availableResources;
            AvailableCapacity = availableCapacity;
        }

        public static PlacementValidationResult SuccessResult(
            int costRequired,
            int capacityRequired,
            int availableResources,
            int availableCapacity)
        {
            return new PlacementValidationResult(
                isValid: true,
                status: PlacementStatus.Success,
                message: "Placement constraints satisfied.",
                costRequired: costRequired,
                capacityRequired: capacityRequired,
                availableResources: availableResources,
                availableCapacity: availableCapacity
            );
        }

        public static PlacementValidationResult FailureResult(
            PlacementStatus status,
            string message,
            int costRequired = 0,
            int capacityRequired = 0,
            int availableResources = 0,
            int availableCapacity = 0)
        {
            return new PlacementValidationResult(
                isValid: false,
                status: status,
                message: message,
                costRequired: costRequired,
                capacityRequired: capacityRequired,
                availableResources: availableResources,
                availableCapacity: availableCapacity
            );
        }

        public override string ToString()
        {
            if (IsValid)
            {
                return $"[Placement Valid] Cost: {CostRequired}/{AvailableResources}, Capacity: {CapacityRequired}/{AvailableCapacity}";
            }
            return $"[Placement Failed: {Status}] {Message} (Cost: {CostRequired}/{AvailableResources}, Capacity: {CapacityRequired}/{AvailableCapacity})";
        }
    }
}
