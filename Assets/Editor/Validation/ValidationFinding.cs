using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Editor.Validation
{
    public enum ValidationFindingSource
    {
        Data,
        AISetup
    }

    public enum ValidationSubjectType
    {
        Global,
        Character,
        Skill,
        Item
    }

    [Serializable]
    public class ValidationFinding
    {
        public ValidationSeverity Severity;
        public ValidationFindingSource Source;
        public ValidationSubjectType SubjectType;
        public string SubjectId; // Character ID, Skill ID, Item ID, or null/empty for Global
        public string Category;
        public string TargetName;
        public string AssetPath;
        public string Message;
        public string SuggestedFix;
        public Action FixAction;

        public ValidationFinding() { }

        public ValidationFinding(
            ValidationSeverity severity,
            ValidationFindingSource source,
            ValidationSubjectType subjectType,
            string subjectId,
            string category,
            string targetName,
            string assetPath,
            string message,
            string suggestedFix,
            Action fixAction = null)
        {
            Severity = severity;
            Source = source;
            SubjectType = subjectType;
            SubjectId = subjectId ?? string.Empty;
            Category = category ?? string.Empty;
            TargetName = targetName ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Message = message ?? string.Empty;
            SuggestedFix = suggestedFix ?? string.Empty;
            FixAction = fixAction;
        }

        public ValidationBadge ToBadge()
        {
            return new ValidationBadge(Severity, Category, TargetName, AssetPath, Message, SuggestedFix);
        }

        public override string ToString() => $"[{Severity}] ({Source} | {SubjectType}: {SubjectId}) {TargetName}: {Message}";
    }
}
