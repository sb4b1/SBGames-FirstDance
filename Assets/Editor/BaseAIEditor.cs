using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using CrashKonijn.Agent.Runtime;
using SBGames.Personality;

[CustomEditor(typeof(BaseAI))]
public class BaseAIEditor : Editor
{
    private BaseAI baseAI;

    // Header Badges
    private Label agentTypeBadge;
    private Label activeGoalBadge;
    private Label activeSkillBadge;
    private Label skillReadyBadge;

    // Diagnostics Elements
    private VisualElement diagnosticsContainer;
    private HelpBox diagnosticsHelpBox;
    private Foldout diagnosticsFoldout;

    // Blackboard Elements - Perception
    private Label targetNameLabel;
    private Label targetDistanceLabel;
    private Label enemiesCountLabel;
    private Label alliesCountLabel;

    // Blackboard Elements - Emotion & Resources
    private ProgressBar threatBar;
    private ProgressBar fearBar;
    private Label fearLockedBadge;
    private ProgressBar lifeBar;
    private ProgressBar manaBar;

    // Blackboard Elements - Squad & Personality
    private Label squadOrderBadge;
    private Label squadLeaderLabel;
    private Label focusFireLabel;
    private Label personalityArchetypeLabel;
    private Label personalityBiasesLabel;

    // Scoreboards
    private VisualElement goalScoreboardContainer;
    private VisualElement skillScoreboardContainer;

    // Play Mode Container & Scheduled Item
    private VisualElement playModeActionsContainer;
    private IVisualElementScheduledItem stateUpdateSchedule;

    private void OnEnable()
    {
        baseAI = (BaseAI)target;
    }

    private void OnDisable()
    {
        stateUpdateSchedule?.Pause();
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.style.paddingLeft = 4;
        root.style.paddingRight = 4;
        root.style.paddingTop = 6;
        root.style.paddingBottom = 6;

        // 1. Header & Agent Identity
        root.Add(CreateHeaderSection());

        // 2. Blackboard & Perception (Segmented Cards)
        root.Add(CreatePerceptionSection());
        root.Add(CreateEmotionAndResourcesSection());
        root.Add(CreateSquadAndPersonalitySection());

        // 3. Utility AI Scoreboard (Live Rankings)
        root.Add(CreateScoreboardSection());

        // 4. Play Mode AI Debug & Simulation Controls
        playModeActionsContainer = CreatePlayModeActionsSection();
        root.Add(playModeActionsContainer);

        // 5. Component Diagnostics & 1-Click Auto-Wiring (Moved down)
        root.Add(CreateDiagnosticsSection());

        // 6. Default Serialized Properties Foldout
        root.Add(CreatePropertiesSection());

        // Bind Serialized Object
        root.Bind(serializedObject);

        // Schedule periodic update for runtime state & scoreboards
        stateUpdateSchedule = root.schedule.Execute(OnScheduledUpdate).Every(100);

        // Initial update
        RefreshDiagnostics();
        UpdateAllMeters();
        UpdateScoreboards();
        UpdatePlayModeVisibility();

        return root;
    }

    // =========================================================================
    // SECTION 1: Header & Agent Identity
    // =========================================================================
    private VisualElement CreateHeaderSection()
    {
        var card = CreateCard("GOAP Agent Identity & Status");

        var headerRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 6
            }
        };

        var titleLabel = new Label(baseAI != null ? baseAI.name : "BaseAI")
        {
            style =
            {
                fontSize = 13,
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };

        var topBadgeRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
        };

        agentTypeBadge = CreateBadge("Agent", new Color(0.2f, 0.45f, 0.65f, 0.85f));
        skillReadyBadge = CreateBadge("Skill Ready", new Color(0.18f, 0.55f, 0.25f, 0.85f));

        topBadgeRow.Add(agentTypeBadge);
        topBadgeRow.Add(skillReadyBadge);

        headerRow.Add(titleLabel);
        headerRow.Add(topBadgeRow);
        card.Add(headerRow);

        // Status Row
        var statusRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                flexWrap = Wrap.Wrap
            }
        };

        activeGoalBadge = CreateBadge("Goal: None", new Color(0.35f, 0.25f, 0.65f, 0.9f));
        activeSkillBadge = CreateBadge("Skill: None", new Color(0.2f, 0.5f, 0.45f, 0.9f));

        statusRow.Add(activeGoalBadge);
        statusRow.Add(activeSkillBadge);
        card.Add(statusRow);

        return card;
    }

    private void UpdateHeaderBadges()
    {
        if (baseAI == null) return;

        serializedObject.Update();
        var agentTypeProp = serializedObject.FindProperty("agentType");
        agentTypeBadge.text = agentTypeProp != null ? agentTypeProp.stringValue : "NormalAgent";

        var currentGoalProp = serializedObject.FindProperty("currentGoalKey");
        string goalText = currentGoalProp != null && !string.IsNullOrEmpty(currentGoalProp.stringValue)
            ? currentGoalProp.stringValue
            : "None";
        activeGoalBadge.text = $"Goal: {goalText}";

        var currentSkillProp = serializedObject.FindProperty("currentSkillKey");
        var currentSkillScoreProp = serializedObject.FindProperty("currentSkillScore");
        string skillText = currentSkillProp != null && !string.IsNullOrEmpty(currentSkillProp.stringValue)
            ? currentSkillProp.stringValue
            : "None";
        float skillScore = currentSkillScoreProp != null ? currentSkillScoreProp.floatValue : 0f;
        activeSkillBadge.text = skillScore > 0f ? $"Skill: {skillText} ({skillScore:F1})" : $"Skill: {skillText}";

        bool ready = baseAI.IsSkillReady;
        skillReadyBadge.text = ready ? "Skill Ready" : "Skills on CD";
        skillReadyBadge.style.backgroundColor = ready
            ? new StyleColor(new Color(0.15f, 0.55f, 0.25f, 0.9f))
            : new StyleColor(new Color(0.45f, 0.45f, 0.45f, 0.7f));
    }

    // =========================================================================
    // SECTION 2: Component Diagnostics & Auto-Setup
    // =========================================================================
    private VisualElement CreateDiagnosticsSection()
    {
        var card = CreateCard(null);

        diagnosticsFoldout = new Foldout
        {
            text = "Component Diagnostics & Wiring",
            value = false,
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };

        diagnosticsHelpBox = new HelpBox("", HelpBoxMessageType.None)
        {
            style = { marginTop = 4, marginBottom = 6, display = DisplayStyle.None }
        };
        diagnosticsFoldout.Add(diagnosticsHelpBox);

        diagnosticsContainer = new VisualElement
        {
            style = { marginBottom = 6 }
        };
        diagnosticsFoldout.Add(diagnosticsContainer);

        var autoWireBtn = new Button(AutoWireReferences)
        {
            text = "Auto-Wire AI References",
            tooltip = "Scan hierarchy for NavMeshAgent, AgentBehaviour, Brain, Stats, Movement, Skills, Vision, Squad, and Personality.",
            style = { height = 24 }
        };
        diagnosticsFoldout.Add(autoWireBtn);

        card.Add(diagnosticsFoldout);
        return card;
    }

    private void RefreshDiagnostics()
    {
        if (baseAI == null || diagnosticsContainer == null) return;

        diagnosticsContainer.Clear();
        serializedObject.Update();

        var agentProp = serializedObject.FindProperty("agent");
        var agentBehaviourProp = serializedObject.FindProperty("agentBehaviour");
        var brainProp = serializedObject.FindProperty("brain");
        var statsProp = serializedObject.FindProperty("stats");
        var motionProp = serializedObject.FindProperty("motionController");
        var skillProp = serializedObject.FindProperty("skillController");
        var visionProp = serializedObject.FindProperty("vision");
        var squadProp = serializedObject.FindProperty("squadMember");
        var personalityProp = serializedObject.FindProperty("personality");
        var goalsProp = serializedObject.FindProperty("goals");

        bool hasAgent = agentProp != null && agentProp.objectReferenceValue != null;
        bool hasBehaviour = agentBehaviourProp != null && agentBehaviourProp.objectReferenceValue != null;
        bool hasBrain = brainProp != null && brainProp.objectReferenceValue != null;
        bool hasStats = statsProp != null && statsProp.objectReferenceValue != null;
        bool hasMotion = motionProp != null && motionProp.objectReferenceValue != null;
        bool hasSkill = skillProp != null && skillProp.objectReferenceValue != null;
        bool hasVision = visionProp != null && visionProp.objectReferenceValue != null;
        bool hasSquad = squadProp != null && squadProp.objectReferenceValue != null;
        bool hasPersonality = personalityProp != null && personalityProp.objectReferenceValue != null;
        int goalsCount = goalsProp != null ? goalsProp.arraySize : 0;

        diagnosticsContainer.Add(CreateRefDiagnosticRow("NavMeshAgent (Parent)", hasAgent, true));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("AgentBehaviour (Self)", hasBehaviour, true));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterBrain (Parent)", hasBrain, true));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterStatsManager (Parent)", hasStats, true));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterMovementController (Parent)", hasMotion, false));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterSkillController", hasSkill, false));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("VisionController", hasVision, false));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("SquadMember (Parent)", hasSquad, false));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterPersonality (Parent)", hasPersonality, false));
        diagnosticsContainer.Add(CreateGoalsDiagnosticRow(goalsCount));

        int criticalMissing = (!hasAgent ? 1 : 0) + (!hasBehaviour ? 1 : 0) + (!hasBrain ? 1 : 0) + (!hasStats ? 1 : 0);
        int warningsMissing = (!hasMotion ? 1 : 0) + (!hasSkill ? 1 : 0) + (!hasVision ? 1 : 0) + (goalsCount == 0 ? 1 : 0);
        int totalMissing = criticalMissing + warningsMissing;

        if (diagnosticsFoldout != null)
        {
            diagnosticsFoldout.text = totalMissing > 0
                ? $"Component Diagnostics & Wiring ({totalMissing} Unassigned)"
                : "Component Diagnostics & Wiring (All Assigned)";
        }

        if (criticalMissing > 0)
        {
            diagnosticsHelpBox.style.display = DisplayStyle.Flex;
            diagnosticsHelpBox.messageType = HelpBoxMessageType.Error;
            diagnosticsHelpBox.text = $"{criticalMissing} critical component(s) missing. Click 'Auto-Wire AI References' to resolve.";
        }
        else if (warningsMissing > 0)
        {
            diagnosticsHelpBox.style.display = DisplayStyle.Flex;
            diagnosticsHelpBox.messageType = HelpBoxMessageType.Warning;
            diagnosticsHelpBox.text = $"{warningsMissing} optional component(s) or goals unassigned. Check below.";
        }
        else
        {
            diagnosticsHelpBox.style.display = DisplayStyle.None;
        }
    }

    private VisualElement CreateRefDiagnosticRow(string labelText, bool isBound, bool isCritical)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 2
            }
        };

        var label = new Label(labelText)
        {
            style = { fontSize = 11, flexGrow = 1 }
        };

        Color badgeColor = isBound
            ? new Color(0.15f, 0.5f, 0.25f, 0.8f)
            : (isCritical ? new Color(0.7f, 0.2f, 0.2f, 0.85f) : new Color(0.6f, 0.45f, 0.15f, 0.8f));

        var indicator = CreateBadge(isBound ? "Assigned" : "Missing", badgeColor);

        row.Add(label);
        row.Add(indicator);
        return row;
    }

    private VisualElement CreateGoalsDiagnosticRow(int count)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 2
            }
        };

        var label = new Label("Utility Goals")
        {
            style = { fontSize = 11, flexGrow = 1 }
        };

        Color badgeColor = count > 0
            ? new Color(0.15f, 0.5f, 0.25f, 0.8f)
            : new Color(0.65f, 0.35f, 0.15f, 0.8f);

        var indicator = CreateBadge($"{count} Goal(s)", badgeColor);

        row.Add(label);
        row.Add(indicator);
        return row;
    }

    private void AutoWireReferences()
    {
        if (baseAI == null) return;

        Undo.RecordObject(baseAI, "Auto-Wire BaseAI References");
        serializedObject.Update();

        int wiredCount = 0;

        void TryAssign<T>(string propName, T found) where T : UnityEngine.Object
        {
            var prop = serializedObject.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue == null && found != null)
            {
                prop.objectReferenceValue = found;
                wiredCount++;
            }
        }

        TryAssign("agent", baseAI.GetComponentInParent<NavMeshAgent>() ?? baseAI.GetComponent<NavMeshAgent>());
        TryAssign("agentBehaviour", baseAI.GetComponent<AgentBehaviour>() ?? baseAI.GetComponentInParent<AgentBehaviour>());
        TryAssign("brain", baseAI.GetComponentInParent<CharacterBrain>() ?? baseAI.GetComponent<CharacterBrain>());
        TryAssign("stats", baseAI.GetComponentInParent<CharacterStatsManager>() ?? baseAI.GetComponent<CharacterStatsManager>());
        TryAssign("motionController", baseAI.GetComponentInParent<CharacterMovementController>() ?? baseAI.GetComponent<CharacterMovementController>());
        TryAssign("squadMember", baseAI.GetComponentInParent<SquadMember>() ?? baseAI.GetComponent<SquadMember>());
        TryAssign("personality", baseAI.GetComponentInParent<CharacterPersonality>() ?? baseAI.GetComponent<CharacterPersonality>());

        var foundSkill = baseAI.GetComponent<CharacterSkillController>()
            ?? baseAI.GetComponentInChildren<CharacterSkillController>()
            ?? baseAI.GetComponentInParent<CharacterSkillController>()
            ?? (baseAI.transform.root != null ? baseAI.transform.root.GetComponentInChildren<CharacterSkillController>() : null);
        TryAssign("skillController", foundSkill);

        var foundVision = baseAI.GetComponent<VisionController>()
            ?? baseAI.GetComponentInChildren<VisionController>()
            ?? baseAI.GetComponentInParent<VisionController>()
            ?? (baseAI.transform.root != null ? baseAI.transform.root.GetComponentInChildren<VisionController>() : null);
        TryAssign("vision", foundVision);

        serializedObject.ApplyModifiedProperties();
        MarkDirtyAndSave();
        RefreshDiagnostics();

        if (wiredCount > 0)
        {
            Debug.Log($"[BaseAIEditor] Successfully auto-wired {wiredCount} reference(s) on {baseAI.name}.", baseAI);
        }
    }

    // =========================================================================
    // SECTION 3: Blackboard & Perception (Segmented Cards)
    // =========================================================================
    private VisualElement CreatePerceptionSection()
    {
        var card = CreateCard("Perception & Targets");

        var targetRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 4 }
        };
        targetNameLabel = new Label("Target: None") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold } };
        targetDistanceLabel = new Label("Distance: 0.0 m") { style = { fontSize = 11 } };
        targetRow.Add(targetNameLabel);
        targetRow.Add(targetDistanceLabel);
        card.Add(targetRow);

        var countsRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween }
        };
        enemiesCountLabel = new Label("Perceived Enemies: 0") { style = { fontSize = 11 } };
        alliesCountLabel = new Label("Perceived Allies: 0") { style = { fontSize = 11 } };
        countsRow.Add(enemiesCountLabel);
        countsRow.Add(alliesCountLabel);
        card.Add(countsRow);

        return card;
    }

    private VisualElement CreateEmotionAndResourcesSection()
    {
        var card = CreateCard("Emotion & Resources");

        var threatContainer = new VisualElement { style = { marginBottom = 4 } };
        threatBar = new ProgressBar { title = "Threat Level: 0", lowValue = 0, highValue = 100, value = 0 };
        threatContainer.Add(threatBar);
        card.Add(threatContainer);

        var fearContainer = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 }
        };
        fearBar = new ProgressBar { title = "Fear Level: 0", lowValue = 0, highValue = 100, value = 0, style = { flexGrow = 1 } };
        fearLockedBadge = CreateBadge("Locked", new Color(0.6f, 0.2f, 0.5f, 0.9f));
        fearLockedBadge.style.display = DisplayStyle.None;
        fearContainer.Add(fearBar);
        fearContainer.Add(fearLockedBadge);
        card.Add(fearContainer);

        var resourcesRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween }
        };
        lifeBar = new ProgressBar { title = "Life: 0 / 0", lowValue = 0, highValue = 100, value = 100, style = { flexGrow = 1, marginRight = 2 } };
        manaBar = new ProgressBar { title = "Mana: 0 / 0", lowValue = 0, highValue = 100, value = 100, style = { flexGrow = 1, marginLeft = 2 } };
        resourcesRow.Add(lifeBar);
        resourcesRow.Add(manaBar);
        card.Add(resourcesRow);

        return card;
    }

    private VisualElement CreateSquadAndPersonalitySection()
    {
        var card = CreateCard("Squad & Personality");

        var squadRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 4 }
        };
        squadOrderBadge = CreateBadge("Order: None", new Color(0.2f, 0.4f, 0.6f, 0.85f));
        squadLeaderLabel = new Label("Leader: None") { style = { fontSize = 11 } };
        focusFireLabel = new Label("Focus Target: None") { style = { fontSize = 11 } };
        squadRow.Add(squadOrderBadge);
        squadRow.Add(squadLeaderLabel);
        squadRow.Add(focusFireLabel);
        card.Add(squadRow);

        var personalityBox = new VisualElement
        {
            style =
            {
                backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 0.4f)),
                paddingLeft = 6,
                paddingRight = 6,
                paddingTop = 4,
                paddingBottom = 4,
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4
            }
        };

        personalityArchetypeLabel = new Label("Archetype: Default") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } };
        personalityBiasesLabel = new Label("Threat: x1.00 | Fear: x1.00 | Survival HP: 25%") { style = { fontSize = 10, color = new StyleColor(new Color(0.8f, 0.8f, 0.8f, 1f)) } };
        personalityBox.Add(personalityArchetypeLabel);
        personalityBox.Add(personalityBiasesLabel);
        card.Add(personalityBox);

        return card;
    }

    private void UpdateAllMeters()
    {
        if (baseAI == null) return;

        // Perception
        if (baseAI.CurrentTarget != null)
        {
            targetNameLabel.text = $"Target: {baseAI.CurrentTarget.name}";
            targetDistanceLabel.text = $"Distance: {baseAI.TargetDistance:F1} m";
        }
        else
        {
            targetNameLabel.text = "Target: None";
            targetDistanceLabel.text = "Distance: 0.0 m";
        }

        int enemyCount = baseAI.EnemyTargets != null ? baseAI.EnemyTargets.Count : 0;
        int allyCount = baseAI.AllyTargets != null ? baseAI.AllyTargets.Count : 0;
        enemiesCountLabel.text = $"Perceived Enemies: {enemyCount}";
        alliesCountLabel.text = $"Perceived Allies: {allyCount}";

        // Emotion
        int threat = baseAI.ThreatLevel;
        threatBar.value = threat;
        threatBar.title = $"Threat Level: {threat}";

        int fear = baseAI.FearLevel;
        fearBar.value = fear;
        fearBar.title = $"Fear Level: {fear}";
        fearLockedBadge.style.display = baseAI.IsFearLocked ? DisplayStyle.Flex : DisplayStyle.None;

        // Resources
        float life = baseAI.AgentLife;
        float maxLife = baseAI.StatsManager != null && baseAI.StatsManager.MaxLife != null ? baseAI.StatsManager.MaxLife.Value : 100f;
        lifeBar.highValue = Mathf.Max(1f, maxLife);
        lifeBar.value = life;
        lifeBar.title = $"Life: {life:F0} / {maxLife:F0}";

        float mana = baseAI.AgentMana;
        float maxMana = baseAI.StatsManager != null && baseAI.StatsManager.MaxMana != null ? baseAI.StatsManager.MaxMana.Value : 100f;
        manaBar.highValue = Mathf.Max(1f, maxMana);
        manaBar.value = mana;
        manaBar.title = $"Mana: {mana:F0} / {maxMana:F0}";

        // Squad
        squadOrderBadge.text = $"Order: {baseAI.SquadOrder}";
        squadLeaderLabel.text = baseAI.LeaderTransform != null ? $"Leader: {baseAI.LeaderTransform.name}" : "Leader: None";
        focusFireLabel.text = baseAI.FocusFireTarget != null ? $"Focus: {baseAI.FocusFireTarget.name}" : "Focus: None";

        // Personality
        if (baseAI.Personality != null)
        {
            string race = baseAI.Personality.RaceInstinct != null ? baseAI.Personality.RaceInstinct.name : "None";
            string trait = baseAI.Personality.ActiveTrait != null ? baseAI.Personality.ActiveTrait.name : "None";
            personalityArchetypeLabel.text = $"Race: {race} | Trait: {trait}";
        }
        else
        {
            personalityArchetypeLabel.text = "Personality: None (Default)";
        }

        personalityBiasesLabel.text =
            $"Threat: x{baseAI.EffectiveThreatMultiplier:F2} | " +
            $"Fear: x{baseAI.EffectiveFearMultiplier:F2} | " +
            $"Survival HP: {baseAI.SurvivalHpThreshold * 100f:F0}% | " +
            $"Low HP Priority: x{baseAI.LowHealthTargetPriority:F2}";
    }

    // =========================================================================
    // SECTION 4: Utility AI Scoreboard (Live Rankings)
    // =========================================================================
    private VisualElement CreateScoreboardSection()
    {
        var card = CreateCard("Utility AI Scoreboards");

        var goalHeader = new Label("Goal Utility Rankings")
        {
            style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
        };
        card.Add(goalHeader);

        goalScoreboardContainer = new VisualElement { style = { marginBottom = 8 } };
        card.Add(goalScoreboardContainer);

        var skillHeader = new Label("Skill Utility Rankings")
        {
            style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
        };
        card.Add(skillHeader);

        skillScoreboardContainer = new VisualElement();
        card.Add(skillScoreboardContainer);

        return card;
    }

    private void UpdateScoreboards()
    {
        if (baseAI == null) return;

        UpdateGoalScoreboard();
        UpdateSkillScoreboard();
    }

    private void UpdateGoalScoreboard()
    {
        if (goalScoreboardContainer == null) return;

        serializedObject.Update();
        var goalScoresProp = serializedObject.FindProperty("goalScores");
        var currentGoalProp = serializedObject.FindProperty("currentGoalKey");
        string winningGoal = currentGoalProp != null ? currentGoalProp.stringValue : "";

        if (goalScoresProp == null) return;
        var pairsProp = goalScoresProp.FindPropertyRelative("pairs");
        if (pairsProp == null || pairsProp.arraySize == 0)
        {
            goalScoreboardContainer.Clear();
            var emptyLabel = new Label("No goal scores evaluated yet (scores update during arbitration).")
            {
                style = { fontSize = 10, color = Color.gray }
            };
            goalScoreboardContainer.Add(emptyLabel);
            return;
        }

        // Extract and sort pairs
        var list = new List<(string key, float score)>();
        float highestScore = 0f;

        for (int i = 0; i < pairsProp.arraySize; i++)
        {
            var pair = pairsProp.GetArrayElementAtIndex(i);
            var keyProp = pair.FindPropertyRelative("Key");
            var valProp = pair.FindPropertyRelative("Value");

            string key = keyProp.enumNames != null && keyProp.enumValueIndex >= 0 && keyProp.enumValueIndex < keyProp.enumNames.Length
                ? keyProp.enumNames[keyProp.enumValueIndex]
                : keyProp.intValue.ToString();
            float score = valProp.floatValue;

            list.Add((key, score));
            if (score > highestScore) highestScore = score;
        }

        list.Sort((a, b) => b.score.CompareTo(a.score));

        goalScoreboardContainer.Clear();
        foreach (var item in list)
        {
            bool isWinner = !string.IsNullOrEmpty(winningGoal) && item.key == winningGoal && item.score > 0f;
            goalScoreboardContainer.Add(CreateScoreboardRow(item.key, item.score, highestScore, isWinner, new Color(0.2f, 0.6f, 0.35f, 0.9f)));
        }
    }

    private void UpdateSkillScoreboard()
    {
        if (skillScoreboardContainer == null) return;

        serializedObject.Update();
        var skillScoresProp = serializedObject.FindProperty("skillScores");
        var currentSkillProp = serializedObject.FindProperty("currentSkillKey");
        string winningSkill = currentSkillProp != null ? currentSkillProp.stringValue : "";

        if (skillScoresProp == null) return;
        var pairsProp = skillScoresProp.FindPropertyRelative("pairs");
        if (pairsProp == null || pairsProp.arraySize == 0)
        {
            skillScoreboardContainer.Clear();
            var emptyLabel = new Label("No skill scores evaluated yet.")
            {
                style = { fontSize = 10, color = Color.gray }
            };
            skillScoreboardContainer.Add(emptyLabel);
            return;
        }

        var list = new List<(string key, float score)>();
        float highestScore = 0f;

        for (int i = 0; i < pairsProp.arraySize; i++)
        {
            var pair = pairsProp.GetArrayElementAtIndex(i);
            var keyProp = pair.FindPropertyRelative("Key");
            var valProp = pair.FindPropertyRelative("Value");

            string key = keyProp.stringValue;
            float score = valProp.floatValue;

            list.Add((key, score));
            if (score > highestScore) highestScore = score;
        }

        list.Sort((a, b) => b.score.CompareTo(a.score));

        skillScoreboardContainer.Clear();
        foreach (var item in list)
        {
            bool isWinner = !string.IsNullOrEmpty(winningSkill) && item.key == winningSkill && item.score > 0f;
            skillScoreboardContainer.Add(CreateScoreboardRow(item.key, item.score, highestScore, isWinner, new Color(0.35f, 0.25f, 0.65f, 0.9f)));
        }
    }

    private VisualElement CreateScoreboardRow(string name, float score, float maxScore, bool isWinner, Color accentColor)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginBottom = 2,
                backgroundColor = isWinner ? new StyleColor(new Color(0.25f, 0.35f, 0.25f, 0.35f)) : new StyleColor(Color.clear),
                paddingLeft = 4,
                paddingRight = 4,
                paddingTop = 2,
                paddingBottom = 2,
                borderTopLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomLeftRadius = 3,
                borderBottomRightRadius = 3
            }
        };

        var nameLabel = new Label(name)
        {
            style =
            {
                width = 110,
                fontSize = 11,
                unityFontStyleAndWeight = isWinner ? FontStyle.Bold : FontStyle.Normal
            }
        };
        row.Add(nameLabel);

        // Score Bar Container
        var barContainer = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                height = 12,
                backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.6f)),
                marginRight = 6,
                borderTopLeftRadius = 2,
                borderTopRightRadius = 2,
                borderBottomLeftRadius = 2,
                borderBottomRightRadius = 2,
                overflow = Overflow.Hidden
            }
        };

        float ratio = maxScore > 0f ? Mathf.Clamp01(score / maxScore) : 0f;
        var fill = new VisualElement
        {
            style =
            {
                width = Length.Percent(ratio * 100f),
                height = Length.Percent(100),
                backgroundColor = new StyleColor(isWinner ? accentColor : new Color(0.4f, 0.4f, 0.4f, 0.6f))
            }
        };
        barContainer.Add(fill);
        row.Add(barContainer);

        var scoreLabel = new Label($"{score:F1}")
        {
            style =
            {
                width = 40,
                fontSize = 10,
                unityTextAlign = TextAnchor.MiddleRight,
                unityFontStyleAndWeight = isWinner ? FontStyle.Bold : FontStyle.Normal
            }
        };
        row.Add(scoreLabel);

        if (isWinner)
        {
            var winBadge = CreateBadge("WIN", accentColor);
            winBadge.style.marginLeft = 4;
            row.Add(winBadge);
        }

        return row;
    }

    // =========================================================================
    // SECTION 5: Play Mode AI Debug & Simulation Controls
    // =========================================================================
    private VisualElement CreatePlayModeActionsSection()
    {
        var card = CreateCard("Play Mode AI Simulation & Controls");

        var helpBox = new HelpBox("Live GOAP decision evaluation & simulation triggers available in Play Mode.", HelpBoxMessageType.Info)
        {
            style = { marginBottom = 6 }
        };
        card.Add(helpBox);

        // Row 1: Decision arbitration & Interrupt
        var row1 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginBottom = 4 }
        };

        var evalBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.PerformStrategicDecision();
            UpdateHeaderBadges();
            UpdateScoreboards();
        })
        {
            text = "Force Evaluate Goal",
            tooltip = "Evaluates Utility AI goals and requests the winning goal from GOAP.",
            style = { flexGrow = 1, marginRight = 2 }
        };

        var interruptBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.InterruptCurrentPlan();
            UpdateHeaderBadges();
            UpdateScoreboards();
        })
        {
            text = "Interrupt Plan",
            tooltip = "Halts GOAP action, clears active goal, and stops NavMeshAgent.",
            style =
            {
                flexGrow = 1,
                marginLeft = 2,
                backgroundColor = new StyleColor(new Color(0.65f, 0.2f, 0.2f, 0.9f)),
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };

        row1.Add(evalBtn);
        row1.Add(interruptBtn);
        card.Add(row1);

        // Row 2: Sense / Lost Enemy
        var row2 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginBottom = 4 }
        };

        var senseBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.SenseEnemy();
            UpdateAllMeters();
        })
        {
            text = "Sense Enemy (+Threat/Fear)",
            style = { flexGrow = 1, marginRight = 2 }
        };

        var lostBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.LostEnemy();
            UpdateAllMeters();
        })
        {
            text = "Lost Enemy (-Threat/Fear)",
            style = { flexGrow = 1, marginLeft = 2 }
        };

        row2.Add(senseBtn);
        row2.Add(lostBtn);
        card.Add(row2);

        // Row 3: Skills & Targets
        var row3 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row }
        };

        var initSkillsBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.InitializeSkills();
            UpdateScoreboards();
        })
        {
            text = "Re-Initialize Skills",
            style = { flexGrow = 1, marginRight = 2 }
        };

        var updateTargetsBtn = new Button(() =>
        {
            if (baseAI == null) return;
            baseAI.UpdateTargets();
            UpdateAllMeters();
        })
        {
            text = "Update Targets",
            style = { flexGrow = 1, marginLeft = 2 }
        };

        row3.Add(initSkillsBtn);
        row3.Add(updateTargetsBtn);
        card.Add(row3);

        return card;
    }

    private void UpdatePlayModeVisibility()
    {
        if (playModeActionsContainer == null) return;
        playModeActionsContainer.SetEnabled(Application.isPlaying);
    }

    // =========================================================================
    // SECTION 6: Serialized Properties Foldout
    // =========================================================================
    private VisualElement CreatePropertiesSection()
    {
        var foldout = new Foldout
        {
            text = "Serialized Properties",
            value = false,
            style = { marginTop = 6 }
        };

        var propIterator = serializedObject.GetIterator();
        if (propIterator.NextVisible(true))
        {
            do
            {
                if (propIterator.name == "m_Script") continue;
                var propField = new PropertyField(propIterator);
                propField.Bind(serializedObject);
                foldout.Add(propField);
            } while (propIterator.NextVisible(false));
        }

        return foldout;
    }

    // =========================================================================
    // Periodic Tick & Helpers
    // =========================================================================
    private void OnScheduledUpdate()
    {
        UpdateHeaderBadges();
        UpdatePlayModeVisibility();
        UpdateAllMeters();
        UpdateScoreboards();
    }

    private VisualElement CreateCard(string title)
    {
        var card = new VisualElement
        {
            style =
            {
                backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f, 0.45f)),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 0.7f)),
                borderBottomColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 0.7f)),
                borderLeftColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 0.7f)),
                borderRightColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 0.7f)),
                borderTopLeftRadius = 5,
                borderTopRightRadius = 5,
                borderBottomLeftRadius = 5,
                borderBottomRightRadius = 5,
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 6,
                paddingBottom = 6,
                marginBottom = 8
            }
        };

        if (!string.IsNullOrEmpty(title))
        {
            var header = new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    marginBottom = 6,
                    color = new StyleColor(new Color(0.85f, 0.85f, 0.85f, 1f))
                }
            };
            card.Add(header);
        }

        return card;
    }

    private Label CreateBadge(string text, Color backgroundColor)
    {
        return new Label(text)
        {
            style =
            {
                backgroundColor = new StyleColor(backgroundColor),
                color = Color.white,
                fontSize = 10,
                unityFontStyleAndWeight = FontStyle.Bold,
                paddingLeft = 6,
                paddingRight = 6,
                paddingTop = 2,
                paddingBottom = 2,
                marginRight = 4,
                marginBottom = 4,
                borderTopLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomLeftRadius = 3,
                borderBottomRightRadius = 3
            }
        };
    }

    private void MarkDirtyAndSave()
    {
        if (baseAI == null) return;
        serializedObject.Update();
        EditorUtility.SetDirty(baseAI);
        if (baseAI.gameObject != null)
        {
            EditorUtility.SetDirty(baseAI.gameObject);
        }

        if (PrefabUtility.IsPartOfPrefabInstance(baseAI))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(baseAI);
        }
    }
}
