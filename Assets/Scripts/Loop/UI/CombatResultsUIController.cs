using System;
using ArtificeToolkit.Attributes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Loop.UI
{
    /// <summary>
    /// UI Controller for the Combat Results Screen.
    /// - Displays when combat ends (Victory or Defeat).
    /// - Shows outcome banner, round metrics, adventurer casualties/retreats, boss vitals, and energy/essence rewards.
    /// - Pauses time while open, and proceeds to the next round upon clicking 'Continue'.
    /// - Built with UI Toolkit (UIDocument) with automatic procedural styling (zero setup required).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatResultsUIController : MonoBehaviour
    {
        public static CombatResultsUIController Instance { get; private set; }

        [Header("Dependencies")]
        [Tooltip("Reference to the CombatResultHandler.")]
        [SerializeField] private CombatResultHandler combatHandler;

        [Tooltip("Reference to CoreGameLoop.")]
        [SerializeField] private CoreGameLoop coreGameLoop;

        [Header("Configuration")]
        [Tooltip("When true, pauses gameplay simulation while the results screen is open.")]
        [SerializeField] private bool pauseTimeWhileOpen = true;

        // UI Toolkit Elements
        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement modalOverlay;
        private VisualElement modalCard;
        private Label titleLabel;
        private Label subtitleLabel;
        private VisualElement badgeContainer;
        private Label badgeLabel;
        private VisualElement statsRow;
        private Label summaryTextLabel;
        private Button continueButton;

        // State
        private bool isOpen = false;
        private CombatResultData currentData;
        private float previousTimeScale = 1f;

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            var existing = FindAnyObjectByType<CombatResultsUIController>();
            if (existing != null) return;

            var go = new GameObject("[CombatResultsUI]");
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.sortingOrder = 999;

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

            go.AddComponent<CombatResultsUIController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return;
            }
            Instance = this;

            EnsurePanelSettings();
            FindAndSubscribeCombatHandler();

            if (coreGameLoop == null)
            {
                coreGameLoop = GetComponent<CoreGameLoop>() ?? FindAnyObjectByType<CoreGameLoop>(FindObjectsInactive.Include);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnsubscribeCombatHandler();
        }

        private void EnsurePanelSettings()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) uiDocument = gameObject.AddComponent<UIDocument>();

            if (uiDocument.panelSettings == null)
            {
                uiDocument.panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
            }

            if (uiDocument.panelSettings == null)
            {
                var allDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
                foreach (var d in allDocs)
                {
                    if (d != uiDocument && d.panelSettings != null)
                    {
                        uiDocument.panelSettings = d.panelSettings;
                        break;
                    }
                }
            }

            if (uiDocument.panelSettings == null)
            {
                var allSettings = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (allSettings != null && allSettings.Length > 0)
                {
                    uiDocument.panelSettings = allSettings[0];
                }
            }

            uiDocument.sortingOrder = 999;
        }

        public void Initialize(CombatResultHandler handler = null, CoreGameLoop loop = null)
        {
            EnsurePanelSettings();
            UnsubscribeCombatHandler();

            if (handler != null)
            {
                combatHandler = handler;
            }

            if (loop != null)
            {
                coreGameLoop = loop;
            }

            FindAndSubscribeCombatHandler();
        }

        private void OnEnable()
        {
            EnsurePanelSettings();
            FindAndSubscribeCombatHandler();
            SetupUIElements();
        }

        private void OnDisable()
        {
            UnsubscribeCombatHandler();
        }

        private void Start()
        {
            EnsurePanelSettings();
            FindAndSubscribeCombatHandler();
            SetupUIElements();
            if (isOpen)
            {
                ShowResults();
            }
            else
            {
                HideResults();
            }
        }

        private void FindAndSubscribeCombatHandler()
        {
            if (combatHandler == null)
            {
                combatHandler = GetComponent<CombatResultHandler>() ?? FindAnyObjectByType<CombatResultHandler>(FindObjectsInactive.Include);
            }

            if (combatHandler != null)
            {
                combatHandler.OnShowResultsScreen -= HandleCombatEnded;
                combatHandler.OnCombatReset -= HandleCombatReset;
                combatHandler.OnShowResultsScreen += HandleCombatEnded;
                combatHandler.OnCombatReset += HandleCombatReset;
            }
        }

        private void UnsubscribeCombatHandler()
        {
            if (combatHandler != null)
            {
                combatHandler.OnShowResultsScreen -= HandleCombatEnded;
                combatHandler.OnCombatReset -= HandleCombatReset;
            }
        }

        private void SetupUIElements()
        {
            EnsurePanelSettings();
            if (uiDocument == null) return;

            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null) return;

            rootElement.Clear();
            rootElement.style.flexGrow = 1;
            rootElement.style.width = Length.Percent(100);
            rootElement.style.height = Length.Percent(100);
            rootElement.style.position = Position.Absolute;
            rootElement.style.alignItems = Align.Center;
            rootElement.style.justifyContent = Justify.Center;
            rootElement.pickingMode = PickingMode.Ignore;

            // 1. Modal Overlay
            modalOverlay = new VisualElement();
            modalOverlay.style.position = Position.Absolute;
            modalOverlay.style.width = Length.Percent(100);
            modalOverlay.style.height = Length.Percent(100);
            modalOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.75f);
            modalOverlay.style.alignItems = Align.Center;
            modalOverlay.style.justifyContent = Justify.Center;
            modalOverlay.pickingMode = PickingMode.Position;

            // 2. Results Modal Card
            modalCard = new VisualElement();
            modalCard.style.width = 520;
            modalCard.style.backgroundColor = new Color(0.09f, 0.11f, 0.16f, 0.98f);
            modalCard.style.borderTopWidth = 2;
            modalCard.style.borderBottomWidth = 2;
            modalCard.style.borderLeftWidth = 2;
            modalCard.style.borderRightWidth = 2;
            modalCard.style.borderTopColor = new Color(0.3f, 0.35f, 0.48f);
            modalCard.style.borderBottomColor = new Color(0.3f, 0.35f, 0.48f);
            modalCard.style.borderLeftColor = new Color(0.3f, 0.35f, 0.48f);
            modalCard.style.borderRightColor = new Color(0.3f, 0.35f, 0.48f);
            modalCard.style.borderTopLeftRadius = 14;
            modalCard.style.borderTopRightRadius = 14;
            modalCard.style.borderBottomLeftRadius = 14;
            modalCard.style.borderBottomRightRadius = 14;
            modalCard.style.paddingTop = 26;
            modalCard.style.paddingBottom = 26;
            modalCard.style.paddingLeft = 28;
            modalCard.style.paddingRight = 28;
            modalCard.style.alignItems = Align.Center;

            // Title Banner
            titleLabel = new Label("VICTORY");
            titleLabel.style.fontSize = 32;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.letterSpacing = 2;
            titleLabel.style.marginBottom = 4;
            modalCard.Add(titleLabel);

            // Subtitle / Round Duration
            subtitleLabel = new Label("Round 1 • Defense Concluded in 12.4s");
            subtitleLabel.style.fontSize = 13;
            subtitleLabel.style.color = new Color(0.65f, 0.72f, 0.85f);
            subtitleLabel.style.marginBottom = 16;
            modalCard.Add(subtitleLabel);

            // Reward / Penalty Badge
            badgeContainer = new VisualElement();
            badgeContainer.style.flexDirection = FlexDirection.Row;
            badgeContainer.style.alignItems = Align.Center;
            badgeContainer.style.justifyContent = Justify.Center;
            badgeContainer.style.borderTopLeftRadius = 8;
            badgeContainer.style.borderTopRightRadius = 8;
            badgeContainer.style.borderBottomLeftRadius = 8;
            badgeContainer.style.borderBottomRightRadius = 8;
            badgeContainer.style.paddingTop = 8;
            badgeContainer.style.paddingBottom = 8;
            badgeContainer.style.paddingLeft = 18;
            badgeContainer.style.paddingRight = 18;
            badgeContainer.style.marginBottom = 18;

            badgeLabel = new Label("+25 Energy Awarded");
            badgeLabel.style.fontSize = 15;
            badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            badgeContainer.Add(badgeLabel);
            modalCard.Add(badgeContainer);

            // Stats Cards Row
            statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.width = Length.Percent(100);
            statsRow.style.justifyContent = Justify.SpaceBetween;
            statsRow.style.marginBottom = 16;
            modalCard.Add(statsRow);

            // Summary Message Label
            summaryTextLabel = new Label();
            summaryTextLabel.style.fontSize = 13;
            summaryTextLabel.style.color = new Color(0.8f, 0.84f, 0.92f);
            summaryTextLabel.style.whiteSpace = WhiteSpace.Normal;
            summaryTextLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            summaryTextLabel.style.marginBottom = 22;
            modalCard.Add(summaryTextLabel);

            // Continue Button
            continueButton = new Button(OnContinueClicked);
            continueButton.text = "Continue to Planning";
            continueButton.style.width = 260;
            continueButton.style.height = 46;
            continueButton.style.fontSize = 16;
            continueButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            continueButton.style.color = Color.white;
            continueButton.style.borderTopLeftRadius = 8;
            continueButton.style.borderTopRightRadius = 8;
            continueButton.style.borderBottomLeftRadius = 8;
            continueButton.style.borderBottomRightRadius = 8;
            continueButton.style.borderTopWidth = 0;
            continueButton.style.borderBottomWidth = 0;
            continueButton.style.borderLeftWidth = 0;
            continueButton.style.borderRightWidth = 0;
            modalCard.Add(continueButton);

            modalOverlay.Add(modalCard);
            rootElement.Add(modalOverlay);
        }

        private void HandleCombatEnded(CombatResultData data)
        {
            currentData = data;
            ShowResults();
        }

        private void HandleCombatReset()
        {
            HideResults();
        }

        public void ShowResults()
        {
            isOpen = true;
            if (pauseTimeWhileOpen && Application.isPlaying)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            EnsurePanelSettings();

            if (modalOverlay == null || rootElement == null)
            {
                SetupUIElements();
            }

            if (modalOverlay != null)
            {
                modalOverlay.style.display = DisplayStyle.Flex;
                modalOverlay.style.visibility = Visibility.Visible;
                modalOverlay.style.opacity = 1f;

                bool isWin = currentData.Outcome == BattleOutcome.Victory;

                titleLabel.text = isWin ? "DUNGEON DEFENDED" : "DEFENSE BREACHED";
                titleLabel.style.color = isWin ? new Color(0.25f, 0.9f, 0.45f) : new Color(0.95f, 0.28f, 0.28f);

                modalCard.style.borderTopColor = isWin ? new Color(0.2f, 0.6f, 0.35f) : new Color(0.7f, 0.2f, 0.25f);
                modalCard.style.borderBottomColor = modalCard.style.borderTopColor;
                modalCard.style.borderLeftColor = modalCard.style.borderTopColor;
                modalCard.style.borderRightColor = modalCard.style.borderTopColor;

                subtitleLabel.text = $"Round {currentData.RoundNumber} • Duration: {currentData.DurationSeconds:F1}s";

                if (isWin)
                {
                    int energy = currentData.EnergyReward.HasValue ? currentData.EnergyReward.Value : 25;
                    badgeContainer.style.backgroundColor = new Color(0.12f, 0.45f, 0.22f, 0.6f);
                    badgeLabel.text = $"+{energy} Magical Energy Awarded";
                    badgeLabel.style.color = new Color(0.55f, 1.0f, 0.65f);
                }
                else
                {
                    int essence = currentData.EssenceDamage.HasValue ? currentData.EssenceDamage.Value : 25;
                    badgeContainer.style.backgroundColor = new Color(0.55f, 0.12f, 0.15f, 0.6f);
                    badgeLabel.text = $"-{essence} Master Essence Lost";
                    badgeLabel.style.color = new Color(1.0f, 0.55f, 0.55f);
                }

                statsRow.Clear();
                statsRow.Add(CreateStatCard("Adventurers Slain", currentData.DefeatedAdventurers.ToString(), new Color(0.95f, 0.4f, 0.4f)));
                statsRow.Add(CreateStatCard("Retreated", currentData.RetreatedAdventurers.ToString(), new Color(0.95f, 0.75f, 0.3f)));
                
                string bossHealthStr = currentData.BossRecord != null
                    ? $"{currentData.BossRecord.CurrentHealth:F0}/{currentData.BossRecord.MaxHealth:F0}"
                    : (isWin ? "Guarded" : "Fallen");
                statsRow.Add(CreateStatCard("Guardian HP", bossHealthStr, new Color(0.45f, 0.75f, 1.0f)));

                summaryTextLabel.text = $"<i>\"{currentData.SummaryMessage}\"</i>";

                continueButton.text = isWin ? "Continue to Next Round" : "Acknowledge Defeat";
                continueButton.style.backgroundColor = isWin ? new Color(0.18f, 0.52f, 0.88f) : new Color(0.6f, 0.22f, 0.25f);
            }
        }

        private VisualElement CreateStatCard(string labelText, string valueText, Color valueColor)
        {
            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.marginLeft = 4;
            card.style.marginRight = 4;
            card.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.alignItems = Align.Center;

            var valLbl = new Label(valueText);
            valLbl.style.fontSize = 18;
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valLbl.style.color = valueColor;
            valLbl.style.marginBottom = 2;
            card.Add(valLbl);

            var titleLbl = new Label(labelText);
            titleLbl.style.fontSize = 11;
            titleLbl.style.color = new Color(0.65f, 0.7f, 0.8f);
            card.Add(titleLbl);

            return card;
        }

        public void HideResults()
        {
            isOpen = false;
            if (pauseTimeWhileOpen && Application.isPlaying && Time.timeScale == 0f)
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            }

            if (modalOverlay != null)
            {
                modalOverlay.style.display = DisplayStyle.None;
            }
        }

        private void OnContinueClicked()
        {
            HideResults();

            if (combatHandler != null)
            {
                combatHandler.AcknowledgeResultsAndProceed();
            }
        }

        #region Inspector Debug Methods

        [ContextMenu("Debug/Preview Victory Screen")]
        [Button]
        public void DebugPreviewVictoryScreen()
        {
            currentData = new CombatResultData(
                BattleOutcome.Victory,
                roundNumber: 1,
                durationSeconds: 15.2f,
                totalAdventurers: 4,
                defeatedAdventurers: 3,
                retreatedAdventurers: 1,
                survivingAdventurers: 0,
                adventurerRecords: null,
                bossRecord: new BossCombatRecord("boss_1", "Dungeon Master Guardian", 1000f, 850f),
                energyReward: 25,
                essenceDamage: 0,
                summaryMessage: "Dungeon successfully defended! All intruders neutralized."
            );
            ShowResults();
        }

        [ContextMenu("Debug/Preview Defeat Screen")]
        [Button]
        public void DebugPreviewDefeatScreen()
        {
            currentData = new CombatResultData(
                BattleOutcome.Defeat,
                roundNumber: 1,
                durationSeconds: 22.8f,
                totalAdventurers: 5,
                defeatedAdventurers: 2,
                retreatedAdventurers: 0,
                survivingAdventurers: 3,
                adventurerRecords: null,
                bossRecord: new BossCombatRecord("boss_1", "Dungeon Master Guardian", 1000f, 0f),
                energyReward: 0,
                essenceDamage: 25,
                summaryMessage: "Final Boss was destroyed. Dungeon defense breached!"
            );
            ShowResults();
        }

        #endregion
    }
}
