namespace SBGames.Input
{
    /// <summary>
    /// Logical button identifier for UI navigation and window toggles.
    /// Maps abstract button inputs across Keyboard and Gamepad devices.
    /// </summary>
    public enum UIButton
    {
        B,          // Keyboard 'B' key & Gamepad 'B' / East button (Inventory)
        Escape,     // Keyboard 'Escape' key & Gamepad 'Start' / Menu button (Pause / Settings)
        M,          // Keyboard 'M' key & Gamepad 'Select' / View button (Map)
        Tab,        // Keyboard 'Tab' key (Scoreboard / Quick Wheel)
        C,          // Keyboard 'C' key (Character Details)
        J,          // Keyboard 'J' key (Journal / Quests)
        P,          // Keyboard 'P' key (Talent Tree)
        H           // Keyboard 'H' key (DM Abilities & Actions Menu)
    }
}
