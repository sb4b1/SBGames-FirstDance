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
    /// UI Toolkit controller managing the Dungeon Master Abilities & Actions Menu.
    /// - Toggled via 'H' key (registered with InputManager on UIButton.H).
    /// - Includes Core DM actions from DungeonMaster.cs:
    ///   * Recover Essence (spends energy to heal essence)
    ///   * Forge Artifact (spends essence to create new anchors)
    ///   * Empower Artifact (spends energy to level up artifact power)
    ///   * Sacrifice Artifact (sacrifices artifact for emergency energy)
    /// - Displays and triggers unlocked active powers from the DM Talent Tree during Battle.
    /// - Pauses game simulation while open.
    /// - Supports zero-setup Auto-Bootstrapping.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class DMAbilitiesUIController : MonoBehaviour, IToggleableUI
    {
        public static DMAbilitiesUIController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private DungeonMaster dungeonMaster;
        [SerializeField] private DMTalentTreeManager talentManager;
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonProgression dungeonProgression;

        [Header("UI Configuration")]
        [Tooltip("When true, Time.timeScale is frozen to 0 while the menu is open.")]
        [SerializeField] private bool pauseTimeWhileOpen = true;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement modalContainer;
        private Label headerStatsLabel;
        private Label feedbackLabel;
        private VisualElement artifactsListContainer;
        private VisualElement activePowersContainer;

        private bool isOpen = false;
        private float previousTimeScale = 1f;
        private bool isRegisteredWithInputManager = false;

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            var existing = FindAnyObjectByType<DMAbilitiesUIController>();
            if (existing != null) return;

            var go = new GameObject("[DMAbilitiesUI]");
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.sortingOrder = 905;

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

            go.AddComponent<DMAbilitiesUIController>();
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
            uiDocument.sortingOrder = 905;

            EnsureDependencies();
            BuildUIHierarchy();
        }

        private void Start()
        {
            RegisterWithInputManager();

            if (dungeonMaster != null)
            {
                dungeonMaster.OnEnergyChanged += (c, m) => RefreshUI();
                dungeonMaster.OnEssenceChanged += (c, m) => RefreshUI();
                dungeonMaster.OnArtifactForged += a => RefreshUI();
                dungeonMaster.OnArtifactEmpowered += a => RefreshUI();
                dungeonMaster.OnArtifactRemoved += a => RefreshUI();
            }

            if (talentManager != null)
            {
                talentManager.OnTalentUnlocked += t => RefreshUI();
                talentManager.OnActivePowerTriggered += t => RefreshUI();
            }

            Close();
        }

        private void OnDestroy()
        {
            UnregisterFromInputManager();

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

            if (!isRegisteredWithInputManager && WasHKeyPressedThisFrame())
            {
                Toggle();
            }
        }

        private bool WasHKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.H);
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
            rootElement.style.backgroundColor = new Color(0.04f, 0.03f, 0.06f, 0.92f);
            rootElement.style.justifyContent = Justify.Center;
            rootElement.style.alignItems = Align.Center;

            // Main Modal Window Card
            modalContainer = new VisualElement();
            modalContainer.pickingMode = PickingMode.Position;
            modalContainer.style.width = Length.Percent(88);
            modalContainer.style.height = Length.Percent(84);
            modalContainer.style.backgroundColor = new Color(0.12f, 0.10f, 0.16f, 0.98f);
            modalContainer.style.borderTopLeftRadius = 12;
            modalContainer.style.borderTopRightRadius = 12;
            modalContainer.style.borderBottomLeftRadius = 12;
            modalContainer.style.borderBottomRightRadius = 12;
            modalContainer.style.borderLeftWidth = 2;
            modalContainer.style.borderRightWidth = 2;
            modalContainer.style.borderTopWidth = 2;
            modalContainer.style.borderBottomWidth = 2;
            modalContainer.style.borderLeftColor = new Color(0.65f, 0.35f, 0.55f, 0.8f);
            modalContainer.style.borderRightColor = new Color(0.65f, 0.35f, 0.55f, 0.8f);
            modalContainer.style.borderTopColor = new Color(0.65f, 0.35f, 0.55f, 0.8f);
            modalContainer.style.borderBottomColor = new Color(0.65f, 0.35f, 0.55f, 0.8f);
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
            var title = new Label("👑 DUNGEON MASTER SANCTUM");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.75f, 0.35f);
            titleGroup.Add(title);

            var subtitle = new Label("Core DM Vitals, Artifact Anchors & Battle Powers [H]");
            subtitle.style.fontSize = 12;
            subtitle.style.color = new Color(0.75f, 0.7f, 0.8f);
            titleGroup.Add(subtitle);
            headerRow.Add(titleGroup);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            headerStatsLabel = new Label("");
            headerStatsLabel.style.fontSize = 14;
            headerStatsLabel.style.color = new Color(0.9f, 0.92f, 0.98f);
            headerStatsLabel.style.backgroundColor = new Color(0.06f, 0.05f, 0.10f, 0.9f);
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
            closeBtn.text = "✕ CLOSE (H)";
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

            // Feedback / Status message label
            feedbackLabel = new Label("");
            feedbackLabel.style.fontSize = 12;
            feedbackLabel.style.color = new Color(0.95f, 0.8f, 0.25f);
            feedbackLabel.style.marginBottom = 8;
            feedbackLabel.style.display = DisplayStyle.None;
            modalContainer.Add(feedbackLabel);

            // Two-Column Body Content
            var bodyRow = new VisualElement();
            bodyRow.style.flexDirection = FlexDirection.Row;
            bodyRow.style.flexGrow = 1;

            // Left Column: Core DM Actions & Artifacts
            var leftCol = new VisualElement();
            leftCol.style.flexGrow = 1.2f;
            leftCol.style.marginRight = 16;
            leftCol.style.backgroundColor = new Color(0.08f, 0.07f, 0.11f, 0.9f);
            leftCol.style.paddingTop = 12;
            leftCol.style.paddingBottom = 12;
            leftCol.style.paddingLeft = 14;
            leftCol.style.paddingRight = 14;
            leftCol.style.borderTopLeftRadius = 8;
            leftCol.style.borderTopRightRadius = 8;
            leftCol.style.borderBottomLeftRadius = 8;
            leftCol.style.borderBottomRightRadius = 8;

            var coreHeader = new Label("🔮 Core Sanctum Operations");
            coreHeader.style.fontSize = 15;
            coreHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            coreHeader.style.color = new Color(0.9f, 0.8f, 0.4f);
            coreHeader.style.marginBottom = 10;
            leftCol.Add(coreHeader);

            // Actions Row (Recover Essence + Forge Artifact)
            var actionBtnsRow = new VisualElement();
            actionBtnsRow.style.flexDirection = FlexDirection.Row;
            actionBtnsRow.style.marginBottom = 12;

            var recoverBtn = new Button(OnRecoverEssenceClicked);
            recoverBtn.text = "❤️ Recover Essence (-20E -> +15HP)";
            recoverBtn.style.flexGrow = 1;
            recoverBtn.style.height = 36;
            recoverBtn.style.fontSize = 11;
            recoverBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            recoverBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.25f);
            recoverBtn.style.color = Color.white;
            recoverBtn.style.marginRight = 8;
            recoverBtn.style.borderTopLeftRadius = 6;
            recoverBtn.style.borderTopRightRadius = 6;
            recoverBtn.style.borderBottomLeftRadius = 6;
            recoverBtn.style.borderBottomRightRadius = 6;
            actionBtnsRow.Add(recoverBtn);

            var forgeBtn = new Button(OnForgeArtifactClicked);
            forgeBtn.text = "⚒️ Forge Artifact (-25HP -> New Anchor)";
            forgeBtn.style.flexGrow = 1;
            forgeBtn.style.height = 36;
            forgeBtn.style.fontSize = 11;
            forgeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            forgeBtn.style.backgroundColor = new Color(0.5f, 0.35f, 0.15f);
            forgeBtn.style.color = Color.white;
            forgeBtn.style.borderTopLeftRadius = 6;
            forgeBtn.style.borderTopRightRadius = 6;
            forgeBtn.style.borderBottomLeftRadius = 6;
            forgeBtn.style.borderBottomRightRadius = 6;
            actionBtnsRow.Add(forgeBtn);

            leftCol.Add(actionBtnsRow);

            // Held Artifacts Section
            var artHeader = new Label("💎 Soul Anchors & Artifacts (Losing all = Defeat)");
            artHeader.style.fontSize = 13;
            artHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            artHeader.style.color = new Color(0.75f, 0.8f, 0.95f);
            artHeader.style.marginBottom = 8;
            leftCol.Add(artHeader);

            var artScroll = new ScrollView();
            artScroll.style.flexGrow = 1;
            artifactsListContainer = new VisualElement();
            artScroll.Add(artifactsListContainer);
            leftCol.Add(artScroll);

            bodyRow.Add(leftCol);

            // Right Column: Unlocked Active Powers
            var rightCol = new VisualElement();
            rightCol.style.flexGrow = 1f;
            rightCol.style.backgroundColor = new Color(0.08f, 0.07f, 0.11f, 0.9f);
            rightCol.style.paddingTop = 12;
            rightCol.style.paddingBottom = 12;
            rightCol.style.paddingLeft = 14;
            rightCol.style.paddingRight = 14;
            rightCol.style.borderTopLeftRadius = 8;
            rightCol.style.borderTopRightRadius = 8;
            rightCol.style.borderBottomLeftRadius = 8;
            rightCol.style.borderBottomRightRadius = 8;

            var powersHeader = new Label("⚡ Active Talent Powers (Battle Phase)");
            powersHeader.style.fontSize = 15;
            powersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            powersHeader.style.color = new Color(0.4f, 0.75f, 0.95f);
            powersHeader.style.marginBottom = 10;
            rightCol.Add(powersHeader);

            var powersScroll = new ScrollView();
            powersScroll.style.flexGrow = 1;
            activePowersContainer = new VisualElement();
            powersScroll.Add(activePowersContainer);
            rightCol.Add(powersScroll);

            bodyRow.Add(rightCol);

            modalContainer.Add(bodyRow);
            rootElement.Add(modalContainer);
        }

        public void RefreshUI()
        {
            if (dungeonMaster == null) EnsureDependencies();

            // Header Stats
            if (headerStatsLabel != null)
            {
                int energy = dungeonMaster != null ? dungeonMaster.Energy : 0;
                int maxEnergy = dungeonMaster != null ? dungeonMaster.MaxEnergy : 100;
                int essence = dungeonMaster != null ? dungeonMaster.Essence : 0;
                int maxEssence = dungeonMaster != null ? dungeonMaster.MaxEssence : 100;
                int artCount = dungeonMaster != null ? dungeonMaster.ArtifactCount : 0;
                string phase = coreGameLoop != null ? coreGameLoop.CurrentPhase.ToString() : "Planning";

                headerStatsLabel.text = $"⚡ Energy: {energy}/{maxEnergy}  |  ❤️ Essence: {essence}/{maxEssence}  |  💎 Anchors: {artCount}  |  ⏳ {phase}";
            }

            // Refresh Artifact Cards
            RefreshArtifactsList();

            // Refresh Active Powers
            RefreshActivePowersList();
        }

        private void RefreshArtifactsList()
        {
            if (artifactsListContainer == null || dungeonMaster == null) return;
            artifactsListContainer.Clear();

            var artifacts = dungeonMaster.Artifacts;
            if (artifacts == null || artifacts.Count == 0)
            {
                var emptyLabel = new Label("No artifacts held! (Critical danger of defeat)");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.9f, 0.3f, 0.3f);
                artifactsListContainer.Add(emptyLabel);
                return;
            }

            foreach (var art in artifacts)
            {
                if (art == null) continue;

                var card = new VisualElement();
                card.style.flexDirection = FlexDirection.Row;
                card.style.justifyContent = Justify.SpaceBetween;
                card.style.alignItems = Align.Center;
                card.style.backgroundColor = new Color(0.12f, 0.13f, 0.18f, 0.9f);
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.marginBottom = 6;
                card.style.borderTopLeftRadius = 6;
                card.style.borderTopRightRadius = 6;
                card.style.borderBottomLeftRadius = 6;
                card.style.borderBottomRightRadius = 6;

                var infoGroup = new VisualElement();
                var nameLbl = new Label($"💎 {art.ArtifactName}");
                nameLbl.style.fontSize = 12;
                nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = new Color(0.9f, 0.85f, 0.6f);
                infoGroup.Add(nameLbl);

                var powerText = $"Power Level: {art.PowerLevel}  |  Essence Cost: {art.EssenceCost}";
                if (art.IsEmpowered)
                {
                    powerText += "  |  🌐 Global Aura";
                }
                var powerLbl = new Label(powerText);
                powerLbl.style.fontSize = 10;
                powerLbl.style.color = art.IsEmpowered ? new Color(0.6f, 0.9f, 0.7f) : new Color(0.7f, 0.72f, 0.8f);
                infoGroup.Add(powerLbl);
                card.Add(infoGroup);

                // Button group (Empower + Sacrifice)
                var btnGroup = new VisualElement();
                btnGroup.style.flexDirection = FlexDirection.Row;

                var empowerBtn = new Button(() => OnEmpowerArtifactClicked(art));
                empowerBtn.style.fontSize = 10;
                empowerBtn.style.marginRight = 6;

                bool canMultiEmpower = dungeonMaster != null && dungeonMaster.CanMultiEmpower();
                bool hasEnergy = dungeonMaster != null && dungeonMaster.Energy >= 15;

                if (!art.IsEmpowered)
                {
                    empowerBtn.text = "⚡ Empower (15E)";
                    empowerBtn.style.backgroundColor = new Color(0.2f, 0.45f, 0.65f);
                    empowerBtn.style.color = Color.white;
                    empowerBtn.SetEnabled(hasEnergy);
                }
                else if (canMultiEmpower)
                {
                    empowerBtn.text = "⚡ Overcharge (+1 Pwr, 15E)";
                    empowerBtn.style.backgroundColor = new Color(0.2f, 0.55f, 0.75f);
                    empowerBtn.style.color = Color.white;
                    empowerBtn.SetEnabled(hasEnergy);
                }
                else
                {
                    empowerBtn.text = "⚡ Empowered (Requires Relic Overcharge Talent)";
                    empowerBtn.style.backgroundColor = new Color(0.3f, 0.35f, 0.4f);
                    empowerBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
                    empowerBtn.SetEnabled(false);
                }

                btnGroup.Add(empowerBtn);

                var sacrificeBtn = new Button(() => OnSacrificeArtifactClicked(art));
                sacrificeBtn.text = "🔥 Sacrifice (+25E)";
                sacrificeBtn.style.fontSize = 10;
                sacrificeBtn.style.backgroundColor = new Color(0.6f, 0.25f, 0.2f);
                sacrificeBtn.style.color = Color.white;
                btnGroup.Add(sacrificeBtn);

                card.Add(btnGroup);
                artifactsListContainer.Add(card);
            }
        }

        private void RefreshActivePowersList()
        {
            if (activePowersContainer == null) return;
            activePowersContainer.Clear();

            if (talentManager == null)
            {
                var noManagerLabel = new Label("Talent Manager not connected.");
                activePowersContainer.Add(noManagerLabel);
                return;
            }

            var unlockedActivePowers = talentManager.UnlockedTalents
                .Where(t => t != null && t.Type == TalentType.ActivePower)
                .ToList();

            if (unlockedActivePowers.Count == 0)
            {
                var emptyLabel = new Label("No Active Battle Powers unlocked yet.\nUnlock active powers in the Talent Tree [P] to trigger tactical DM spells during battle.");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.6f, 0.62f, 0.7f);
                emptyLabel.style.marginTop = 10;
                activePowersContainer.Add(emptyLabel);
                return;
            }

            bool isBattlePhase = coreGameLoop == null || coreGameLoop.CurrentPhase == GamePhase.Battle;

            foreach (var power in unlockedActivePowers)
            {
                var card = new VisualElement();
                card.style.backgroundColor = new Color(0.12f, 0.13f, 0.18f, 0.9f);
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.marginBottom = 8;
                card.style.borderTopLeftRadius = 6;
                card.style.borderTopRightRadius = 6;
                card.style.borderBottomLeftRadius = 6;
                card.style.borderBottomRightRadius = 6;

                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.justifyContent = Justify.SpaceBetween;
                topRow.style.alignItems = Align.Center;

                var nameLbl = new Label(power.DisplayName);
                nameLbl.style.fontSize = 12;
                nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = new Color(0.45f, 0.8f, 1f);
                topRow.Add(nameLbl);

                float cooldown = talentManager.GetRemainingCooldown(power);
                string costText = power.ActiveEnergyCost > 0 ? $"Cost: {power.ActiveEnergyCost}E" : "Free";
                string cdText = cooldown > 0 ? $"⏳ {cooldown:F1}s" : "⚡ Ready";

                var statusLbl = new Label($"{costText}  |  {cdText}");
                statusLbl.style.fontSize = 10;
                statusLbl.style.color = cooldown > 0 ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.3f, 0.8f, 0.4f);
                topRow.Add(statusLbl);
                card.Add(topRow);

                var descLbl = new Label(power.Description);
                descLbl.style.fontSize = 10;
                descLbl.style.color = new Color(0.75f, 0.78f, 0.85f);
                descLbl.style.whiteSpace = WhiteSpace.Normal;
                descLbl.style.marginTop = 4;
                descLbl.style.marginBottom = 6;
                card.Add(descLbl);

                var triggerBtn = new Button(() => OnTriggerActivePowerClicked(power));
                triggerBtn.text = isBattlePhase ? $"TRIGGER POWER ({power.ActiveEnergyCost}E)" : "Locked (Battle Phase Only)";
                triggerBtn.style.height = 26;
                triggerBtn.style.fontSize = 11;
                triggerBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                triggerBtn.style.backgroundColor = (isBattlePhase && cooldown <= 0) ? new Color(0.2f, 0.55f, 0.85f) : new Color(0.3f, 0.3f, 0.35f);
                triggerBtn.style.color = Color.white;
                card.Add(triggerBtn);

                activePowersContainer.Add(card);
            }
        }

        #endregion

        #region Actions & Handlers

        private void OnRecoverEssenceClicked()
        {
            if (dungeonMaster == null) return;

            bool success = dungeonMaster.RecoverEssence(essenceAmount: 15, energyCost: 20);
            if (success)
            {
                ShowNotification("❤️ Restored +15 Essence for 20 Energy.");
                RefreshUI();
            }
            else
            {
                ShowNotification("⚠️ Could not recover essence (Already at max or insufficient energy).");
            }
        }

        private void OnForgeArtifactClicked()
        {
            if (dungeonMaster == null) return;

            string artName = $"Relic of Shadows #{dungeonMaster.ArtifactCount + 1}";
            var forged = dungeonMaster.ForgeArtifact(artName, essenceCost: 25, startingPower: 1);
            if (forged != null)
            {
                ShowNotification($"⚒️ Forged '{artName}' for 25 Essence!");
                RefreshUI();
            }
            else
            {
                ShowNotification("⚠️ Cannot forge artifact: Insufficient Essence (Requires 25 Essence).");
            }
        }

        private void OnEmpowerArtifactClicked(Artifact artifact)
        {
            if (dungeonMaster == null || artifact == null) return;

            if (artifact.IsEmpowered && !dungeonMaster.CanMultiEmpower())
            {
                ShowNotification("⚠️ Requires 'Relic Overcharge' talent to empower an artifact multiple times.");
                return;
            }

            if (dungeonMaster.Energy < 15)
            {
                ShowNotification("⚠️ Could not empower artifact: Insufficient Energy (Requires 15 Energy).");
                return;
            }

            bool wasEmpowered = artifact.IsEmpowered;
            bool success = dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);
            if (success)
            {
                string actionType = wasEmpowered ? "Overcharged" : "Empowered";
                ShowNotification($"⚡ {actionType} '{artifact.ArtifactName}' (Power Level {artifact.PowerLevel}, Global Aura active)!");
                RefreshUI();
            }
            else
            {
                ShowNotification("⚠️ Could not empower artifact.");
            }
        }

        private void OnSacrificeArtifactClicked(Artifact artifact)
        {
            if (dungeonMaster == null || artifact == null) return;

            if (dungeonMaster.ArtifactCount <= 1)
            {
                ShowNotification("⚠️ Cannot sacrifice your LAST anchor artifact (Doing so causes defeat)!");
                return;
            }

            bool success = dungeonMaster.SacrificeArtifact(artifact, energyReward: 25);
            if (success)
            {
                ShowNotification($"🔥 Sacrificed '{artifact.ArtifactName}' for +25 emergency Energy!");
                RefreshUI();
            }
        }

        private void OnTriggerActivePowerClicked(TalentNodeSO power)
        {
            if (talentManager == null || power == null) return;

            bool success = talentManager.TryActivatePower(power, out string failReason);
            if (success)
            {
                ShowNotification($"⚡ Triggered '{power.DisplayName}'!");
                RefreshUI();
            }
            else
            {
                ShowNotification($"⚠️ Could not trigger power: {failReason}");
            }
        }

        private void ShowNotification(string message)
        {
            if (feedbackLabel == null) return;
            feedbackLabel.text = message;
            feedbackLabel.style.display = DisplayStyle.Flex;
        }

        private void EnsureDependencies()
        {
            if (dungeonMaster == null)
            {
                dungeonMaster = FindAnyObjectByType<DungeonMaster>();
                if (dungeonMaster == null)
                {
                    var dmGo = new GameObject("[DungeonMaster]");
                    dungeonMaster = dmGo.AddComponent<DungeonMaster>();
                    dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 50);
                }
            }
            if (talentManager == null)
            {
                talentManager = FindAnyObjectByType<DMTalentTreeManager>();
            }
            if (coreGameLoop == null)
            {
                coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            }
            if (dungeonProgression == null)
            {
                dungeonProgression = FindAnyObjectByType<DungeonProgression>();
            }
        }

        private void RegisterWithInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.RegisterUISystem(UIButton.H, this);
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
