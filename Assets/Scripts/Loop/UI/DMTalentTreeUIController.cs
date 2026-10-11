using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Input;
using SBGames.Loop.Talents;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SBGames.Loop.UI
{
    /// <summary>
    /// UI Toolkit controller managing the Dungeon Master Talent Tree interface.
    /// - Toggled via 'P' key (registered with InputManager on UIButton.P).
    /// - Features a multi-column pillar layout (Core, Swarm, Trap, Boss, Capstones).
    /// - Dynamically renders node statuses, costs, prerequisites, and interactive unlock buttons.
    /// - Pauses game simulation while open.
    /// - Supports zero-setup Auto-Bootstrapping.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class DMTalentTreeUIController : MonoBehaviour, IToggleableUI
    {
        public static DMTalentTreeUIController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private DMTalentTreeManager talentManager;
        [SerializeField] private DungeonMaster dungeonMaster;
        [SerializeField] private DungeonProgression dungeonProgression;
        [SerializeField] private CoreGameLoop coreGameLoop;

        [Header("UI Configuration")]
        [Tooltip("When true, Time.timeScale is frozen to 0 while the menu is open.")]
        [SerializeField] private bool pauseTimeWhileOpen = true;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement modalContainer;
        private VisualElement columnsContainer;
        private Label headerStatsLabel;
        private Label statusMessageLabel;

        private bool isOpen = false;
        private float previousTimeScale = 1f;
        private bool isRegisteredWithInputManager = false;

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            if (SBGames.Testing.CombatTestEnvironmentBootstrap.ShouldBypassMetaAutoBootstrap()) return;

            var existing = FindAnyObjectByType<DMTalentTreeUIController>();
            if (existing != null) return;

            var go = new GameObject("[DMTalentTreeUI]");
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.sortingOrder = 900;

            var panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
            if (panelSettings == null)
            {
                var allDocs = FindObjectsByType<UIDocument>();
                foreach (var d in allDocs)
                {
                    if (d != uiDoc && d.panelSettings != null)
                    {
                        panelSettings = d.panelSettings;
                        break;
                    }
                }
            }
            if (panelSettings == null)
            {
                var allSettings = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (allSettings != null && allSettings.Length > 0)
                {
                    panelSettings = allSettings[0];
                }
            }
            if (panelSettings != null)
            {
                uiDoc.panelSettings = panelSettings;
            }

            go.AddComponent<DMTalentTreeUIController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }
            uiDocument.sortingOrder = 900;

            EnsureDependencies();
            BuildUIHierarchy();
        }

        private void Start()
        {
            RegisterWithInputManager();

            if (talentManager != null)
            {
                talentManager.OnTalentUnlocked += HandleTalentUnlocked;
                talentManager.OnTalentTreeReset += HandleTreeReset;
            }
            if (dungeonMaster != null)
            {
                dungeonMaster.OnEnergyChanged += (curr, max) => RefreshUI();
                dungeonMaster.OnEssenceChanged += (curr, max) => RefreshUI();
            }

            Close();
        }

        private void OnDestroy()
        {
            UnregisterFromInputManager();

            if (talentManager != null)
            {
                talentManager.OnTalentUnlocked -= HandleTalentUnlocked;
                talentManager.OnTalentTreeReset -= HandleTreeReset;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!isRegisteredWithInputManager)
            {
                RegisterWithInputManager();
            }

            if (!isRegisteredWithInputManager && WasPKeyPressedThisFrame())
            {
                Toggle();
            }
        }

        private bool WasPKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.P);
#else
            return false;
#endif
        }

        #region IToggleableUI Implementation

        public void Open()
        {
            EnsureDependencies();
            isOpen = true;

            if (rootElement == null || rootElement.childCount == 0)
            {
                BuildUIHierarchy();
            }

            if (rootElement != null)
            {
                rootElement.style.display = DisplayStyle.Flex;
            }

            if (pauseTimeWhileOpen)
            {
                previousTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }

            RefreshUI();
        }

        public void Close()
        {
            isOpen = false;
            if (rootElement != null)
            {
                rootElement.style.display = DisplayStyle.None;
            }

            if (pauseTimeWhileOpen)
            {
                Time.timeScale = previousTimeScale;
            }
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        #endregion

        #region UI Construction

        private void BuildUIHierarchy()
        {
            if (uiDocument == null) return;
            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null) return;
            rootElement.Clear();

            // Fullscreen backdrop overlay
            rootElement.pickingMode = PickingMode.Position;
            rootElement.style.position = Position.Absolute;
            rootElement.style.left = 0;
            rootElement.style.top = 0;
            rootElement.style.right = 0;
            rootElement.style.bottom = 0;
            rootElement.style.width = Length.Percent(100);
            rootElement.style.height = Length.Percent(100);
            rootElement.style.backgroundColor = new Color(0.03f, 0.03f, 0.06f, 0.92f);
            rootElement.style.justifyContent = Justify.Center;
            rootElement.style.alignItems = Align.Center;

            // Main Modal Window Card
            modalContainer = new VisualElement();
            modalContainer.pickingMode = PickingMode.Position;
            modalContainer.style.width = Length.Percent(92);
            modalContainer.style.height = Length.Percent(88);
            modalContainer.style.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 0.98f);
            modalContainer.style.borderTopLeftRadius = 12;
            modalContainer.style.borderTopRightRadius = 12;
            modalContainer.style.borderBottomLeftRadius = 12;
            modalContainer.style.borderBottomRightRadius = 12;
            modalContainer.style.borderLeftWidth = 2;
            modalContainer.style.borderRightWidth = 2;
            modalContainer.style.borderTopWidth = 2;
            modalContainer.style.borderBottomWidth = 2;
            modalContainer.style.borderLeftColor = new Color(0.35f, 0.45f, 0.65f, 0.8f);
            modalContainer.style.borderRightColor = new Color(0.35f, 0.45f, 0.65f, 0.8f);
            modalContainer.style.borderTopColor = new Color(0.35f, 0.45f, 0.65f, 0.8f);
            modalContainer.style.borderBottomColor = new Color(0.35f, 0.45f, 0.65f, 0.8f);
            modalContainer.style.paddingTop = 16;
            modalContainer.style.paddingBottom = 16;
            modalContainer.style.paddingLeft = 20;
            modalContainer.style.paddingRight = 20;

            // Header Bar
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 12;

            var titleGroup = new VisualElement();
            var title = new Label("⚔ DUNGEON MASTER TALENTS");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.82f, 0.35f);
            titleGroup.Add(title);

            var subtitle = new Label("Unlock autonomous combat mechanics & passive enhancements [P]");
            subtitle.style.fontSize = 12;
            subtitle.style.color = new Color(0.7f, 0.72f, 0.8f);
            titleGroup.Add(subtitle);
            headerRow.Add(titleGroup);

            // Right header stats & close button
            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            headerStatsLabel = new Label("");
            headerStatsLabel.style.fontSize = 14;
            headerStatsLabel.style.color = new Color(0.9f, 0.92f, 0.98f);
            headerStatsLabel.style.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 0.9f);
            headerStatsLabel.style.paddingTop = 6;
            headerStatsLabel.style.paddingBottom = 6;
            headerStatsLabel.style.paddingLeft = 12;
            headerStatsLabel.style.paddingRight = 12;
            headerStatsLabel.style.marginRight = 12;
            headerStatsLabel.style.borderTopLeftRadius = 6;
            headerStatsLabel.style.borderTopRightRadius = 6;
            headerStatsLabel.style.borderBottomLeftRadius = 6;
            headerStatsLabel.style.borderBottomRightRadius = 6;
            rightGroup.Add(headerStatsLabel);

            var closeBtn = new Button(Close);
            closeBtn.text = "✕ CLOSE (P)";
            closeBtn.style.height = 32;
            closeBtn.style.paddingLeft = 14;
            closeBtn.style.paddingRight = 14;
            closeBtn.style.fontSize = 12;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeBtn.style.backgroundColor = new Color(0.35f, 0.15f, 0.15f);
            closeBtn.style.color = Color.white;
            closeBtn.style.borderTopLeftRadius = 6;
            closeBtn.style.borderTopRightRadius = 6;
            closeBtn.style.borderBottomLeftRadius = 6;
            closeBtn.style.borderBottomRightRadius = 6;
            rightGroup.Add(closeBtn);

            headerRow.Add(rightGroup);
            modalContainer.Add(headerRow);

            // Status notification label
            statusMessageLabel = new Label("");
            statusMessageLabel.style.fontSize = 12;
            statusMessageLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
            statusMessageLabel.style.marginBottom = 8;
            statusMessageLabel.style.display = DisplayStyle.None;
            modalContainer.Add(statusMessageLabel);

            // Scrollable Columns Container
            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1;

            columnsContainer = new VisualElement();
            columnsContainer.style.flexDirection = FlexDirection.Row;
            columnsContainer.style.flexGrow = 1;
            scrollView.Add(columnsContainer);

            modalContainer.Add(scrollView);
            rootElement.Add(modalContainer);
        }

        public void RefreshUI()
        {
            if (headerStatsLabel != null)
            {
                int energy = dungeonMaster != null ? dungeonMaster.Energy : 0;
                int maxEnergy = dungeonMaster != null ? dungeonMaster.MaxEnergy : 100;
                int essence = dungeonMaster != null ? dungeonMaster.Essence : 0;
                int maxEssence = dungeonMaster != null ? dungeonMaster.MaxEssence : 100;
                string rank = dungeonProgression != null ? dungeonProgression.CurrentRank.GetDisplayName() : "Rank E";
                string phase = coreGameLoop != null ? coreGameLoop.CurrentPhase.ToString() : "Planning";

                headerStatsLabel.text = $"⚡ Energy: {energy}/{maxEnergy}  |  ❤️ Essence: {essence}/{maxEssence}  |  🏆 {rank}  |  ⏳ {phase}";
            }

            if (columnsContainer == null || talentManager == null) return;
            columnsContainer.Clear();

            var pillars = new[]
            {
                (TalentPillar.Core, "🏛️ Core Foundation", new Color(0.3f, 0.45f, 0.6f)),
                (TalentPillar.MonsterSwarm, "🕷️ Swarm Legion", new Color(0.2f, 0.55f, 0.3f)),
                (TalentPillar.TrapControl, "⚡ Eldritch Traps", new Color(0.55f, 0.4f, 0.15f)),
                (TalentPillar.ApexBoss, "👑 Apex Overlord", new Color(0.55f, 0.2f, 0.2f)),
                (TalentPillar.Hybrid, "🌌 Capstones & Hybrids", new Color(0.45f, 0.25f, 0.55f))
            };

            var allTalents = talentManager.GetAllTalents();

            foreach (var (pillar, title, headerColor) in pillars)
            {
                var col = CreatePillarColumn(pillar, title, headerColor, allTalents);
                columnsContainer.Add(col);
            }
        }

        private VisualElement CreatePillarColumn(
            TalentPillar pillar,
            string title,
            Color headerColor,
            IReadOnlyList<TalentNodeSO> allTalents)
        {
            var col = new VisualElement();
            col.style.width = 240;
            col.style.minWidth = 220;
            col.style.marginRight = 14;
            col.style.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 0.9f);
            col.style.borderTopLeftRadius = 8;
            col.style.borderTopRightRadius = 8;
            col.style.borderBottomLeftRadius = 8;
            col.style.borderBottomRightRadius = 8;
            col.style.paddingTop = 10;
            col.style.paddingBottom = 10;
            col.style.paddingLeft = 10;
            col.style.paddingRight = 10;

            // Pillar Header
            var colHeader = new Label(title);
            colHeader.style.fontSize = 14;
            colHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            colHeader.style.color = Color.white;
            colHeader.style.backgroundColor = headerColor;
            colHeader.style.paddingTop = 6;
            colHeader.style.paddingBottom = 6;
            colHeader.style.paddingLeft = 8;
            colHeader.style.paddingRight = 8;
            colHeader.style.borderTopLeftRadius = 6;
            colHeader.style.borderTopRightRadius = 6;
            colHeader.style.borderBottomLeftRadius = 6;
            colHeader.style.borderBottomRightRadius = 6;
            colHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            colHeader.style.marginBottom = 10;
            col.Add(colHeader);

            var pillarTalents = allTalents
                .Where(t => t != null && (t.Pillar == pillar || (pillar == TalentPillar.Hybrid && (t.Pillar == TalentPillar.Hybrid || t.Pillar == TalentPillar.Transcendent))))
                .OrderBy(t => (int)t.Tier)
                .ToList();

            if (pillarTalents.Count == 0)
            {
                var emptyLabel = new Label("No talents defined.");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                col.Add(emptyLabel);
                return col;
            }

            foreach (var talent in pillarTalents)
            {
                var card = CreateTalentCard(talent);
                col.Add(card);
            }

            return col;
        }

        private VisualElement CreateTalentCard(TalentNodeSO talent)
        {
            var card = new VisualElement();
            card.style.marginBottom = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;

            var status = talentManager.GetTalentStatus(talent);

            // Styling based on status
            Color borderCol;
            Color bgCol;
            string statusBadgeText;
            Color badgeBg;

            switch (status)
            {
                case TalentUnlockStatus.Unlocked:
                    borderCol = new Color(0.2f, 0.7f, 0.3f);
                    bgCol = new Color(0.08f, 0.16f, 0.10f);
                    statusBadgeText = "✓ UNLOCKED";
                    badgeBg = new Color(0.18f, 0.55f, 0.25f);
                    break;
                case TalentUnlockStatus.Available:
                    borderCol = new Color(0.3f, 0.6f, 0.95f);
                    bgCol = new Color(0.08f, 0.12f, 0.18f);
                    statusBadgeText = $"⚡ {talent.EnergyCost} Energy";
                    badgeBg = new Color(0.2f, 0.45f, 0.75f);
                    break;
                case TalentUnlockStatus.AffordableOnlyWhenGained:
                    borderCol = new Color(0.85f, 0.65f, 0.2f);
                    bgCol = new Color(0.14f, 0.12f, 0.08f);
                    statusBadgeText = $"⚡ Need {talent.EnergyCost}E";
                    badgeBg = new Color(0.55f, 0.4f, 0.15f);
                    break;
                default: // Locked
                    borderCol = new Color(0.3f, 0.32f, 0.38f);
                    bgCol = new Color(0.08f, 0.08f, 0.10f);
                    statusBadgeText = talent.MinimumRank > DungeonRank.E ? $"🔒 Req {talent.MinimumRank.GetDisplayName()}" : "🔒 Locked";
                    badgeBg = new Color(0.25f, 0.26f, 0.30f);
                    break;
            }

            card.style.borderLeftColor = borderCol;
            card.style.borderRightColor = borderCol;
            card.style.borderTopColor = borderCol;
            card.style.borderBottomColor = borderCol;
            card.style.backgroundColor = bgCol;

            // Card Header (Name & Status Badge)
            var cardHeader = new VisualElement();
            cardHeader.style.flexDirection = FlexDirection.Row;
            cardHeader.style.justifyContent = Justify.SpaceBetween;
            cardHeader.style.alignItems = Align.Center;
            cardHeader.style.marginBottom = 4;

            var nameLabel = new Label(talent.DisplayName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.flexShrink = 1;
            cardHeader.Add(nameLabel);

            var badge = new Label(statusBadgeText);
            badge.style.fontSize = 10;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = Color.white;
            badge.style.backgroundColor = badgeBg;
            badge.style.paddingLeft = 5;
            badge.style.paddingRight = 5;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            cardHeader.Add(badge);

            card.Add(cardHeader);

            // Description
            var descLabel = new Label(talent.Description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.8f, 0.82f, 0.88f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 6;
            card.Add(descLabel);

            // Unlock Button
            if (status == TalentUnlockStatus.Available)
            {
                var unlockBtn = new Button(() => OnUnlockClicked(talent));
                unlockBtn.text = $"UNLOCK ({talent.EnergyCost}E)";
                unlockBtn.style.height = 26;
                unlockBtn.style.fontSize = 11;
                unlockBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                unlockBtn.style.backgroundColor = new Color(0.22f, 0.6f, 0.32f);
                unlockBtn.style.color = Color.white;
                unlockBtn.style.borderTopLeftRadius = 4;
                unlockBtn.style.borderTopRightRadius = 4;
                unlockBtn.style.borderBottomLeftRadius = 4;
                unlockBtn.style.borderBottomRightRadius = 4;
                card.Add(unlockBtn);
            }
            else if (status == TalentUnlockStatus.AffordableOnlyWhenGained)
            {
                var disabledBtn = new Label($"Need {talent.EnergyCost} Energy");
                disabledBtn.style.fontSize = 10;
                disabledBtn.style.color = new Color(0.85f, 0.7f, 0.3f);
                disabledBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.Add(disabledBtn);
            }
            else if (status == TalentUnlockStatus.Locked && talent.Prerequisites != null && talent.Prerequisites.Count > 0)
            {
                string prereqNames = string.Join(", ", talent.Prerequisites.Where(p => p != null).Select(p => p.DisplayName));
                var prereqLabel = new Label($"Prereq: {prereqNames}");
                prereqLabel.style.fontSize = 10;
                prereqLabel.style.color = new Color(0.6f, 0.62f, 0.7f);
                card.Add(prereqLabel);
            }

            return card;
        }

        private void OnUnlockClicked(TalentNodeSO talent)
        {
            if (talentManager == null) return;

            bool success = talentManager.TryUnlockTalent(talent, out string reason);
            if (success)
            {
                ShowNotification($"✓ Successfully unlocked '{talent.DisplayName}'!");
                RefreshUI();
            }
            else
            {
                ShowNotification($"⚠️ Could not unlock: {reason}");
            }
        }

        private void ShowNotification(string message)
        {
            if (statusMessageLabel == null) return;
            statusMessageLabel.text = message;
            statusMessageLabel.style.display = DisplayStyle.Flex;
        }

        private void HandleTalentUnlocked(TalentNodeSO node)
        {
            if (isOpen) RefreshUI();
        }

        private void HandleTreeReset()
        {
            if (isOpen) RefreshUI();
        }

        private void EnsureDependencies()
        {
            if (talentManager == null)
            {
                talentManager = FindAnyObjectByType<DMTalentTreeManager>();
                if (talentManager == null)
                {
                    var tmGo = new GameObject("[DMTalentTreeManager]");
                    talentManager = tmGo.AddComponent<DMTalentTreeManager>();
                }
            }
            if (dungeonMaster == null)
            {
                dungeonMaster = FindAnyObjectByType<DungeonMaster>();
            }
            if (dungeonProgression == null)
            {
                dungeonProgression = FindAnyObjectByType<DungeonProgression>();
            }
            if (coreGameLoop == null)
            {
                coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            }
        }

        private void RegisterWithInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.RegisterUISystem(UIButton.P, this);
                isRegisteredWithInputManager = true;
            }
        }

        private void UnregisterFromInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.UnregisterUISystem(this);
            }
            isRegisteredWithInputManager = false;
        }

        #endregion
    }
}
