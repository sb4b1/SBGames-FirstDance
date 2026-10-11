using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Squads.UI
{
    /// <summary>
    /// Read-only DM HUD controller displaying Scout reconnaissance telemetry and intel synchronization logs.
    /// Uses UI Toolkit (UnityEngine.UIElements). Zero setup auto-bootstrapping.
    /// Auto-scrolls entries when new events occur without player input buttons.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class ScoutIntelHUDController : MonoBehaviour
    {
        public static ScoutIntelHUDController Instance { get; private set; }

        private UIDocument uiDocument;
        private VisualElement rootVisualElement;
        private ScrollView logScrollView;
        private Label statusBadgeLabel;
        private Label currentScoutLabel;

        private const int MAX_LOG_ENTRIES = 20;
        private readonly List<Label> entryLabels = new(MAX_LOG_ENTRIES);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            if (SBGames.Testing.CombatTestEnvironmentBootstrap.ShouldBypassMetaAutoBootstrap()) return;
            if (FindFirstObjectByType<ScoutIntelHUDController>() != null) return;

            var hudGo = new GameObject("[HUD]_ScoutIntel");
            hudGo.AddComponent<ScoutIntelHUDController>();
            DontDestroyOnLoad(hudGo);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureUIDocument();
            BuildUIHierarchy();
        }

        private void OnEnable()
        {
            HookPartyLeaders();
        }

        private void OnDisable()
        {
            UnhookPartyLeaders();
        }

        private void EnsureUIDocument()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            // Ensure PanelSettings
            if (uiDocument.panelSettings == null)
            {
                var existingSettings = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (existingSettings != null && existingSettings.Length > 0)
                {
                    uiDocument.panelSettings = existingSettings[0];
                }
            }
        }

        [Header("UI Toolkit Assets")]
        [SerializeField] private VisualTreeAsset hudVisualTree;
        [SerializeField] private StyleSheet hudStyleSheet;

        private void BuildUIHierarchy()
        {
            rootVisualElement = uiDocument.rootVisualElement;
            if (rootVisualElement == null) return;

            rootVisualElement.Clear();

            // Try loading from serialized assets or Resources
            if (hudVisualTree == null)
            {
#if UNITY_EDITOR
                hudVisualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/HUD/ScoutIntelHUD.uxml");
                hudStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/HUD/ScoutIntelHUD.uss");
#endif
            }

            if (hudVisualTree != null)
            {
                var instantiated = hudVisualTree.Instantiate();
                instantiated.style.position = Position.Absolute;
                instantiated.style.top = 10;
                instantiated.style.right = 10;
                if (hudStyleSheet != null)
                {
                    instantiated.styleSheets.Add(hudStyleSheet);
                }
                rootVisualElement.Add(instantiated);

                statusBadgeLabel = instantiated.Q<Label>("status-badge");
                currentScoutLabel = instantiated.Q<Label>("scout-name-label");
                logScrollView = instantiated.Q<ScrollView>("intel-log-scrollview");

                AppendLogEntry("Telemetry feed initialized.", new Color(0.5f, 0.5f, 0.5f, 0.8f));
                return;
            }

            // Procedural Fallback (for automated tests and headless scenes)
            var container = new VisualElement();
            container.name = "scout-intel-hud-container";
            container.style.position = Position.Absolute;
            container.style.top = 10;
            container.style.right = 10;
            container.style.width = 300;
            container.style.maxHeight = 220;
            container.style.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.88f);
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(0.25f, 0.3f, 0.4f, 0.8f);
            container.style.borderBottomColor = new Color(0.25f, 0.3f, 0.4f, 0.8f);
            container.style.borderLeftColor = new Color(0.25f, 0.3f, 0.4f, 0.8f);
            container.style.borderRightColor = new Color(0.25f, 0.3f, 0.4f, 0.8f);
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;

            // Header Title
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            var titleLabel = new Label("RECON INTEL FEED");
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.45f, 0.75f, 1f, 1f);
            header.Add(titleLabel);

            statusBadgeLabel = new Label("IDLE");
            statusBadgeLabel.style.fontSize = 10;
            statusBadgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusBadgeLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            statusBadgeLabel.style.backgroundColor = new Color(0.18f, 0.2f, 0.25f, 1f);
            statusBadgeLabel.style.paddingLeft = 6;
            statusBadgeLabel.style.paddingRight = 6;
            statusBadgeLabel.style.paddingTop = 2;
            statusBadgeLabel.style.paddingBottom = 2;
            statusBadgeLabel.style.borderTopLeftRadius = 3;
            statusBadgeLabel.style.borderTopRightRadius = 3;
            statusBadgeLabel.style.borderBottomLeftRadius = 3;
            statusBadgeLabel.style.borderBottomRightRadius = 3;
            header.Add(statusBadgeLabel);

            container.Add(header);

            // Active Scout info
            currentScoutLabel = new Label("No Active Scout");
            currentScoutLabel.style.fontSize = 10;
            currentScoutLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
            currentScoutLabel.style.marginBottom = 4;
            container.Add(currentScoutLabel);

            // Log ScrollView (read-only, auto scrolling)
            logScrollView = new ScrollView(ScrollViewMode.Vertical);
            logScrollView.style.flexGrow = 1;
            logScrollView.style.height = 130;
            logScrollView.showVertical = false;
            logScrollView.showHorizontal = false;

            container.Add(logScrollView);
            rootVisualElement.Add(container);

            AppendLogEntry("Telemetry feed initialized.", new Color(0.5f, 0.5f, 0.5f, 0.8f));
        }

        public void SetScoutStatus(string status, Color badgeColor, string scoutName = null)
        {
            if (statusBadgeLabel != null)
            {
                statusBadgeLabel.text = status;
                statusBadgeLabel.style.color = badgeColor;
            }

            if (currentScoutLabel != null && !string.IsNullOrEmpty(scoutName))
            {
                currentScoutLabel.text = $"Scout: {scoutName}";
            }
        }

        public void AppendLogEntry(string message, Color color)
        {
            if (logScrollView == null) return;

            var entry = new Label($"[{Time.time:F1}s] {message}");
            entry.style.fontSize = 10;
            entry.style.color = color;
            entry.style.whiteSpace = WhiteSpace.Normal;
            entry.style.marginTop = 2;
            entry.style.marginBottom = 2;

            logScrollView.Add(entry);
            entryLabels.Add(entry);

            // Prune old entries to keep bounded memory
            if (entryLabels.Count > MAX_LOG_ENTRIES)
            {
                var oldest = entryLabels[0];
                entryLabels.RemoveAt(0);
                logScrollView.Remove(oldest);
            }

            // Auto-scroll to bottom
            logScrollView.schedule.Execute(() =>
            {
                logScrollView.scrollOffset = new Vector2(0f, logScrollView.contentContainer.layout.height);
            });
        }

        private void HookPartyLeaders()
        {
            var leaders = FindObjectsByType<PartyLeader>(FindObjectsSortMode.None);
            for (int i = 0; i < leaders.Length; i++)
            {
                HookLeader(leaders[i]);
            }
        }

        private void UnhookPartyLeaders()
        {
            var leaders = FindObjectsByType<PartyLeader>(FindObjectsSortMode.None);
            for (int i = 0; i < leaders.Length; i++)
            {
                UnhookLeader(leaders[i]);
            }
        }

        public void HookLeader(PartyLeader leader)
        {
            if (leader == null) return;
            leader.OnScoutDispatched -= HandleScoutDispatched;
            leader.OnScoutDispatched += HandleScoutDispatched;

            leader.OnScoutDebriefed -= HandleScoutDebriefed;
            leader.OnScoutDebriefed += HandleScoutDebriefed;

            leader.OnScoutEliminated -= HandleScoutEliminated;
            leader.OnScoutEliminated += HandleScoutEliminated;

            if (leader.Blackboard != null)
            {
                leader.Blackboard.OnHazardReported -= HandleHazardReported;
                leader.Blackboard.OnHazardReported += HandleHazardReported;

                leader.Blackboard.OnEnemyReported -= HandleEnemyReported;
                leader.Blackboard.OnEnemyReported += HandleEnemyReported;
            }
        }

        public void UnhookLeader(PartyLeader leader)
        {
            if (leader == null) return;
            leader.OnScoutDispatched -= HandleScoutDispatched;
            leader.OnScoutDebriefed -= HandleScoutDebriefed;
            leader.OnScoutEliminated -= HandleScoutEliminated;

            if (leader.Blackboard != null)
            {
                leader.Blackboard.OnHazardReported -= HandleHazardReported;
                leader.Blackboard.OnEnemyReported -= HandleEnemyReported;
            }
        }

        private void HandleScoutDispatched(SquadMember scout)
        {
            string name = scout != null ? scout.name : "Scout";
            SetScoutStatus("INFILTRATING", new Color(0.2f, 0.8f, 1f, 1f), name);
            AppendLogEntry($"Scout {name} dispatched forward on reconnaissance.", new Color(0.2f, 0.8f, 1f, 1f));
        }

        private void HandleScoutDebriefed(SquadMember scout)
        {
            string name = scout != null ? scout.name : "Scout";
            SetScoutStatus("INTEL SYNCHRONIZED", new Color(0.2f, 1f, 0.4f, 1f), name);
            AppendLogEntry($"Scout {name} returned! Intel synchronized to squad.", new Color(0.2f, 1f, 0.4f, 1f));
        }

        private void HandleScoutEliminated(SquadMember scout)
        {
            string name = scout != null ? scout.name : "Scout";
            SetScoutStatus("SCOUT ELIMINATED!", new Color(1f, 0.2f, 0.2f, 1f), name);
            AppendLogEntry($"Scout {name} died in action! Intelligence lost.", new Color(1f, 0.2f, 0.2f, 1f));
        }

        private void HandleHazardReported(Vector2Int gridPos, Vector3 worldPos, float danger)
        {
            AppendLogEntry($"Trap confirmed at ({gridPos.x}, {gridPos.y}) [Danger: {danger:F0}]", new Color(1f, 0.6f, 0.2f, 1f));

            if (ReconWorldMarkerManager.Instance != null)
            {
                ReconWorldMarkerManager.Instance.SpawnHazardMarker(worldPos, 30f);
            }
        }

        private void HandleEnemyReported(Transform enemy, Vector3 pos)
        {
            string targetName = enemy != null ? enemy.name : "Unknown Hostile";
            AppendLogEntry($"Hostile detected: {targetName}", new Color(1f, 0.85f, 0.2f, 1f));

            if (ReconWorldMarkerManager.Instance != null)
            {
                ReconWorldMarkerManager.Instance.SpawnEnemyMarker(enemy, pos, 6f);
            }
        }
    }
}
