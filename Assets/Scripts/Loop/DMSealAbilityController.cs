using System.Collections;
using SB4b1.Logging;
using SBGames.Characters.AI;
using SBGames.Dungeon.Markers;
using SBGames.Loop.Talents;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Auto-active Dungeon Master ability: "Dungeon Seal".
    /// Intended as a mid-tier node in the Dungeon Control / TrapControl pillar.
    /// Triggers when an adventurer party initiates a boss battle (crosses the threshold into the boss room).
    /// If unlocked and uses remain, waits a configurable delay (default 5.0s), then closes/seals the boss door,
    /// trapping any inside while locking out members outside, and marks RetreatBlocked for the whole party.
    /// Re-opens and releases the seal once the boss is slain OR all party members inside the boss room are dead.
    /// </summary>
    [DisallowMultipleComponent]
    public class DMSealAbilityController : MonoBehaviour
    {
        public const string SEAL_TALENT_ID = "DM_CONTROL_SEAL";

        [Header("System Dependencies")]
        [SerializeField] private DMTalentTreeManager talentManager;
        [SerializeField] private CombatResultHandler combatResultHandler;

        [Header("Seal Configuration")]
        [Tooltip("Delay in seconds after boss fight starts before the door seals.")]
        [SerializeField, Min(0f)] private float sealDelaySeconds = 5.0f;

        [Tooltip("Maximum number of seal activations per battle.")]
        [SerializeField, Min(1)] private int maxUsesPerBattle = 1;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private int remainingUses = 1;
        [SerializeField] private bool isSealActive = false;
        [SerializeField] private DoorMarker activeSealedDoor;
        [SerializeField] private PartyLeader activeParty;
        [SerializeField] private RoomVolume activeBossRoom;

        private Coroutine sealSequenceCoroutine;

        public bool IsSealActive => isSealActive;
        public int RemainingUses => remainingUses;
        public DoorMarker ActiveSealedDoor => activeSealedDoor;

        private void Awake()
        {
            if (talentManager == null) talentManager = FindAnyObjectByType<DMTalentTreeManager>();
            if (combatResultHandler == null) combatResultHandler = FindAnyObjectByType<CombatResultHandler>();
        }

        public void Initialize(DMTalentTreeManager tm, CombatResultHandler crh)
        {
            if (combatResultHandler != null)
            {
                combatResultHandler.OnCombatReset -= HandleCombatReset;
                combatResultHandler.OnBossHealthChanged -= HandleBossHealthChanged;
            }

            talentManager = tm;
            combatResultHandler = crh;

            if (combatResultHandler != null && enabled)
            {
                combatResultHandler.OnCombatReset += HandleCombatReset;
                combatResultHandler.OnBossHealthChanged += HandleBossHealthChanged;
            }
        }

        private void OnEnable()
        {
            AdventurerEventHub.OnBossFightStarted += HandleBossFightStarted;
            AdventurerEventHub.OnLeaderDied += HandleLeaderDied;
            if (combatResultHandler != null)
            {
                combatResultHandler.OnCombatReset += HandleCombatReset;
                combatResultHandler.OnBossHealthChanged += HandleBossHealthChanged;
            }
        }

        private void OnDisable()
        {
            AdventurerEventHub.OnBossFightStarted -= HandleBossFightStarted;
            AdventurerEventHub.OnLeaderDied -= HandleLeaderDied;
            if (combatResultHandler != null)
            {
                combatResultHandler.OnCombatReset -= HandleCombatReset;
                combatResultHandler.OnBossHealthChanged -= HandleBossHealthChanged;
            }
            if (sealSequenceCoroutine != null)
            {
                StopCoroutine(sealSequenceCoroutine);
                sealSequenceCoroutine = null;
            }
            if (isSealActive)
            {
                ReleaseSeal("ControllerDisabled");
            }
        }

        public void ResetUses()
        {
            remainingUses = maxUsesPerBattle;
            isSealActive = false;
            activeSealedDoor = null;
            activeParty = null;
            activeBossRoom = null;
        }

        private void HandleCombatReset()
        {
            ResetUses();
        }

        private void HandleBossHealthChanged(float curr, float max)
        {
            if (isSealActive && curr <= 0f)
            {
                ReleaseSeal("BossSlain");
            }
        }

        private void HandleLeaderDied(LeaderDiedEvent evt)
        {
            if (isSealActive && AreAllInteriorPartyMembersDead())
            {
                ReleaseSeal("InteriorPartyWiped");
            }
        }

        public void TriggerImmediateSealForTesting(DoorMarker door = null, PartyLeader party = null, RoomVolume bossRoom = null)
        {
            if (sealSequenceCoroutine != null)
            {
                StopCoroutine(sealSequenceCoroutine);
                sealSequenceCoroutine = null;
            }

            if (door != null) activeSealedDoor = door;
            if (party != null) activeParty = party;
            if (bossRoom != null) activeBossRoom = bossRoom;

            remainingUses--;
            isSealActive = true;

            if (activeSealedDoor != null)
            {
                activeSealedDoor.Close();
            }

            if (activeParty != null)
            {
                activeParty.RetreatBlocked = true;
            }

            AdventurerEventHub.RaiseSealActivated(activeSealedDoor, activeParty);
        }

        public bool EvaluateSealReleaseForTesting()
        {
            if (!isSealActive) return false;
            bool bossDead = (combatResultHandler != null && !combatResultHandler.IsBossAlive);
            bool interiorPartyWiped = AreAllInteriorPartyMembersDead();

            if (bossDead || interiorPartyWiped)
            {
                string reason = bossDead ? "BossSlain" : "InteriorPartyWiped";
                ReleaseSeal(reason);
                return true;
            }
            return false;
        }

        private void HandleBossFightStarted(BossFightStartedEvent evt)
        {
            if (isSealActive || remainingUses <= 0) return;

            // Check if DM has unlocked the Seal ability in the talent tree
            bool isUnlocked = talentManager != null && talentManager.IsUnlocked(SEAL_TALENT_ID);
            if (!isUnlocked)
            {
                // If DM does not have the ability, nothing happens
                return;
            }

            activeParty = evt.Leader;
            activeBossRoom = evt.BossRoom;
            activeSealedDoor = evt.BossDoor;

            // If no door provided directly by event, look for an exit from the boss room that is marked IsBossDoor
            if (activeSealedDoor == null && activeBossRoom != null)
            {
                for (int i = 0; i < activeBossRoom.Exits.Count; i++)
                {
                    if (activeBossRoom.Exits[i].IsBossDoor)
                    {
                        activeSealedDoor = activeBossRoom.Exits[i];
                        break;
                    }
                }
            }

            if (activeSealedDoor == null)
            {
                Debug.LogWarning("[DMSealAbilityController] Cannot activate seal: No boss door found!");
                return;
            }

            sealSequenceCoroutine = StartCoroutine(SealSequenceRoutine(activeSealedDoor, activeParty));
        }

        private IEnumerator SealSequenceRoutine(DoorMarker door, PartyLeader party)
        {
            ELogger.Log("DMSeal", this, $"Seal countdown started: {sealDelaySeconds:F1}s");

            if (sealDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(sealDelaySeconds);
            }

            // Consume one use
            remainingUses--;
            isSealActive = true;

            // 1. Physically & visually close door
            door.Close();

            // 2. Set RetreatBlocked for the whole party
            if (party != null)
            {
                party.RetreatBlocked = true;
            }

            ELogger.Log("DMSeal", this, "Seal Activated. Boss door closed and party retreat blocked.");
            AdventurerEventHub.RaiseSealActivated(door, party);

            // 3. Monitor until conditions to open are satisfied:
            // Boss is slain OR all party members inside the boss room are dead
            while (isSealActive)
            {
                yield return new WaitForSeconds(0.5f);

                bool bossDead = (combatResultHandler != null && !combatResultHandler.IsBossAlive);
                bool interiorPartyWiped = AreAllInteriorPartyMembersDead();

                if (bossDead || interiorPartyWiped)
                {
                    string reason = bossDead ? "BossSlain" : "InteriorPartyWiped";
                    ReleaseSeal(reason);
                    break;
                }
            }

            sealSequenceCoroutine = null;
        }

        private bool AreAllInteriorPartyMembersDead()
        {
            if (activeBossRoom == null || activeParty == null) return true;

            // Check leader
            if (activeParty.gameObject.activeInHierarchy && activeParty.IsAlive && !activeParty.IsEscaped && activeBossRoom.Contains(activeParty.transform.position))
            {
                return false;
            }

            // Check members
            var members = activeParty.Members;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m != null && m.gameObject.activeInHierarchy && m.IsAlive && !m.IsEscaped && activeBossRoom.Contains(m.transform.position))
                {
                    return false;
                }
            }

            return true;
        }

        public void ReleaseSeal(string reason)
        {
            if (!isSealActive) return;

            isSealActive = false;

            if (activeSealedDoor != null)
            {
                activeSealedDoor.Open();
            }

            if (activeParty != null)
            {
                activeParty.RetreatBlocked = false;
            }

            ELogger.Log("DMSeal", this, $"Seal Released. Reason: {reason}");
            AdventurerEventHub.RaiseSealReleased(activeSealedDoor, activeParty, reason);

            // Survivors then decide normally through the Phase 4 checkpoint
            if (activeParty != null && activeParty.IsAlive)
            {
                activeParty.RequestCheckpoint();
            }
        }
    }
}
