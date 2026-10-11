using System;
using SBGames.Input;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SBGames.Loop.UI
{
    /// <summary>
    /// UI Toolkit controller managing the Escape / Pause / Start Game menu.
    /// - On scene load, opens automatically to block gameplay until player clicks 'Start Game'.
    /// - During gameplay, pressing Escape toggles the menu (pausing and resuming time).
    /// - In-game shows 'Resume', 'Restart Run', and 'Quit Game', alongside live DM stats.
    /// - Implements IToggleableUI for unified InputManager arbitration.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class EscapeMenuUIController : MonoBehaviour, IToggleableUI
    {
        public static EscapeMenuUIController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonMaster dungeonMaster;

        [Header("Menu Configuration")]
        [Tooltip("When true, the menu automatically starts open when the scene is loaded.")]
        [SerializeField] private bool openOnSceneStart = false;

        [Tooltip("When true, Time.timeScale is frozen to 0 while the menu is open.")]
        [SerializeField] private bool pauseTimeWhileOpen = true;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement modalCard;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label statsLabel;

        // Buttons
        private Button startResumeButton;
        private Button restartButton;
        private Button quitButton;

        private bool isOpen = false;
        private float previousTimeScale = 1f;
        private bool isRegisteredWithInputManager = false;

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            if (SBGames.Testing.CombatTestEnvironmentBootstrap.ShouldBypassMetaAutoBootstrap()) return;

            // Auto-bootstrap ensures that hitting Play in ANY scene immediately enables the loop & menu
            var existingMenu = FindAnyObjectByType<EscapeMenuUIController>();
            if (existingMenu != null) return;

            // 1. Ensure DungeonMaster exists
            var dm = FindAnyObjectByType<DungeonMaster>();
            if (dm == null)
            {
                var dmGo = new GameObject("[DungeonMaster]");
                dm = dmGo.AddComponent<DungeonMaster>();
                dm.Initialize(startingEssence: 100, startingEnergy: 50);
                Debug.Log("[AutoBootstrap] Created [DungeonMaster] instance.");
            }

            // 2. Ensure CoreGameLoop exists
            var loop = FindAnyObjectByType<CoreGameLoop>();
            if (loop == null)
            {
                var loopGo = new GameObject("[CoreGameLoop]");
                loop = loopGo.AddComponent<CoreGameLoop>();
                loop.Initialize(dm: dm, initialRound: 1, planningAllowance: 15, victoryBonus: 25);
                Debug.Log("[AutoBootstrap] Created [CoreGameLoop] instance.");
            }

            // 3. Ensure Escape Menu exists
            var menuGo = new GameObject("[EscapeMenu]");
            var uiDoc = menuGo.AddComponent<UIDocument>();
            uiDoc.sortingOrder = 999; // Render on top of all other screens

            var panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
            if (panelSettings == null)
            {
                var anyDoc = FindObjectsByType<UIDocument>();
                foreach (var d in anyDoc)
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

            var menuController = menuGo.AddComponent<EscapeMenuUIController>();
            Debug.Log("[AutoBootstrap] Created [EscapeMenu] UI with UIDocument.");
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
            uiDocument.sortingOrder = 999;

            if (uiDocument.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
                if (panelSettings == null)
                {
                    var allDocs = FindObjectsByType<UIDocument>();
                    foreach (var d in allDocs)
                    {
                        if (d != uiDocument && d.panelSettings != null)
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
                    uiDocument.panelSettings = panelSettings;
                }
                else
                {
                    Debug.LogWarning("[EscapeMenuUIController] PanelSettings could not be found! UI cannot be displayed.");
                }
            }

            if (coreGameLoop == null)
            {
                coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            }

            if (dungeonMaster == null)
            {
                dungeonMaster = FindAnyObjectByType<DungeonMaster>();
            }

            BuildUIHierarchy();
        }

        private void Start()
        {
            RegisterWithInputManager();

            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandlePhaseChanged;
            }

            if (openOnSceneStart)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            UnregisterFromInputManager();

            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandlePhaseChanged;
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

            // Direct keyboard fallback ONLY if InputManager is not active or hasn't consumed Escape
            if (!isRegisteredWithInputManager && WasEscapePressedThisFrame())
            {
                Toggle();
            }
        }

        private bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        #region IToggleableUI Implementation

        public void Open()
        {
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

            RefreshMenuState();
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

        #region UI Construction & State

        private void BuildUIHierarchy()
        {
            if (uiDocument == null) return;
            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null) return;
            rootElement.Clear();

            // Fullscreen backdrop overlay (semi-transparent dark)
            rootElement.pickingMode = PickingMode.Position;
            rootElement.style.position = Position.Absolute;
            rootElement.style.left = 0;
            rootElement.style.top = 0;
            rootElement.style.right = 0;
            rootElement.style.bottom = 0;
            rootElement.style.width = Length.Percent(100);
            rootElement.style.height = Length.Percent(100);
            rootElement.style.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 0.88f);
            rootElement.style.justifyContent = Justify.Center;
            rootElement.style.alignItems = Align.Center;

            // Center modal container card
            modalCard = new VisualElement();
            modalCard.pickingMode = PickingMode.Position;
            modalCard.style.width = 460;
            modalCard.style.paddingTop = 28;
            modalCard.style.paddingBottom = 28;
            modalCard.style.paddingLeft = 32;
            modalCard.style.paddingRight = 32;
            modalCard.style.backgroundColor = new Color(0.12f, 0.13f, 0.18f, 0.98f);
            modalCard.style.borderTopLeftRadius = 12;
            modalCard.style.borderTopRightRadius = 12;
            modalCard.style.borderBottomLeftRadius = 12;
            modalCard.style.borderBottomRightRadius = 12;
            modalCard.style.borderLeftWidth = 2;
            modalCard.style.borderRightWidth = 2;
            modalCard.style.borderTopWidth = 2;
            modalCard.style.borderBottomWidth = 2;
            modalCard.style.borderLeftColor = new Color(0.35f, 0.38f, 0.52f, 0.8f);
            modalCard.style.borderRightColor = new Color(0.35f, 0.38f, 0.52f, 0.8f);
            modalCard.style.borderTopColor = new Color(0.35f, 0.38f, 0.52f, 0.8f);
            modalCard.style.borderBottomColor = new Color(0.35f, 0.38f, 0.52f, 0.8f);

            // Title
            titleLabel = new Label("DUNGEON MASTER");
            titleLabel.style.fontSize = 28;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.95f, 0.85f, 0.45f); // Golden glow
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.marginBottom = 4;
            modalCard.Add(titleLabel);

            // Subtitle
            subtitleLabel = new Label("Dungeon Defense Auto-Battler");
            subtitleLabel.style.fontSize = 13;
            subtitleLabel.style.color = new Color(0.7f, 0.72f, 0.8f);
            subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitleLabel.style.marginBottom = 20;
            modalCard.Add(subtitleLabel);

            // Live Stats Container
            statsLabel = new Label("");
            statsLabel.style.fontSize = 13;
            statsLabel.style.color = new Color(0.85f, 0.88f, 0.95f);
            statsLabel.style.backgroundColor = new Color(0.07f, 0.08f, 0.12f, 0.85f);
            statsLabel.style.paddingTop = 8;
            statsLabel.style.paddingBottom = 8;
            statsLabel.style.paddingLeft = 12;
            statsLabel.style.paddingRight = 12;
            statsLabel.style.borderTopLeftRadius = 6;
            statsLabel.style.borderTopRightRadius = 6;
            statsLabel.style.borderBottomLeftRadius = 6;
            statsLabel.style.borderBottomRightRadius = 6;
            statsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            statsLabel.style.marginBottom = 22;
            modalCard.Add(statsLabel);

            // Action Buttons
            startResumeButton = CreateStyledButton("START GAME", new Color(0.2f, 0.55f, 0.25f), OnStartResumeClicked);
            modalCard.Add(startResumeButton);

            restartButton = CreateStyledButton("RESTART RUN", new Color(0.55f, 0.35f, 0.15f), OnRestartClicked);
            modalCard.Add(restartButton);

            quitButton = CreateStyledButton("QUIT GAME", new Color(0.45f, 0.18f, 0.18f), OnQuitClicked);
            modalCard.Add(quitButton);

            rootElement.Add(modalCard);
        }

        private Button CreateStyledButton(string text, Color baseColor, Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = text;
            btn.style.height = 44;
            btn.style.fontSize = 15;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.color = Color.white;
            btn.style.backgroundColor = baseColor;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.marginBottom = 10;
            return btn;
        }

        private void RefreshMenuState()
        {
            bool hasGameStarted = coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.NotStarted;

            if (!hasGameStarted)
            {
                startResumeButton.text = "▶ START GAME";
                startResumeButton.style.backgroundColor = new Color(0.22f, 0.6f, 0.28f);
                restartButton.style.display = DisplayStyle.None;
                statsLabel.text = "Press Start Game to enter the Planning phase.\nUse [G] in Planning to open Build Menu (costs Energy).";
            }
            else
            {
                startResumeButton.text = "▶ RESUME GAME (Esc)";
                startResumeButton.style.backgroundColor = new Color(0.22f, 0.45f, 0.65f);
                restartButton.style.display = DisplayStyle.Flex;

                string phaseName = coreGameLoop.CurrentPhase.ToString();
                int round = coreGameLoop.CurrentRound;
                int essence = dungeonMaster != null ? dungeonMaster.Essence : 0;
                int energy = dungeonMaster != null ? dungeonMaster.Energy : 0;
                int artifacts = dungeonMaster != null ? dungeonMaster.ArtifactCount : 0;

                statsLabel.text = $"Round: {round}  |  Phase: {phaseName}\nEssence: {essence}  |  Energy: {energy}  |  Artifacts: {artifacts}";
            }
        }

        private void OnStartResumeClicked()
        {
            if (coreGameLoop == null)
            {
                coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            }

            if (coreGameLoop != null && coreGameLoop.CurrentPhase == GamePhase.NotStarted)
            {
                Close();
                coreGameLoop.StartGame();
                Debug.Log("[EscapeMenu] Game Started! Entered Planning phase.");
            }
            else
            {
                Close();
            }
        }

        private void OnRestartClicked()
        {
            Close();

            if (dungeonMaster != null)
            {
                dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 50);
            }

            if (coreGameLoop != null)
            {
                coreGameLoop.Initialize(dungeonMaster, initialRound: 1);
                coreGameLoop.StartGame();
                Debug.Log("[EscapeMenu] Run Restarted! Reset to Round 1 in Planning phase.");
            }
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandlePhaseChanged(GamePhase newPhase)
        {
            if (isOpen)
            {
                RefreshMenuState();
            }
        }

        private void RegisterWithInputManager()
        {
            var im = InputManager.Instance != null ? InputManager.Instance : FindAnyObjectByType<InputManager>();
            if (im != null)
            {
                im.RegisterUISystem(UIButton.Escape, this);
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
