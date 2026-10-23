using System;
using System.Collections.Generic;
using SBGames.Editor.Validation;

namespace SBGames.Editor.Stats.Tabs
{
    /// <summary>
    /// Contract for domain-specific tabs within the Database window.
    /// Manages loading, in-memory buffering, bulk transformation, and saving of catalog statistics.
    /// </summary>
    public interface IStatTab
    {
        string Title { get; }
        bool HasUnsavedChanges { get; }
        int ValidationIssueCount { get; }
        int ErrorCount { get; }
        int WarningCount { get; }

        void Initialize(StatBalanceManagerWindow hostWindow);
        void ReloadData();
        void SaveData();
        void DiscardChanges();
        void DrawGUI();
        void ApplyExternalFindings(List<ValidationFinding> findings);
        void SetFilterIssuesOnly(bool filterIssues);
    }
}
