using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Inventory.Save
{
    /// <summary>
    /// Lightweight Data Transfer Object (DTO) for persisting inventory state and character equipment into JSON.
    /// Stores slot states, upgrade tiers, and hero equipment profiles.
    /// </summary>
    [Serializable]
    public class InventorySaveData
    {
        [SerializeField] public int capacity;
        [SerializeField] public List<InventorySlotSaveDTO> slots = new();
        [SerializeField] public List<CharacterEquipmentSaveDTO> characterEquipments = new();
        [SerializeField] public long timestamp;

        public InventorySaveData()
        {
            capacity = 24;
            slots = new List<InventorySlotSaveDTO>();
            characterEquipments = new List<CharacterEquipmentSaveDTO>();
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public InventorySaveData(int capacity)
        {
            this.capacity = capacity;
            slots = new List<InventorySlotSaveDTO>();
            characterEquipments = new List<CharacterEquipmentSaveDTO>();
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Serializes this DTO to a JSON string.
        /// </summary>
        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        /// <summary>
        /// Deserializes JSON string into an InventorySaveData DTO.
        /// </summary>
        public static InventorySaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<InventorySaveData>(json);
        }
    }

    /// <summary>
    /// Serialization DTO for an individual inventory slot.
    /// </summary>
    [Serializable]
    public class InventorySlotSaveDTO
    {
        [SerializeField] public int slotIndex;
        [SerializeField] public string instanceID;
        [SerializeField] public string itemID;
        [SerializeField] public int quantity;
        [SerializeField] public int upgradeTier;

        public InventorySlotSaveDTO() { }

        public InventorySlotSaveDTO(int slotIndex, string instanceID, string itemID, int quantity, int upgradeTier)
        {
            this.slotIndex = slotIndex;
            this.instanceID = instanceID;
            this.itemID = itemID;
            this.quantity = quantity;
            this.upgradeTier = upgradeTier;
        }
    }

    /// <summary>
    /// Serialization DTO for a character's equipped items (Artifact & Inscribed Runes).
    /// </summary>
    [Serializable]
    public class CharacterEquipmentSaveDTO
    {
        [SerializeField] public string characterID;
        [SerializeField] public string artifactItemID;
        [SerializeField] public List<string> inscribedRuneIDs = new();

        public CharacterEquipmentSaveDTO() { }

        public CharacterEquipmentSaveDTO(string characterID, string artifactItemID, IEnumerable<string> runeIDs = null)
        {
            this.characterID = characterID;
            this.artifactItemID = artifactItemID;
            if (runeIDs != null)
            {
                inscribedRuneIDs.AddRange(runeIDs);
            }
        }
    }
}
