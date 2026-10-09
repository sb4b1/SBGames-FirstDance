using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Shop.Core;
using SBGames.Shop.Data;
using SBGames.Shop.Mock;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class SkillsAndShopTests
    {
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnedObjects != null)
            {
                for (int i = 0; i < _spawnedObjects.Count; i++)
                {
                    if (_spawnedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_spawnedObjects[i]);
                    }
                }
                _spawnedObjects.Clear();
            }
        }

        private GameObject Track(GameObject go)
        {
            _spawnedObjects.Add(go);
            return go;
        }

        private class MockDamageable : MonoBehaviour, IDamageable
        {
            public float LastDamageTaken { get; private set; }
            public DamageType LastDamageType { get; private set; }

            public bool TakeDamage(in DamageData damage)
            {
                LastDamageTaken = damage.Amount;
                LastDamageType = damage.Type;
                return false;
            }

            public void TakeDamage(float damage)
            {
                TakeDamage(new DamageData(damage, DamageType.Physical));
            }

            public void ForceDeath()
            {
            }
        }

        [Test]
        public void SkillInstance_OnDespawn_ResetsWithoutRecursion()
        {
            var skillGo = Track(new GameObject("Test_SkillInstance"));
            var instance = skillGo.AddComponent<SkillInstance>();

            // Simulate initialized state
            instance.CurrentDamage = 50f;
            instance.OnSpawn();

            // Calling OnDespawn must reset state cleanly without throwing or looping
            instance.OnDespawn();

            Assert.AreEqual(0f, instance.CurrentDamage);
            Assert.IsNull(instance.Data);
            Assert.IsNull(instance.Stats);
            Assert.AreEqual(0, instance.RemainingChains);
        }

        [Test]
        public void SkillHitResolver_NullSkillData_DoesNotThrowNRE()
        {
            var targetGo = Track(new GameObject("Test_Target"));
            var mockDamageable = targetGo.AddComponent<MockDamageable>();

            var skillGo = Track(new GameObject("Test_Skill"));
            var resolver = skillGo.AddComponent<SkillHitResolver>();
            var instance = skillGo.AddComponent<SkillInstance>();
            instance.CurrentDamage = 42f;

            // Resolve hit with no SkillData attached to instance
            Assert.DoesNotThrow(() => resolver.ResolveHit(instance, targetGo));
            Assert.AreEqual(42f, mockDamageable.LastDamageTaken);
            Assert.AreEqual(DamageType.Physical, mockDamageable.LastDamageType);
        }

        [Test]
        public void ShopConfig_Probabilities_ReturnsCachedInstances()
        {
            var config = new ShopConfig();

            var rankDict1 = config.GetRankProbabilities(1, DungeonRank.E);
            var rankDict2 = config.GetRankProbabilities(1, DungeonRank.E);
            Assert.AreSame(rankDict1, rankDict2, "GetRankProbabilities must return cached dictionary instances.");

            var lvlDict1 = config.GetLevelProbabilities(1, DungeonRank.E);
            var lvlDict2 = config.GetLevelProbabilities(1, DungeonRank.E);
            Assert.AreSame(lvlDict1, lvlDict2, "GetLevelProbabilities must return cached dictionary instances.");
        }

        [Test]
        public void MonsterShopSystem_RefreshAndLock_OperatesDeterministically()
        {
            var catalog = new MockMonsterCatalog();
            var wallet = new ResourceWallet(initialResources: 100);
            var config = new ShopConfig(slotCount: 3, manualRerollCost: 2);
            var shop = new MonsterShopSystem(catalog, wallet, config);

            bool refreshed = shop.TryRefresh(1, DungeonRank.E, isManualReroll: false, out var res);
            Assert.IsTrue(refreshed);
            Assert.AreEqual(3, shop.Slots.Count);

            // Toggle lock on slot 0
            bool locked = shop.TryToggleLock(0, out bool isLocked, out _);
            Assert.IsTrue(locked);
            Assert.IsTrue(isLocked);
            Assert.IsTrue(shop.Slots[0].IsLocked);

            // Manual reroll deducts currency and preserves locked slot
            string lockedItemName = shop.Slots[0].CurrentItem.DisplayName;
            bool manualRefreshed = shop.TryRefresh(1, DungeonRank.E, isManualReroll: true, out _);
            Assert.IsTrue(manualRefreshed);
            Assert.AreEqual(98, wallet.CurrentResources, "Manual reroll should deduct 2 currency.");
            Assert.AreEqual(lockedItemName, shop.Slots[0].CurrentItem.DisplayName, "Locked slot should be preserved.");
        }
    }
}
