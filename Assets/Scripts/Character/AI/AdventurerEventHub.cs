using System;
using UnityEngine;

namespace SBGames.Characters.AI
{
    public readonly struct CharacterDiedEvent
    {
        public readonly CharacterStatsManager CharacterStats;
        public readonly GameObject CharacterGameObject;
        public readonly float Timestamp;

        public CharacterDiedEvent(CharacterStatsManager characterStats, GameObject characterGameObject, float timestamp)
        {
            CharacterStats = characterStats;
            CharacterGameObject = characterGameObject;
            Timestamp = timestamp;
        }
    }

    public readonly struct LeaderDiedEvent
    {
        public readonly PartyLeader Leader;
        public readonly Vector3 Position;
        public readonly float Timestamp;

        public LeaderDiedEvent(PartyLeader leader, Vector3 position, float timestamp)
        {
            Leader = leader;
            Position = position;
            Timestamp = timestamp;
        }
    }


    public readonly struct LeaderChangedEvent
    {
        public readonly PartyLeader OldLeader;
        public readonly PartyLeader NewLeader;
        public readonly float Timestamp;

        public LeaderChangedEvent(PartyLeader oldLeader, PartyLeader newLeader, float timestamp)
        {
            OldLeader = oldLeader;
            NewLeader = newLeader;
            Timestamp = timestamp;
        }
    }

    public readonly struct CheckpointDecisionEvent
    {
        public readonly PartyLeader Leader;
        public readonly CheckpointDecision Decision;
        public readonly CheckpointReason Reason;
        public readonly float Timestamp;

        public CheckpointDecisionEvent(PartyLeader leader, CheckpointDecision decision, CheckpointReason reason, float timestamp)
        {
            Leader = leader;
            Decision = decision;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    public readonly struct RoomEnteredEvent
    {
        public readonly PartyLeader Leader;
        public readonly Dungeon.Markers.RoomVolume Room;
        public readonly int RoomId;
        public readonly float Timestamp;

        public RoomEnteredEvent(PartyLeader leader, Dungeon.Markers.RoomVolume room, int roomId, float timestamp)
        {
            Leader = leader;
            Room = room;
            RoomId = roomId;
            Timestamp = timestamp;
        }
    }

    public readonly struct RoomClearedEvent
    {
        public readonly PartyLeader Leader;
        public readonly Dungeon.Markers.RoomVolume Room;
        public readonly int RoomId;
        public readonly float Timestamp;

        public RoomClearedEvent(PartyLeader leader, Dungeon.Markers.RoomVolume room, int roomId, float timestamp)
        {
            Leader = leader;
            Room = room;
            RoomId = roomId;
            Timestamp = timestamp;
        }
    }

    public readonly struct PotionConsumedEvent
    {
        public readonly GameObject User;
        public readonly string UserName;
        public readonly ConsumableItemCategory Category;
        public readonly string ItemId;
        public readonly float Timestamp;

        public PotionConsumedEvent(GameObject user, string userName, ConsumableItemCategory category, string itemId, float timestamp)
        {
            User = user;
            UserName = userName;
            Category = category;
            ItemId = itemId;
            Timestamp = timestamp;
        }
    }

    public readonly struct BossFightStartedEvent
    {
        public readonly PartyLeader Leader;
        public readonly Dungeon.Markers.RoomVolume BossRoom;
        public readonly Dungeon.Markers.DoorMarker BossDoor;
        public readonly float Timestamp;

        public BossFightStartedEvent(PartyLeader leader, Dungeon.Markers.RoomVolume bossRoom, Dungeon.Markers.DoorMarker bossDoor, float timestamp)
        {
            Leader = leader;
            BossRoom = bossRoom;
            BossDoor = bossDoor;
            Timestamp = timestamp;
        }
    }

    public readonly struct LeaderRetreatBlockedEvent
    {
        public readonly PartyLeader Leader;
        public readonly CheckpointReason RefusalReason;
        public readonly Vector3 Position;
        public readonly float Timestamp;

        public LeaderRetreatBlockedEvent(PartyLeader leader, CheckpointReason refusalReason, Vector3 position, float timestamp)
        {
            Leader = leader;
            RefusalReason = refusalReason;
            Position = position;
            Timestamp = timestamp;
        }
    }

    public readonly struct SealActivatedEvent
    {
        public readonly Dungeon.Markers.DoorMarker SealedDoor;
        public readonly PartyLeader AffectedParty;
        public readonly float Timestamp;

        public SealActivatedEvent(Dungeon.Markers.DoorMarker sealedDoor, PartyLeader affectedParty, float timestamp)
        {
            SealedDoor = sealedDoor;
            AffectedParty = affectedParty;
            Timestamp = timestamp;
        }
    }

    public readonly struct SealReleasedEvent
    {
        public readonly Dungeon.Markers.DoorMarker ReleasedDoor;
        public readonly PartyLeader AffectedParty;
        public readonly string Reason;
        public readonly float Timestamp;

        public SealReleasedEvent(Dungeon.Markers.DoorMarker releasedDoor, PartyLeader affectedParty, string reason, float timestamp)
        {
            ReleasedDoor = releasedDoor;
            AffectedParty = affectedParty;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    public readonly struct UnitEscapedEvent
    {
        public readonly string UnitId;
        public readonly string UnitName;
        public readonly Vector3 EscapePosition;
        public readonly float Timestamp;

        public UnitEscapedEvent(string unitId, string unitName, Vector3 escapePosition, float timestamp)
        {
            UnitId = unitId;
            UnitName = unitName;
            EscapePosition = escapePosition;
            Timestamp = timestamp;
        }
    }

    public readonly struct AdventurerWarpEventData
    {
        public readonly GameObject User;
        public readonly string UserName;
        public readonly string ItemId;
        public readonly float Timestamp;

        public AdventurerWarpEventData(GameObject user, string userName, string itemId, float timestamp)
        {
            User = user;
            UserName = userName;
            ItemId = itemId;
            Timestamp = timestamp;
        }
    }

    public readonly struct AdventurerWarpForfeitEventData
    {
        public readonly GameObject User;
        public readonly string UserName;
        public readonly string ItemId;
        public readonly int EnergyAwarded;
        public readonly float Timestamp;

        public AdventurerWarpForfeitEventData(GameObject user, string userName, string itemId, int energyAwarded, float timestamp)
        {
            User = user;
            UserName = userName;
            ItemId = itemId;
            EnergyAwarded = energyAwarded;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Central zero-allocation event hub for adventurer progression, tactical orders, and DM seal interactions.
    /// Manages domain reload reset discipline via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>.
    /// </summary>
    public static class AdventurerEventHub
    {
        public static event Action<CharacterDiedEvent> OnCharacterDied;
        public static event Action<LeaderDiedEvent> OnLeaderDied;
        public static event Action<LeaderChangedEvent> OnLeaderChanged;
        public static event Action<CheckpointDecisionEvent> OnCheckpointDecision;
        public static event Action<RoomEnteredEvent> OnRoomEntered;
        public static event Action<RoomClearedEvent> OnRoomCleared;
        public static event Action<PotionConsumedEvent> OnPotionConsumed;
        public static event Action<BossFightStartedEvent> OnBossFightStarted;
        public static event Action<LeaderRetreatBlockedEvent> OnLeaderRetreatBlocked;
        public static event Action<SealActivatedEvent> OnSealActivated;
        public static event Action<SealReleasedEvent> OnSealReleased;
        public static event Action<UnitEscapedEvent> OnUnitEscaped;
        public static event Action<AdventurerWarpEventData> OnWarpStarted;
        public static event Action<AdventurerWarpEventData> OnWarpCompleted;
        public static event Action<AdventurerWarpEventData> OnWarpInterrupted;
        public static event Action<AdventurerWarpForfeitEventData> OnWarpItemForfeited;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnCharacterDied = null;
            OnLeaderDied = null;
            OnLeaderChanged = null;
            OnCheckpointDecision = null;
            OnRoomEntered = null;
            OnRoomCleared = null;
            OnPotionConsumed = null;
            OnBossFightStarted = null;
            OnLeaderRetreatBlocked = null;
            OnSealActivated = null;
            OnSealReleased = null;
            OnUnitEscaped = null;
            OnWarpStarted = null;
            OnWarpCompleted = null;
            OnWarpInterrupted = null;
            OnWarpItemForfeited = null;
        }

        public static void RaiseCharacterDied(CharacterStatsManager characterStats, GameObject characterGameObject)
        {
            OnCharacterDied?.Invoke(new CharacterDiedEvent(characterStats, characterGameObject, Time.time));
        }

        public static void RaiseLeaderDied(PartyLeader leader, Vector3 position)
        {
            OnLeaderDied?.Invoke(new LeaderDiedEvent(leader, position, Time.time));
        }

        public static void RaiseLeaderChanged(PartyLeader oldLeader, PartyLeader newLeader)
        {
            OnLeaderChanged?.Invoke(new LeaderChangedEvent(oldLeader, newLeader, Time.time));
        }

        public static void RaiseCheckpointDecision(PartyLeader leader, CheckpointDecision decision, CheckpointReason reason)
        {
            OnCheckpointDecision?.Invoke(new CheckpointDecisionEvent(leader, decision, reason, Time.time));
        }

        public static void RaiseRoomEntered(PartyLeader leader, Dungeon.Markers.RoomVolume room, int roomId)
        {
            OnRoomEntered?.Invoke(new RoomEnteredEvent(leader, room, roomId, Time.time));
        }

        public static void RaiseRoomCleared(PartyLeader leader, Dungeon.Markers.RoomVolume room, int roomId)
        {
            OnRoomCleared?.Invoke(new RoomClearedEvent(leader, room, roomId, Time.time));
        }

        public static void RaisePotionConsumed(GameObject user, string userName, ConsumableItemCategory category, string itemId)
        {
            OnPotionConsumed?.Invoke(new PotionConsumedEvent(user, userName, category, itemId, Time.time));
        }

        public static void RaiseBossFightStarted(PartyLeader leader, Dungeon.Markers.RoomVolume bossRoom, Dungeon.Markers.DoorMarker bossDoor)
        {
            OnBossFightStarted?.Invoke(new BossFightStartedEvent(leader, bossRoom, bossDoor, Time.time));
        }

        public static void RaiseLeaderRetreatBlocked(PartyLeader leader, CheckpointReason refusalReason, Vector3 position)
        {
            OnLeaderRetreatBlocked?.Invoke(new LeaderRetreatBlockedEvent(leader, refusalReason, position, Time.time));
        }

        public static void RaiseSealActivated(Dungeon.Markers.DoorMarker sealedDoor, PartyLeader affectedParty)
        {
            OnSealActivated?.Invoke(new SealActivatedEvent(sealedDoor, affectedParty, Time.time));
        }

        public static void RaiseSealReleased(Dungeon.Markers.DoorMarker releasedDoor, PartyLeader affectedParty, string reason)
        {
            OnSealReleased?.Invoke(new SealReleasedEvent(releasedDoor, affectedParty, reason, Time.time));
        }

        public static void RaiseUnitEscaped(string unitId, string unitName, Vector3 escapePosition)
        {
            OnUnitEscaped?.Invoke(new UnitEscapedEvent(unitId, unitName, escapePosition, Time.time));
        }

        public static void RaiseWarpStarted(GameObject user, string userName, string itemId)
        {
            OnWarpStarted?.Invoke(new AdventurerWarpEventData(user, userName, itemId, Time.time));
        }

        public static void RaiseWarpCompleted(GameObject user, string userName, string itemId)
        {
            OnWarpCompleted?.Invoke(new AdventurerWarpEventData(user, userName, itemId, Time.time));
        }

        public static void RaiseWarpInterrupted(GameObject user, string userName, string itemId)
        {
            OnWarpInterrupted?.Invoke(new AdventurerWarpEventData(user, userName, itemId, Time.time));
        }

        public static void RaiseWarpItemForfeited(GameObject user, string userName, string itemId, int energyAwarded)
        {
            OnWarpItemForfeited?.Invoke(new AdventurerWarpForfeitEventData(user, userName, itemId, energyAwarded, Time.time));
        }
    }
}
