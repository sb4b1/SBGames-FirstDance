using NUnit.Framework;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class BasicAttackModeTests
    {
        private GameObject _characterGo;
        private CharacterStatsManager _stats;
        private CharacterBrain _brain;
        private CharacterCombatController _combat;

        [SetUp]
        public void SetUp()
        {
            _characterGo = new GameObject("Test_Character");
            _stats = _characterGo.AddComponent<CharacterStatsManager>();
            _brain = _characterGo.GetComponent<CharacterBrain>();
            _combat = _characterGo.AddComponent<CharacterCombatController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_characterGo != null)
            {
                Object.DestroyImmediate(_characterGo);
            }
        }

        [Test]
        public void BasicAttackMode_DefaultIsAuto()
        {
            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            Assert.AreEqual(BasicAttackMode.Auto, baseStat.basicAttackMode, "Default BasicAttackMode on CharacterBaseStatSO must be Auto.");
            Assert.AreEqual(BasicAttackMode.Auto, baseStat.BasicAttackMode, "BasicAttackMode property must return Auto by default.");
        }

        [Test]
        public void BasicAttackMode_None_BlocksCanAttackAndTryStartAttack()
        {
            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.basicAttackMode = BasicAttackMode.None;
            baseStat.MaxLife = 100f;
            baseStat.MaxMana = 50f;
            baseStat.Damage = 10f;
            _stats.SetBaseStats(baseStat);

            Assert.AreEqual(BasicAttackMode.None, _stats.BasicAttackMode);
            Assert.IsFalse(_brain.CanAttack, "CanAttack must return false when BasicAttackMode is None.");
            Assert.IsFalse(_brain.TryStartAttack(), "TryStartAttack must fail when BasicAttackMode is None.");
            Assert.IsFalse(_combat.Attack(), "CharacterCombatController.Attack must return false when BasicAttackMode is None.");
        }

        [Test]
        public void BasicAttackMode_Auto_AllowsAttackWhenReady()
        {
            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.basicAttackMode = BasicAttackMode.Auto;
            baseStat.MaxLife = 100f;
            baseStat.MaxMana = 50f;
            baseStat.Damage = 10f;
            _stats.SetBaseStats(baseStat);

            Assert.AreEqual(BasicAttackMode.Auto, _stats.BasicAttackMode);
            Assert.IsTrue(_stats.AttackReady, "AttackReady should be true initially.");
            Assert.IsTrue(_brain.CanAttack, "CanAttack must return true when BasicAttackMode is Auto and attack is ready.");
            Assert.IsTrue(_brain.TryStartAttack(), "TryStartAttack must succeed for Auto characters.");
        }

        [Test]
        public void KillEnemyGoal_NoneMode_WithNoDamageSkill_EvaluatesToZero()
        {
            var aiGo = new GameObject("Test_AI");
            aiGo.transform.SetParent(_characterGo.transform);
            var baseAI = aiGo.AddComponent<BaseAI>();

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.basicAttackMode = BasicAttackMode.None;
            _stats.SetBaseStats(baseStat);

            // Set high threat
            baseAI.SenseEnemy();
            Assert.Greater(baseAI.ThreatLevel, 0);

            var killGoal = ScriptableObject.CreateInstance<UtilityKillEnemyGoal>();
            float score = killGoal.Evaluate(baseAI);

            Assert.AreEqual(0f, score, "None mode character without damage skill must evaluate KillEnemyGoal score to 0.");
        }

        [Test]
        public void BaseAI_CannotCastDetection_WhenManaBelowCheapestSkill()
        {
            var aiGo = new GameObject("Test_AI");
            aiGo.transform.SetParent(_characterGo.transform);
            var baseAI = aiGo.AddComponent<BaseAI>();

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.basicAttackMode = BasicAttackMode.None;
            baseStat.MaxMana = 50f;
            _stats.SetBaseStats(baseStat);

            _stats.TryConsumeMana(50f);
            baseAI.UpdateResources();

            Assert.IsTrue(baseAI.CannotCast, "CannotCast must be true when agent has 0 mana and skills cost mana.");
        }
    }
}
