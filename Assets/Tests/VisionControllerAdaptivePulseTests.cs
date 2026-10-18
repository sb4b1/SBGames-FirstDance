using NUnit.Framework;
using SB4b1.GameMath;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class VisionControllerAdaptivePulseTests
    {
        private GameObject _unitGo;
        private VisionController _vision;
        private BaseAI _baseAI;
        private PerceptionTuningSO _tuning;

        [SetUp]
        public void SetUp()
        {
            _unitGo = new GameObject("TestUnit");
            _vision = _unitGo.AddComponent<VisionController>();
            _baseAI = _unitGo.AddComponent<BaseAI>();

            _tuning = ScriptableObject.CreateInstance<PerceptionTuningSO>();
            _vision.Tuning = _tuning;
        }

        [TearDown]
        public void TearDown()
        {
            if (_tuning != null)
            {
                Object.DestroyImmediate(_tuning);
            }
            if (_unitGo != null)
            {
                Object.DestroyImmediate(_unitGo);
            }
        }

        [Test]
        public void FallbackWhenNoSOAssigned_UsesDefaultSearchInterval()
        {
            _vision.Tuning = null;

            Assert.AreEqual(VisionController.DEFAULT_SEARCH_INTERVAL, _vision.GetTierInterval(PerceptionTier.Default), 0.0001f);
            Assert.AreEqual(VisionController.DEFAULT_SEARCH_INTERVAL, _vision.GetTierInterval(PerceptionTier.Calm), 0.0001f);
            Assert.AreEqual(VisionController.DEFAULT_SEARCH_INTERVAL, _vision.GetTierInterval(PerceptionTier.Engaged), 0.0001f);
            Assert.AreEqual(VisionController.DEFAULT_SEARCH_INTERVAL, _vision.GetTierInterval(PerceptionTier.Critical), 0.0001f);
        }

        [Test]
        public void TierSelection_CalmWhenNoEnemiesAndCalmGoal()
        {
            // BaseAI default state: ThreatLevel=0, FearLevel=0, HasKnownEnemies=false, ActiveGoal=None
            PerceptionTier tier = _vision.EvaluateTier();
            Assert.AreEqual(PerceptionTier.Calm, tier);
            Assert.AreEqual(_tuning.CalmInterval, _vision.GetTierInterval(tier), 0.0001f);
        }

        [Test]
        public void TierSelection_EngagedWhenTargetOrThreatPresent()
        {
            // Set ThreatLevel > 0
            var enemyGo = new GameObject("Enemy");
            try
            {
                _baseAI.SetTarget(enemyGo.transform, 2.0f);
                PerceptionTier tier = _vision.EvaluateTier();
                Assert.AreEqual(PerceptionTier.Engaged, tier);
                Assert.AreEqual(_tuning.EngagedInterval, _vision.GetTierInterval(tier), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(enemyGo);
            }
        }

        [Test]
        public void TierSelection_CriticalOnLowHealthOrHighFearOrFallBack()
        {
            // Critical health <= 0.3
            _baseAI.SetAgentLife(20f); // Default MaxLife is 100, so 20/100 = 0.20 <= 0.30
            PerceptionTier tierHp = _vision.EvaluateTier();
            Assert.AreEqual(PerceptionTier.Critical, tierHp);

            // Restore HP, set FearLevel >= 80 (Morale Shock / Panic)
            _baseAI.SetAgentLife(100f);
            _baseAI.SetFearLevel(85);
            PerceptionTier tierFear = _vision.EvaluateTier();
            Assert.AreEqual(PerceptionTier.Critical, tierFear);
            Assert.AreEqual(_tuning.CriticalInterval, _vision.GetTierInterval(tierFear), 0.0001f);
        }

        [Test]
        public void Hysteresis_ImmediateOnEscalation_RequiresDwellOnDeescalation()
        {
            // Initialize unit into Calm tier
            _vision.InitializePhaseOffset();
            // Fast-forward initial drop to Calm
            _vision.UpdateTierSelection(_tuning.HysteresisDwellTime);
            Assert.AreEqual(PerceptionTier.Calm, _vision.CurrentTier);

            // Escalate to Critical immediately
            _baseAI.SetAgentLife(10f);
            _vision.UpdateTierSelection(0.01f);
            Assert.AreEqual(PerceptionTier.Critical, _vision.CurrentTier, "Transition to faster/higher priority tier must be immediate.");

            // Remove critical state; evaluates back to Calm
            _baseAI.SetAgentLife(100f);
            _baseAI.SetFearLevel(0);

            // First tick: dt = 0.2s < HysteresisDwellTime (0.5s), should NOT drop yet
            _vision.UpdateTierSelection(0.2f);
            Assert.AreEqual(PerceptionTier.Critical, _vision.CurrentTier, "Must not drop to slower tier before dwell time expires.");
            Assert.AreEqual(0.2f, _vision.TierDwellTimer, 0.001f);

            // Second tick: dt = 0.35s (cumulative 0.55s >= 0.5s), now it drops to Calm
            _vision.UpdateTierSelection(0.35f);
            Assert.AreEqual(PerceptionTier.Calm, _vision.CurrentTier, "Must transition to slower tier once dwell time is satisfied.");
            Assert.AreEqual(0f, _vision.TierDwellTimer, 0.001f);
        }


        [Test]
        public void PhaseOffset_SpreadAcrossMultipleUnits_UsesRngContract()
        {
            const int unitCount = 10;
            var offsets = new float[unitCount];
            var rng = new GameRng(42u);

            for (int i = 0; i < unitCount; i++)
            {
                _vision.InitializePhaseOffset(ref rng);
                offsets[i] = _vision.SearchTimer;
                Assert.GreaterOrEqual(offsets[i], 0f);
                Assert.Less(offsets[i], _vision.GetTierInterval(_vision.CurrentTier));
            }

            // Verify they are not all identical (spread across [0, interval))
            bool allSame = true;
            for (int i = 1; i < unitCount; i++)
            {
                if (Mathf.Abs(offsets[i] - offsets[0]) > 0.001f)
                {
                    allSame = false;
                    break;
                }
            }
            Assert.IsFalse(allSame, "Pulse phase offsets must vary across units to desynchronize perception spikes.");
        }

        [Test]
        public void ForcePulse_TriggersPulseAndIsRateLimited()
        {
            // Initial force pulse should succeed
            _vision.RequestImmediatePulse();
            Assert.IsTrue(_vision.ForcePulseRequested, "Force pulse should set request flag.");

            // Simulate tick processing the force pulse
            // LastForcePulseTime is updated to current time
            _vision.GetType().GetField("lastForcePulseTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_vision, Time.time);
            _vision.GetType().GetField("forcePulseRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_vision, false);

            // Immediate second pulse within minGap (0.05s) must be rejected
            _vision.RequestImmediatePulse();
            Assert.IsFalse(_vision.ForcePulseRequested, "Burst force pulses within minGap must be rate limited.");
        }
    }
}
