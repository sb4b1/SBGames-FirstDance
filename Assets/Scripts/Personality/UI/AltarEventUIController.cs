using System;
using System.Collections.Generic;
using SBGames.Events;
using SBGames.Events.Types;
using SBGames.Loop;
using SBGames.Personality.Altar;
using SBGames.Personality.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Personality.UI
{
    /// <summary>
    /// UI Toolkit Controller for the Altar of Transmutation modal.
    /// Manages the unit selector, current race/trait comparison, 1-of-3 trait draft cards,
    /// affordability validation, and minimize/reopen workflows.
    /// </summary>
    [DisallowMultipleComponent]
    public class AltarEventUIController : MonoBehaviour
    {
        public static AltarEventUIController Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private VisualTreeAsset uxmlTemplate;
        [SerializeField] private StyleSheet ussStylesheet;
        [SerializeField] private bool pauseTimeWhileOpen = true;

        // UI Document & Elements
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _overlay;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _costBadge;
        private Label _walletStatus;
        private VisualElement _unitListContainer;
        private Label _currentRaceLabel;
        private Label _currentRaceDesc;
        private Label _currentTraitLabel;
        private Label _currentTraitDesc;
        private VisualElement _traitCardsContainer;
        private Label _detailTraitName;
        private Label _detailTraitDesc;
        private Button _minimizeBtn;
        private Button _skipBtn;
        private Button _confirmBtn;
        private Button _reopenPill;

        // Runtime State
        private AltarEventManager _manager;
        private CharacterTraitSO _selectedDraftTrait;
        private bool _isKeepCurrentSelected = false;
        private bool _isOpen = false;
        private float _previousTimeScale = 1f;

        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            var existing = FindAnyObjectByType<AltarEventUIController>();
            if (existing != null) return;

            var go = new GameObject("[AltarEventUI]");
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.sortingOrder = 950;

            var panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
            if (panelSettings == null)
            {
                var allDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
                foreach (var d in allDocs)
                {
                    if (d != uiDoc && d.panelSettings != null)
                    {
                        panelSettings = d.panelSettings;
                        break;
                    }
                }
            }

            if (panelSettings != null)
            {
                uiDoc.panelSettings = panelSettings;
            }

            go.AddComponent<AltarEventUIController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureUIDocument();
        }

        private void Start()
        {
            BuildUI();
        }

        private void EnsureUIDocument()
        {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) _uiDocument = gameObject.AddComponent<UIDocument>();

            if (uxmlTemplate == null)
            {
                uxmlTemplate = Resources.Load<VisualTreeAsset>("UI/Altar/AltarEventModal");
            }
            if (ussStylesheet == null)
            {
                ussStylesheet = Resources.Load<StyleSheet>("UI/Altar/AltarEventModal");
            }
        }

        private void BuildUI()
        {
            EnsureUIDocument();
            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            _root.Clear();

            if (uxmlTemplate != null)
            {
                uxmlTemplate.CloneTree(_root);
            }
            else
            {
                BuildProceduralUI(_root);
            }

            if (ussStylesheet != null && !_root.styleSheets.Contains(ussStylesheet))
            {
                _root.styleSheets.Add(ussStylesheet);
            }

            // Query elements
            _overlay = _root.Q<VisualElement>("altar-overlay");
            _titleLabel = _root.Q<Label>("altar-title");
            _subtitleLabel = _root.Q<Label>("altar-subtitle");
            _costBadge = _root.Q<Label>("altar-cost-badge");
            _walletStatus = _root.Q<Label>("altar-wallet-status");
            _unitListContainer = _root.Q<VisualElement>("unit-list-container");
            _currentRaceLabel = _root.Q<Label>("current-race-label");
            _currentRaceDesc = _root.Q<Label>("current-race-desc");
            _currentTraitLabel = _root.Q<Label>("current-trait-label");
            _currentTraitDesc = _root.Q<Label>("current-trait-desc");
            _traitCardsContainer = _root.Q<VisualElement>("trait-cards-container");
            _detailTraitName = _root.Q<Label>("detail-trait-name");
            _detailTraitDesc = _root.Q<Label>("detail-trait-description");
            _minimizeBtn = _root.Q<Button>("minimize-btn");
            _skipBtn = _root.Q<Button>("skip-btn");
            _confirmBtn = _root.Q<Button>("confirm-btn");
            _reopenPill = _root.Q<Button>("reopen-pill");

            // Bind actions
            if (_minimizeBtn != null)
            {
                _minimizeBtn.clicked += HandleMinimizeClicked;
            }
            if (_skipBtn != null)
            {
                _skipBtn.clicked += HandleSkipClicked;
            }
            if (_confirmBtn != null)
            {
                _confirmBtn.clicked += HandleConfirmClicked;
            }
            if (_reopenPill != null)
            {
                _reopenPill.clicked += HandleReopenClicked;
            }

            // Initially hide
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_reopenPill != null) _reopenPill.style.display = DisplayStyle.None;
        }

        public void OpenModal(AltarEventManager manager)
        {
            _manager = manager;
            if (_root == null || _overlay == null)
            {
                BuildUI();
            }

            _isOpen = true;
            _selectedDraftTrait = null;
            _isKeepCurrentSelected = false;

            if (pauseTimeWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
            if (_reopenPill != null) _reopenPill.style.display = DisplayStyle.None;

            RefreshView();
        }

        public void CloseModal()
        {
            _isOpen = false;
            if (pauseTimeWhileOpen)
            {
                Time.timeScale = _previousTimeScale;
            }

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_reopenPill != null) _reopenPill.style.display = DisplayStyle.None;
        }

        public void RefreshView()
        {
            if (_manager == null) return;

            // Header & Currency
            bool isBlood = _manager.ActiveCurrencyType == AltarCurrencyType.Blood;
            if (_titleLabel != null)
            {
                _titleLabel.text = isBlood ? "🩸 BLOOD ALTAR" : "⚡ ARCANE ALTAR";
                _titleLabel.style.color = isBlood ? new Color(0.95f, 0.3f, 0.35f) : new Color(0.4f, 0.75f, 1f);
            }

            if (_costBadge != null)
            {
                _costBadge.text = isBlood ? $"Cost: {_manager.ActiveCost} Essence" : $"Cost: {_manager.ActiveCost} Energy";
            }

            var dm = DungeonMaster.Instance;
            if (_walletStatus != null && dm != null)
            {
                _walletStatus.text = $"DM Essence: {dm.Essence}/{dm.MaxEssence} | Energy: {dm.Energy}/{dm.MaxEnergy}";
            }

            // Populate Unit List
            PopulateUnitList();

            // Populate Current Inspection
            PopulateCurrentInspection();

            // Populate Trait Draft Cards
            PopulateDraftCards();

            // Update Confirm Button
            UpdateConfirmState();
        }

        private void PopulateUnitList()
        {
            if (_unitListContainer == null || _manager == null) return;
            _unitListContainer.Clear();

            var units = _manager.AvailableUnits;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var btn = new Button();
                btn.AddToClassList("unit-item-btn");

                if (unit == _manager.SelectedUnit)
                {
                    btn.AddToClassList("selected");
                }

                // Name label
                var nameLabel = new Label(unit.DisplayName);
                btn.Add(nameLabel);

                // Badge (Grid vs Bench)
                var badge = new Label(unit.IsOnGrid ? "GRID" : "BENCH");
                badge.AddToClassList("unit-badge");
                badge.AddToClassList(unit.IsOnGrid ? "badge-grid" : "badge-bench");
                btn.Add(badge);

                btn.clicked += () =>
                {
                    _selectedDraftTrait = null;
                    _isKeepCurrentSelected = false;
                    _manager.SelectUnit(unit);
                };

                _unitListContainer.Add(btn);
            }
        }

        private void PopulateCurrentInspection()
        {
            var unit = _manager?.SelectedUnit;
            if (unit == null) return;

            if (_currentRaceLabel != null)
            {
                _currentRaceLabel.text = unit.Race != null ? unit.Race.DisplayName : "Unknown";
            }
            if (_currentRaceDesc != null)
            {
                _currentRaceDesc.text = unit.Race != null ? unit.Race.Description : "No specific racial instincts.";
            }

            if (_currentTraitLabel != null)
            {
                _currentTraitLabel.text = unit.CurrentTrait != null ? unit.CurrentTrait.DisplayName : "None";
            }
            if (_currentTraitDesc != null)
            {
                _currentTraitDesc.text = unit.CurrentTrait != null ? unit.CurrentTrait.Description : "No active trait.";
            }
        }

        private void PopulateDraftCards()
        {
            if (_traitCardsContainer == null || _manager == null) return;
            _traitCardsContainer.Clear();

            var drafted = _manager.DraftedTraits;

            // 1-of-N Draft Cards
            for (int i = 0; i < drafted.Count; i++)
            {
                var trait = drafted[i];
                if (trait == null) continue;

                var cardBtn = new Button();
                cardBtn.AddToClassList("trait-card-btn");

                if (trait == _selectedDraftTrait && !_isKeepCurrentSelected)
                {
                    cardBtn.AddToClassList("selected");
                }

                var title = new Label(trait.DisplayName);
                title.AddToClassList("card-title");
                title.style.color = trait.BadgeColor;
                cardBtn.Add(title);

                var desc = new Label(trait.Description);
                desc.AddToClassList("card-desc");
                cardBtn.Add(desc);

                cardBtn.clicked += () =>
                {
                    _selectedDraftTrait = trait;
                    _isKeepCurrentSelected = false;
                    SelectTrait(trait);
                };

                _traitCardsContainer.Add(cardBtn);
            }

            // Fallback Card: Keep Current Trait
            var keepCard = new Button();
            keepCard.AddToClassList("trait-card-btn");
            keepCard.AddToClassList("keep-trait");

            if (_isKeepCurrentSelected)
            {
                keepCard.AddToClassList("selected");
            }

            var keepTitle = new Label("Keep Current Trait");
            keepTitle.AddToClassList("card-title");
            keepTitle.style.color = new Color(0.7f, 0.8f, 0.95f);
            keepCard.Add(keepTitle);

            var keepDesc = new Label("Maintain current instincts without expending essence or energy.");
            keepDesc.AddToClassList("card-desc");
            keepCard.Add(keepDesc);

            keepCard.clicked += () =>
            {
                _selectedDraftTrait = null;
                _isKeepCurrentSelected = true;
                if (_detailTraitName != null) _detailTraitName.text = "Keep Current Trait";
                if (_detailTraitDesc != null) _detailTraitDesc.text = "Unit retains its existing psychological trait. Zero resource cost.";
                RefreshCardsSelectionVisual();
                UpdateConfirmState();
            };

            _traitCardsContainer.Add(keepCard);
        }

        private void SelectTrait(CharacterTraitSO trait)
        {
            if (trait == null) return;

            if (_detailTraitName != null) _detailTraitName.text = trait.DisplayName;
            if (_detailTraitDesc != null) _detailTraitDesc.text = trait.Description;

            RefreshCardsSelectionVisual();
            UpdateConfirmState();
        }

        private void RefreshCardsSelectionVisual()
        {
            // Re-render draft container selections without resetting unit list
            PopulateDraftCards();
        }

        private void UpdateConfirmState()
        {
            if (_confirmBtn == null) return;

            if (_isKeepCurrentSelected)
            {
                _confirmBtn.SetEnabled(true);
                _confirmBtn.text = "Keep Trait (Free)";
                return;
            }

            if (_selectedDraftTrait == null)
            {
                _confirmBtn.SetEnabled(false);
                _confirmBtn.text = "Select an Offering";
                return;
            }

            string reason = "Cannot afford cost";
            bool canAfford = _manager != null && _manager.CanAffordCost(out reason);
            _confirmBtn.SetEnabled(canAfford);

            if (!canAfford)
            {
                _confirmBtn.text = reason;
            }
            else
            {
                string costText = _manager.ActiveCurrencyType == AltarCurrencyType.Blood
                    ? $"-{_manager.ActiveCost} Essence"
                    : $"-{_manager.ActiveCost} Energy";
                _confirmBtn.text = $"Transmute Trait ({costText})";
            }
        }

        private void HandleConfirmClicked()
        {
            if (_manager == null) return;
            _manager.ConfirmTransmutation(_selectedDraftTrait, _isKeepCurrentSelected);
        }

        private void HandleSkipClicked()
        {
            if (_manager == null) return;
            _manager.SkipAltar();
        }

        private void HandleMinimizeClicked()
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_reopenPill != null) _reopenPill.style.display = DisplayStyle.Flex;

            if (pauseTimeWhileOpen)
            {
                Time.timeScale = _previousTimeScale;
            }

            if (DungeonEventManager.Instance != null)
            {
                DungeonEventManager.Instance.MinimizeActiveEvent();
            }
        }

        private void HandleReopenClicked()
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
            if (_reopenPill != null) _reopenPill.style.display = DisplayStyle.None;

            if (pauseTimeWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (DungeonEventManager.Instance != null)
            {
                DungeonEventManager.Instance.ReopenActiveEvent();
            }

            RefreshView();
        }

        private void BuildProceduralUI(VisualElement root)
        {
            var overlay = new VisualElement { name = "altar-overlay" };
            overlay.AddToClassList("altar-overlay");

            var card = new VisualElement { name = "altar-card" };
            card.AddToClassList("altar-card");

            var header = new VisualElement { name = "altar-header" };
            header.AddToClassList("altar-header");
            var title = new Label("ALTAR OF TRANSMUTATION") { name = "altar-title" };
            title.AddToClassList("altar-title");
            var cost = new Label("Cost: 10") { name = "altar-cost-badge" };
            cost.AddToClassList("cost-badge");
            header.Add(title);
            header.Add(cost);
            card.Add(header);

            var body = new VisualElement { name = "altar-body" };
            body.AddToClassList("altar-body");

            var leftCol = new VisualElement { name = "left-column" };
            leftCol.AddToClassList("left-column");
            var unitList = new VisualElement { name = "unit-list-container" };
            leftCol.Add(unitList);
            body.Add(leftCol);

            var rightCol = new VisualElement { name = "right-column" };
            rightCol.AddToClassList("right-column");
            var traitCards = new VisualElement { name = "trait-cards-container" };
            rightCol.Add(traitCards);
            body.Add(rightCol);

            card.Add(body);

            var footer = new VisualElement { name = "altar-footer" };
            footer.AddToClassList("altar-footer");
            var skip = new Button { name = "skip-btn", text = "Leave Altar" };
            skip.AddToClassList("secondary-btn");
            var confirm = new Button { name = "confirm-btn", text = "Transmute Trait" };
            confirm.AddToClassList("primary-btn");
            footer.Add(skip);
            footer.Add(confirm);
            card.Add(footer);

            overlay.Add(card);
            root.Add(overlay);

            var pill = new Button { name = "reopen-pill", text = "⚡ Altar Active" };
            pill.AddToClassList("reopen-pill");
            pill.style.display = DisplayStyle.None;
            root.Add(pill);
        }
    }
}
