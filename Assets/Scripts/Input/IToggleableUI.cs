namespace SBGames.Input
{
    /// <summary>
    /// Contract for any UI system (Inventory, Settings, Map, Pause Menu, etc.)
    /// that can be toggled open and closed via input triggers.
    /// Decouples UI implementation from input detection logic.
    /// </summary>
    public interface IToggleableUI
    {
        /// <summary>
        /// Indicates whether the UI system is currently visible and active.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Opens and focuses the UI system.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes and defocusses the UI system.
        /// </summary>
        void Close();

        /// <summary>
        /// Toggles between Open and Closed states.
        /// </summary>
        void Toggle();
    }
}
