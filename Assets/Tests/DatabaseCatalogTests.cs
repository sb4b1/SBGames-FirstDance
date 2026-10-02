using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SBGames.Database;
using SBGames.Database.Runtime;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DatabaseCatalogTests
    {
        private ItemDatabaseSO _itemDb;
        private SkillDatabaseSO _skillDb;
        private CharacterDatabaseSO _charDb;
        private MasterGameDatabaseSO _masterDb;

        private ItemDataSO _potion;
        private ItemDataSO _runeFire;
        private ItemDataSO _artifactRelic;
        private SkillData _fireballSkill;
        private CharacterEntry _heroChar;

        [SetUp]
        public void Setup()
        {
            _potion = ItemDataSO.CreateRuntimeInstance("potion_health", "Health Potion", ItemType.Consumable, maxStack: 10);
            _runeFire = ItemDataSO.CreateRuntimeInstance("rune_fire", "Fire Rune", ItemType.Rune);
            _artifactRelic = ItemDataSO.CreateRuntimeInstance("artifact_chalice", "Sacred Chalice", ItemType.Artifact);

            _itemDb = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            _itemDb.SetItemsEditor(new List<ItemDataSO> { _potion, _runeFire, _artifactRelic });
            _itemDb.Initialize();

            _fireballSkill = ScriptableObject.CreateInstance<SkillData>();
            _fireballSkill.skillID = "skill_fireball";
            _fireballSkill.skillName = "Fireball";
            _fireballSkill.baseDamage = 50f;
            _fireballSkill.type = SkillType.Projectile;

            _skillDb = ScriptableObject.CreateInstance<SkillDatabaseSO>();
            _skillDb.SetSkillsEditor(new List<SkillData> { _fireballSkill });
            _skillDb.Initialize();

            _heroChar = new CharacterEntry
            {
                id = "char_warrior",
                characterName = "Warrior"
            };

            _charDb = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
            _charDb.SetCharactersEditor(new List<CharacterEntry> { _heroChar });
            _charDb.Initialize();

            _masterDb = ScriptableObject.CreateInstance<MasterGameDatabaseSO>();
            _masterDb.SetSubCatalogsEditor(_itemDb, _skillDb, _charDb);
            _masterDb.InitializeAll();

            GameRegistry.SetDatabase(_masterDb);
        }

        [TearDown]
        public void TearDown()
        {
            GameRegistry.SetDatabase(null);

            if (_potion != null) Object.DestroyImmediate(_potion);
            if (_runeFire != null) Object.DestroyImmediate(_runeFire);
            if (_artifactRelic != null) Object.DestroyImmediate(_artifactRelic);
            if (_fireballSkill != null) Object.DestroyImmediate(_fireballSkill);
            if (_itemDb != null) Object.DestroyImmediate(_itemDb);
            if (_skillDb != null) Object.DestroyImmediate(_skillDb);
            if (_charDb != null) Object.DestroyImmediate(_charDb);
            if (_masterDb != null) Object.DestroyImmediate(_masterDb);
        }

        [Test]
        public void ItemDatabase_GetAndTryGet_ReturnsExpectedItem()
        {
            var item = _itemDb.Get("potion_health");
            Assert.IsNotNull(item);
            Assert.AreEqual("Health Potion", item.ItemName);

            bool found = _itemDb.TryGet("rune_fire", out var rune);
            Assert.IsTrue(found);
            Assert.IsNotNull(rune);
            Assert.AreEqual(ItemType.Rune, rune.ItemType);

            bool notFound = _itemDb.TryGet("non_existent_item", out var missing);
            Assert.IsFalse(notFound);
            Assert.IsNull(missing);
        }

        [Test]
        public void ItemDatabase_CategoryQueries_FilterCorrectly()
        {
            var runes = _itemDb.GetRunes().ToList();
            var artifacts = _itemDb.GetArtifacts().ToList();
            var consumables = _itemDb.GetConsumables().ToList();

            Assert.AreEqual(1, runes.Count);
            Assert.AreEqual("rune_fire", runes[0].Id);

            Assert.AreEqual(1, artifacts.Count);
            Assert.AreEqual("artifact_chalice", artifacts[0].Id);

            Assert.AreEqual(1, consumables.Count);
            Assert.AreEqual("potion_health", consumables[0].Id);
        }

        [Test]
        public void SkillDatabase_GetAndTryGet_ReturnsExpectedSkill()
        {
            var skill = _skillDb.Get("skill_fireball");
            Assert.IsNotNull(skill);
            Assert.AreEqual(50f, skill.baseDamage);

            bool found = _skillDb.TryGet("skill_fireball", out var queriedSkill);
            Assert.IsTrue(found);
            Assert.AreEqual("Fireball", queriedSkill.skillName);

            bool notFound = _skillDb.TryGet("non_existent_skill", out var missing);
            Assert.IsFalse(notFound);
            Assert.IsNull(missing);
        }

        [Test]
        public void CharacterDatabase_GetAndTryGet_ReturnsExpectedCharacter()
        {
            var character = _charDb.Get("char_warrior");
            Assert.IsNotNull(character);
            Assert.AreEqual("Warrior", character.characterName);

            bool notFound = _charDb.TryGet("char_dragon", out var missing);
            Assert.IsFalse(notFound);
            Assert.IsNull(missing);
        }

        [Test]
        public void GameRegistry_FacilitatesUnifiedLookup()
        {
            var item = GameRegistry.GetItem("potion_health");
            Assert.IsNotNull(item);
            Assert.AreEqual("Health Potion", item.ItemName);

            var skill = GameRegistry.GetSkill("skill_fireball");
            Assert.IsNotNull(skill);
            Assert.AreEqual(50f, skill.baseDamage);

            var character = GameRegistry.GetCharacter("char_warrior");
            Assert.IsNotNull(character);
            Assert.AreEqual("Warrior", character.characterName);

            // Verify safe missing ID fallback (does not throw)
            bool foundMissing = GameRegistry.TryGetItem("unknown_id", out var missingItem);
            Assert.IsFalse(foundMissing);
            Assert.IsNull(missingItem);
        }
    }
}
