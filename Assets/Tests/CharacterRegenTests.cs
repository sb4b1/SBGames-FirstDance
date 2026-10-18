using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Database.CSV;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using SBGames.Inventory.Stats;
using SBGames.Loop;
using SBGames.Testing;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CharacterRegenTests
    {
        private GameObject _unitGo;
        private CharacterBrain _brain;
        private CharacterStatsManager _statsManager;

        [SetUp]
        public void SetUp()
        {
            _unitGo = new GameObject("Test_RegenUnit");
            _brain = _unitGo.AddComponent<CharacterBrain>();
            _statsManager = _unitGo.GetComponent<CharacterStatsManager>();
            if (_statsManager == null)
            {
                _statsManager = _unitGo.AddComponent<CharacterStatsManager>();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_unitGo != null)
            {
                Object.DestroyImmediate(_unitGo);
            }
        }

        [Test]
        public void CharacterBaseStats_Defaults_LifeRegenAndManaRegenAreZero()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            Assert.AreEqual(0f, baseStats.LifeRegen, 0.0001f);
            Assert.AreEqual(0f, baseStats.ManaRegen, 0.0001f);

            _statsManager.SetBaseStats(baseStats);
            Assert.AreEqual(0f, _statsManager.LifeRegen.Value, 0.0001f);
            Assert.AreEqual(0f, _statsManager.ManaRegen.Value, 0.0001f);

            Object.DestroyImmediate(baseStats);
        }

        [Test]
        public void RegenTuningSO_Defaults_DelayIsFive_MultiplierIsThree()
        {
            var tuning = ScriptableObject.CreateInstance<RegenTuningSO>();
            Assert.AreEqual(5f, tuning.OutOfCombatDelay, 0.0001f);
            Assert.AreEqual(3.0f, tuning.OutOfCombatRegenMultiplier, 0.0001f);
            Object.DestroyImmediate(tuning);

#if UNITY_EDITOR
            var defaultAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<RegenTuningSO>("Assets/ScriptableObjects/RegenTuning_Default.asset");
            Assert.IsNotNull(defaultAsset, "Default RegenTuningSO asset must exist at Assets/ScriptableObjects/RegenTuning_Default.asset.");
            Assert.AreEqual(5f, defaultAsset.OutOfCombatDelay, 0.0001f);
            Assert.AreEqual(3.0f, defaultAsset.OutOfCombatRegenMultiplier, 0.0001f);
#endif
        }

        [Test]
        public void TickRegen_InCombat_AppliesNormalRate()
        {
            _statsManager.MaxLife.BaseValue = 100f;
            _statsManager.RestoreLife(100f);
            _statsManager.MaxMana.BaseValue = 100f;
            _statsManager.RestoreMana(100f);
            _statsManager.LifeRegen.BaseValue = 4f;
            _statsManager.ManaRegen.BaseValue = 6f;

            // Take damage to enter in-combat state (timer = 0) and reduce Life/Mana
            var dmg = new DamageData(50f, DamageType.Physical);
            _statsManager.TakeDamage(in dmg);
            _statsManager.TryConsumeMana(50f);

            Assert.AreEqual(50f, _statsManager.Life, 0.001f);
            Assert.AreEqual(50f, _statsManager.Mana, 0.001f);
            Assert.AreEqual(0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.001f);
            Assert.IsFalse(_statsManager.IsOutOfCombat);

            // Tick 2.0 seconds (still < 5.0s OutOfCombatDelay)
            _statsManager.TickRegen(2.0f);

            // Expected: Life += 4 * 1 * 2 = +8 -> 58; Mana += 6 * 1 * 2 = +12 -> 62
            Assert.AreEqual(58f, _statsManager.Life, 0.001f);
            Assert.AreEqual(62f, _statsManager.Mana, 0.001f);
            Assert.AreEqual(2.0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.001f);
            Assert.IsFalse(_statsManager.IsOutOfCombat);
        }

        [Test]
        public void TickRegen_OutOfCombat_AppliesBoostedMultiplier()
        {
            var tuning = ScriptableObject.CreateInstance<RegenTuningSO>();
            _statsManager.SetRegenTuning(tuning);

            _statsManager.MaxLife.BaseValue = 200f;
            _statsManager.RestoreLife(200f);
            _statsManager.MaxMana.BaseValue = 200f;
            _statsManager.RestoreMana(200f);
            _statsManager.LifeRegen.BaseValue = 5f;
            _statsManager.ManaRegen.BaseValue = 10f;

            // Take 100 damage and consume 100 mana -> Life = 100, Mana = 100, timer = 0
            var dmg = new DamageData(100f, DamageType.True);
            _statsManager.TakeDamage(in dmg);
            _statsManager.TryConsumeMana(100f);

            // Advance 5.0 seconds in 1.0s increments so timer reaches 5.0s (OutOfCombatDelay)
            for (int i = 0; i < 4; i++)
            {
                _statsManager.TickRegen(1.0f);
            }
            Assert.IsFalse(_statsManager.IsOutOfCombat);

            // On the 5th 1.0s tick, timer reaches 5.0s -> out-of-combat boost applies
            _statsManager.TickRegen(1.0f);
            Assert.IsTrue(_statsManager.IsOutOfCombat, "Unit should be out of combat after 5 seconds without dealing or taking damage.");

            float lifeAtFiveSec = _statsManager.Life;
            float manaAtFiveSec = _statsManager.Mana;

            // Next 1.0s tick is out-of-combat -> rate * 3.0 * 1.0s
            _statsManager.TickRegen(1.0f);

            Assert.AreEqual(lifeAtFiveSec + (5f * 3.0f * 1.0f), _statsManager.Life, 0.001f);
            Assert.AreEqual(manaAtFiveSec + (10f * 3.0f * 1.0f), _statsManager.Mana, 0.001f);

            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void TimerReset_OnDamageTakenAndDamageDealt_ResetsOutOfCombatTimer_WhileHealingDoesNot()
        {
            _statsManager.MaxLife.BaseValue = 200f;
            _statsManager.RestoreLife(200f);
            _statsManager.LifeRegen.BaseValue = 2f;

            // Taking damage via IDamageable.TakeDamage resets timer
            IDamageable damageable = _statsManager;
            var hit1 = new DamageData(40f, DamageType.Physical);
            damageable.TakeDamage(in hit1);

            Assert.AreEqual(0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.0001f);
            Assert.IsFalse(_statsManager.IsOutOfCombat);

            // Advance 6 seconds -> out of combat again
            for (int i = 0; i < 6; i++)
            {
                _statsManager.TickRegen(1.0f);
            }
            Assert.IsTrue(_statsManager.IsOutOfCombat);
            Assert.AreEqual(6.0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.001f);

            // Receiving healing does NOT reset timer
            _statsManager.RestoreLife(10f);
            _statsManager.RestoreMana(10f);
            Assert.IsTrue(_statsManager.IsOutOfCombat, "Healing received must not reset out-of-combat timer.");
            Assert.AreEqual(6.0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.001f);

            // Dealing damage to another target resets attacker's timer
            var targetGo = new GameObject("TargetUnit");
            targetGo.AddComponent<CharacterBrain>();
            var targetStats = targetGo.GetComponent<CharacterStatsManager>() ?? targetGo.AddComponent<CharacterStatsManager>();

            var dealtDmg = new DamageData(25f, DamageType.Physical, _statsManager);
            targetStats.TakeDamage(in dealtDmg);

            Assert.AreEqual(0f, _statsManager.TimeSinceLastDamageTakenOrDealt, 0.0001f, "Dealing damage must reset attacker's out-of-combat timer.");
            Assert.IsFalse(_statsManager.IsOutOfCombat);
            Assert.AreEqual(0f, targetStats.TimeSinceLastDamageTakenOrDealt, 0.0001f, "Taking damage must reset defender's out-of-combat timer.");

            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void TickRegen_WhenDead_DoesNotRegenerateLifeOrMana()
        {
            _statsManager.MaxLife.BaseValue = 100f;
            _statsManager.RestoreLife(100f);
            _statsManager.MaxMana.BaseValue = 100f;
            _statsManager.RestoreMana(100f);
            _statsManager.TryConsumeMana(50f);
            _statsManager.LifeRegen.BaseValue = 10f;
            _statsManager.ManaRegen.BaseValue = 10f;

            // Kill unit via TakeDamage
            var lethal = new DamageData(200f, DamageType.True);
            bool died = _statsManager.TakeDamage(in lethal);
            Assert.IsTrue(died);
            Assert.AreEqual(0f, _statsManager.Life, 0.0001f);

            // Tick regen for 10 seconds -> should remain at 0 Life and 50 Mana
            _statsManager.TickRegen(10.0f);
            Assert.AreEqual(0f, _statsManager.Life, 0.0001f, "Dead characters must not regenerate Life.");
            Assert.AreEqual(50f, _statsManager.Mana, 0.0001f, "Dead characters must not regenerate Mana.");
        }

        [Test]
        public void TickRegen_ClampsAtMaxLifeAndMaxMana_AndUsesRestoreLife()
        {
            _statsManager.MaxLife.BaseValue = 100f;
            _statsManager.RestoreLife(100f);
            _statsManager.MaxMana.BaseValue = 80f;
            _statsManager.RestoreMana(80f);

            var dmg = new DamageData(5f, DamageType.True);
            _statsManager.TakeDamage(in dmg);
            _statsManager.TryConsumeMana(5f);

            _statsManager.LifeRegen.BaseValue = 20f;
            _statsManager.ManaRegen.BaseValue = 20f;

            float lifeChangedValue = -1f;
            float regenRestoredAmount = 0f;
            float externalLifeRestoredAmount = 0f;

            _statsManager.OnLifeChanged += (cur, max) => lifeChangedValue = cur;
            _statsManager.OnRegenLifeRestored += amt => regenRestoredAmount += amt;
            _statsManager.OnLifeRestored += amt => externalLifeRestoredAmount += amt;

            _statsManager.TickRegen(1.0f);

            Assert.AreEqual(100f, _statsManager.Life, 0.0001f, "Life must be clamped to MaxLife.");
            Assert.AreEqual(80f, _statsManager.Mana, 0.0001f, "Mana must be clamped to MaxMana.");
            Assert.AreEqual(100f, lifeChangedValue, 0.0001f, "RestoreLife should notify OnLifeChanged.");
            Assert.AreEqual(5f, regenRestoredAmount, 0.0001f, "OnRegenLifeRestored should report actual clamped amount restored.");
            Assert.AreEqual(0f, externalLifeRestoredAmount, 0.0001f, "Regen must not fire OnLifeRestored (used for skill/potion healing).");
        }

        [Test]
        public void TickRegen_OnlyTicksDuringBattlePhase()
        {
            var dmGo = new GameObject("Test_DM");
            var loopGo = new GameObject("Test_CoreGameLoop");
            try
            {
                var dm = dmGo.AddComponent<DungeonMaster>();
                dm.Initialize(startingEssence: 100, startingEnergy: 50);

                var gameLoop = loopGo.AddComponent<CoreGameLoop>();
                gameLoop.Initialize(dm, initialRound: 1);

                _statsManager.MaxLife.BaseValue = 100f;
                _statsManager.RestoreLife(100f);
                var dmg = new DamageData(40f, DamageType.True);
                _statsManager.TakeDamage(in dmg);
                _statsManager.LifeRegen.BaseValue = 10f;

                // Default CoreGameLoop phase is NotStarted -> regen should not tick
                Assert.AreEqual(GamePhase.NotStarted, gameLoop.CurrentPhase);
                _statsManager.TickRegen(1.0f);
                Assert.AreEqual(60f, _statsManager.Life, 0.0001f, "Regen should not tick in NotStarted phase.");

                // StartGame transitions to Planning phase -> regen should not tick
                gameLoop.StartGame();
                Assert.AreEqual(GamePhase.Planning, gameLoop.CurrentPhase);
                _statsManager.TickRegen(1.0f);
                Assert.AreEqual(60f, _statsManager.Life, 0.0001f, "Regen should not tick in Planning phase.");

                // StartBattle transitions to Battle phase -> regen SHOULD tick
                gameLoop.StartBattle();
                Assert.AreEqual(GamePhase.Battle, gameLoop.CurrentPhase);
                _statsManager.TickRegen(1.0f);
                Assert.AreEqual(70f, _statsManager.Life, 0.0001f, "Regen must tick during Battle phase.");
            }
            finally
            {
                if (loopGo != null) Object.DestroyImmediate(loopGo);
                if (dmGo != null) Object.DestroyImmediate(dmGo);
            }
        }

        [Test]
        public void ItemStatModifiers_TargetLifeRegenAndManaRegen_FlatPercentAddPercentMult()
        {
            _statsManager.LifeRegen.BaseValue = 10f;
            _statsManager.ManaRegen.BaseValue = 20f;

            var invManager = _unitGo.AddComponent<CharacterInventoryManager>();
            invManager.InitializeInventory();

            var rune = ItemDataSO.CreateRuntimeInstance(
                id: "rune_regen",
                itemName: "Regen Rune",
                itemType: ItemType.Rune,
                maxStack: 10,
                baseValue: 50,
                slotType: EquipmentSlotType.Rune);

            // Flat +5, PercentAdd +0.50 (+50%), PercentMult +0.20 (+20%)
            // Stat formula: ((Base + Flat) * (1 + PercentAdd)) * (1 + PercentMult)
            // For LifeRegen (Base 10): ((10 + 5) * 1.5) * 1.2 = 27.0
            // For ManaRegen (Base 20): ((20 + 5) * 1.5) * 1.2 = 45.0
            rune.AddModifier(new ItemStatModifierData(StatType.LifeRegen, 5f, StatModType.Flat));
            rune.AddModifier(new ItemStatModifierData(StatType.LifeRegen, 0.5f, StatModType.PercentAdd));
            rune.AddModifier(new ItemStatModifierData(StatType.LifeRegen, 0.2f, StatModType.PercentMult));

            rune.AddModifier(new ItemStatModifierData(StatType.ManaRegen, 5f, StatModType.Flat));
            rune.AddModifier(new ItemStatModifierData(StatType.ManaRegen, 0.5f, StatModType.PercentAdd));
            rune.AddModifier(new ItemStatModifierData(StatType.ManaRegen, 0.2f, StatModType.PercentMult));

            bool equipped = invManager.Equip(0, rune, out _, out _);
            Assert.IsTrue(equipped);

            Assert.AreEqual(27f, _statsManager.LifeRegen.Value, 0.001f);
            Assert.AreEqual(45f, _statsManager.ManaRegen.Value, 0.001f);

            Object.DestroyImmediate(rune);
        }

        [Test]
        public void BattleSimulationTelemetry_TracksRegenHealingSeparatelyFromSkillAndPotion()
        {
            var runnerGo = new GameObject("Test_Runner");
            var runner = runnerGo.AddComponent<BattleSimulationTestRunner>();

            var slot = new BattleSimulationTestRunner.UnitSpawnSlot();
            var combatant = new BattleSimulationTestRunner.TrackedCombatant(
                _unitGo, _brain, _statsManager, null, BattleSimulationTestRunner.BattleFaction.Adventurer, slot, runner);

            _statsManager.OnDamaged += combatant.OnDamagedHandler;
            _statsManager.OnLifeRestored += combatant.OnLifeRestoredHandler;
            _statsManager.OnRegenLifeRestored += combatant.OnRegenLifeRestoredHandler;

            _statsManager.MaxLife.BaseValue = 100f;
            _statsManager.RestoreLife(100f);

            var dmg = new DamageData(50f, DamageType.Physical);
            _statsManager.TakeDamage(in dmg);

            // Reset initial full-heal counters before testing
            var freshCombatant = new BattleSimulationTestRunner.TrackedCombatant(
                _unitGo, _brain, _statsManager, null, BattleSimulationTestRunner.BattleFaction.Adventurer, slot, runner);
            _statsManager.OnLifeRestored -= combatant.OnLifeRestoredHandler;
            _statsManager.OnRegenLifeRestored -= combatant.OnRegenLifeRestoredHandler;
            _statsManager.OnLifeRestored += freshCombatant.OnLifeRestoredHandler;
            _statsManager.OnRegenLifeRestored += freshCombatant.OnRegenLifeRestoredHandler;

            // 1. Tick regen (+10 HP)
            _statsManager.LifeRegen.BaseValue = 10f;
            _statsManager.TickRegen(1.0f);

            Assert.AreEqual(10f, freshCombatant.RegenHealingReceived, 0.001f);
            Assert.AreEqual(0f, freshCombatant.SkillHealingReceived, 0.001f);
            Assert.AreEqual(0f, freshCombatant.PotionHealingReceived, 0.001f);

            // 2. Direct RestoreLife without skill timestamp -> counts as Potion healing (+15 HP)
            _statsManager.RestoreLife(15f);
            Assert.AreEqual(10f, freshCombatant.RegenHealingReceived, 0.001f);
            Assert.AreEqual(0f, freshCombatant.SkillHealingReceived, 0.001f);
            Assert.AreEqual(15f, freshCombatant.PotionHealingReceived, 0.001f);

            // 3. Simulate skill heal timestamp -> counts as Skill healing (+20 HP), while regen during same frame still counts as Regen
            runner.LastSkillHealTimestamp = Time.time;
            _statsManager.RestoreLife(20f);
            _statsManager.TickRegen(0.5f); // +5 HP regen during same frame as skill heal

            Assert.AreEqual(15f, freshCombatant.RegenHealingReceived, 0.001f);
            Assert.AreEqual(20f, freshCombatant.SkillHealingReceived, 0.001f);
            Assert.AreEqual(15f, freshCombatant.PotionHealingReceived, 0.001f);

            Object.DestroyImmediate(runnerGo);
        }

        [Test]
        public void CharacterStatsTab_Validate_FlagsNegativeLifeRegenAndManaRegen()
        {
            var tabType = System.Type.GetType("SBGames.Editor.Stats.Tabs.CharacterStatsTab, Assembly-CSharp-Editor");
            Assert.IsNotNull(tabType, "CharacterStatsTab type must be present in Assembly-CSharp-Editor.");

            var tab = System.Activator.CreateInstance(tabType);
            var reloadMethod = tabType.GetMethod("ReloadData");
            Assert.IsNotNull(reloadMethod);
            reloadMethod.Invoke(tab, null);

            var rowsField = tabType.GetField("_rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(rowsField);
            var rows = rowsField.GetValue(tab) as System.Collections.IList;
            Assert.IsNotNull(rows);
            Assert.Greater(rows.Count, 0);

            var firstRow = rows[0];
            var rowType = firstRow.GetType();
            var validateRowMethod = rowType.GetMethod("Validate");
            var valErrField = rowType.GetField("ValidationError");

            Assert.AreEqual(0f, (float)rowType.GetField("LifeRegen").GetValue(firstRow), 0.0001f);
            Assert.AreEqual(0f, (float)rowType.GetField("ManaRegen").GetValue(firstRow), 0.0001f);
            Assert.AreEqual(string.Empty, (string)valErrField.GetValue(firstRow));

            rowType.GetField("LifeRegen").SetValue(firstRow, -1.5f);
            validateRowMethod.Invoke(firstRow, null);
            StringAssert.Contains("LifeRegen", (string)valErrField.GetValue(firstRow));

            rowType.GetField("LifeRegen").SetValue(firstRow, 0f);
            rowType.GetField("ManaRegen").SetValue(firstRow, -2.0f);
            validateRowMethod.Invoke(firstRow, null);
            StringAssert.Contains("ManaRegen", (string)valErrField.GetValue(firstRow));

            rowType.GetField("ManaRegen").SetValue(firstRow, 0f);
            validateRowMethod.Invoke(firstRow, null);
            Assert.AreEqual(string.Empty, (string)valErrField.GetValue(firstRow));
        }
    }
}
