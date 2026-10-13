using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using SBGames.Loop.Talents;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Represents an artifact possessed by the Dungeon Master.
    /// Artifacts grant passive potency or abilities and act as anchors to the DM's existence.
    /// When unempowered, artifacts apply their modifiers locally to the equipping unit.
    /// When empowered, their modifiers become global across all friendly units and gain the 'Empowered ' prefix.
    /// If all artifacts are lost or destroyed, the DM is defeated.
    /// </summary>
    [Serializable]
    public class Artifact
    {
        [SerializeField] private string id;
        [SerializeField] private string itemDefinitionId;
        [SerializeField] private string sourceInstanceId;
        [SerializeField] private string artifactName;
        [SerializeField] private int powerLevel;
        [SerializeField] private int essenceCost;
        [SerializeField] private bool isEmpowered;
        [SerializeField] private string locationBadge;
        [SerializeField] private List<ItemStatModifierData> statModifiers = new();

        public string Id => id;
        public string ItemDefinitionId => string.IsNullOrEmpty(itemDefinitionId) ? id : itemDefinitionId;
        public string SourceInstanceId => string.IsNullOrEmpty(sourceInstanceId) ? id : sourceInstanceId;
        public string ArtifactName => artifactName;
        public int PowerLevel => powerLevel;
        public int EssenceCost => essenceCost;
        public bool IsEmpowered => isEmpowered;
        public string LocationBadge => string.IsNullOrEmpty(locationBadge) ? "[Inventory]" : locationBadge;
        public IReadOnlyList<ItemStatModifierData> StatModifiers => statModifiers;

        public Artifact(
            string artifactName,
            int powerLevel = 1,
            int essenceCost = 0,
            string id = null,
            bool isEmpowered = false,
            IEnumerable<ItemStatModifierData> modifiers = null,
            string itemDefinitionId = null,
            string sourceInstanceId = null,
            string locationBadge = null)
        {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            this.itemDefinitionId = itemDefinitionId ?? this.id;
            this.sourceInstanceId = sourceInstanceId ?? this.id;
            this.locationBadge = locationBadge ?? "[Inventory]";
            this.artifactName = artifactName;
            this.powerLevel = Mathf.Max(1, powerLevel);
            this.essenceCost = Mathf.Max(0, essenceCost);
            this.isEmpowered = isEmpowered;
            if (modifiers != null)
            {
                this.statModifiers.AddRange(modifiers);
            }
            if (isEmpowered && !string.IsNullOrEmpty(this.artifactName) && !this.artifactName.StartsWith("Empowered ", StringComparison.OrdinalIgnoreCase))
            {
                this.artifactName = "Empowered " + this.artifactName;
            }
        }

        public void SetLocationBadge(string badge)
        {
            locationBadge = badge;
        }

        public void SetSourceInstanceId(string instId)
        {
            sourceInstanceId = instId;
        }

        public void SetItemDefinitionId(string defId)
        {
            itemDefinitionId = defId;
        }

        public void IncreasePower(int amount)
        {
            if (amount <= 0) return;
            powerLevel += amount;
        }

        public void AddModifier(ItemStatModifierData modifier)
        {
            statModifiers.Add(modifier);
        }

        public void Empower(int powerIncrease = 1)
        {
            if (!isEmpowered)
            {
                isEmpowered = true;
                if (!string.IsNullOrEmpty(artifactName) && !artifactName.StartsWith("Empowered ", StringComparison.OrdinalIgnoreCase))
                {
                    artifactName = "Empowered " + artifactName;
                }
            }

            if (powerIncrease > 0)
            {
                IncreasePower(powerIncrease);
            }
        }

        public void Unempower()
        {
            if (!isEmpowered) return;

            isEmpowered = false;
            if (!string.IsNullOrEmpty(artifactName) && artifactName.StartsWith("Empowered ", StringComparison.OrdinalIgnoreCase))
            {
                artifactName = artifactName.Substring("Empowered ".Length).Trim();
            }
        }
    }

    /// <summary>
    /// Reason for Dungeon Master defeat.
    /// </summary>
    public enum DefeatReason
    {
        None = 0,
        EssenceDepleted = 1,
        ArtifactsLost = 2
    }

    /// <summary>
    /// Persistent data container and resource management system for the Dungeon Master (player).
    /// Tracks vital essence (health), magical energy (currency), and artifact collection.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonMaster : MonoBehaviour
    {
        [Header("Vitals (Health)")]
        [Tooltip("Maximum Essence the Dungeon Master can hold.")]
        [SerializeField] private int maxEssence = 100;

        [Tooltip("Current Essence. If this drops to 0, Game Over is triggered.")]
        [SerializeField] private int currentEssence = 100;

        [Header("Energy (Currency)")]
        [Tooltip("Maximum Energy capacity.")]
        [SerializeField] private int maxEnergy = 100;

        [Tooltip("Current Energy used to purchase monsters, empower artifacts, and recover essence.")]
        [SerializeField] private int currentEnergy = 50;

        [Header("Round Penalties & Defaults")]
        [Tooltip("Essence deducted when adventurers breach the dungeon defense during a round loss.")]
        [SerializeField] private int roundLossEssencePenalty = 25;

        [Tooltip("Default Energy cost required to recover Essence.")]
        [SerializeField] private int essenceRecoveryEnergyCost = 20;

        [Tooltip("Amount of Essence restored per recovery action.")]
        [SerializeField] private int essenceRecoveryAmount = 15;

        [Tooltip("Base Energy gained when sacrificing an artifact.")]
        [SerializeField] private int baseArtifactSacrificeEnergyReward = 25;

        [Header("Artifacts")]
        [Tooltip("Collection of artifacts bound to the Dungeon Master. If this becomes empty, Game Over is triggered.")]
        [SerializeField] private List<Artifact> artifacts = new List<Artifact>();

        [Header("Talents & Abilities")]
        [Tooltip("Reference to the DM Talent Tree Manager for talent-locked abilities.")]
        [SerializeField] private DMTalentTreeManager talentTreeManager;
        [SerializeField] private string multiEmpowerTalentId = "DM_ARTIFACT_OVERCHARGE";

        // Events
        public event Action<int, int> OnEssenceChanged;
        public event Action<int, int> OnEnergyChanged;
        public event Action<Artifact> OnArtifactForged;
        public event Action<Artifact> OnArtifactEmpowered;
        public event Action<Artifact> OnArtifactUnempowered;
        public event Action<Artifact> OnArtifactRemoved;
        public event Action<Artifact, int> OnArtifactSacrificed;
        public event Action OnInventorySynced;
        public event Action<DefeatReason> OnDefeated;

        // Properties
        public int Essence => currentEssence;
        public int MaxEssence => maxEssence;
        public int Energy => currentEnergy;
        public int MaxEnergy => maxEnergy;
        public int RoundLossEssencePenalty => roundLossEssencePenalty;
        public IReadOnlyList<Artifact> Artifacts => artifacts;
        public int ArtifactCount => artifacts.Count;
        public bool IsDefeated => currentEssence <= 0 || artifacts.Count == 0;
        public DMTalentTreeManager TalentTreeManager { get => talentTreeManager; set => talentTreeManager = value; }
        public string MultiEmpowerTalentId { get => multiEmpowerTalentId; set => multiEmpowerTalentId = value; }

        private static DungeonMaster instance;
        public static DungeonMaster Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DungeonMaster>();
                }
                return instance;
            }
            set => instance = value;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            ClampValues();
            if (talentTreeManager == null)
            {
                talentTreeManager = GetComponent<DMTalentTreeManager>() ?? FindAnyObjectByType<DMTalentTreeManager>();
            }
            if (artifacts == null)
            {
                artifacts = new List<Artifact>();
            }
        }

        private void Start()
        {
            HookInventoryEvents();
            SyncWithInventory();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            UnhookInventoryEvents();
        }

        private void HookInventoryEvents()
        {
            var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
            if (userInv != null)
            {
                userInv.OnInventoryChanged += HandleInventoryChanged;
            }
            CharacterInventoryManager.OnAnyEquipmentChanged += HandleCharacterEquipmentChanged;
            CharacterInventoryManager.OnAnyArtifactDropped += HandleCharacterArtifactDropped;
        }

        private void UnhookInventoryEvents()
        {
            var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
            if (userInv != null)
            {
                userInv.OnInventoryChanged -= HandleInventoryChanged;
            }
            CharacterInventoryManager.OnAnyEquipmentChanged -= HandleCharacterEquipmentChanged;
            CharacterInventoryManager.OnAnyArtifactDropped -= HandleCharacterArtifactDropped;
        }

        private void HandleInventoryChanged()
        {
            SyncWithInventory();
        }

        private void HandleCharacterEquipmentChanged(CharacterInventoryManager mgr)
        {
            SyncWithInventory();
        }

        private void HandleCharacterArtifactDropped(CharacterInventoryManager mgr, ItemDataSO item, Vector3 pos)
        {
            SyncWithInventory();
        }

        private struct InventoryArtifactSource
        {
            public string InstanceId;
            public string ItemDefId;
            public string ItemName;
            public ItemDataSO ItemData;
            public bool IsEquipped;
            public string LocationBadge;
            public CharacterInventoryManager CharManager;
            public int CharSlotIndex;
            public int UserSlotIndex;
        }

        private bool _isSyncing = false;
        private int _syncLockCount = 0;
        public bool IsSyncSuppressed => _syncLockCount > 0;

        public sealed class ScopedSyncLock : IDisposable
        {
            private readonly DungeonMaster _dm;
            public ScopedSyncLock(DungeonMaster dm)
            {
                _dm = dm;
                if (_dm != null)
                {
                    _dm._syncLockCount++;
                }
            }

            public void Dispose()
            {
                if (_dm != null)
                {
                    _dm._syncLockCount--;
                    if (_dm._syncLockCount <= 0)
                    {
                        _dm._syncLockCount = 0;
                        _dm.SyncWithInventory();
                    }
                }
            }
        }

        public IDisposable SuppressSync()
        {
            return new ScopedSyncLock(this);
        }

        /// <summary>
        /// Synchronizes DungeonMaster artifact collection with all Artifact items in User Inventory and Character Equipment slots.
        /// Maintains 1-to-1 mapping so duplicate items of the same ID are accurately tracked and displayed.
        /// </summary>
        public void SyncWithInventory()
        {
            if (_isSyncing || _syncLockCount > 0) return;
            _isSyncing = true;

            try
            {
                var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
                var charManagers = FindObjectsByType<CharacterInventoryManager>(FindObjectsInactive.Include);

                bool hasEquippedArtifacts = charManagers != null && charManagers.Any(cm => cm != null && cm.Inventory != null &&
                    Enumerable.Range(0, cm.Inventory.SlotCount).Any(i => cm.Inventory.GetSlot(i) != null && cm.Inventory.GetSlot(i).IsOccupied && cm.Inventory.GetSlot(i).EquippedItem != null &&
                        (cm.Inventory.GetSlot(i).SlotType == EquipmentSlotType.Artifact || cm.Inventory.GetSlot(i).EquippedItem.ItemType == ItemType.Artifact)));

                if (userInv == null && !hasEquippedArtifacts)
                {
                    // Headless test or standalone DM; preserve current in-memory artifacts
                    return;
                }

            var sources = new List<InventoryArtifactSource>();

            if (userInv != null)
            {
                for (int i = 0; i < userInv.Capacity; i++)
                {
                    var slot = userInv.GetSlot(i);
                    if (slot != null && !slot.IsEmpty && slot.Item != null && slot.Item.ItemType == ItemType.Artifact)
                    {
                        int count = Mathf.Max(1, slot.Quantity);
                        string baseInstId = slot.ItemInstance != null && !string.IsNullOrEmpty(slot.ItemInstance.InstanceID)
                            ? slot.ItemInstance.InstanceID
                            : $"inv_{i}_{slot.Item.Id}";

                        for (int k = 0; k < count; k++)
                        {
                            string instId = count == 1 ? baseInstId : $"{baseInstId}_{k}";
                            sources.Add(new InventoryArtifactSource
                            {
                                InstanceId = instId,
                                ItemDefId = slot.Item.Id,
                                ItemName = slot.Item.ItemName,
                                ItemData = slot.Item,
                                IsEquipped = false,
                                LocationBadge = "[Inventory]",
                                UserSlotIndex = i
                            });
                        }
                    }
                }
            }

            if (charManagers != null)
            {
                foreach (var cm in charManagers)
                {
                    if (cm == null || cm.Inventory == null) continue;
                    for (int i = 0; i < cm.Inventory.SlotCount; i++)
                    {
                        var slot = cm.Inventory.GetSlot(i);
                        if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                        {
                            if (slot.SlotType == EquipmentSlotType.Artifact || slot.EquippedItem.ItemType == ItemType.Artifact)
                            {
                                string instId = $"equip_{cm.GetEntityId()}_{i}_{slot.EquippedItem.Id}";
                                sources.Add(new InventoryArtifactSource
                                {
                                    InstanceId = instId,
                                    ItemDefId = slot.EquippedItem.Id,
                                    ItemName = slot.EquippedItem.ItemName,
                                    ItemData = slot.EquippedItem,
                                    IsEquipped = true,
                                    LocationBadge = $"[Equipped: {cm.CharacterDisplayName}]",
                                    CharManager = cm,
                                    CharSlotIndex = i
                                });
                            }
                        }
                    }
                }
            }

            // Match existing artifacts against sources (1-to-1)
            var matchedSources = new HashSet<int>();
            var matchedArtifacts = new HashSet<Artifact>();

            // Pass 1: Match by exact SourceInstanceId or Id
            for (int a = 0; a < artifacts.Count; a++)
            {
                var art = artifacts[a];
                if (art == null) continue;

                for (int s = 0; s < sources.Count; s++)
                {
                    if (matchedSources.Contains(s)) continue;
                    var src = sources[s];

                    if (art.SourceInstanceId == src.InstanceId || art.Id == src.InstanceId)
                    {
                        matchedSources.Add(s);
                        matchedArtifacts.Add(art);
                        art.SetLocationBadge(src.LocationBadge);
                        art.SetSourceInstanceId(src.InstanceId);
                        art.SetItemDefinitionId(src.ItemDefId);
                        break;
                    }
                }
            }

            // Pass 2: Match remaining artifacts by ItemDefinitionId or Name
            for (int a = 0; a < artifacts.Count; a++)
            {
                var art = artifacts[a];
                if (art == null || matchedArtifacts.Contains(art)) continue;

                for (int s = 0; s < sources.Count; s++)
                {
                    if (matchedSources.Contains(s)) continue;
                    var src = sources[s];

                    bool nameMatches = !string.IsNullOrEmpty(src.ItemName) &&
                        (src.ItemName == art.ArtifactName || "Empowered " + src.ItemName == art.ArtifactName || art.ArtifactName == "Empowered " + src.ItemName);
                    bool idMatches = !string.IsNullOrEmpty(src.ItemDefId) &&
                        (art.ItemDefinitionId == src.ItemDefId || art.Id == src.ItemDefId);

                    if (idMatches || nameMatches)
                    {
                        matchedSources.Add(s);
                        matchedArtifacts.Add(art);
                        art.SetLocationBadge(src.LocationBadge);
                        art.SetSourceInstanceId(src.InstanceId);
                        art.SetItemDefinitionId(src.ItemDefId);
                        break;
                    }
                }
            }

            bool changed = false;

            // Pass 3: Create new Artifact for any unmatched sources
            for (int s = 0; s < sources.Count; s++)
            {
                if (matchedSources.Contains(s)) continue;
                var src = sources[s];

                bool isEmp = !string.IsNullOrEmpty(src.ItemName) && src.ItemName.StartsWith("Empowered ", StringComparison.OrdinalIgnoreCase);
                var newArt = new Artifact(
                    artifactName: src.ItemName,
                    powerLevel: 1,
                    essenceCost: 0,
                    id: src.InstanceId,
                    isEmpowered: isEmp,
                    modifiers: src.ItemData.StatModifiers,
                    itemDefinitionId: src.ItemDefId,
                    sourceInstanceId: src.InstanceId,
                    locationBadge: src.LocationBadge
                );
                artifacts.Add(newArt);
                changed = true;
                OnArtifactForged?.Invoke(newArt);
            }

            // Pass 4: Remove any unmatched artifacts
            for (int a = artifacts.Count - 1; a >= 0; a--)
            {
                var art = artifacts[a];
                if (art == null || !matchedArtifacts.Contains(art))
                {
                    if (!sources.Any(src => src.InstanceId == art.SourceInstanceId || src.InstanceId == art.Id))
                    {
                        artifacts.RemoveAt(a);
                        changed = true;
                        OnArtifactRemoved?.Invoke(art);
                    }
                }
            }

            if (changed)
            {
                CheckDefeatCondition();
            }

            OnInventorySynced?.Invoke();
            }
            finally
            {
                _isSyncing = false;
            }
        }

        /// <summary>
        /// Spawns 1 starting artifact into the user inventory if no artifacts are currently held.
        /// </summary>
        public void SpawnStartingArtifactIfNeeded()
        {
            SyncWithInventory();
            if (artifacts.Count > 0) return;

            var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
            ItemDataSO starterItem = null;

            // Try loading from database or resources
            starterItem = Resources.Load<ItemDataSO>("Items/Artifacts/Brown_Boots");
            if (starterItem == null)
            {
                var scannedArtifacts = Resources.FindObjectsOfTypeAll<ItemDataSO>();
                if (scannedArtifacts != null)
                {
                    starterItem = scannedArtifacts.FirstOrDefault(i => i != null && i.ItemType == ItemType.Artifact);
                }
            }

            if (starterItem == null)
            {
                starterItem = ItemDataSO.CreateRuntimeInstance(
                    "ITEM_ARTIFACT_001",
                    "Brown Boots",
                    ItemType.Artifact,
                    EquipmentSlotType.Artifact
                );
            }

            if (userInv != null)
            {
                userInv.AddItem(starterItem, 1);
                SyncWithInventory();
            }
            else
            {
                var art = new Artifact(starterItem.ItemName, 1, 0, starterItem.Id, false, starterItem.StatModifiers);
                artifacts.Add(art);
                OnArtifactForged?.Invoke(art);
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime and unit test setups without scene dependency.
        /// </summary>
        public void Initialize(
            int startingEssence = 100,
            int startingEnergy = 50,
            IEnumerable<Artifact> startingArtifacts = null,
            int roundLossPenalty = 25,
            int maximumEssence = 100,
            int maximumEnergy = 100,
            DMTalentTreeManager talentMgr = null)
        {
            maxEssence = Mathf.Max(1, maximumEssence);
            maxEnergy = Mathf.Max(1, maximumEnergy);
            currentEssence = Mathf.Clamp(startingEssence, 0, maxEssence);
            currentEnergy = Mathf.Clamp(startingEnergy, 0, maxEnergy);
            roundLossEssencePenalty = Mathf.Max(0, roundLossPenalty);
            if (talentMgr != null)
            {
                talentTreeManager = talentMgr;
            }

            artifacts.Clear();
            if (startingArtifacts != null)
            {
                artifacts.AddRange(startingArtifacts);
            }

            OnEssenceChanged?.Invoke(currentEssence, maxEssence);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }

        /// <summary>
        /// Checks if the player has unlocked the talent required to empower an artifact multiple times.
        /// </summary>
        public bool CanMultiEmpower()
        {
            if (talentTreeManager == null)
            {
                talentTreeManager = GetComponent<DMTalentTreeManager>() ?? FindAnyObjectByType<DMTalentTreeManager>();
            }
            return talentTreeManager != null && talentTreeManager.IsUnlocked(multiEmpowerTalentId);
        }

        /// <summary>
        /// Attempts to spend the specified amount of energy.
        /// Returns true if successful, false if insufficient energy.
        /// </summary>
        public bool SpendEnergy(int amount)
        {
            if (amount <= 0) return true;
            if (currentEnergy < amount) return false;

            currentEnergy -= amount;
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
            return true;
        }

        /// <summary>
        /// Adds energy to the Dungeon Master, capped at MaxEnergy.
        /// </summary>
        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;

            currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }

        /// <summary>
        /// Alias for <see cref="GainEnergy(int)"/> to award bonus energy (e.g. from forfeited warp items).
        /// </summary>
        public void AddEnergy(int amount) => GainEnergy(amount);

        /// <summary>
        /// Reduces the Dungeon Master's essence. If essence reaches 0, triggers defeat.
        /// </summary>
        public void LoseEssence(int amount)
        {
            if (amount <= 0) return;

            currentEssence = Mathf.Max(0, currentEssence - amount);
            OnEssenceChanged?.Invoke(currentEssence, maxEssence);

            CheckDefeatCondition();
        }

        /// <summary>
        /// Spends energy to recover essence up to MaxEssence.
        /// </summary>
        public bool RecoverEssence(int essenceAmount = -1, int energyCost = -1)
        {
            int cost = energyCost >= 0 ? energyCost : essenceRecoveryEnergyCost;
            int restoreAmount = essenceAmount >= 0 ? essenceAmount : essenceRecoveryAmount;

            if (currentEssence >= maxEssence) return false;
            if (!SpendEnergy(cost)) return false;

            currentEssence = Mathf.Min(currentEssence + restoreAmount, maxEssence);
            OnEssenceChanged?.Invoke(currentEssence, maxEssence);
            return true;
        }

        /// <summary>
        /// Convenience method to purchase monsters during the planning phase.
        /// </summary>
        public bool PurchaseMonster(int energyCost)
        {
            return SpendEnergy(energyCost);
        }

        /// <summary>
        /// Forges a new artifact by expending vital essence.
        /// Returns the newly forged Artifact, or null if insufficient essence.
        /// Also adds the newly forged ItemDataSO into the player's InventoryManager.
        /// </summary>
        public Artifact ForgeArtifact(string artifactName, int essenceCost, int startingPower = 1)
        {
            if (string.IsNullOrEmpty(artifactName)) return null;
            if (essenceCost < 0) return null;

            // Cannot forge if cost exceeds current essence
            if (currentEssence < essenceCost) return null;

            LoseEssence(essenceCost);

            var artifact = new Artifact(artifactName, startingPower, essenceCost);
            artifacts.Add(artifact);

            // Add corresponding ItemDataSO into UserInventory
            var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
            if (userInv != null)
            {
                var itemDef = ItemDataSO.CreateRuntimeInstance(
                    artifact.Id,
                    artifact.ArtifactName,
                    ItemType.Artifact,
                    EquipmentSlotType.Artifact
                );
                userInv.AddItem(itemDef, 1);
            }

            OnArtifactForged?.Invoke(artifact);
            return artifact;
        }

        /// <summary>
        /// Directly adds an artifact to the Dungeon Master's collection (e.g. from rare drops or rewards).
        /// </summary>
        public void AddArtifact(Artifact artifact)
        {
            if (artifact == null) return;
            if (!artifacts.Contains(artifact))
            {
                artifacts.Add(artifact);
                OnArtifactForged?.Invoke(artifact);
            }
        }

        /// <summary>
        /// Empowers an existing artifact by spending energy to make its effect global and increase its power level.
        /// The first empower turns its effect into a passive global aura and adds the 'Empowered ' prefix.
        /// Subsequent empowerments require the 'DM_ARTIFACT_OVERCHARGE' talent unlocked in DMTalentTreeManager.
        /// Updates the ItemDataSO in User Inventory and auto-unequips from any character.
        /// </summary>
        public bool EmpowerArtifact(Artifact artifact, int energyCost, int powerIncrease = 1)
        {
            if (artifact == null) return false;
            if (!artifacts.Contains(artifact)) return false;
            if (powerIncrease <= 0) return false;

            // If already empowered, multi-empowering requires the talent
            if (artifact.IsEmpowered && !CanMultiEmpower())
            {
                return false;
            }

            if (!SpendEnergy(energyCost)) return false;

            // Enforce single empowered artifact: unempower any other currently empowered artifact
            if (!artifact.IsEmpowered)
            {
                for (int i = 0; i < artifacts.Count; i++)
                {
                    var other = artifacts[i];
                    if (other != null && other != artifact && other.IsEmpowered)
                    {
                        other.Unempower();
                        OnArtifactUnempowered?.Invoke(other);
                    }
                }
            }

            string originalName = artifact.ArtifactName;
            artifact.Empower(powerIncrease);

            // Auto-unequip from characters if it was equipped
            AutoUnequipFromCharacters(artifact.Id, originalName, artifact.SourceInstanceId);

            OnArtifactEmpowered?.Invoke(artifact);
            return true;
        }

        /// <summary>
        /// Retrieves the currently active empowered artifact, or null if none is empowered.
        /// </summary>
        public Artifact GetActiveEmpoweredArtifact()
        {
            if (artifacts == null) return null;
            return artifacts.FirstOrDefault(a => a != null && a.IsEmpowered);
        }

        /// <summary>
        /// Empowers an existing artifact looked up by its unique ID.
        /// </summary>
        public bool EmpowerArtifact(string artifactId, int energyCost, int powerIncrease = 1)
        {
            var artifact = artifacts.Find(a => a.Id == artifactId || a.SourceInstanceId == artifactId);
            return EmpowerArtifact(artifact, energyCost, powerIncrease);
        }

        /// <summary>
        /// Attempts to unequip the specified artifact from any active character inventory if equipped.
        /// </summary>
        public void AutoUnequipFromCharacters(string artifactId, string fallbackName = null, string sourceInstanceId = null)
        {
            var characterInventories = FindObjectsByType<CharacterInventoryManager>(FindObjectsInactive.Include);
            if (characterInventories == null || characterInventories.Length == 0) return;

            string cleanFallback = fallbackName != null ? fallbackName.Replace("Empowered ", "").Trim() : null;

            using (SuppressSync())
            {
                bool unequippedAny = false;

                // Priority 1: Match by exact sourceInstanceId
                if (!string.IsNullOrEmpty(sourceInstanceId))
                {
                    foreach (var charMgr in characterInventories)
                    {
                        if (charMgr == null || charMgr.Inventory == null) continue;
                        for (int i = 0; i < charMgr.Inventory.SlotCount; i++)
                        {
                            var slot = charMgr.Inventory.GetSlot(i);
                            if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                            {
                                string equipInstId = $"equip_{charMgr.GetEntityId()}_{i}_{slot.EquippedItem.Id}";
                                if (sourceInstanceId == equipInstId)
                                {
                                    if (charMgr.Unequip(i, out var unequippedItem) && unequippedItem != null)
                                    {
                                        var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
                                        if (userInv != null)
                                        {
                                            userInv.AddItem(unequippedItem, 1, force: true);
                                        }
                                    }
                                    unequippedAny = true;
                                    break;
                                }
                            }
                        }
                        if (unequippedAny) break;
                    }
                }

                // Priority 2: Match by artifactId or fallback name if not unequipped yet
                if (!unequippedAny)
                {
                    foreach (var charMgr in characterInventories)
                    {
                        if (charMgr == null || charMgr.Inventory == null) continue;
                        for (int i = 0; i < charMgr.Inventory.SlotCount; i++)
                        {
                            var slot = charMgr.Inventory.GetSlot(i);
                            if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                            {
                                string itemId = slot.EquippedItem.Id;
                                string itemName = slot.EquippedItem.ItemName;
                                string equipInstId = $"equip_{charMgr.GetEntityId()}_{i}_{slot.EquippedItem.Id}";

                                if ((!string.IsNullOrEmpty(artifactId) && (itemId == artifactId || artifactId == equipInstId)) ||
                                    (!string.IsNullOrEmpty(cleanFallback) && (itemName == cleanFallback || itemName == "Empowered " + cleanFallback)))
                                {
                                    if (charMgr.Unequip(i, out var unequippedItem) && unequippedItem != null)
                                    {
                                        var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
                                        if (userInv != null)
                                        {
                                            userInv.AddItem(unequippedItem, 1, force: true);
                                        }
                                    }
                                    unequippedAny = true;
                                    break;
                                }
                            }
                        }
                        if (unequippedAny) break;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves all stat modifiers provided by empowered (global aura) artifacts.
        /// </summary>
        public List<ItemStatModifierData> GetGlobalArtifactStatModifiers()
        {
            var list = new List<ItemStatModifierData>();
            if (artifacts == null) return list;

            for (int i = 0; i < artifacts.Count; i++)
            {
                var art = artifacts[i];
                if (art != null && art.IsEmpowered && art.StatModifiers != null)
                {
                    list.AddRange(art.StatModifiers);
                }
            }
            return list;
        }

        /// <summary>
        /// Retrieves total power level granted by all empowered (global aura) artifacts.
        /// </summary>
        public int GetTotalEmpoweredArtifactPower()
        {
            int total = 0;
            if (artifacts == null) return total;

            for (int i = 0; i < artifacts.Count; i++)
            {
                var art = artifacts[i];
                if (art != null && art.IsEmpowered)
                {
                    total += art.PowerLevel;
                }
            }
            return total;
        }

        /// <summary>
        /// Sacrifices an artifact in exchange for immediate emergency Energy.
        /// Removes the corresponding item from the User Inventory or character equipment.
        /// If this empties the artifact collection, triggers defeat.
        /// </summary>
        public bool SacrificeArtifact(Artifact artifact, int energyReward = -1)
        {
            if (artifact == null) return false;
            if (!artifacts.Contains(artifact)) return false;

            int energyToGain = energyReward >= 0 ? energyReward : baseArtifactSacrificeEnergyReward;

            artifacts.Remove(artifact);
            GainEnergy(energyToGain);

            bool removedFromLocation = false;

            // 1. If equipped on a character, remove from character equipment
            var characterInventories = FindObjectsByType<CharacterInventoryManager>(FindObjectsInactive.Include);
            if (characterInventories != null)
            {
                foreach (var cm in characterInventories)
                {
                    if (cm == null || cm.Inventory == null) continue;
                    for (int i = 0; i < cm.Inventory.SlotCount; i++)
                    {
                        var slot = cm.Inventory.GetSlot(i);
                        if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                        {
                            string equipInstId = $"equip_{cm.GetEntityId()}_{i}_{slot.EquippedItem.Id}";
                            if (artifact.SourceInstanceId == equipInstId ||
                                slot.EquippedItem.Id == artifact.Id ||
                                slot.EquippedItem.Id == artifact.ItemDefinitionId ||
                                slot.EquippedItem.ItemName == artifact.ArtifactName ||
                                "Empowered " + slot.EquippedItem.ItemName == artifact.ArtifactName)
                            {
                                cm.Unequip(i, out _);
                                removedFromLocation = true;
                                break;
                            }
                        }
                    }
                    if (removedFromLocation) break;
                }
            }

            // 2. If not removed from character, remove from User Inventory
            if (!removedFromLocation)
            {
                var userInv = InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
                if (userInv != null)
                {
                    for (int i = 0; i < userInv.Capacity; i++)
                    {
                        var slot = userInv.GetSlot(i);
                        if (slot != null && !slot.IsEmpty && slot.Item != null)
                        {
                            string instId = slot.ItemInstance != null ? slot.ItemInstance.InstanceID : null;
                            if ((instId != null && (instId == artifact.SourceInstanceId || instId == artifact.Id)) ||
                                slot.Item.Id == artifact.Id ||
                                slot.Item.Id == artifact.ItemDefinitionId ||
                                slot.Item.ItemName == artifact.ArtifactName ||
                                "Empowered " + slot.Item.ItemName == artifact.ArtifactName)
                            {
                                userInv.RemoveItemAt(i, 1);
                                break;
                            }
                        }
                    }
                }
            }

            OnArtifactSacrificed?.Invoke(artifact, energyToGain);
            OnArtifactRemoved?.Invoke(artifact);

            CheckDefeatCondition();
            return true;
        }

        /// <summary>
        /// Removes an artifact from the Dungeon Master's collection (e.g. dropped when an equipped monster dies or looted).
        /// If this causes the artifact collection to become empty, triggers defeat.
        /// </summary>
        public bool RemoveArtifact(Artifact artifact)
        {
            if (artifact == null) return false;

            bool removed = artifacts.Remove(artifact);
            if (removed)
            {
                OnArtifactRemoved?.Invoke(artifact);
                CheckDefeatCondition();
            }

            return removed;
        }

        /// <summary>
        /// Clears all artifacts. Triggers defeat due to empty artifact collection.
        /// </summary>
        public void ClearArtifacts()
        {
            artifacts.Clear();
            CheckDefeatCondition();
        }

        /// <summary>
        /// Applies the essence penalty when a round defense is lost.
        /// </summary>
        public void ApplyRoundLossPenalty(int? customPenalty = null)
        {
            int penalty = customPenalty.HasValue ? Mathf.Max(0, customPenalty.Value) : roundLossEssencePenalty;
            LoseEssence(penalty);
        }

        /// <summary>
        /// Evaluates whether the Dungeon Master is defeated and broadcasts the reason.
        /// </summary>
        public DefeatReason CheckDefeatCondition()
        {
            if (currentEssence <= 0)
            {
                OnDefeated?.Invoke(DefeatReason.EssenceDepleted);
                return DefeatReason.EssenceDepleted;
            }

            if (artifacts.Count == 0)
            {
                OnDefeated?.Invoke(DefeatReason.ArtifactsLost);
                return DefeatReason.ArtifactsLost;
            }

            return DefeatReason.None;
        }

        private void ClampValues()
        {
            maxEssence = Mathf.Max(1, maxEssence);
            maxEnergy = Mathf.Max(1, maxEnergy);
            currentEssence = Mathf.Clamp(currentEssence, 0, maxEssence);
            currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
            roundLossEssencePenalty = Mathf.Max(0, roundLossEssencePenalty);
        }
    }
}
