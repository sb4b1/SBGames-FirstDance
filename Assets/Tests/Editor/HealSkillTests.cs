using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Character;
using SBGames.Character.AI;
using SBGames.Characters.AI;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class HealSkillTests
    {
        private GameObject _casterGo;
        private GameObject _targetGo;
        private GameObject _otherAllyGo;
        private GameObject _enemyGo;

        private CharacterStatsManager _casterStats;
        private CharacterStatsManager _targetStats;
        private CharacterStatsManager _otherAllyStats;
        private CharacterStatsManager _enemyStats;

        private BaseAI _casterAi;
        private BaseAI _targetAi;
        private BaseAI _otherAllyAi;

        private SkillData _healSkill;

        [SetUp]
        public void SetUp()
        {
            _casterGo = new GameObject("Caster");
            _targetGo = new GameObject("Target");
            _otherAllyGo = new GameObject("OtherAlly");
            _enemyGo = new GameObject("Enemy");

            _casterGo.tag = "Adventurer";
            _targetGo.tag = "Adventurer";
            _otherAllyGo.tag = "Adventurer";
            _enemyGo.tag = "Monster";

            var casterFaction = _casterGo.AddComponent<FactionMember>();
            casterFaction.Faction = FactionId.Adventurer;
            var targetFaction = _targetGo.AddComponent<FactionMember>();
            targetFaction.Faction = FactionId.Adventurer;
            var otherAllyFaction = _otherAllyGo.AddComponent<FactionMember>();
            otherAllyFaction.Faction = FactionId.Adventurer;
            var enemyFaction = _enemyGo.AddComponent<FactionMember>();
            enemyFaction.Faction = FactionId.Monster;

            _casterGo.AddComponent<CharacterBrain>();
            _targetGo.AddComponent<CharacterBrain>();
            _otherAllyGo.AddComponent<CharacterBrain>();
            _enemyGo.AddComponent<CharacterBrain>();

            _casterStats = _casterGo.GetComponent<CharacterStatsManager>();
            _targetStats = _targetGo.GetComponent<CharacterStatsManager>();
            _otherAllyStats = _otherAllyGo.GetComponent<CharacterStatsManager>();
            _enemyStats = _enemyGo.GetComponent<CharacterStatsManager>();

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.MaxLife = 100f;
            baseStat.MaxMana = 100f;
            baseStat.LifeRegen = 0f;
            baseStat.ManaRegen = 0f;

            _casterStats.SetBaseStats(baseStat);
            _targetStats.SetBaseStats(baseStat);
            _otherAllyStats.SetBaseStats(baseStat);
            _enemyStats.SetBaseStats(baseStat);

            _casterAi = _casterGo.AddComponent<BaseAI>();
            _targetAi = _targetGo.AddComponent<BaseAI>();
            _otherAllyAi = _otherAllyGo.AddComponent<BaseAI>();

            _healSkill = ScriptableObject.CreateInstance<SkillData>();
            _healSkill.skillID = "SKILL_HEAL_TEST";
            _healSkill.targetFilter = SkillData.SkillTargetFilter.AlliesOnly;
            _healSkill.type = SkillType.TargetedBurst;
            _healSkill.baseHeal = 25f;
            _healSkill.windupDuration = 0.2f;
            _healSkill.selfTarget = SkillData.SelfTarget.Allowed;
        }

        [TearDown]
        public void TearDown()
        {
            if (_casterGo != null) Object.DestroyImmediate(_casterGo);
            if (_targetGo != null) Object.DestroyImmediate(_targetGo);
            if (_otherAllyGo != null) Object.DestroyImmediate(_otherAllyGo);
            if (_enemyGo != null) Object.DestroyImmediate(_enemyGo);
            if (_healSkill != null) Object.DestroyImmediate(_healSkill);
        }

        [Test]
        public void ApplyHeal_ClampsToMaxLife()
        {
            _targetStats.TakeDamage(new DamageData(20f, DamageType.True, null));
            Assert.AreEqual(80f, _targetStats.Life, 0.01f);

            bool success = _targetStats.ApplyHeal(50f, out float actualHealed, out float overheal);
            Assert.IsTrue(success);
            Assert.AreEqual(100f, _targetStats.Life, 0.01f);
            Assert.AreEqual(20f, actualHealed, 0.01f);
            Assert.AreEqual(30f, overheal, 0.01f);
        }

        [Test]
        public void ApplyHeal_IgnoresDeadTargets()
        {
            _targetStats.TakeDamage(new DamageData(150f, DamageType.True, null));
            Assert.AreEqual(0f, _targetStats.Life, 0.01f);

            bool success = _targetStats.ApplyHeal(50f, out float actualHealed, out float overheal);
            Assert.IsFalse(success);
            Assert.AreEqual(0f, _targetStats.Life, 0.01f);
            Assert.AreEqual(0f, actualHealed, 0.01f);
            Assert.AreEqual(0f, overheal, 0.01f);
        }

        [Test]
        public void ApplyHeal_ReportsOverhealCorrectly()
        {
            _targetStats.TakeDamage(new DamageData(10f, DamageType.True, null));
            Assert.AreEqual(90f, _targetStats.Life, 0.01f);

            _targetStats.ApplyHeal(40f, out float actualHealed, out float overheal);
            Assert.AreEqual(10f, actualHealed, 0.01f);
            Assert.AreEqual(30f, overheal, 0.01f);
        }

        [Test]
        public void TargetedBurst_DoesNotApplyHeal_BeforeBurstMoment()
        {
            _targetStats.TakeDamage(new DamageData(50f, DamageType.True, null));
            Assert.AreEqual(50f, _targetStats.Life, 0.01f);

            var fxGo = new GameObject("FX");
            var resolver = fxGo.AddComponent<SkillHitResolver>();
            var instance = fxGo.AddComponent<SkillInstance>();
            var burst = fxGo.AddComponent<TargetedBurstDetonation>();

            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            Assert.AreEqual(0.2f, burst.WindupDuration, 0.001f);
            Assert.IsFalse(burst.HasBurst);
            Assert.AreEqual(50f, _targetStats.Life, 0.01f, "Life should not change before burst duration elapsed");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void TargetedBurst_AppliesHealAtBurstMoment_AndFizzlesOnDeadTarget()
        {
            _targetStats.TakeDamage(new DamageData(150f, DamageType.True, null));
            Assert.AreEqual(0f, _targetStats.Life, 0.01f);

            var fxGo = new GameObject("FX");
            var resolver = fxGo.AddComponent<SkillHitResolver>();
            var instance = fxGo.AddComponent<SkillInstance>();
            var burst = fxGo.AddComponent<TargetedBurstDetonation>();

            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            // Trigger burst directly
            burst.TriggerBurst();
            Assert.AreEqual(0f, _targetStats.Life, 0.01f, "Dead target must not be healed");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void TargetedBurst_HealsOnlyChosenTarget_NeverNearbyAlliesOrEnemies()
        {
            _targetStats.TakeDamage(new DamageData(50f, DamageType.True, null));
            _otherAllyStats.TakeDamage(new DamageData(50f, DamageType.True, null));
            _enemyStats.TakeDamage(new DamageData(50f, DamageType.True, null));

            // Position them close together
            _targetGo.transform.position = Vector3.zero;
            _otherAllyGo.transform.position = new Vector3(0.1f, 0f, 0f);
            _enemyGo.transform.position = new Vector3(0.2f, 0f, 0f);

            var fxGo = new GameObject("FX");
            var resolver = fxGo.AddComponent<SkillHitResolver>();
            var instance = fxGo.AddComponent<SkillInstance>();
            var burst = fxGo.AddComponent<TargetedBurstDetonation>();

            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            burst.TriggerBurst();

            Assert.AreEqual(75f, _targetStats.Life, 0.01f, "Target should receive 25 heal");
            Assert.AreEqual(50f, _otherAllyStats.Life, 0.01f, "Other ally should NOT receive heal");
            Assert.AreEqual(50f, _enemyStats.Life, 0.01f, "Enemy should NOT receive heal");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void SelfTarget_Allowed_ValidatesBothSelfAndAlly()
        {
            _healSkill.selfTarget = SkillData.SelfTarget.Allowed;
            var fxGo = new GameObject("FX");
            var instance = fxGo.AddComponent<SkillInstance>();
            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            Assert.IsTrue(instance.CanHitTarget(_casterGo, HitContext.Direct), "Allowed should permit self");
            Assert.IsTrue(instance.CanHitTarget(_targetGo, HitContext.Direct), "Allowed should permit ally");
            Assert.IsFalse(instance.CanHitTarget(_enemyGo, HitContext.Direct), "Allowed should reject enemy");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void SelfTarget_Excluded_RejectsSelf_PermitsAlly()
        {
            _healSkill.selfTarget = SkillData.SelfTarget.Excluded;
            var fxGo = new GameObject("FX");
            var instance = fxGo.AddComponent<SkillInstance>();
            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            Assert.IsFalse(instance.CanHitTarget(_casterGo, HitContext.Direct), "Excluded must reject self");
            Assert.IsTrue(instance.CanHitTarget(_targetGo, HitContext.Direct), "Excluded must permit ally");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void SelfTarget_Only_RejectsAlly_PermitsSelf()
        {
            _healSkill.selfTarget = SkillData.SelfTarget.Only;
            var fxGo = new GameObject("FX");
            var instance = fxGo.AddComponent<SkillInstance>();
            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            Assert.IsTrue(instance.CanHitTarget(_casterGo, HitContext.Direct), "Only must permit self");
            Assert.IsFalse(instance.CanHitTarget(_targetGo, HitContext.Direct), "Only must reject ally");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void GetBestWoundedAllyTarget_Respects_SelfTargetSetting()
        {
            // Setup party relationship
            var leader = _casterGo.AddComponent<PartyLeader>();
            var targetMember = _targetGo.AddComponent<SquadMember>();
            leader.RegisterMember(targetMember);

            // Caster hurt, ally hurt (threshold defaults to 0.50f)
            _casterStats.TakeDamage(new DamageData(70f, DamageType.True, null)); // life 30 (30% < 50%)
            _targetStats.TakeDamage(new DamageData(60f, DamageType.True, null)); // life 40 (40% < 50%)

            // With Excluded, caster should never be picked even if more wounded
            bool foundExcluded = _casterAi.GetBestWoundedAllyTarget(out Transform candidateExcluded, out _, SkillData.SelfTarget.Excluded);
            Assert.IsTrue(foundExcluded);
            Assert.AreEqual(_targetGo.transform, candidateExcluded, "Excluded must pick wounded ally, never caster");

            // With Only, ally should never be picked
            bool foundOnly = _casterAi.GetBestWoundedAllyTarget(out Transform candidateOnly, out _, SkillData.SelfTarget.Only);
            Assert.IsTrue(foundOnly);
            Assert.AreEqual(_casterGo.transform, candidateOnly, "Only must pick caster, never ally");
        }

        [Test]
        public void TargetedBurst_Revalidation_FizzlesIfTargetBecomesEnemyOrUnqualified()
        {
            _targetStats.TakeDamage(new DamageData(50f, DamageType.True, null));

            var fxGo = new GameObject("FX");
            var resolver = fxGo.AddComponent<SkillHitResolver>();
            var instance = fxGo.AddComponent<SkillInstance>();
            var burst = fxGo.AddComponent<TargetedBurstDetonation>();

            instance.Initialize(_healSkill, Vector3.right, "Caster", _targetGo, _casterGo);

            // Target switches faction before burst triggers
            _targetGo.GetComponent<FactionMember>().Faction = FactionId.Monster;
            _targetGo.tag = "Monster";

            burst.TriggerBurst();

            Assert.AreEqual(50f, _targetStats.Life, 0.01f, "Fizzled heal must not restore health to invalid faction");

            Object.DestroyImmediate(fxGo);
        }

        [Test]
        public void ApplyHeal_FullHealth_ReturnsOverhealWithoutLifeNotification()
        {
            int notifications = 0;
            _targetStats.OnLifeChanged += (life, max) => notifications++;
            Assert.IsTrue(_targetStats.ApplyHeal(25f, out float actual, out float excess));
            Assert.AreEqual(0f, actual);
            Assert.AreEqual(25f, excess);
            Assert.AreEqual(0, notifications);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TargetedBurst_CasterLostDuringWindup_StillHealsAndEmitsBothEvents(int mode)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "[Target (CharacterStatsManager)/TakeDamage] Target took <color=red>5</color> True damage");
            _targetStats.TakeDamage(new DamageData(5f, DamageType.True));
            var fx = new GameObject("CommittedHeal");
            var skill = fx.AddComponent<SkillInstance>();
            var burst = fx.AddComponent<TargetedBurstDetonation>();
            int oldEvents = 0, newEvents = 0;
            System.Action<SkillInstance, GameObject, float> oldHandler = (source, target, actual) =>
            {
                Assert.AreEqual(5f, actual);
                oldEvents++;
            };
            System.Action<SkillInstance, GameObject, float, float> newHandler = (source, target, actual, excess) =>
            {
                Assert.AreEqual(5f, actual);
                Assert.AreEqual(20f, excess);
                newEvents++;
            };
            SkillHitResolver.OnSkillHealApplied += oldHandler;
            SkillHitResolver.OnSkillHealWithOverhealApplied += newHandler;
            try
            {
                skill.Initialize(_healSkill, Vector3.right, "caster", _targetGo, _casterGo);
                if (mode == 0)
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "[Caster (CharacterStatsManager)/OnDeath] Caster died");
                    _casterStats.ForceDeath();
                }
                if (mode == 1) _casterGo.SetActive(false);
                if (mode == 2)
                {
                    _casterGo.SetActive(false);
                    _casterGo.GetComponent<FactionMember>().Faction = FactionId.Monster;
                    _casterGo.tag = "Monster";
                    _casterGo.SetActive(true);
                }
                if (mode == 3) Object.DestroyImmediate(_casterGo);
                burst.TriggerBurst();
                Assert.AreEqual(100f, _targetStats.Life);
                Assert.AreEqual(1, oldEvents);
                Assert.AreEqual(1, newEvents);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                SkillHitResolver.OnSkillHealApplied -= oldHandler;
                SkillHitResolver.OnSkillHealWithOverhealApplied -= newHandler;
                Object.DestroyImmediate(fx);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TargetedBurst_TargetPooledDuringWindup_FizzlesEvenAfterReuse(bool reuse)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "[Target (CharacterStatsManager)/TakeDamage] Target took <color=red>50</color> True damage");
            _targetStats.TakeDamage(new DamageData(50f, DamageType.True));
            var fx = new GameObject("FizzledHeal");
            var skill = fx.AddComponent<SkillInstance>();
            var burst = fx.AddComponent<TargetedBurstDetonation>();
            try
            {
                skill.Initialize(_healSkill, Vector3.right, "caster", _targetGo, _casterGo);
                _targetGo.SetActive(false);
                // EditMode does not dispatch non-ExecuteAlways lifecycle callbacks.
                typeof(TargetedBurstTargetLifetime).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_targetGo.GetComponent<TargetedBurstTargetLifetime>(), null);
                if (reuse) _targetGo.SetActive(true);
                burst.TriggerBurst();
                Assert.AreEqual(50f, _targetStats.Life);
                Assert.IsFalse(fx.activeSelf);
                Assert.IsNull(fx.transform.parent);
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(fx); }
        }
    }
}
