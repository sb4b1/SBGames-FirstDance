using System.Collections.Generic;
using SBGames.Input;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Data;
using SBGames.Inventory.Stats;
using SBGames.Loop;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Inventory.UI
{
    /// <summary>
    /// UI Toolkit controller providing a functional mockup interface for the inventory and equipment system.
    /// Strictly decoupled from core game logic:
    /// - Observes inventory and stat managers via C# events.
    /// - Invokes public API methods on managers when user clicks action buttons.
    /// - Procedurally constructs the UI hierarchy at runtime to avoid missing UXML/USS asset dependencies.
    /// - Implements IToggleableUI so it can be managed dynamically by InputManager on UIButton.B.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class InventoryUIController : MonoBehaviour, IToggleableUI
    {
        [Header("References")]
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private DungeonMaster dungeonMaster;
        [SerializeField] private List<CharacterInventoryManager> characters = new();

        private UIDocument _uiDocument;
        private CharacterInventoryManager _selectedCharacter;

        // Visual Element Containers
        private VisualElement _root;
        private DropdownField _characterDropdown;
        private VisualElement _equipmentContainer;
        private VisualElement _statsContainer;
        private VisualElement _inventoryScrollView;
        private Label _statusFeedbackLabel;
        private VisualElement _modalOverlay;
        private VisualElement _modalBox;

        // Test item templates for quick prototyping
        private ItemDataSO _potionItem;
        private ItemDataSO _manaItem;
        private ItemDataSO _atkRuneItem;
        private ItemDataSO _critRuneItem;
        private ItemDataSO _speedArtifactItem;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            CreateTestItemData();
        }

        private void Start()
        {
            if (inventoryManager == null)
            {
                inventoryManager = InventoryManager.Instance != null ? InventoryManager.Instance : FindAnyObjectByType<InventoryManager>();
                if (inventoryManager == null)
                {
                    var userGo = new GameObject("[InventoryManager]");
                    inventoryManager = userGo.AddComponent<InventoryManager>();
                }
            }

            if (dungeonMaster == null)
            {
                dungeonMaster = DungeonMaster.Instance != null ? DungeonMaster.Instance : FindAnyObjectByType<DungeonMaster>();
            }

            if (characters == null || characters.Count == 0)
            {
                characters = new List<CharacterInventoryManager>(FindObjectsByType<CharacterInventoryManager>(FindObjectsInactive.Exclude));
            }

            if (characters.Count > 0)
            {
                _selectedCharacter = characters[0];
            }

            BuildUIHierarchy();
            BindEvents();
            RefreshAll();

            // Register with foundational InputManager
            RegisterWithInputManager();

            // Start closed so pressing 'B' opens the inventory
            Close();
        }

        private void OnDestroy()
        {
            UnregisterFromInputManager();
        }

        #region IToggleableUI Implementation

        public bool IsOpen => _root != null && _root.style.display != DisplayStyle.None;

        public void Open()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                RefreshAll();
            }
        }

        public void Close()
        {
            HideModal();
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        #endregion

        private void OnEnable()
        {
            BindEvents();
            RegisterWithInputManager();
        }

        private void OnDisable()
        {
            UnbindEvents();
            UnregisterFromInputManager();
        }

        private void RegisterWithInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.RegisterUISystem(UIButton.B, this);
            }
        }

        private void UnregisterFromInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.UnregisterUISystem(this);
            }
        }

        private void BindEvents()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged -= RefreshInventoryUI;
                inventoryManager.OnInventoryChanged += RefreshInventoryUI;
            }

            if (dungeonMaster == null)
            {
                dungeonMaster = DungeonMaster.Instance != null ? DungeonMaster.Instance : FindAnyObjectByType<DungeonMaster>();
            }

            if (dungeonMaster != null)
            {
                dungeonMaster.OnArtifactEmpowered -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactEmpowered += HandleArtifactStateChanged;
                dungeonMaster.OnArtifactUnempowered -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactUnempowered += HandleArtifactStateChanged;
                dungeonMaster.OnArtifactForged -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactForged += HandleArtifactStateChanged;
                dungeonMaster.OnArtifactRemoved -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactRemoved += HandleArtifactStateChanged;
                dungeonMaster.OnInventorySynced -= RefreshAll;
                dungeonMaster.OnInventorySynced += RefreshAll;
            }

            if (_selectedCharacter != null)
            {
                _selectedCharacter.OnEquipmentOrStatsChanged -= RefreshCharacterUI;
                _selectedCharacter.OnEquipmentOrStatsChanged += RefreshCharacterUI;

                if (_selectedCharacter.StatsManager != null)
                {
                    _selectedCharacter.StatsManager.OnLifeChanged -= OnLifeChangedHandler;
                    _selectedCharacter.StatsManager.OnLifeChanged += OnLifeChangedHandler;
                }
            }
        }

        private void UnbindEvents()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged -= RefreshInventoryUI;
            }

            if (dungeonMaster != null)
            {
                dungeonMaster.OnArtifactEmpowered -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactUnempowered -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactForged -= HandleArtifactStateChanged;
                dungeonMaster.OnArtifactRemoved -= HandleArtifactStateChanged;
                dungeonMaster.OnInventorySynced -= RefreshAll;
            }

            if (_selectedCharacter != null)
            {
                _selectedCharacter.OnEquipmentOrStatsChanged -= RefreshCharacterUI;
                if (_selectedCharacter.StatsManager != null)
                {
                    _selectedCharacter.StatsManager.OnLifeChanged -= OnLifeChangedHandler;
                }
            }
        }

        private void HandleArtifactStateChanged(Artifact _)
        {
            RefreshAll();
        }

        private void OnLifeChangedHandler(float current, float max)
        {
            RefreshStatsUI();
        }

        public void SelectCharacter(CharacterInventoryManager character)
        {
            if (_selectedCharacter == character) return;

            UnbindEvents();
            _selectedCharacter = character;
            BindEvents();
            RefreshCharacterUI();
        }

        #region UI Construction

        private void BuildUIHierarchy()
        {
            _root = _uiDocument.rootVisualElement;
            _root.Clear();

            _root.style.flexDirection = FlexDirection.Column;
            _root.style.width = Length.Percent(100);
            _root.style.height = Length.Percent(100);
            _root.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            _root.style.paddingLeft = 12;
            _root.style.paddingRight = 12;
            _root.style.paddingTop = 10;
            _root.style.paddingBottom = 10;

            // Title Bar
            var titleBar = new VisualElement();
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.justifyContent = Justify.SpaceBetween;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.marginBottom = 8;

            var title = new Label("INVENTORY & EQUIPMENT SYSTEM");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.9f, 0.95f);
            titleBar.Add(title);

            var closeBtn = new Button(() => Close());
            closeBtn.text = "✕ Close (B)";
            closeBtn.style.fontSize = 12;
            closeBtn.style.paddingLeft = 10;
            closeBtn.style.paddingRight = 10;
            closeBtn.style.paddingTop = 4;
            closeBtn.style.paddingBottom = 4;
            closeBtn.style.backgroundColor = new Color(0.3f, 0.15f, 0.15f);
            closeBtn.style.color = Color.white;
            titleBar.Add(closeBtn);

            _root.Add(titleBar);

            // Status message label
            _statusFeedbackLabel = new Label("System initialized.");
            _statusFeedbackLabel.style.fontSize = 12;
            _statusFeedbackLabel.style.color = new Color(0.4f, 0.85f, 0.4f);
            _statusFeedbackLabel.style.marginBottom = 8;
            _root.Add(_statusFeedbackLabel);

            // Main Columns
            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            _root.Add(columns);

            // Left Column (Character Equipment & Stats)
            var leftColumn = new VisualElement();
            leftColumn.style.width = Length.Percent(48);
            leftColumn.style.marginRight = 10;
            leftColumn.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f);
            leftColumn.style.paddingLeft = 10;
            leftColumn.style.paddingRight = 10;
            leftColumn.style.paddingTop = 10;
            leftColumn.style.paddingBottom = 10;
            leftColumn.style.borderTopLeftRadius = 6;
            leftColumn.style.borderTopRightRadius = 6;
            leftColumn.style.borderBottomLeftRadius = 6;
            leftColumn.style.borderBottomRightRadius = 6;
            columns.Add(leftColumn);

            // Right Column (User Inventory)
            var rightColumn = new VisualElement();
            rightColumn.style.width = Length.Percent(52);
            rightColumn.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f);
            rightColumn.style.paddingLeft = 10;
            rightColumn.style.paddingRight = 10;
            rightColumn.style.paddingTop = 10;
            rightColumn.style.paddingBottom = 10;
            rightColumn.style.borderTopLeftRadius = 6;
            rightColumn.style.borderTopRightRadius = 6;
            rightColumn.style.borderBottomLeftRadius = 6;
            rightColumn.style.borderBottomRightRadius = 6;
            columns.Add(rightColumn);

            // Populate Left Column
            BuildCharacterHeader(leftColumn);
            BuildEquipmentPanel(leftColumn);
            BuildStatsPanel(leftColumn);

            // Populate Right Column
            BuildInventoryPanel(rightColumn);

            // Bottom Bar (Testing Buttons)
            BuildTestingToolBar(_root);

            // Modal Overlay for Slot Selection & Confirmations
            BuildModalOverlay(_root);
        }

        private void BuildCharacterHeader(VisualElement parent)
        {
            var charHeader = new Label("CHARACTER PROFILE");
            charHeader.style.fontSize = 15;
            charHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            charHeader.style.color = new Color(0.95f, 0.8f, 0.3f);
            charHeader.style.marginBottom = 6;
            parent.Add(charHeader);

            var charChoices = new List<string>();
            for (int i = 0; i < characters.Count; i++)
            {
                charChoices.Add(characters[i].CharacterDisplayName);
            }

            if (charChoices.Count == 0) charChoices.Add("No Characters Found");

            _characterDropdown = new DropdownField("Active Character", charChoices, 0);
            _characterDropdown.style.marginBottom = 10;
            _characterDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = charChoices.IndexOf(evt.newValue);
                if (index >= 0 && index < characters.Count)
                {
                    SelectCharacter(characters[index]);
                }
            });
            parent.Add(_characterDropdown);
        }

        private void BuildEquipmentPanel(VisualElement parent)
        {
            var header = new Label("EQUIPPED SLOTS (4 Runes, 1 Artifact)");
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.8f, 0.8f, 0.9f);
            header.style.marginBottom = 4;
            parent.Add(header);

            _equipmentContainer = new VisualElement();
            _equipmentContainer.style.flexDirection = FlexDirection.Column;
            _equipmentContainer.style.marginBottom = 10;
            parent.Add(_equipmentContainer);
        }

        private void BuildStatsPanel(VisualElement parent)
        {
            var header = new Label("REAL-TIME STATS (CharacterStatsManager)");
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.8f, 0.8f, 0.9f);
            header.style.marginBottom = 4;
            parent.Add(header);

            _statsContainer = new VisualElement();
            _statsContainer.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
            _statsContainer.style.paddingLeft = 8;
            _statsContainer.style.paddingRight = 8;
            _statsContainer.style.paddingTop = 6;
            _statsContainer.style.paddingBottom = 6;
            _statsContainer.style.borderTopLeftRadius = 4;
            _statsContainer.style.borderTopRightRadius = 4;
            _statsContainer.style.borderBottomLeftRadius = 4;
            _statsContainer.style.borderBottomRightRadius = 4;
            parent.Add(_statsContainer);
        }

        private void BuildInventoryPanel(VisualElement parent)
        {
            var header = new Label("USER INVENTORY");
            header.style.fontSize = 15;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.4f, 0.9f, 0.7f);
            header.style.marginBottom = 6;
            parent.Add(header);

            _inventoryScrollView = new ScrollView();
            _inventoryScrollView.style.flexGrow = 1;
            parent.Add(_inventoryScrollView);
        }

        private void BuildTestingToolBar(VisualElement parent)
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.marginTop = 8;
            toolbar.style.paddingTop = 6;
            toolbar.style.borderTopWidth = 1;
            toolbar.style.borderTopColor = new Color(0.3f, 0.3f, 0.35f);
            parent.Add(toolbar);

            var barLabel = new Label("TEST ACTIONS: ");
            barLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            barLabel.style.alignSelf = Align.Center;
            barLabel.style.marginRight = 6;
            toolbar.Add(barLabel);

            AddActionButton(toolbar, "+ Potion (x3)", () =>
            {
                inventoryManager.AddItem(_potionItem, 3);
                SetStatus("Added 3x Health Potions.");
            });

            AddActionButton(toolbar, "+ Mana (x2)", () =>
            {
                inventoryManager.AddItem(_manaItem, 2);
                SetStatus("Added 2x Mana Potions.");
            });

            AddActionButton(toolbar, "+ Atk Rune (+10 Flat, +15% Add)", () =>
            {
                inventoryManager.AddItem(_atkRuneItem, 1);
                SetStatus("Added Fierce Attack Rune.");
            });

            AddActionButton(toolbar, "+ Crit Rune (+12% Crit)", () =>
            {
                inventoryManager.AddItem(_critRuneItem, 1);
                SetStatus("Added Sharp Crit Rune.");
            });

            AddActionButton(toolbar, "+ Speed Artifact (+2.5 Flat, +20% Mult)", () =>
            {
                inventoryManager.AddItem(_speedArtifactItem, 1);
                SetStatus("Added Windwalker Boots Artifact.");
            });

            AddActionButton(toolbar, "Damage Hero (-35 Life)", () =>
            {
                if (_selectedCharacter != null && _selectedCharacter.StatsManager != null)
                {
                    _selectedCharacter.StatsManager.TakeDamage(35f);
                    SetStatus($"Dealt 35 damage to {_selectedCharacter.CharacterDisplayName}.");
                }
            });
        }

        private void AddActionButton(VisualElement parent, string text, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.marginRight = 5;
            btn.style.marginBottom = 4;
            btn.style.backgroundColor = new Color(0.25f, 0.3f, 0.4f);
            btn.style.color = Color.white;
            parent.Add(btn);
        }

        #endregion

        #region Refresh Logic

        public void RefreshAll()
        {
            if (dungeonMaster == null)
            {
                dungeonMaster = DungeonMaster.Instance != null ? DungeonMaster.Instance : FindAnyObjectByType<DungeonMaster>();
            }
            dungeonMaster?.SyncWithInventory();

            RefreshCharacterUI();
            RefreshInventoryUI();
        }

        public void RefreshCharacterUI()
        {
            RefreshEquipmentUI();
            RefreshStatsUI();
        }

        private void RefreshEquipmentUI()
        {
            if (_equipmentContainer == null) return;
            _equipmentContainer.Clear();

            if (_selectedCharacter == null || _selectedCharacter.Inventory == null)
            {
                _equipmentContainer.Add(new Label("No character selected."));
                return;
            }

            var activeEmpowered = dungeonMaster != null ? dungeonMaster.GetActiveEmpoweredArtifact() : null;
            var charInv = _selectedCharacter.Inventory;
            for (int i = 0; i < charInv.SlotCount; i++)
            {
                int slotIndex = i;
                var slot = charInv.GetSlot(i);

                bool isEmpoweredSlot = false;
                if (activeEmpowered != null && slot.IsOccupied && slot.EquippedItem != null &&
                    (slot.SlotType == EquipmentSlotType.Artifact || slot.EquippedItem.ItemType == ItemType.Artifact))
                {
                    string equipInstId = $"equip_{_selectedCharacter.GetEntityId()}_{slotIndex}_{slot.EquippedItem.Id}";
                    if (activeEmpowered.SourceInstanceId == equipInstId ||
                        activeEmpowered.Id == equipInstId ||
                        (activeEmpowered.IsEmpowered && (slot.EquippedItem.Id == activeEmpowered.Id || slot.EquippedItem.Id == activeEmpowered.ItemDefinitionId)))
                    {
                        isEmpoweredSlot = true;
                    }
                }

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3;
                row.style.paddingBottom = 3;
                row.style.marginBottom = 2;
                row.style.backgroundColor = isEmpoweredSlot ? new Color(0.24f, 0.20f, 0.10f) : new Color(0.14f, 0.14f, 0.17f);
                row.style.paddingLeft = 6;
                row.style.paddingRight = 6;

                string slotTitle = $"[{slot.SlotType}] {slot.SlotName}: ";
                string badgeStr = isEmpoweredSlot ? " [★ EMPOWERED]" : "";
                string itemText = slot.IsOccupied ? $"{slot.EquippedItem.ItemName}{badgeStr}" : "<Empty>";

                var label = new Label($"{slotTitle} {itemText}");
                label.style.color = isEmpoweredSlot ? new Color(1f, 0.85f, 0.2f) : (slot.IsOccupied ? new Color(1f, 0.85f, 0.4f) : new Color(0.5f, 0.5f, 0.55f));
                label.style.unityFontStyleAndWeight = isEmpoweredSlot ? FontStyle.Bold : FontStyle.Normal;
                row.Add(label);

                if (slot.IsOccupied)
                {
                    if (slot.CanUnequip)
                    {
                        var unequipBtn = new Button(() =>
                        {
                            if (inventoryManager.UnequipFromCharacter(_selectedCharacter, slotIndex, out string msg))
                            {
                                SetStatus(msg);
                            }
                            else
                            {
                                SetStatus($"<color=red>{msg}</color>");
                            }
                        })
                        { text = "Unequip" };
                        unequipBtn.style.backgroundColor = new Color(0.5f, 0.25f, 0.25f);
                        unequipBtn.style.color = Color.white;
                        row.Add(unequipBtn);
                    }
                    else
                    {
                        var actionBtns = new VisualElement();
                        actionBtns.style.flexDirection = FlexDirection.Row;

                        var replaceBtn = new Button(() =>
                        {
                            ShowPickRuneForSlotModal(slotIndex, slot);
                        })
                        { text = "Replace" };
                        replaceBtn.style.backgroundColor = new Color(0.55f, 0.35f, 0.15f);
                        replaceBtn.style.color = Color.white;
                        replaceBtn.style.marginRight = 4;
                        actionBtns.Add(replaceBtn);

                        var deleteBtn = new Button(() =>
                        {
                            if (inventoryManager.DestroyRuneFromCharacter(_selectedCharacter, slotIndex, out string msg))
                            {
                                SetStatus(msg);
                            }
                            else
                            {
                                SetStatus($"<color=red>{msg}</color>");
                            }
                        })
                        { text = "Delete Rune" };
                        deleteBtn.style.backgroundColor = new Color(0.6f, 0.18f, 0.18f);
                        deleteBtn.style.color = Color.white;
                        actionBtns.Add(deleteBtn);

                        row.Add(actionBtns);
                    }
                }
                else if (slot.SlotType == EquipmentSlotType.Rune)
                {
                    var inscribeBtn = new Button(() =>
                    {
                        ShowPickRuneForSlotModal(slotIndex, slot);
                    })
                    { text = "Inscribe" };
                    inscribeBtn.style.backgroundColor = new Color(0.2f, 0.45f, 0.3f);
                    inscribeBtn.style.color = Color.white;
                    row.Add(inscribeBtn);
                }

                _equipmentContainer.Add(row);
            }
        }

        private void RefreshStatsUI()
        {
            if (_statsContainer == null) return;
            _statsContainer.Clear();

            if (_selectedCharacter == null || _selectedCharacter.StatsManager == null)
            {
                _statsContainer.Add(new Label("No stats available."));
                return;
            }

            var stats = _selectedCharacter.StatsManager;

            AddStatRow("Life", $"{stats.Life:0.#} / {stats.MaxLife.Value:0.#}", new Color(0.9f, 0.3f, 0.3f));
            AddStatRow("Mana", $"{stats.Mana:0.#} / {stats.MaxMana.Value:0.#}", new Color(0.3f, 0.6f, 0.95f));
            AddStatRow("Damage (Attack)", $"{stats.Damage.Value:0.##}", new Color(1f, 0.7f, 0.2f));
            AddStatRow("Movespeed", $"{stats.Movespeed.Value:0.##}", new Color(0.4f, 0.9f, 0.4f));
            AddStatRow("Attack Interval", $"{stats.AttackTimeInterval.Value:0.##}s", new Color(0.7f, 0.7f, 0.9f));

            // Dynamic supplementary stats
            float def = stats.GetStatValue(StatType.Defense);
            if (def > 0) AddStatRow("Defense", $"{def:0.##}", new Color(0.6f, 0.8f, 1f));

            float crit = stats.GetStatValue(StatType.CritChance);
            if (crit > 0) AddStatRow("Crit Chance", $"{crit * 100:0.#}%", new Color(1f, 0.4f, 0.8f));
        }

        private void AddStatRow(string statName, string statValue, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;

            var nameLbl = new Label(statName);
            nameLbl.style.color = new Color(0.8f, 0.8f, 0.8f);

            var valLbl = new Label(statValue);
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valLbl.style.color = color;

            row.Add(nameLbl);
            row.Add(valLbl);
            _statsContainer.Add(row);
        }

        private void RefreshInventoryUI()
        {
            if (_inventoryScrollView == null) return;
            _inventoryScrollView.Clear();

            if (inventoryManager == null)
            {
                _inventoryScrollView.Add(new Label("Inventory manager uninitialized."));
                return;
            }

            for (int i = 0; i < inventoryManager.Capacity; i++)
            {
                int slotIndex = i;
                var slot = inventoryManager.GetSlot(i);

                if (slot == null || slot.IsEmpty || slot.Item == null) continue;

                // Cache item details before action callbacks, because equipping/consuming/dropping
                // removes the item from the slot, making slot.Item null in subsequent callback lines.
                string itemName = slot.Item.ItemName;
                string itemTypeStr = slot.Item.ItemType.ToString();
                int itemAmount = slot.Amount;
                bool isConsumable = slot.Item.IsConsumable;
                bool isEquippable = slot.Item.IsEquippable;

                var activeEmpowered = dungeonMaster != null ? dungeonMaster.GetActiveEmpoweredArtifact() : null;
                bool isEmpoweredArtifact = false;
                if (activeEmpowered != null && slot.Item != null && slot.Item.ItemType == ItemType.Artifact)
                {
                    string slotInstId = slot.ItemInstance != null && !string.IsNullOrEmpty(slot.ItemInstance.InstanceID)
                        ? slot.ItemInstance.InstanceID
                        : $"inv_{slotIndex}_{slot.Item.Id}";

                    if (activeEmpowered.SourceInstanceId == slotInstId ||
                        activeEmpowered.Id == slotInstId ||
                        (slot.ItemInstance != null && (activeEmpowered.SourceInstanceId == slot.ItemInstance.InstanceID || activeEmpowered.Id == slot.ItemInstance.InstanceID)))
                    {
                        isEmpoweredArtifact = true;
                    }
                }

                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.justifyContent = Justify.SpaceBetween;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.paddingLeft = 8;
                itemRow.style.paddingRight = 8;
                itemRow.style.paddingTop = 4;
                itemRow.style.paddingBottom = 4;
                itemRow.style.marginBottom = 3;
                itemRow.style.backgroundColor = isEmpoweredArtifact ? new Color(0.24f, 0.20f, 0.10f) : new Color(0.14f, 0.14f, 0.17f);
                itemRow.style.borderTopLeftRadius = 4;
                itemRow.style.borderTopRightRadius = 4;
                itemRow.style.borderBottomLeftRadius = 4;
                itemRow.style.borderBottomRightRadius = 4;

                string badgeStr = isEmpoweredArtifact ? " [★ EMPOWERED]" : "";
                string labelText = $"[{slotIndex}] {itemName} x{itemAmount} ({itemTypeStr}){badgeStr}";
                var itemLabel = new Label(labelText);
                itemLabel.style.color = isEmpoweredArtifact ? new Color(1f, 0.85f, 0.2f) : Color.white;
                itemLabel.style.unityFontStyleAndWeight = isEmpoweredArtifact ? FontStyle.Bold : FontStyle.Normal;
                itemLabel.style.flexGrow = 1;
                itemRow.Add(itemLabel);

                var actionsContainer = new VisualElement();
                actionsContainer.style.flexDirection = FlexDirection.Row;
                itemRow.Add(actionsContainer);

                // Use button for consumables
                if (isConsumable)
                {
                    var useBtn = new Button(() =>
                    {
                        if (inventoryManager.ConsumeItem(slotIndex, _selectedCharacter, out string msg))
                        {
                            SetStatus(msg);
                        }
                        else
                        {
                            SetStatus($"<color=red>{msg}</color>");
                        }
                    })
                    { text = "Use" };
                    useBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
                    useBtn.style.color = Color.white;
                    actionsContainer.Add(useBtn);
                }

                // Equip / Inscribe button for Runes / Artifacts
                if (isEquippable && _selectedCharacter != null)
                {
                    bool isRune = slot.Item.ItemType == ItemType.Rune;
                    var equipBtn = new Button(() =>
                    {
                        if (_selectedCharacter == null)
                        {
                            SetStatus("<color=red>No character selected.</color>");
                            return;
                        }

                        if (isRune)
                        {
                            ShowInscribeSlotSelectionModal(slotIndex, slot.Item);
                            return;
                        }

                        bool success = inventoryManager.EquipToCharacter(
                            slotIndex,
                            _selectedCharacter,
                            -1,
                            confirmDestroyRune: true);

                        if (success)
                        {
                            SetStatus($"Equipped {itemName} to {_selectedCharacter.CharacterDisplayName}.");
                        }
                        else
                        {
                            SetStatus($"<color=red>Failed to equip {itemName}.</color>");
                        }
                    })
                    { text = isRune ? "Inscribe" : "Equip" };
                    equipBtn.style.backgroundColor = isRune ? new Color(0.55f, 0.35f, 0.15f) : new Color(0.2f, 0.4f, 0.6f);
                    equipBtn.style.color = Color.white;
                    actionsContainer.Add(equipBtn);
                }

                // Drop 1 button
                var dropBtn = new Button(() =>
                {
                    inventoryManager.RemoveItemAt(slotIndex, 1);
                    SetStatus($"Dropped 1x {itemName}.");
                })
                { text = "Drop" };
                dropBtn.style.backgroundColor = new Color(0.4f, 0.3f, 0.3f);
                dropBtn.style.color = Color.white;
                actionsContainer.Add(dropBtn);

                _inventoryScrollView.Add(itemRow);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusFeedbackLabel != null)
            {
                _statusFeedbackLabel.text = message;
            }
        }

        #endregion

        #region Mockup Item Creation

        private void CreateTestItemData()
        {
            var healEffect = ScriptableObject.CreateInstance<RestoreHealthEffect>();
            healEffect.HealAmount = 35f;

            var manaEffect = ScriptableObject.CreateInstance<RestoreManaEffect>();
            manaEffect.ManaAmount = 25f;

            _potionItem = ItemDataSO.CreateRuntimeInstance(
                id: "item_health_potion",
                itemName: "Health Potion",
                itemType: ItemType.Consumable,
                slotType: EquipmentSlotType.None,
                maxStack: 99,
                effect: healEffect);

            _manaItem = ItemDataSO.CreateRuntimeInstance(
                id: "item_mana_potion",
                itemName: "Mana Potion",
                itemType: ItemType.Consumable,
                slotType: EquipmentSlotType.None,
                maxStack: 99,
                effect: manaEffect);

            _atkRuneItem = ItemDataSO.CreateRuntimeInstance(
                id: "rune_fierce_strike",
                itemName: "Fierce Attack Rune",
                itemType: ItemType.Rune,
                slotType: EquipmentSlotType.Rune,
                maxStack: 1,
                modifiers: new[]
                {
                    new ItemStatModifierData(StatType.Attack, 10f, StatModType.Flat),
                    new ItemStatModifierData(StatType.Attack, 0.15f, StatModType.PercentAdd)
                });

            _critRuneItem = ItemDataSO.CreateRuntimeInstance(
                id: "rune_sharp_eye",
                itemName: "Sharp Crit Rune",
                itemType: ItemType.Rune,
                slotType: EquipmentSlotType.Rune,
                maxStack: 1,
                modifiers: new[]
                {
                    new ItemStatModifierData(StatType.CritChance, 0.12f, StatModType.Flat)
                });

            _speedArtifactItem = ItemDataSO.CreateRuntimeInstance(
                id: "artifact_wind_boots",
                itemName: "Windwalker Boots",
                itemType: ItemType.Artifact,
                slotType: EquipmentSlotType.Artifact,
                maxStack: 1,
                modifiers: new[]
                {
                    new ItemStatModifierData(StatType.MoveSpeed, 2.5f, StatModType.Flat),
                    new ItemStatModifierData(StatType.MoveSpeed, 0.20f, StatModType.PercentMult)
                });
        }

        #endregion

        #region Rune Inscription Modal & Confirmation Dialogs

        private void BuildModalOverlay(VisualElement parent)
        {
            _modalOverlay = new VisualElement();
            _modalOverlay.style.position = Position.Absolute;
            _modalOverlay.style.left = 0;
            _modalOverlay.style.top = 0;
            _modalOverlay.style.right = 0;
            _modalOverlay.style.bottom = 0;
            _modalOverlay.style.width = Length.Percent(100);
            _modalOverlay.style.height = Length.Percent(100);
            _modalOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
            _modalOverlay.style.justifyContent = Justify.Center;
            _modalOverlay.style.alignItems = Align.Center;
            _modalOverlay.style.display = DisplayStyle.None;

            _modalBox = new VisualElement();
            _modalBox.style.width = 480;
            _modalBox.style.backgroundColor = new Color(0.16f, 0.16f, 0.20f);
            _modalBox.style.borderTopLeftRadius = 8;
            _modalBox.style.borderTopRightRadius = 8;
            _modalBox.style.borderBottomLeftRadius = 8;
            _modalBox.style.borderBottomRightRadius = 8;
            _modalBox.style.borderLeftWidth = 1;
            _modalBox.style.borderRightWidth = 1;
            _modalBox.style.borderTopWidth = 1;
            _modalBox.style.borderBottomWidth = 1;
            _modalBox.style.borderLeftColor = new Color(0.35f, 0.35f, 0.45f);
            _modalBox.style.borderRightColor = new Color(0.35f, 0.35f, 0.45f);
            _modalBox.style.borderTopColor = new Color(0.35f, 0.35f, 0.45f);
            _modalBox.style.borderBottomColor = new Color(0.35f, 0.35f, 0.45f);
            _modalBox.style.paddingLeft = 18;
            _modalBox.style.paddingRight = 18;
            _modalBox.style.paddingTop = 16;
            _modalBox.style.paddingBottom = 16;

            _modalOverlay.Add(_modalBox);
            parent.Add(_modalOverlay);
        }

        private void HideModal()
        {
            if (_modalOverlay != null)
            {
                _modalOverlay.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Prompts the user to select which Rune Slot on the active character to inscribe the rune into.
        /// </summary>
        private void ShowInscribeSlotSelectionModal(int userSlotIndex, ItemDataSO runeItem)
        {
            if (_selectedCharacter == null || _selectedCharacter.Inventory == null)
            {
                SetStatus("<color=red>No character selected.</color>");
                return;
            }

            var charInv = _selectedCharacter.Inventory;
            _modalBox.Clear();
            _modalOverlay.style.display = DisplayStyle.Flex;

            // Title & Description
            var title = new Label($"INSCRIBE RUNE: {runeItem.ItemName.ToUpper()}");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(1f, 0.85f, 0.35f);
            title.style.marginBottom = 4;
            _modalBox.Add(title);

            var desc = new Label($"Select which Rune Slot on <b>{_selectedCharacter.CharacterDisplayName}</b> to inscribe into:");
            desc.style.fontSize = 12;
            desc.style.color = new Color(0.8f, 0.8f, 0.85f);
            desc.style.marginBottom = 12;
            _modalBox.Add(desc);

            var slotListContainer = new VisualElement();
            slotListContainer.style.marginBottom = 12;

            int runeSlotCount = 0;
            for (int i = 0; i < charInv.SlotCount; i++)
            {
                int targetSlotIndex = i;
                var slot = charInv.GetSlot(i);
                if (slot.SlotType != EquipmentSlotType.Rune) continue;

                runeSlotCount++;

                var slotBtn = new Button(() =>
                {
                    if (slot.IsOccupied)
                    {
                        // Slot already has an inscribed rune -> Ask confirmation before deleting old rune
                        ShowInscribeConfirmationModal(userSlotIndex, runeItem, targetSlotIndex, slot.EquippedItem, slot.SlotName);
                    }
                    else
                    {
                        // Slot is empty -> Inscribe directly
                        HideModal();
                        bool success = inventoryManager.EquipToCharacter(
                            userSlotIndex,
                            _selectedCharacter,
                            targetSlotIndex,
                            confirmDestroyRune: true);

                        if (success)
                        {
                            SetStatus($"Inscribed {runeItem.ItemName} into {slot.SlotName}.");
                        }
                        else
                        {
                            SetStatus($"<color=red>Failed to inscribe {runeItem.ItemName}.</color>");
                        }
                    }
                });

                slotBtn.style.paddingTop = 8;
                slotBtn.style.paddingBottom = 8;
                slotBtn.style.paddingLeft = 12;
                slotBtn.style.paddingRight = 12;
                slotBtn.style.marginBottom = 6;
                slotBtn.style.borderTopLeftRadius = 4;
                slotBtn.style.borderTopRightRadius = 4;
                slotBtn.style.borderBottomLeftRadius = 4;
                slotBtn.style.borderBottomRightRadius = 4;

                if (slot.IsOccupied)
                {
                    slotBtn.text = $"{slot.SlotName}: [{slot.EquippedItem.ItemName}]  (⚠️ Will Delete Old Rune)";
                    slotBtn.style.backgroundColor = new Color(0.28f, 0.18f, 0.16f);
                    slotBtn.style.color = new Color(1f, 0.75f, 0.55f);
                }
                else
                {
                    slotBtn.text = $"{slot.SlotName}: <Empty>  (Ready)";
                    slotBtn.style.backgroundColor = new Color(0.16f, 0.24f, 0.18f);
                    slotBtn.style.color = new Color(0.6f, 0.95f, 0.65f);
                }

                slotListContainer.Add(slotBtn);
            }

            if (runeSlotCount == 0)
            {
                var noSlotsLabel = new Label("No Rune Slots found on this character.");
                noSlotsLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                slotListContainer.Add(noSlotsLabel);
            }

            _modalBox.Add(slotListContainer);

            var cancelBtn = new Button(() => HideModal()) { text = "Cancel" };
            cancelBtn.style.backgroundColor = new Color(0.28f, 0.28f, 0.32f);
            cancelBtn.style.color = Color.white;
            cancelBtn.style.paddingTop = 6;
            cancelBtn.style.paddingBottom = 6;
            _modalBox.Add(cancelBtn);
        }

        /// <summary>
        /// Confirms with the user whether they want to overwrite and delete the existing rune in the chosen slot.
        /// </summary>
        private void ShowInscribeConfirmationModal(
            int userSlotIndex,
            ItemDataSO newRune,
            int targetSlotIndex,
            ItemDataSO oldRune,
            string slotName)
        {
            _modalBox.Clear();

            var title = new Label("⚠️ CONFIRM RUNE REPLACEMENT");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.35f, 0.3f);
            title.style.marginBottom = 10;
            _modalBox.Add(title);

            var warningBox = new VisualElement();
            warningBox.style.backgroundColor = new Color(0.24f, 0.14f, 0.14f);
            warningBox.style.paddingLeft = 12;
            warningBox.style.paddingRight = 12;
            warningBox.style.paddingTop = 10;
            warningBox.style.paddingBottom = 10;
            warningBox.style.marginBottom = 14;
            warningBox.style.borderTopLeftRadius = 4;
            warningBox.style.borderTopRightRadius = 4;
            warningBox.style.borderBottomLeftRadius = 4;
            warningBox.style.borderBottomRightRadius = 4;
            warningBox.style.borderLeftWidth = 1;
            warningBox.style.borderRightWidth = 1;
            warningBox.style.borderTopWidth = 1;
            warningBox.style.borderBottomWidth = 1;
            warningBox.style.borderLeftColor = new Color(0.65f, 0.25f, 0.25f);
            warningBox.style.borderRightColor = new Color(0.65f, 0.25f, 0.25f);
            warningBox.style.borderTopColor = new Color(0.65f, 0.25f, 0.25f);
            warningBox.style.borderBottomColor = new Color(0.65f, 0.25f, 0.25f);

            var msg = new Label(
                $"<b>{slotName}</b> already has <b>[{oldRune.ItemName}]</b> inscribed.\n\n" +
                $"Inscribing <b>[{newRune.ItemName}]</b> will permanently <b>DELETE and DESTROY</b> [{oldRune.ItemName}].\n\n" +
                "Runes cannot be unequipped and deleted runes cannot be recovered.\n\n" +
                "Are you sure you want to proceed?");
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.fontSize = 12;
            msg.style.color = new Color(0.95f, 0.9f, 0.9f);
            warningBox.Add(msg);
            _modalBox.Add(warningBox);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;

            var backBtn = new Button(() => ShowInscribeSlotSelectionModal(userSlotIndex, newRune))
            {
                text = "← Back to Slots"
            };
            backBtn.style.marginRight = 8;
            backBtn.style.paddingLeft = 10;
            backBtn.style.paddingRight = 10;
            backBtn.style.paddingTop = 6;
            backBtn.style.paddingBottom = 6;
            backBtn.style.backgroundColor = new Color(0.28f, 0.28f, 0.32f);
            backBtn.style.color = Color.white;
            buttonRow.Add(backBtn);

            var cancelBtn = new Button(() => HideModal())
            {
                text = "Cancel"
            };
            cancelBtn.style.marginRight = 8;
            cancelBtn.style.paddingLeft = 10;
            cancelBtn.style.paddingRight = 10;
            cancelBtn.style.paddingTop = 6;
            cancelBtn.style.paddingBottom = 6;
            cancelBtn.style.backgroundColor = new Color(0.28f, 0.28f, 0.32f);
            cancelBtn.style.color = Color.white;
            buttonRow.Add(cancelBtn);

            var confirmBtn = new Button(() =>
            {
                HideModal();
                bool success = inventoryManager.EquipToCharacter(
                    userSlotIndex,
                    _selectedCharacter,
                    targetSlotIndex,
                    confirmDestroyRune: true);

                if (success)
                {
                    SetStatus($"Inscribed {newRune.ItemName} into {slotName} (deleted {oldRune.ItemName}).");
                }
                else
                {
                    SetStatus($"<color=red>Failed to inscribe {newRune.ItemName}.</color>");
                }
            })
            {
                text = "Confirm Inscribe (Delete Old)"
            };
            confirmBtn.style.paddingLeft = 12;
            confirmBtn.style.paddingRight = 12;
            confirmBtn.style.paddingTop = 6;
            confirmBtn.style.paddingBottom = 6;
            confirmBtn.style.backgroundColor = new Color(0.75f, 0.2f, 0.2f);
            confirmBtn.style.color = Color.white;
            confirmBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            buttonRow.Add(confirmBtn);

            _modalBox.Add(buttonRow);
        }

        /// <summary>
        /// Prompts the user to select which Rune from their user inventory to inscribe into the designated slot.
        /// </summary>
        private void ShowPickRuneForSlotModal(int targetSlotIndex, EquipmentSlot slot)
        {
            if (inventoryManager == null) return;

            _modalBox.Clear();
            _modalOverlay.style.display = DisplayStyle.Flex;

            var title = new Label(slot.IsOccupied
                ? $"REPLACE RUNE IN {slot.SlotName.ToUpper()}"
                : $"INSCRIBE RUNE INTO {slot.SlotName.ToUpper()}");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(1f, 0.85f, 0.35f);
            title.style.marginBottom = 4;
            _modalBox.Add(title);

            var desc = new Label(slot.IsOccupied
                ? $"Select a Rune from inventory to inscribe over [{slot.EquippedItem.ItemName}] (will delete old rune):"
                : "Select a Rune from inventory to inscribe:");
            desc.style.fontSize = 12;
            desc.style.color = new Color(0.8f, 0.8f, 0.85f);
            desc.style.marginBottom = 12;
            _modalBox.Add(desc);

            var runeList = new VisualElement();
            runeList.style.marginBottom = 12;

            int availableRuneCount = 0;
            for (int i = 0; i < inventoryManager.Capacity; i++)
            {
                int userSlotIndex = i;
                var userSlot = inventoryManager.GetSlot(i);
                if (userSlot == null || userSlot.IsEmpty || userSlot.Item == null || userSlot.Item.ItemType != ItemType.Rune) continue;

                availableRuneCount++;
                var runeItem = userSlot.Item;

                var runeBtn = new Button(() =>
                {
                    if (slot.IsOccupied)
                    {
                        ShowInscribeConfirmationModal(userSlotIndex, runeItem, targetSlotIndex, slot.EquippedItem, slot.SlotName);
                    }
                    else
                    {
                        HideModal();
                        bool success = inventoryManager.EquipToCharacter(
                            userSlotIndex,
                            _selectedCharacter,
                            targetSlotIndex,
                            confirmDestroyRune: true);

                        if (success)
                        {
                            SetStatus($"Inscribed {runeItem.ItemName} into {slot.SlotName}.");
                        }
                        else
                        {
                            SetStatus($"<color=red>Failed to inscribe {runeItem.ItemName}.</color>");
                        }
                    }
                });

                runeBtn.text = $"{runeItem.ItemName} (x{userSlot.Amount})";
                runeBtn.style.paddingTop = 8;
                runeBtn.style.paddingBottom = 8;
                runeBtn.style.paddingLeft = 12;
                runeBtn.style.paddingRight = 12;
                runeBtn.style.marginBottom = 6;
                runeBtn.style.backgroundColor = new Color(0.22f, 0.22f, 0.28f);
                runeBtn.style.color = new Color(0.95f, 0.9f, 0.7f);
                runeList.Add(runeBtn);
            }

            if (availableRuneCount == 0)
            {
                var emptyMsg = new Label("No Runes found in inventory. Add runes using the toolbar below.");
                emptyMsg.style.color = new Color(0.7f, 0.7f, 0.7f);
                emptyMsg.style.marginBottom = 10;
                runeList.Add(emptyMsg);
            }

            _modalBox.Add(runeList);

            var cancelBtn = new Button(() => HideModal()) { text = "Cancel" };
            cancelBtn.style.backgroundColor = new Color(0.28f, 0.28f, 0.32f);
            cancelBtn.style.color = Color.white;
            cancelBtn.style.paddingTop = 6;
            cancelBtn.style.paddingBottom = 6;
            _modalBox.Add(cancelBtn);
        }

        #endregion
    }
}
