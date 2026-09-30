using System;
using System.Collections;
using ArtificeToolkit.Attributes;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Loop.UI
{
    /// <summary>
    /// UI Toolkit controller for displaying the Dungeon Fame & Notoriety HUD.
    /// Shows current Fame Tier, Title, undefeated win-streak counter, active reward multipliers,
    /// and displays high-rank challenger warning banners when famous dungeons attract elite parties.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class DungeonFameUIController : MonoBehaviour
    {
        public static DungeonFameUIController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private DungeonFameSystem fameSystem;
        [SerializeField] private DungeonProgression dungeonProgression;

        [Header("Display Settings")]
        [Tooltip("Auto-hide challenger warning banners after this many seconds.")]
        [SerializeField] private float warningBannerDuration = 4.0f;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement fameBadgeContainer;
        private Label fameTierLabel;
        private Label fameTitleLabel;
        private Label streakLabel;
        private Label multipliersLabel;
        private VisualElement challengerWarningBanner;
        private Label warningTitleLabel;
        private Label warningDescLabel;
        private Coroutine warningCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            uiDocument = GetComponent<UIDocument>();
            BindDependencies();
            BuildUI();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RefreshUI();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void Initialize(DungeonFameSystem fame, DungeonProgression progression = null)
        {
            UnsubscribeEvents();
            fameSystem = fame;
            dungeonProgression = progression;
            SubscribeEvents();
            RefreshUI();
        }

        private void BindDependencies()
        {
            if (fameSystem == null)
            {
                fameSystem = GetComponent<DungeonFameSystem>() ?? FindAnyObjectByType<DungeonFameSystem>();
            }

            if (dungeonProgression == null)
            {
                dungeonProgression = GetComponent<DungeonProgression>() ?? FindAnyObjectByType<DungeonProgression>();
            }
        }

        private void SubscribeEvents()
        {
            if (fameSystem != null)
            {
                fameSystem.OnFameStreakChanged += HandleFameStreakChanged;
                fameSystem.OnFameTierChanged += HandleFameTierChanged;
                fameSystem.OnHighRankAdventurerAttracted += HandleHighRankAdventurerAttracted;
                fameSystem.OnRareArtifactLooted += HandleRareArtifactLooted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (fameSystem != null)
            {
                fameSystem.OnFameStreakChanged -= HandleFameStreakChanged;
                fameSystem.OnFameTierChanged -= HandleFameTierChanged;
                fameSystem.OnHighRankAdventurerAttracted -= HandleHighRankAdventurerAttracted;
                fameSystem.OnRareArtifactLooted -= HandleRareArtifactLooted;
            }
        }

        #region UI Construction & Styling

        private void BuildUI()
        {
            if (uiDocument == null) return;
            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null) return;

            rootElement.Clear();

            // 1. Fame HUD Badge (Top-Right overlay)
            fameBadgeContainer = new VisualElement();
            fameBadgeContainer.name = "FameBadgeContainer";
            fameBadgeContainer.style.position = Position.Absolute;
            fameBadgeContainer.style.top = 16;
            fameBadgeContainer.style.right = 16;
            fameBadgeContainer.style.paddingTop = 8;
            fameBadgeContainer.style.paddingBottom = 8;
            fameBadgeContainer.style.paddingLeft = 14;
            fameBadgeContainer.style.paddingRight = 14;
            fameBadgeContainer.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.88f);
            fameBadgeContainer.style.borderTopLeftRadius = 8;
            fameBadgeContainer.style.borderTopRightRadius = 8;
            fameBadgeContainer.style.borderBottomLeftRadius = 8;
            fameBadgeContainer.style.borderBottomRightRadius = 8;
            fameBadgeContainer.style.borderTopWidth = 2;
            fameBadgeContainer.style.borderBottomWidth = 2;
            fameBadgeContainer.style.borderLeftWidth = 2;
            fameBadgeContainer.style.borderRightWidth = 2;
            fameBadgeContainer.style.borderTopColor = new Color(0.6f, 0.5f, 0.2f, 0.9f);
            fameBadgeContainer.style.borderBottomColor = new Color(0.6f, 0.5f, 0.2f, 0.9f);
            fameBadgeContainer.style.borderLeftColor = new Color(0.6f, 0.5f, 0.2f, 0.9f);
            fameBadgeContainer.style.borderRightColor = new Color(0.6f, 0.5f, 0.2f, 0.9f);

            fameTierLabel = new Label("FAME: UNKNOWN");
            fameTierLabel.style.fontSize = 12;
            fameTierLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            fameTierLabel.style.color = new Color(0.9f, 0.8f, 0.4f);
            fameBadgeContainer.Add(fameTierLabel);

            fameTitleLabel = new Label("Obscure Hideout");
            fameTitleLabel.style.fontSize = 15;
            fameTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            fameTitleLabel.style.color = Color.white;
            fameBadgeContainer.Add(fameTitleLabel);

            streakLabel = new Label("Undefeated Streak: 0");
            streakLabel.style.fontSize = 11;
            streakLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            streakLabel.style.marginTop = 2;
            fameBadgeContainer.Add(streakLabel);

            multipliersLabel = new Label("Rewards: 1.0x Energy | 1.0x XP");
            multipliersLabel.style.fontSize = 10;
            multipliersLabel.style.color = new Color(0.4f, 0.85f, 0.5f);
            multipliersLabel.style.marginTop = 2;
            fameBadgeContainer.Add(multipliersLabel);

            rootElement.Add(fameBadgeContainer);

            // 2. High-Rank Challenger Warning Banner (Top-Center)
            challengerWarningBanner = new VisualElement();
            challengerWarningBanner.name = "ChallengerWarningBanner";
            challengerWarningBanner.style.position = Position.Absolute;
            challengerWarningBanner.style.top = 24;
            challengerWarningBanner.style.left = Length.Percent(25);
            challengerWarningBanner.style.width = Length.Percent(50);
            challengerWarningBanner.style.paddingTop = 10;
            challengerWarningBanner.style.paddingBottom = 10;
            challengerWarningBanner.style.paddingLeft = 20;
            challengerWarningBanner.style.paddingRight = 20;
            challengerWarningBanner.style.backgroundColor = new Color(0.55f, 0.08f, 0.08f, 0.95f);
            challengerWarningBanner.style.borderTopLeftRadius = 8;
            challengerWarningBanner.style.borderTopRightRadius = 8;
            challengerWarningBanner.style.borderBottomLeftRadius = 8;
            challengerWarningBanner.style.borderBottomRightRadius = 8;
            challengerWarningBanner.style.borderTopWidth = 2;
            challengerWarningBanner.style.borderBottomWidth = 2;
            challengerWarningBanner.style.borderLeftWidth = 2;
            challengerWarningBanner.style.borderRightWidth = 2;
            challengerWarningBanner.style.borderTopColor = new Color(1f, 0.85f, 0.2f);
            challengerWarningBanner.style.borderBottomColor = new Color(1f, 0.85f, 0.2f);
            challengerWarningBanner.style.borderLeftColor = new Color(1f, 0.85f, 0.2f);
            challengerWarningBanner.style.borderRightColor = new Color(1f, 0.85f, 0.2f);
            challengerWarningBanner.style.display = DisplayStyle.None;

            warningTitleLabel = new Label("⚠ HIGH-RANK CHALLENGERS ATTRACTED!");
            warningTitleLabel.style.fontSize = 16;
            warningTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            warningTitleLabel.style.color = new Color(1f, 0.9f, 0.2f);
            warningTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            challengerWarningBanner.Add(warningTitleLabel);

            warningDescLabel = new Label("Rumors of your undefeated dungeon drew Rank S champions!");
            warningDescLabel.style.fontSize = 12;
            warningDescLabel.style.color = Color.white;
            warningDescLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            warningDescLabel.style.marginTop = 4;
            challengerWarningBanner.Add(warningDescLabel);

            rootElement.Add(challengerWarningBanner);
        }

        #endregion

        #region UI Updates & Event Handlers

        public void RefreshUI()
        {
            if (fameBadgeContainer == null || fameSystem == null) return;

            var tier = fameSystem.CurrentTier;
            var def = fameSystem.GetActiveDefinition();

            ColorUtility.TryParseHtmlString(tier.GetColorHex(), out Color tierColor);

            if (fameTierLabel != null)
            {
                fameTierLabel.text = $"FAME: {tier.ToString().ToUpperInvariant()}";
                fameTierLabel.style.color = tierColor;
            }

            if (fameTitleLabel != null)
            {
                fameTitleLabel.text = fameSystem.CurrentTitle;
            }

            if (streakLabel != null)
            {
                streakLabel.text = $"Undefeated Streak: {fameSystem.CurrentWinStreak} (Best: {fameSystem.HighestWinStreak})";
            }

            if (multipliersLabel != null)
            {
                multipliersLabel.text = $"Rewards: {def.EnergyRewardMultiplier:F2}x Energy | {def.XpRewardMultiplier:F2}x XP";
            }

            if (fameBadgeContainer != null)
            {
                fameBadgeContainer.style.borderTopColor = tierColor;
                fameBadgeContainer.style.borderBottomColor = tierColor;
                fameBadgeContainer.style.borderLeftColor = tierColor;
                fameBadgeContainer.style.borderRightColor = tierColor;
            }
        }

        private void HandleFameStreakChanged(int currentStreak, int highestStreak)
        {
            RefreshUI();
        }

        private void HandleFameTierChanged(DungeonFameTier oldTier, DungeonFameTier newTier)
        {
            RefreshUI();
        }

        private void HandleHighRankAdventurerAttracted(DungeonRank adventurerRank, DungeonRank dungeonRank, DungeonFameTier fameTier)
        {
            ShowWarningBanner(
                $"⚠ HIGH-RANK CHALLENGER INCOMING!",
                $"Your fame as a '{fameTier.GetTitle()}' attracted {adventurerRank.GetDisplayName()} adventurers (Dungeon: {dungeonRank.GetDisplayName()})!");
        }

        private void HandleRareArtifactLooted(Artifact artifact)
        {
            if (artifact == null) return;
            ShowWarningBanner(
                $"★ RARE RELIC LOOTED!",
                $"Defeated high-rank challengers dropped: '{artifact.ArtifactName}' (Power {artifact.PowerLevel})!");
        }

        private void ShowWarningBanner(string title, string description)
        {
            if (challengerWarningBanner == null) return;

            if (warningTitleLabel != null) warningTitleLabel.text = title;
            if (warningDescLabel != null) warningDescLabel.text = description;

            challengerWarningBanner.style.display = DisplayStyle.Flex;

            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
            }
            warningCoroutine = StartCoroutine(HideWarningBannerRoutine(warningBannerDuration));
        }

        private IEnumerator HideWarningBannerRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (challengerWarningBanner != null)
            {
                challengerWarningBanner.style.display = DisplayStyle.None;
            }
            warningCoroutine = null;
        }

        #endregion
    }
}
