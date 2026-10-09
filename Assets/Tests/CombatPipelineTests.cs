using NUnit.Framework;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CombatPipelineTests
    {
        private GameObject _holderGo;
        private CharacterBrain _brain;
        private CharacterStatsManager _statsManager;

        [SetUp]
        public void SetUp()
        {
            _holderGo = new GameObject("Test_CombatEntity");
            _brain = _holderGo.AddComponent<CharacterBrain>();
            _statsManager = _holderGo.AddComponent<CharacterStatsManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_holderGo != null)
            {
                Object.DestroyImmediate(_holderGo);
            }
        }

        [Test]
        public void CharacterStatsManager_GetStat_MapsStatTypesCorrectly()
        {
            Assert.AreEqual(_statsManager.MaxLife, _statsManager.GetStat(StatType.MaxHealth));
            Assert.AreEqual(_statsManager.MaxLife, _statsManager.GetStat(StatType.Health));
            Assert.AreEqual(_statsManager.Damage, _statsManager.GetStat(StatType.Attack));
            Assert.AreEqual(_statsManager.Movespeed, _statsManager.GetStat(StatType.MoveSpeed));
            Assert.AreEqual(_statsManager.AttackTimeInterval, _statsManager.GetStat(StatType.AttackSpeed));
        }

        [Test]
        public void CharacterStatsManager_TakeDamage_AllowsZeroDamage()
        {
            float initialLife = _statsManager.Life;
            var zeroDamage = new DamageData(0f, DamageType.Physical);

            bool killed = _statsManager.TakeDamage(in zeroDamage);

            Assert.IsFalse(killed);
            Assert.AreEqual(initialLife, _statsManager.Life, "Zero damage should not deduct HP.");
        }

        [Test]
        public void StatModifier_IComparable_SortsByOrder()
        {
            var mod1 = new StatModifier(10f, StatModType.Flat, 100);
            var mod2 = new StatModifier(5f, StatModType.PercentAdd, 200);

            Assert.Less(mod1.CompareTo(mod2), 0);
            Assert.Greater(mod2.CompareTo(mod1), 0);
            Assert.AreEqual(0, mod1.CompareTo(new StatModifier(20f, StatModType.Flat, 100)));
        }

        [Test]
        public void Stat_RemoveAllModifiersFromSource_RemovesCleanly()
        {
            var stat = new Stat(100f);
            object sourceA = new object();
            object sourceB = new object();

            stat.AddModifier(new StatModifier(10f, StatModType.Flat, sourceA));
            stat.AddModifier(new StatModifier(20f, StatModType.Flat, sourceB));
            stat.AddModifier(new StatModifier(30f, StatModType.Flat, sourceA));

            Assert.AreEqual(160f, stat.Value);

            bool removed = stat.RemoveAllModifiersFromSource(sourceA);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, stat.Modifiers.Count);
            Assert.AreEqual(120f, stat.Value);
        }

        [Test]
        public void StatusEffectManager_BindsToStatsManager_WithoutCrashing()
        {
            var sem = _holderGo.AddComponent<StatusEffectManager>();
            var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
            effectData.ID = "Test_Buff";
            effectData.hasDuration = true;
            effectData.duration = 3f;
            effectData.statEffects.Add(new StatEffect
            {
                stat = StatType.Attack,
                modType = StatModType.Flat,
                valuePerStack = 15f
            });

            Assert.DoesNotThrow(() => sem.ApplyEffect(effectData));
            Assert.Greater(_statsManager.Damage.Value, _statsManager.Damage.BaseValue);

            Object.DestroyImmediate(effectData);
        }
    }
}
