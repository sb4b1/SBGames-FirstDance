using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Validation
{
    public enum ValidationSeverity
    {
        Error = 0,
        Warning = 1,
        Info = 2
    }

    [Serializable]
    public struct ValidationBadge
    {
        public ValidationSeverity Severity;
        public string Category;
        public string TargetName;
        public string AssetPath;
        public string Message;
        public string SuggestedFix;

        public ValidationBadge(ValidationSeverity severity, string category, string targetName, string assetPath, string message, string suggestedFix)
        {
            Severity = severity;
            Category = category;
            TargetName = targetName;
            AssetPath = assetPath;
            Message = message;
            SuggestedFix = suggestedFix;
        }

        public override string ToString() => $"[{Severity}] ({Category}) {TargetName}: {Message}";
    }

    public class AISetupValidatorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private List<ValidationBadge> _findings = new();
        private bool _filterErrors = true;
        private bool _filterWarnings = true;
        private bool _filterInfo = true;
        private string _searchFilter = "";

        [MenuItem("Tools/SBGames/AI Setup Validator")]
        [MenuItem("SBGames/AI Setup Validator")]
        public static void Open()
        {
            var window = GetWindow<AISetupValidatorWindow>("AI Setup Validator");
            window.minSize = new Vector2(750, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Run Full Audit", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                _findings = AISetupValidatorCore.RunAudit();
            }
            if (GUILayout.Button("Export Markdown Report", EditorStyles.toolbarButton, GUILayout.Width(160)))
            {
                if (_findings.Count == 0) _findings = AISetupValidatorCore.RunAudit();
                string path = AISetupValidatorCore.ExportReportToMarkdown(_findings);
                EditorUtility.DisplayDialog("Report Exported", $"Audit report saved to:\n{path}", "OK");
            }
            GUILayout.FlexibleSpace();
            _filterErrors = GUILayout.Toggle(_filterErrors, $"Errors ({_findings.Count(f => f.Severity == ValidationSeverity.Error)})", EditorStyles.toolbarButton);
            _filterWarnings = GUILayout.Toggle(_filterWarnings, $"Warnings ({_findings.Count(f => f.Severity == ValidationSeverity.Warning)})", EditorStyles.toolbarButton);
            _filterInfo = GUILayout.Toggle(_filterInfo, $"Info ({_findings.Count(f => f.Severity == ValidationSeverity.Info)})", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var filtered = _findings.Where(f =>
            {
                if (f.Severity == ValidationSeverity.Error && !_filterErrors) return false;
                if (f.Severity == ValidationSeverity.Warning && !_filterWarnings) return false;
                if (f.Severity == ValidationSeverity.Info && !_filterInfo) return false;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    string s = _searchFilter.ToLowerInvariant();
                    return (f.TargetName != null && f.TargetName.ToLowerInvariant().Contains(s)) ||
                           (f.Message != null && f.Message.ToLowerInvariant().Contains(s)) ||
                           (f.Category != null && f.Category.ToLowerInvariant().Contains(s));
                }
                return true;
            }).ToList();

            foreach (var finding in filtered)
            {
                Color boxColor = finding.Severity switch
                {
                    ValidationSeverity.Error => new Color(1f, 0.4f, 0.4f, 0.15f),
                    ValidationSeverity.Warning => new Color(1f, 0.85f, 0.3f, 0.15f),
                    _ => new Color(0.4f, 0.7f, 1f, 0.15f)
                };

                string badgePrefix = finding.Severity switch
                {
                    ValidationSeverity.Error => "❌ [ERROR]",
                    ValidationSeverity.Warning => "⚠ [WARNING]",
                    _ => "ℹ [INFO]"
                };

                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = boxColor;
                EditorGUILayout.LabelField($"{badgePrefix} [{finding.Category}] {finding.TargetName}", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField($"Asset: {finding.AssetPath}", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(finding.Message, finding.Severity switch
                {
                    ValidationSeverity.Error => MessageType.Error,
                    ValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                });
                EditorGUILayout.LabelField($"Suggested Fix: {finding.SuggestedFix}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Thin wrapper around <see cref="ValidationService"/> maintaining backwards-compatible API and headless execution.
    /// </summary>
    public static class AISetupValidatorCore
    {
        [MenuItem("SBGames/Run AI Setup Validator (Headless)")]
        public static void RunHeadless()
        {
            var serviceFindings = ValidationService.RunFullValidation();
            string path = ValidationService.ExportReportToMarkdown(serviceFindings);
            Debug.Log($"[AISetupValidator] Headless validation completed. Found {serviceFindings.Count} findings. Report written to {path}");
        }

        public static List<ValidationBadge> RunAudit()
        {
            var serviceFindings = ValidationService.RunFullValidation();
            return serviceFindings.Select(f => f.ToBadge()).OrderBy(f => f.Severity).ToList();
        }

        public static bool IsUtilityCurveNormalized(AnimationCurve curve)
        {
            if (curve == null) return true;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!InUnitRange(keys[i].value)) return false;
                if (i + 1 >= keys.Length) continue;
                var left = keys[i];
                var right = keys[i + 1];
                if (float.IsInfinity(left.outTangent) || float.IsInfinity(right.inTangent)) continue;
                float dt = right.time - left.time;
                float outWeight = (left.weightedMode & WeightedMode.Out) != 0 ? left.outWeight : 1f / 3f;
                float inWeight = (right.weightedMode & WeightedMode.In) != 0 ? right.inWeight : 1f / 3f;
                float p0 = left.value, p1 = p0 + left.outTangent * dt * outWeight;
                float p3 = right.value, p2 = p3 - right.inTangent * dt * inWeight;
                // Cubic Bezier extrema catch tangent overshoot even when all key values are valid.
                float a = -p0 + 3f * p1 - 3f * p2 + p3;
                float b = 2f * (p0 - 2f * p1 + p2);
                float c = p1 - p0;
                if (Mathf.Abs(a) < 0.000001f)
                {
                    if (Mathf.Abs(b) > 0.000001f && !ExtremumInRange(-c / b, p0, p1, p2, p3)) return false;
                }
                else
                {
                    float discriminant = b * b - 4f * a * c;
                    if (discriminant >= 0f)
                    {
                        float sqrt = Mathf.Sqrt(discriminant);
                        if (!ExtremumInRange((-b + sqrt) / (2f * a), p0, p1, p2, p3) ||
                            !ExtremumInRange((-b - sqrt) / (2f * a), p0, p1, p2, p3)) return false;
                    }
                }
            }
            return true;
        }

        private static bool InUnitRange(float value) => !float.IsNaN(value) && value >= 0f && value <= 1f;
        private static bool ExtremumInRange(float t, float p0, float p1, float p2, float p3)
        {
            if (t <= 0f || t >= 1f) return true;
            float u = 1f - t;
            return InUnitRange(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
        }

        public static void AuditUtilityCurves(List<ValidationFinding> findings)
        {
            foreach (string filter in new[] { "t:UtilityGoal", "t:UtilitySkill", "t:RecoverySafetyTuningSO" })
            {
                foreach (string guid in AssetDatabase.FindAssets(filter))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    using var serialized = new SerializedObject(asset);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.AnimationCurve || IsUtilityCurveNormalized(property.animationCurveValue)) continue;
                        findings.Add(new ValidationFinding(ValidationSeverity.Warning, ValidationFindingSource.AISetup,
                            ValidationSubjectType.Global, string.Empty, "Utility Curve Range", asset.name, path,
                            property.propertyPath + " has values outside [0, 1] (including tangent overshoot).",
                            "Normalize curve values and tangents to [0, 1]."));
                    }
                }
            }
        }

        public static string ExportReportToMarkdown(List<ValidationBadge> findings)
        {
            var serviceFindings = findings.Select(b => new ValidationFinding(
                b.Severity,
                ValidationFindingSource.AISetup,
                ValidationSubjectType.Global,
                string.Empty,
                b.Category,
                b.TargetName,
                b.AssetPath,
                b.Message,
                b.SuggestedFix)).ToList();

            return ValidationService.ExportReportToMarkdown(serviceFindings);
        }
    }
}
