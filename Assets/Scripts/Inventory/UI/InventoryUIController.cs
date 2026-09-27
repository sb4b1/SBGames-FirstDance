using System.Collections.Generic;
using SBGames.Input;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Stats;
using SBGames.Inventory.User;
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
        [SerializeField] private UserInventoryManager userInventory;
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

        // Test item templates for quick prototyping
        private ItemDefinition _potionItem;
        private ItemDefinition _manaItem;
        private ItemDefinition _atkRuneItem;
        private ItemDefinition _critRuneItem;
        private ItemDefinition _speedArtifactItem;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            CreateTestItemDefinitions();
        }

        private void Start()
        {
            if (userInventory == null)
            {
                userInventory = FindAnyObjectByType<UserInventoryManager>();
                if (userInventory == null)
                {
                    var userGo = new GameObject("[UserInventoryManager]");
                    userInventory = userGo.AddComponent<UserInventoryManager>();
                }
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
            if (userInventory != null)
            {
                userInventory.OnInventoryChanged -= RefreshInventoryUI;
                userInventory.OnInventoryChanged += RefreshInventoryUI;
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
            if (userInventory != null)
            {
                userInventory.OnInventoryChanged -= RefreshInventoryUI;
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
                userInventory.AddItem(_potionItem, 3);
                SetStatus("Added 3x Health Potions.");
            });

            AddActionButton(toolbar, "+ Mana (x2)", () =>
            {
                userInventory.AddItem(_manaItem, 2);
                SetStatus("Added 2x Mana Potions.");
            });

            AddActionButton(toolbar, "+ Atk Rune (+10 Flat, +15% Add)", () =>
            {
                userInventory.AddItem(_atkRuneItem, 1);
                SetStatus("Added Fierce Attack Rune.");
            });

            AddActionButton(toolbar, "+ Crit Rune (+12% Crit)", () =>
            {
                userInventory.AddItem(_critRuneItem, 1);
                SetStatus("Added Sharp Crit Rune.");
            });

            AddActionButton(toolbar, "+ Speed Artifact (+2.5 Flat, +20% Mult)", () =>
            {
                userInventory.AddItem(_speedArtifactItem, 1);
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

            var charInv = _selectedCharacter.Inventory;
            for (int i = 0; i < charInv.SlotCount; i++)
            {
                int slotIndex = i;
                var slot = charInv.GetSlot(i);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3;
                row.style.paddingBottom = 3;
                row.style.marginBottom = 2;
                row.style.backgroundColor = new Color(0.14f, 0.14f, 0.17f);
                row.style.paddingLeft = 6;
                row.style.paddingRight = 6;

                string slotTitle = $"[{slot.SlotType}] {slot.SlotName}: ";
                string itemText = slot.IsOccupied ? $"{slot.EquippedItem.ItemName}" : "<Empty>";

                var label = new Label($"{slotTitle} {itemText}");
                label.style.color = slot.IsOccupied ? new Color(1f, 0.85f, 0.4f) : new Color(0.5f, 0.5f, 0.55f);
                row.Add(label);

                if (slot.IsOccupied)
                {
                    var unequipBtn = new Button(() =>
                    {
                        if (userInventory.UnequipFromCharacter(_selectedCharacter, slotIndex, out string msg))
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

            if (userInventory == null || userInventory.Inventory == null)
            {
                _inventoryScrollView.Add(new Label("User inventory uninitialized."));
                return;
            }

            var inv = userInventory.Inventory;
            for (int i = 0; i < inv.Capacity; i++)
            {
                int slotIndex = i;
                var slot = inv.GetSlot(i);

                if (slot == null || slot.IsEmpty || slot.Item == null) continue;

                // Cache item details before action callbacks, because equipping/consuming/dropping
                // removes the item from the slot, making slot.Item null in subsequent callback lines.
                string itemName = slot.Item.ItemName;
                string itemTypeStr = slot.Item.ItemType.ToString();
                int itemAmount = slot.Amount;
                bool isConsumable = slot.Item.IsConsumable;
                bool isEquippable = slot.Item.IsEquippable;

                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.justifyContent = Justify.SpaceBetween;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.paddingLeft = 8;
                itemRow.style.paddingRight = 8;
                itemRow.style.paddingTop = 4;
                itemRow.style.paddingBottom = 4;
                itemRow.style.marginBottom = 3;
                itemRow.style.backgroundColor = new Color(0.14f, 0.14f, 0.17f);
                itemRow.style.borderTopLeftRadius = 4;
                itemRow.style.borderTopRightRadius = 4;
                itemRow.style.borderBottomLeftRadius = 4;
                itemRow.style.borderBottomRightRadius = 4;

                string labelText = $"[{slotIndex}] {itemName} x{itemAmount} ({itemTypeStr})";
                var itemLabel = new Label(labelText);
                itemLabel.style.color = Color.white;
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
                        if (userInventory.ConsumeItem(slotIndex, _selectedCharacter, out string msg))
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

                // Equip button for Runes / Artifacts
                if (isEquippable && _selectedCharacter != null)
                {
                    var equipBtn = new Button(() =>
                    {
                        if (_selectedCharacter == null)
                        {
                            SetStatus("<color=red>No character selected.</color>");
                            return;
                        }

                        bool success = userInventory.EquipToCharacter(
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
                    { text = "Equip" };
                    equipBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
                    equipBtn.style.color = Color.white;
                    actionsContainer.Add(equipBtn);
                }

                // Drop 1 button
                var dropBtn = new Button(() =>
                {
                    userInventory.RemoveItemAt(slotIndex, 1);
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

        private void CreateTestItemDefinitions()
        {
            var healEffect = ScriptableObject.CreateInstance<RestoreHealthEffect>();
            healEffect.HealAmount = 35f;

            var manaEffect = ScriptableObject.CreateInstance<RestoreManaEffect>();
            manaEffect.ManaAmount = 25f;

            _potionItem = ItemDefinition.CreateRuntimeInstance(
                id: "item_health_potion",
                itemName: "Health Potion",
                itemType: ItemType.Consumable,
                slotType: EquipmentSlotType.None,
                maxStack: 99,
                effect: healEffect);

            _manaItem = ItemDefinition.CreateRuntimeInstance(
                id: "item_mana_potion",
                itemName: "Mana Potion",
                itemType: ItemType.Consumable,
                slotType: EquipmentSlotType.None,
                maxStack: 99,
                effect: manaEffect);

            _atkRuneItem = ItemDefinition.CreateRuntimeInstance(
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

            _critRuneItem = ItemDefinition.CreateRuntimeInstance(
                id: "rune_sharp_eye",
                itemName: "Sharp Crit Rune",
                itemType: ItemType.Rune,
                slotType: EquipmentSlotType.Rune,
                maxStack: 1,
                modifiers: new[]
                {
                    new ItemStatModifierData(StatType.CritChance, 0.12f, StatModType.Flat)
                });

            _speedArtifactItem = ItemDefinition.CreateRuntimeInstance(
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
    }
}
