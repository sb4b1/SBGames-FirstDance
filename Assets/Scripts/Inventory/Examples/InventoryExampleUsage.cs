using System.Text;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Stats;
using SBGames.Inventory.User;
using UnityEngine;

namespace SBGames.Inventory.Examples
{
    /// <summary>
    /// Demonstrates headless programmatic usage and verification of the inventory and equipment system.
    /// Completely executes without any UI Toolkit or visual components present.
    /// Can be executed by calling RunFullVerificationSuite() from code, Inspector button, or unit tests.
    /// </summary>
    public class InventoryExampleUsage : MonoBehaviour
    {
        [Header("Auto-Run on Start")]
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunFullVerificationSuite();
            }
        }

        [ContextMenu("Run Full Verification Suite")]
        public static bool RunFullVerificationSuite()
        {
            var log = new StringBuilder();
            log.AppendLine("==================================================");
            log.AppendLine("STARTING INVENTORY SYSTEM VERIFICATION SUITE");
            log.AppendLine("==================================================");

            bool allPassed = true;

            // 1. Verify User Inventory (Stacking, Overflow, Capacity)
            allPassed &= VerifyUserInventoryLogic(log);

            // 2. Verify Character Equipment & Stat Application
            allPassed &= VerifyCharacterEquipmentAndStats(log);

            // 3. Verify Artifact Auto-Swap & Rune Destroy-On-Replace
            allPassed &= VerifyEquipmentReplacementRules(log);

            // 4. Verify Consumable Conditional Usage
            allPassed &= VerifyConsumableUsage(log);

            // 5. Verify Bidirectional Decoupling and External Stat Resilience
            allPassed &= VerifyDecouplingAndResilience(log);

            log.AppendLine("==================================================");
            log.AppendLine(allPassed ? ">> ALL VERIFICATION TESTS PASSED SUCCESSFULLY! <<" : ">> SOME VERIFICATION TESTS FAILED! <<");
            log.AppendLine("==================================================");

            if (allPassed)
            {
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }

            return allPassed;
        }

        private static bool VerifyUserInventoryLogic(StringBuilder log)
        {
            log.AppendLine("\n[Test 1] User Inventory Stacking & Capacity Limits");
            var inv = new Core.Inventory(capacity: 3);

            var potion = ItemDefinition.CreateRuntimeInstance("pot_hp", "Health Potion", ItemType.Consumable, maxStack: 5);
            var sword = ItemDefinition.CreateRuntimeInstance("sword", "Iron Sword", ItemType.Misc, maxStack: 1);

            // Add 3 potions -> should go into slot 0
            bool ok = true;
            int left1 = inv.AddItem(potion, 3);
            ok &= Assert(left1 == 0, "All 3 potions added", log);
            ok &= Assert(inv.GetSlot(0).Amount == 3, "Slot 0 has 3 potions", log);

            // Add 4 more potions -> slot 0 fills to 5 (2 added), slot 1 takes remaining 2
            int left2 = inv.AddItem(potion, 4);
            ok &= Assert(left2 == 0, "All 4 potions added across slots", log);
            ok &= Assert(inv.GetSlot(0).Amount == 5, "Slot 0 reached maxStack 5", log);
            ok &= Assert(inv.GetSlot(1).Amount == 2, "Slot 1 received remaining 2 potions", log);

            // Fill slot 2 with sword
            inv.AddItem(sword, 1);
            ok &= Assert(!inv.GetSlot(2).IsEmpty, "Slot 2 has sword", log);

            // Inventory is now full for non-stackable items
            ok &= Assert(!inv.CanAddItem(sword, 1), "Cannot add another sword (capacity reached)", log);
            // But can still fit 3 potions into slot 1!
            ok &= Assert(inv.CanAddItem(potion, 3), "Can still fit 3 potions into slot 1", log);

            return ok;
        }

        private static bool VerifyCharacterEquipmentAndStats(StringBuilder log)
        {
            log.AppendLine("\n[Test 2] Character Equipment & Stat Application (StatModifier Ordering)");
            bool ok = true;

            // Create CharacterStatsManager GameObject
            var charGo = new GameObject("[TestCharacter]");
            var statsManager = charGo.AddComponent<CharacterStatsManager>();
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStats>();
            baseStats.Damage = 20f;
            baseStats.Movespeed = 5f;

            // Reflectively initialize stats since Awake is private in CharacterStatsManager
            InitializeStatsManager(statsManager, baseStats);

            var charInv = new CharacterInventory();
            var binder = new CharacterEquipmentStatBinder(charInv, statsManager);

            // Base Damage: 20
            ok &= Assert(Mathf.Approximately(statsManager.Damage.Value, 20f), $"Initial damage is 20 (got {statsManager.Damage.Value})", log);

            // Create Rune: +10 Flat Damage, +20% PercentAdd Damage
            var atkRune = ItemDefinition.CreateRuntimeInstance(
                "rune_atk",
                "Great Rune of Might",
                ItemType.Rune,
                EquipmentSlotType.Rune,
                maxStack: 1,
                modifiers: new[]
                {
                    new ItemStatModifierData(StatType.Attack, 10f, StatModType.Flat),
                    new ItemStatModifierData(StatType.Attack, 0.20f, StatModType.PercentAdd)
                });

            // Equip Rune to slot 0
            bool equipped = charInv.Equip(0, atkRune, out _, out _);
            ok &= Assert(equipped, "Rune equipped successfully into slot 0", log);

            // Expected calculation: (Base + Flat) * (1 + PercentAdd) = (20 + 10) * (1 + 0.20) = 30 * 1.2 = 36
            float expectedDamage = 36f;
            float actualDamage = statsManager.Damage.Value;
            ok &= Assert(Mathf.Approximately(actualDamage, expectedDamage), $"Damage calculated to {expectedDamage} (got {actualDamage})", log);

            // Unequip Rune -> Damage must revert cleanly to 20
            bool unequipped = charInv.Unequip(0, out var unequippedItem);
            ok &= Assert(unequipped && unequippedItem == atkRune, "Rune unequipped successfully", log);
            ok &= Assert(Mathf.Approximately(statsManager.Damage.Value, 20f), $"Damage reverted back to base 20 (got {statsManager.Damage.Value})", log);

            binder.Dispose();
            DestroyImmediate(charGo);
            return ok;
        }

        private static bool VerifyEquipmentReplacementRules(StringBuilder log)
        {
            log.AppendLine("\n[Test 3] Equipment Replacement Rules (Artifact Auto-Swap vs Rune Destroy-On-Replace)");
            bool ok = true;

            var charInv = new CharacterInventory();

            // Artifacts
            var bootsA = ItemDefinition.CreateRuntimeInstance("bootsA", "Boots of Swiftness", ItemType.Artifact, EquipmentSlotType.Artifact);
            var bootsB = ItemDefinition.CreateRuntimeInstance("bootsB", "Boots of Haste", ItemType.Artifact, EquipmentSlotType.Artifact);

            // Slot 4 is Artifact Slot
            int artifactSlot = 4;
            charInv.Equip(artifactSlot, bootsA, out _, out _);
            ok &= Assert(charInv.GetSlot(artifactSlot).EquippedItem == bootsA, "Boots A equipped", log);

            // Equip Boots B into occupied artifact slot -> Boots A is displaced and returned!
            charInv.Equip(artifactSlot, bootsB, out var displacedArtifact, out var destroyedRune);
            ok &= Assert(displacedArtifact == bootsA, "Boots A was auto-swapped out safely to displacedItem", log);
            ok &= Assert(destroyedRune == null, "No rune was destroyed", log);
            ok &= Assert(charInv.GetSlot(artifactSlot).EquippedItem == bootsB, "Boots B is now equipped", log);

            // Runes
            var runeA = ItemDefinition.CreateRuntimeInstance("runeA", "Fire Rune", ItemType.Rune, EquipmentSlotType.Rune);
            var runeB = ItemDefinition.CreateRuntimeInstance("runeB", "Ice Rune", ItemType.Rune, EquipmentSlotType.Rune);

            int runeSlot = 0;
            charInv.Equip(runeSlot, runeA, out _, out _);
            ok &= Assert(charInv.GetSlot(runeSlot).EquippedItem == runeA, "Rune A equipped", log);

            // Inscribing Rune B over Rune A -> Rune A is destroyed!
            charInv.Equip(runeSlot, runeB, out var displacedRune, out var destroyedRuneResult, confirmDestroyRune: true);
            ok &= Assert(displacedRune == null, "Displaced item is null (rune not returned to inventory)", log);
            ok &= Assert(destroyedRuneResult == runeA, "Rune A was destroyed in inscription process", log);
            ok &= Assert(charInv.GetSlot(runeSlot).EquippedItem == runeB, "Rune B is now equipped", log);

            return ok;
        }

        private static bool VerifyConsumableUsage(StringBuilder log)
        {
            log.AppendLine("\n[Test 4] Consumable Item Conditional Validation");

            var charGo = new GameObject("[TestCharacterConsumable]");
            var stats = charGo.AddComponent<CharacterStatsManager>();
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStats>();
            baseStats.MaxLife = 100f;
            InitializeStatsManager(stats, baseStats);

            var healEffect = ScriptableObject.CreateInstance<RestoreHealthEffect>();
            healEffect.HealAmount = 30f;
            var potion = ItemDefinition.CreateRuntimeInstance("pot_hp", "Health Potion", ItemType.Consumable, effect: healEffect);

            var userGo = new GameObject("[TestUserInv]");
            var userInv = userGo.AddComponent<UserInventoryManager>();
            userInv.InitializeInventory();
            userInv.AddItem(potion, 2);

            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            // At 100% Life, consuming health potion should be rejected to prevent wasting the item
            bool consumedAtFull = userInv.ConsumeItem(0, charInvMgr, out string msgFull);
            bool ok = true;
            ok &= Assert(!consumedAtFull, "Cannot consume potion at full life (item preserved)", log);
            ok &= Assert(userInv.Inventory.GetSlot(0).Amount == 2, "Potion count remains 2", log);

            // Deal 50 damage
            stats.TakeDamage(50f);
            ok &= Assert(Mathf.Approximately(stats.Life, 50f), "Character took 50 damage (life is now 50)", log);

            // Now consume potion
            bool consumedDamaged = userInv.ConsumeItem(0, charInvMgr, out string msgDamaged);
            ok &= Assert(consumedDamaged, "Potion consumed successfully when injured", log);
            ok &= Assert(Mathf.Approximately(stats.Life, 80f), "Life healed by 30 (life is now 80)", log);
            ok &= Assert(userInv.Inventory.GetSlot(0).Amount == 1, "Potion quantity decremented to 1", log);

            DestroyImmediate(charGo);
            DestroyImmediate(userGo);
            return ok;
        }

        private static bool VerifyDecouplingAndResilience(StringBuilder log)
        {
            log.AppendLine("\n[Test 5] Bidirectional Decoupling & Stat Isolation");
            bool ok = true;

            var charGo = new GameObject("[TestDecoupling]");
            var stats = charGo.AddComponent<CharacterStatsManager>();
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStats>();
            baseStats.Damage = 15f;
            InitializeStatsManager(stats, baseStats);

            var charInv = new CharacterInventory();
            var binder = new CharacterEquipmentStatBinder(charInv, stats);

            var rune = ItemDefinition.CreateRuntimeInstance(
                "rune_atk",
                "Combat Rune",
                ItemType.Rune,
                EquipmentSlotType.Rune,
                modifiers: new[] { new ItemStatModifierData(StatType.Attack, 5f, StatModType.Flat) });

            charInv.Equip(0, rune, out _, out _);
            ok &= Assert(Mathf.Approximately(stats.Damage.Value, 20f), "Rune increased damage from 15 to 20", log);

            // External system applies a combat buff (+10 flat) from an external source
            var buffSource = new object();
            var externalBuff = new StatModifier(10f, StatModType.Flat, (int)StatModType.Flat, buffSource);
            stats.Damage.AddModifier(externalBuff);

            ok &= Assert(Mathf.Approximately(stats.Damage.Value, 30f), "External buff applied (Damage is now 30)", log);

            // Unequip rune -> ONLY the rune's modifier is removed, the external buff stays intact!
            charInv.Unequip(0, out _);
            ok &= Assert(Mathf.Approximately(stats.Damage.Value, 25f), "Damage is 25 (base 15 + external buff 10; rune modifier cleanly removed)", log);

            // Inventory state remains completely intact
            ok &= Assert(charInv.GetSlot(0).IsEmpty, "Equipment slot is empty after unequip", log);

            binder.Dispose();
            DestroyImmediate(charGo);
            return ok;
        }

        private static void InitializeStatsManager(CharacterStatsManager statsManager, CharacterBaseStats baseStats)
        {
            var field = typeof(CharacterStatsManager).GetField("baseStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(statsManager, baseStats);

            var method = typeof(CharacterStatsManager).GetMethod("InitializeStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null) method.Invoke(statsManager, null);
        }

        private static bool Assert(bool condition, string testName, StringBuilder log)
        {
            if (condition)
            {
                log.AppendLine($"  [PASS] {testName}");
                return true;
            }
            else
            {
                log.AppendLine($"  [FAIL] >>> {testName} <<<");
                return false;
            }
        }
    }
}
