using System;

namespace SBGames.Editor.Stats.Tabs
{
    /// <summary>
    /// Contract for domain-specific tabs within the Stat Balance Manager window.
    /// Manages loading, in-memory buffering, bulk transformation, and saving of catalog statistics.
    /// </summary>
    public interface IStatTab
    {
        string Title { get; }
        bool HasUnsavedChanges { get; }
        int ValidationIssueCount { get; }

        void Initialize(StatBalanceManagerWindow hostWindow);
        void ReloadData();
        void SaveData();
        void DiscardChanges();
        void DrawGUI();
    }
}
