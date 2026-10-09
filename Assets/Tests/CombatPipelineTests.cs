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

        #region Status Effect Stacking & Expiration Stress Tests

        [Test]
        public void StatusEffect_StackingLimit_CapsCorrectly_AndRecalculatesStats()
        {
            var sem = _holderGo.AddComponent<StatusEffectManager>();
            var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
            effectData.ID = "Stackable_Power";
            effectData.hasDuration = true;
            effectData.duration = 5f;
            effectData.isStackable = true;
            effectData.maxStacks = 3;
            effectData.statEffects.Add(new StatEffect
            {
                stat = StatType.Attack,
                modType = StatModType.Flat,
                valuePerStack = 10f
            });

            float baseDamage = _statsManager.Damage.Value;

            // Apply 1st stack
            sem.ApplyEffect(effectData);
            Assert.AreEqual(baseDamage + 10f, _statsManager.Damage.Value);

            // Apply 2nd stack
            sem.ApplyEffect(effectData);
            Assert.AreEqual(baseDamage + 20f, _statsManager.Damage.Value);

            // Apply 3rd stack (at max)
            sem.ApplyEffect(effectData);
            Assert.AreEqual(baseDamage + 30f, _statsManager.Damage.Value);

            // Apply 4th stack -> should be capped at 3 stacks (30f bonus)
            sem.ApplyEffect(effectData);
            Assert.AreEqual(baseDamage + 30f, _statsManager.Damage.Value, "Damage bonus should not exceed maxStacks * valuePerStack.");

            Object.DestroyImmediate(effectData);
        }

        [Test]
        public void StatusEffect_DurationUpdate_ExpiresAndRemovesModifiersCleanly()
        {
            var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
            effectData.ID = "Temporary_Speed";
            effectData.hasDuration = true;
            effectData.duration = 2.0f;
            effectData.statEffects.Add(new StatEffect
            {
                stat = StatType.MoveSpeed,
                modType = StatModType.Flat,
                valuePerStack = 5f
            });

            var effect = new StatusEffect(effectData);
            float baseSpeed = _statsManager.Movespeed.Value;

            effect.Apply(_statsManager, this);
            Assert.AreEqual(baseSpeed + 5f, _statsManager.Movespeed.Value);

            // Advance 1 second -> should still be active
            bool active1 = effect.Update(1.0f, _holderGo);
            Assert.IsTrue(active1, "Effect should remain active after 1 second.");
            Assert.AreEqual(baseSpeed + 5f, _statsManager.Movespeed.Value);

            // Advance another 1.1 seconds -> should expire
            bool active2 = effect.Update(1.1f, _holderGo);
            Assert.IsFalse(active2, "Effect should expire when time exceeds duration.");

            // Remove modifiers
            effect.Remove(_statsManager, this);
            Assert.AreEqual(baseSpeed, _statsManager.Movespeed.Value, "Speed should restore to base value upon removal.");

            Object.DestroyImmediate(effectData);
        }

        [Test]
        public void StatusEffect_PeriodicTick_TriggersWithoutModifyingStatsDirectly()
        {
            var effectData = ScriptableObject.CreateInstance<StatusEffectData>();
            effectData.ID = "Poison_DoT";
            effectData.hasDuration = true;
            effectData.duration = 3.0f;
            effectData.tickInterval = 1.0f;

            var effect = new StatusEffect(effectData);
            effect.Apply(_statsManager, this);

            // Tick 0.5s -> no tick yet
            bool stillActive = effect.Update(0.5f, _holderGo);
            Assert.IsTrue(stillActive);

            // Tick another 0.6s (total 1.1s) -> tick triggered, timer resets to 1.0s
            stillActive = effect.Update(0.6f, _holderGo);
            Assert.IsTrue(stillActive);

            effect.Remove(_statsManager, this);
            Object.DestroyImmediate(effectData);
        }

        #endregion

        #region Combat Math & Damage Pipeline Fuzzing

        [Test]
        public void CharacterStatsManager_TakeDamage_FuzzingExtremeValues_NeverCorruptsLife()
        {
            // Initial life setup
            float maxLife = _statsManager.MaxLife.Value;

            // Fuzzing 1: Negative damage values should not heal the unit
            for (float negDmg = -100f; negDmg <= -1f; negDmg += 20f)
            {
                float lifeBefore = _statsManager.Life;
                _statsManager.TakeDamage(new DamageData(negDmg, DamageType.Physical));
                Assert.AreEqual(lifeBefore, _statsManager.Life, "Negative damage must not alter or increase HP.");
            }

            // Fuzzing 2: Excessive damage should clamp HP at 0, not go negative
            _statsManager.TakeDamage(new DamageData(999999f, DamageType.Physical));
            Assert.AreEqual(0f, _statsManager.Life, "Damage exceeding life must clamp life to exactly 0.");
        }

        [Test]
        public void CharacterStatsManager_Immortality_ClampsAtOneHp()
        {
            _statsManager.IsImmortal = true;
            _statsManager.TakeDamage(new DamageData(5000f, DamageType.True));

            Assert.AreEqual(1f, _statsManager.Life, "Immortal character should survive fatal damage with at least 1 HP.");
        }

        [Test]
        public void StatModifier_MultipleMixedOperations_ComputesDeterministically()
        {
            var stat = new Stat(100f);

            // Flat +20 -> 120
            stat.AddModifier(new StatModifier(20f, StatModType.Flat, 100));
            // PercentAdd +50% -> 100 * 0.50 = +50
            stat.AddModifier(new StatModifier(50f, StatModType.PercentAdd, 200));
            // PercentMult * 1.2 -> (100 + 20 + 50) * 1.2 = 170 * 1.2 = 204
            stat.AddModifier(new StatModifier(20f, StatModType.PercentMult, 300));

            Assert.AreEqual(204f, stat.Value, 0.001f);
        }

        #endregion
    }
}
