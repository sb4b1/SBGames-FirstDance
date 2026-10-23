using NUnit.Framework;

namespace SBGames.Tests
{
    // Legacy fixtures outside Editor compile into Assembly-CSharp and are not discovered by EditMode.
    public class LegacyRecoveryRegressionTests
    {
        [Test]
        public void UpdatedWarpCommitmentIsNormalized()
        {
            var fixture = new AdventurerAIProfileTests();
            fixture.SetUp();
            try { fixture.Phase8_UtilityWarpGoal_EvaluatesHysteresisDuringCast(); }
            finally { fixture.TearDown(); }
        }

        [Test]
        public void UpdatedPotionCompetitionAndRankPositioning()
        {
            var fixture = new AdventurerAIProfileTests();
            fixture.SetUp();
            try { fixture.Phase5_RecoverAndSurviveCompetition_Hysteresis_And_RankQualityPositioning(); }
            finally { fixture.TearDown(); }
        }
    }
}
