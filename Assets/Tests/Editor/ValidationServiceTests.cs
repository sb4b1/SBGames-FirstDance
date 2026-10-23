using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SBGames.Editor.Validation;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class ValidationServiceTests
    {
        [Test]
        public void ValidationFinding_SeverityAndSubjectMapping_IsCorrect()
        {
            var finding = new ValidationFinding(
                ValidationSeverity.Error,
                ValidationFindingSource.AISetup,
                ValidationSubjectType.Character,
                "ADVENTURER_TEST",
                "Character Prefab",
                "ADVENTURER_TEST",
                "Assets/Test.prefab",
                "Missing Brain",
                "Add Brain");

            Assert.AreEqual(ValidationSeverity.Error, finding.Severity);
            Assert.AreEqual(ValidationFindingSource.AISetup, finding.Source);
            Assert.AreEqual(ValidationSubjectType.Character, finding.SubjectType);
            Assert.AreEqual("ADVENTURER_TEST", finding.SubjectId);
            Assert.AreEqual("Missing Brain", finding.Message);

            var badge = finding.ToBadge();
            Assert.AreEqual(ValidationSeverity.Error, badge.Severity);
            Assert.AreEqual("Character Prefab", badge.Category);
            Assert.AreEqual("Missing Brain", badge.Message);
        }

        [Test]
        public void ValidationFinding_SkillMapping_AssignsSkillSubjectType()
        {
            var finding = new ValidationFinding(
                ValidationSeverity.Warning,
                ValidationFindingSource.AISetup,
                ValidationSubjectType.Skill,
                "SKILL_FIREBALL",
                "Skill Data",
                "SKILL_FIREBALL",
                "Assets/Skill.asset",
                "Missing Utility Data",
                "Assign Utility Data");

            Assert.AreEqual(ValidationSubjectType.Skill, finding.SubjectType);
            Assert.AreEqual("SKILL_FIREBALL", finding.SubjectId);
            Assert.AreEqual(ValidationFindingSource.AISetup, finding.Source);
        }

        [Test]
        public void ValidationService_HeadlessReportParity_MatchesExpectedFormat()
        {
            var findings = new List<ValidationFinding>
            {
                new ValidationFinding(
                    ValidationSeverity.Warning,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Character,
                    "TEST_HERO",
                    "Character Prefab",
                    "TEST_HERO",
                    "Assets/Hero.prefab",
                    "VisionController has no PerceptionTuningSO assigned.",
                    "Assign 'PerceptionTuning_Default.asset' to VisionController tuning.")
            };

            string path = ValidationService.ExportReportToMarkdown(findings);
            Assert.IsTrue(File.Exists(path));

            string content = File.ReadAllText(path);
            StringAssert.Contains("# AI Setup Validation Report", content);
            StringAssert.Contains("## Summary of Findings by Category", content);
            StringAssert.Contains("| Category | Error | Warning | Info | Total |", content);
            StringAssert.Contains("| Character Prefab | 0 | 1 | 0 | 1 |", content);
            StringAssert.Contains("## Category: Character Prefab", content);
            StringAssert.Contains("### 🟡 **WARNING** TEST_HERO", content);
            StringAssert.Contains("- **Problem:** VisionController has no PerceptionTuningSO assigned.", content);
            StringAssert.Contains("- **Suggested Fix:** Assign 'PerceptionTuning_Default.asset' to VisionController tuning.", content);
        }
    }
}
