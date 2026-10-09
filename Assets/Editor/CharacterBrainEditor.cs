using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using SB4b1.ComponentUtils;

[CustomEditor(typeof(CharacterBrain))]
public class CharacterBrainEditor : Editor
{
    private CharacterBrain brain;

    // Header Badges
    private Label modeBadge;
    private Label statusBadge;

    // Diagnostics Elements
    private VisualElement diagnosticsContainer;
    private HelpBox diagnosticsHelpBox;
    private Foldout diagnosticsFoldout;

    // Runtime State Badges
    private Label busyBadge;
    private Label moveBadge;
    private Label attackBadge;
    private Label skillBadge;
    private Label dashingBadge;
    private Label attackingBadge;
    private Label castingBadge;
    private Label recoveringBadge;
    private Label aiPresenceBadge;

    private VisualElement playModeContainer;
    private IVisualElementScheduledItem stateUpdateSchedule;

    private void OnEnable()
    {
        brain = (CharacterBrain)target;
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

        // 1. Header & Control Mode Switcher
        root.Add(CreateHeaderSection());

        // 2. Play Mode Runtime Monitor & Test Controls
        playModeContainer = CreatePlayModeSection();
        root.Add(playModeContainer);

        // 3. Reference Diagnostics & Auto-Setup (Moved down)
        root.Add(CreateDiagnosticsSection());

        // 4. Default Serialized Properties Foldout
        root.Add(CreatePropertiesSection());

        // Bind Serialized Object
        root.Bind(serializedObject);

        // Schedule periodic update for runtime state & diagnostics
        stateUpdateSchedule = root.schedule.Execute(OnScheduledUpdate).Every(100);

        // Initial update
        UpdateHeaderBadges();
        RefreshDiagnostics();
        UpdatePlayModeVisibility();

        return root;
    }

    // =========================================================================
    // SECTION 1: Header & Control Mode
    // =========================================================================
    private VisualElement CreateHeaderSection()
    {
        var card = CreateCard("Control Mode & Status");

        // Status Row
        var statusRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 8
            }
        };

        var titleLabel = new Label(brain != null ? brain.name : "CharacterBrain")
        {
            style =
            {
                fontSize = 13,
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };

        var badgeRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center
            }
        };

        modeBadge = CreateBadge("Unknown", new Color(0.2f, 0.4f, 0.6f, 0.8f));
        statusBadge = CreateBadge("Unknown", new Color(0.3f, 0.3f, 0.3f, 0.8f));

        badgeRow.Add(modeBadge);
        badgeRow.Add(statusBadge);

        statusRow.Add(titleLabel);
        statusRow.Add(badgeRow);
        card.Add(statusRow);

        // Buttons Row
        var buttonsRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween
            }
        };

        var playerBtn = new Button(OnSetPlayerControl)
        {
            text = "Set Player Control",
            style = { flexGrow = 1, marginRight = 2 }
        };

        var agentBtn = new Button(OnSetAgentControl)
        {
            text = "Set AI Control",
            style = { flexGrow = 1, marginLeft = 2, marginRight = 2 }
        };

        var toggleBtn = new Button(OnToggleAI)
        {
            text = "Toggle AI",
            style = { flexGrow = 1, marginLeft = 2 }
        };

        buttonsRow.Add(playerBtn);
        buttonsRow.Add(agentBtn);
        buttonsRow.Add(toggleBtn);

        card.Add(buttonsRow);
        return card;
    }

    private void UpdateHeaderBadges()
    {
        if (brain == null) return;

        bool isPlayer = brain.ControlMode == ControlMode.Player;
        modeBadge.text = isPlayer ? "Player Control" : "AI Control";
        modeBadge.style.backgroundColor = isPlayer
            ? new StyleColor(new Color(0.18f, 0.42f, 0.72f, 0.9f))
            : new StyleColor(new Color(0.35f, 0.22f, 0.65f, 0.9f));

        bool isActive = brain.AgentStatus == AgentStatus.Active;
        statusBadge.text = isActive ? "Agent: Active" : "Agent: Inactive";
        statusBadge.style.backgroundColor = isActive
            ? new StyleColor(new Color(0.15f, 0.55f, 0.25f, 0.9f))
            : new StyleColor(new Color(0.45f, 0.45f, 0.45f, 0.8f));
    }

    private void OnSetPlayerControl()
    {
        if (brain == null) return;
        Undo.RecordObject(brain, "Set Player Control");
        brain.PlayerControl();
        MarkDirtyAndSave();
        UpdateHeaderBadges();
    }

    private void OnSetAgentControl()
    {
        if (brain == null) return;
        Undo.RecordObject(brain, "Set AI Control");
        brain.AgentControl();
        MarkDirtyAndSave();
        UpdateHeaderBadges();
    }

    private void OnToggleAI()
    {
        if (brain == null) return;
        Undo.RecordObject(brain, "Toggle AI Control");
        brain.ToggleAI();
        MarkDirtyAndSave();
        UpdateHeaderBadges();
    }

    // =========================================================================
    // SECTION 2: Reference Diagnostics & Auto-Setup
    // =========================================================================
    private VisualElement CreateDiagnosticsSection()
    {
        var card = CreateCard(null);

        diagnosticsFoldout = new Foldout
        {
            text = "Reference Diagnostics & Setup",
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
            text = "Auto-Wire References",
            tooltip = "Automatically scan self and children to locate AI Augment, StatsManager, AnimController, and SkillController.",
            style = { height = 24 }
        };
        diagnosticsFoldout.Add(autoWireBtn);

        card.Add(diagnosticsFoldout);
        return card;
    }

    private void RefreshDiagnostics()
    {
        if (brain == null || diagnosticsContainer == null) return;

        diagnosticsContainer.Clear();

        serializedObject.Update();
        var aiProp = serializedObject.FindProperty("aiAugment");
        var statsProp = serializedObject.FindProperty("statsManager");
        var animProp = serializedObject.FindProperty("animController");
        var skillProp = serializedObject.FindProperty("skillController");

        bool hasAI = aiProp != null && aiProp.objectReferenceValue != null;
        bool hasStats = statsProp != null && statsProp.objectReferenceValue != null;
        bool hasAnim = animProp != null && animProp.objectReferenceValue != null;
        bool hasSkill = skillProp != null && skillProp.objectReferenceValue != null;

        diagnosticsContainer.Add(CreateRefDiagnosticRow("AI Augment (Tag: AI_Augment)", hasAI, "Child object with tag 'AI_Augment'"));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterStatsManager", hasStats, "Required component for health/stamina/attack cooldown"));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("AnimationObjectController", hasAnim, "Handles attack, hit, and skill animations"));
        diagnosticsContainer.Add(CreateRefDiagnosticRow("CharacterSkillController", hasSkill, "Handles casting and skill execution"));

        int missingCount = (!hasAI ? 1 : 0) + (!hasStats ? 1 : 0) + (!hasAnim ? 1 : 0) + (!hasSkill ? 1 : 0);

        if (diagnosticsFoldout != null)
        {
            diagnosticsFoldout.text = missingCount > 0
                ? $"Reference Diagnostics & Setup ({missingCount} Unassigned)"
                : "Reference Diagnostics & Setup (All Assigned)";
        }

        if (missingCount > 0)
        {
            diagnosticsHelpBox.style.display = DisplayStyle.Flex;
            diagnosticsHelpBox.messageType = !hasStats ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning;
            diagnosticsHelpBox.text = $"{missingCount} reference(s) unassigned. Click 'Auto-Wire References' to discover them automatically.";
        }
        else
        {
            diagnosticsHelpBox.style.display = DisplayStyle.None;
        }
    }

    private VisualElement CreateRefDiagnosticRow(string labelText, bool isBound, string tooltip)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 2
            },
            tooltip = tooltip
        };

        var label = new Label(labelText)
        {
            style = { fontSize = 11, flexGrow = 1 }
        };

        var indicator = CreateBadge(isBound ? "Assigned" : "Missing",
            isBound ? new Color(0.15f, 0.5f, 0.25f, 0.8f) : new Color(0.65f, 0.2f, 0.2f, 0.8f));

        row.Add(label);
        row.Add(indicator);
        return row;
    }

    private void AutoWireReferences()
    {
        if (brain == null) return;

        Undo.RecordObject(brain, "Auto-Wire CharacterBrain References");
        serializedObject.Update();

        var aiProp = serializedObject.FindProperty("aiAugment");
        var statsProp = serializedObject.FindProperty("statsManager");
        var animProp = serializedObject.FindProperty("animController");
        var skillProp = serializedObject.FindProperty("skillController");

        int wiredCount = 0;

        if (aiProp != null && aiProp.objectReferenceValue == null)
        {
            var foundAugment = ComponentUtil.GetChildGameObjectByTag(brain.gameObject, "AI_Augment");
            if (foundAugment != null)
            {
                aiProp.objectReferenceValue = foundAugment;
                wiredCount++;
            }
        }

        if (statsProp != null && statsProp.objectReferenceValue == null)
        {
            var foundStats = brain.GetComponent<CharacterStatsManager>() ?? brain.GetComponentInChildren<CharacterStatsManager>();
            if (foundStats != null)
            {
                statsProp.objectReferenceValue = foundStats;
                wiredCount++;
            }
        }

        if (animProp != null && animProp.objectReferenceValue == null)
        {
            var foundAnim = brain.GetComponent<AnimationObjectController>() ?? brain.GetComponentInChildren<AnimationObjectController>();
            if (foundAnim != null)
            {
                animProp.objectReferenceValue = foundAnim;
                wiredCount++;
            }
        }

        if (skillProp != null && skillProp.objectReferenceValue == null)
        {
            var foundSkill = brain.GetComponent<CharacterSkillController>() ?? brain.GetComponentInChildren<CharacterSkillController>();
            if (foundSkill != null)
            {
                skillProp.objectReferenceValue = foundSkill;
                wiredCount++;
            }
        }

        serializedObject.ApplyModifiedProperties();
        MarkDirtyAndSave();
        RefreshDiagnostics();

        if (wiredCount > 0)
        {
            Debug.Log($"[CharacterBrainEditor] Successfully auto-wired {wiredCount} reference(s) on {brain.name}.", brain);
        }
    }

    // =========================================================================
    // SECTION 3: Play Mode Runtime Monitor & Debug Controls
    // =========================================================================
    private VisualElement CreatePlayModeSection()
    {
        var card = CreateCard("Play Mode Diagnostics & Actions");

        var helpBox = new HelpBox("Live runtime state monitor & test actions available in Play Mode.", HelpBoxMessageType.Info)
        {
            name = "PlayModeHelpBox",
            style = { marginBottom = 6 }
        };
        card.Add(helpBox);

        // State Badges Grid
        var badgesBox = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexWrap = Wrap.Wrap,
                justifyContent = Justify.FlexStart,
                marginBottom = 8
            }
        };

        busyBadge = CreateBadge("Idle", Color.gray);
        moveBadge = CreateBadge("Can Move", Color.gray);
        attackBadge = CreateBadge("Can Attack", Color.gray);
        skillBadge = CreateBadge("Can Cast", Color.gray);
        dashingBadge = CreateBadge("Dashing", Color.gray);
        attackingBadge = CreateBadge("Attacking", Color.gray);
        castingBadge = CreateBadge("Casting", Color.gray);
        recoveringBadge = CreateBadge("Recovering", Color.gray);
        aiPresenceBadge = CreateBadge("Has AI", Color.gray);

        badgesBox.Add(busyBadge);
        badgesBox.Add(moveBadge);
        badgesBox.Add(attackBadge);
        badgesBox.Add(skillBadge);
        badgesBox.Add(dashingBadge);
        badgesBox.Add(attackingBadge);
        badgesBox.Add(castingBadge);
        badgesBox.Add(recoveringBadge);
        badgesBox.Add(aiPresenceBadge);

        card.Add(badgesBox);

        // Action Controls
        var interruptBtn = new Button(OnInterruptActions)
        {
            text = "Interrupt All Actions",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.65f, 0.2f, 0.2f, 0.9f)),
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                height = 24,
                marginBottom = 6
            }
        };
        card.Add(interruptBtn);

        var actionsRow1 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginBottom = 4 }
        };
        actionsRow1.Add(new Button(() => brain?.TryStartDash()) { text = "Try Start Dash", style = { flexGrow = 1, marginRight = 2 } });
        actionsRow1.Add(new Button(() => brain?.EndDash()) { text = "End Dash", style = { flexGrow = 1, marginLeft = 2 } });
        card.Add(actionsRow1);

        var actionsRow2 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginBottom = 4 }
        };
        actionsRow2.Add(new Button(() => brain?.TryStartAttack()) { text = "Try Start Attack", style = { flexGrow = 1, marginRight = 2 } });
        actionsRow2.Add(new Button(() => brain?.EndAttack()) { text = "End Attack", style = { flexGrow = 1, marginLeft = 2 } });
        card.Add(actionsRow2);

        var actionsRow3 = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row }
        };
        actionsRow3.Add(new Button(() => brain?.TryCastSkill()) { text = "Try Cast Skill", style = { flexGrow = 1, marginRight = 2 } });
        actionsRow3.Add(new Button(() => brain?.EndCastSkill()) { text = "End Cast Skill", style = { flexGrow = 1, marginLeft = 2 } });
        card.Add(actionsRow3);

        return card;
    }

    private void UpdatePlayModeVisibility()
    {
        if (playModeContainer == null) return;
        playModeContainer.SetEnabled(Application.isPlaying);
    }

    private void OnInterruptActions()
    {
        if (brain == null) return;
        brain.InterruptAllActions();
    }

    private void UpdateRuntimeStateBadges()
    {
        if (brain == null || !Application.isPlaying) return;

        UpdateBadge(busyBadge, brain.IsBusy ? "Busy" : "Idle", brain.IsBusy, new Color(0.7f, 0.4f, 0.1f, 0.9f), new Color(0.2f, 0.5f, 0.25f, 0.8f));
        UpdateBadge(moveBadge, brain.CanMove ? "Can Move" : "Locked", brain.CanMove, new Color(0.18f, 0.55f, 0.3f, 0.9f), new Color(0.65f, 0.25f, 0.25f, 0.8f));
        UpdateBadge(attackBadge, brain.CanAttack ? "Attack Ready" : "Attack CD/Busy", brain.CanAttack, new Color(0.18f, 0.55f, 0.3f, 0.9f), new Color(0.45f, 0.45f, 0.45f, 0.8f));
        UpdateBadge(skillBadge, brain.CanCastSkill ? "Cast Ready" : "Skill Busy", brain.CanCastSkill, new Color(0.18f, 0.55f, 0.3f, 0.9f), new Color(0.45f, 0.45f, 0.45f, 0.8f));
        UpdateBadge(dashingBadge, "Dashing", brain.IsDashing, new Color(0.2f, 0.5f, 0.7f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0.4f));
        UpdateBadge(attackingBadge, "Attacking", brain.IsAttacking, new Color(0.75f, 0.35f, 0.1f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0.4f));
        UpdateBadge(castingBadge, "Casting", brain.IsCastingSkill, new Color(0.55f, 0.25f, 0.75f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0.4f));
        UpdateBadge(recoveringBadge, "Recovering", brain.IsRecovering, new Color(0.7f, 0.5f, 0.15f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0.4f));
        UpdateBadge(aiPresenceBadge, brain.HasAI ? "Has AI" : "No AI", brain.HasAI, new Color(0.18f, 0.5f, 0.45f, 0.9f), new Color(0.45f, 0.45f, 0.45f, 0.8f));
    }

    private void UpdateBadge(Label badge, string text, bool isActive, Color activeColor, Color inactiveColor)
    {
        if (badge == null) return;
        badge.text = text;
        badge.style.backgroundColor = new StyleColor(isActive ? activeColor : inactiveColor);
    }

    // =========================================================================
    // SECTION 4: Serialized Properties Foldout
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
        UpdateRuntimeStateBadges();
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
        if (brain == null) return;
        serializedObject.Update();
        EditorUtility.SetDirty(brain);
        if (brain.gameObject != null)
        {
            EditorUtility.SetDirty(brain.gameObject);
        }

        if (PrefabUtility.IsPartOfPrefabInstance(brain))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
        }
    }
}
