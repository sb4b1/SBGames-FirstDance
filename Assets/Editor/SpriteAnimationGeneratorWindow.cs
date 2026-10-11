#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Editor.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SBGames.Editor
{
    public class SpriteAnimationGeneratorWindow : EditorWindow
    {
        private enum WindowTab
        {
            SingleSheet,
            MultiSheet,
            BatchFolder
        }

        // Toolbar
        [SerializeField] private WindowTab activeTab = WindowTab.SingleSheet;
        private readonly string[] tabTitles = new[] { "🖼️ Single Sprite Sheet", "📚 Multiple Sprite Sheets", "📂 Batch Folder Generator" };

        // ----------------------------------------------------
        // Single Sheet Mode State
        // ----------------------------------------------------
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private SpriteSortMode sortMode = SpriteSortMode.NaturalNumericName;
        [SerializeField] private string outputFolderPath = "Assets/Animators/Characters/Monsters/Normal";
        [SerializeField] private string baseName = "";
        [SerializeField] private bool generateAnimatorController = true;
        [SerializeField] private bool includeStandardParameters = true;
        [SerializeField] private List<AnimationActionConfig> actions = new();

        // Cached single-sheet data
        [NonSerialized] private Sprite[] _cachedSprites = Array.Empty<Sprite>();
        [NonSerialized] private Texture2D _lastCheckedTexture;
        [NonSerialized] private SpriteSortMode _lastCheckedSortMode;
        [NonSerialized] private string _textureValidationMessage;
        [NonSerialized] private bool _textureIsValid;

        // UI state for single mode
        private Vector2 _singleScrollPos;
        private Vector2 _spritePreviewScrollPos;
        private bool _showPreviewStrip = true;
        private bool _showAdvancedEvents = false;

        // Callback hook for external tools (e.g. Character Wizard)
        public Action<AnimatorController, Sprite> OnGenerationCompleteCallback;

        // ----------------------------------------------------
        // Multi Sheet Mode State
        // ----------------------------------------------------
        [SerializeField] private string multiSheetFolderPath = "Assets/Resources/Characters/Adventurers";
        [SerializeField] private string multiOutputFolderPath = "Assets/Animators/Characters/Adventurers";
        [SerializeField] private string multiBaseName = "ADVENTURER_N001";
        [SerializeField] private SpriteSortMode multiSortMode = SpriteSortMode.NaturalNumericName;
        [SerializeField] private bool multiGenerateAnimatorController = true;
        [SerializeField] private bool multiIncludeStandardParameters = true;
        [SerializeField] private List<MultiSheetActionConfig> multiActions = new();
        [SerializeField] private bool multiShowAdvancedEvents = false;

        private Vector2 _multiScrollPos;

        // ----------------------------------------------------
        // Batch Mode State
        // ----------------------------------------------------
        public enum BatchMode
        {
            SingleSheetPerTexture,
            SubfolderPerCharacterMultiSheet
        }

        [SerializeField] private BatchMode batchMode = BatchMode.SingleSheetPerTexture;
        [SerializeField] private string batchSourceFolder = "Assets/Resources/Characters/Adventurers";
        [SerializeField] private string batchOutputFolder = "Assets/Animators/Characters";
        [SerializeField] private string batchFileFilter = "*.png";
        [SerializeField] private int batchSelectedPresetIndex = 0;
        [SerializeField] private bool batchCreateSubfolders = true;
        [SerializeField] private bool batchOverwriteExisting = true;

        private Vector2 _batchScrollPos;
        private List<string> _batchFoundTexturePaths = new();
        private List<string> _batchFoundSubfolderPaths = new();

        // ----------------------------------------------------
        // Menu Items
        // ----------------------------------------------------
        [MenuItem("Tools/SBGames/Sprite Animation Generator", priority = 10)]
        [MenuItem("SBGames/Animation/Sprite Animation Generator", priority = 15)]
        [MenuItem("Assets/Create/SBGames/Sprite Animation Generator", priority = 55)]
        public static SpriteAnimationGeneratorWindow OpenWindow()
        {
            var window = GetWindow<SpriteAnimationGeneratorWindow>("Animation Generator");
            window.minSize = new Vector2(580, 720);
            window.Show();
            return window;
        }

        /// <summary>
        /// External helper to open and configure the generator with contextual defaults (e.g. from Character Creator Wizard).
        /// </summary>
        public static SpriteAnimationGeneratorWindow OpenWithContext(
            Texture2D texture,
            string targetFolder,
            string characterId,
            Action<AnimatorController, Sprite> onGenerated = null)
        {
            var window = OpenWindow();
            window.activeTab = WindowTab.SingleSheet;
            if (texture != null)
            {
                window.sourceTexture = texture;
            }
            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                window.outputFolderPath = targetFolder;
            }
            if (!string.IsNullOrWhiteSpace(characterId))
            {
                window.baseName = characterId;
            }
            window.OnGenerationCompleteCallback = onGenerated;
            window.RefreshTextureInfo(force: true);
            window.Repaint();
            return window;
        }

        /// <summary>
        /// External helper to open and configure the generator in Multi-Sheet mode with contextual defaults.
        /// </summary>
        public static SpriteAnimationGeneratorWindow OpenWithMultiSheetContext(
            string folderPath,
            string targetFolder,
            string characterId,
            Action<AnimatorController, Sprite> onGenerated = null)
        {
            var window = OpenWindow();
            window.activeTab = WindowTab.MultiSheet;
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                window.multiSheetFolderPath = folderPath;
                window.multiActions = SpriteAnimationBuilder.AutoMatchMultiSheetActionsFromFolder(folderPath, window.multiActions);
            }
            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                window.multiOutputFolderPath = targetFolder;
            }
            if (!string.IsNullOrWhiteSpace(characterId))
            {
                window.multiBaseName = characterId;
            }
            window.OnGenerationCompleteCallback = onGenerated;
            window.Repaint();
            return window;
        }

        private void OnEnable()
        {
            if (actions == null || actions.Count == 0)
            {
                ApplyStandardPreset(0); // Default to 6-action full set
            }
            if (multiActions == null || multiActions.Count == 0)
            {
                multiActions = SpriteAnimationBuilder.GetDefaultMultiSheetActions();
            }
            RefreshTextureInfo(force: true);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabBar();

            EditorGUILayout.Space(6);

            if (activeTab == WindowTab.SingleSheet)
            {
                DrawSingleSheetTab();
            }
            else if (activeTab == WindowTab.MultiSheet)
            {
                DrawMultiSheetTab();
            }
            else
            {
                DrawBatchFolderTab();
            }
        }

        #region Header & Navigation

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("🎬 Sprite Sheet Animation & Controller Generator", titleStyle);
                EditorGUILayout.LabelField("Automates AnimationClip slicing, Event injection & Animator Controller state-machine setup.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawTabBar()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int newTab = GUILayout.Toolbar((int)activeTab, tabTitles, EditorStyles.toolbarButton, GUILayout.Height(24));
                if (newTab != (int)activeTab)
                {
                    activeTab = (WindowTab)newTab;
                    GUI.FocusControl(null);
                }
            }
        }

        #endregion

        #region Single Sheet Mode GUI

        private void DrawSingleSheetTab()
        {
            _singleScrollPos = EditorGUILayout.BeginScrollView(_singleScrollPos);

            // 1. Source Texture Ingestion Section
            DrawSourceTextureSection();
            EditorGUILayout.Space(6);

            // 2. Sprite Preview Strip
            if (sourceTexture != null && _cachedSprites.Length > 0)
            {
                DrawSpritePreviewStrip();
                EditorGUILayout.Space(6);
            }

            // 3. Preset Quick-Selectors
            DrawPresetSelectionSection();
            EditorGUILayout.Space(6);

            // 4. Action Configurations List
            DrawActionConfigurationList();
            EditorGUILayout.Space(6);

            // 5. Output Destination & Generation Options
            DrawOutputDestinationSection();
            EditorGUILayout.Space(12);

            // 6. Big Action Button
            DrawGenerationButton();
            EditorGUILayout.Space(12);

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceTextureSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. Source Sprite Sheet", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Sprite Sheet Texture", "Target sliced Texture2D / Sprite sheet"), sourceTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (sourceTexture != null && string.IsNullOrWhiteSpace(baseName))
                    {
                        baseName = sourceTexture.name;
                    }
                    RefreshTextureInfo(force: true);
                }

                if (sourceTexture != null)
                {
                    EditorGUI.BeginChangeCheck();
                    sortMode = (SpriteSortMode)EditorGUILayout.EnumPopup(new GUIContent("Sprite Slicing Sort Mode", "How sub-sprites are ordered across frames"), sortMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshTextureInfo(force: true);
                    }

                    // Importer Status / Warning
                    if (!_textureIsValid)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.HelpBox($"⚠️ Importer Issue: {_textureValidationMessage}", MessageType.Warning);
                        if (GUILayout.Button("🔧 Fix Importer Settings (Set Sprite Multiple & Point Filter)", GUILayout.Height(26)))
                        {
                            if (SpriteAnimationBuilder.FixTextureImporterSettings(sourceTexture))
                            {
                                RefreshTextureInfo(force: true);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"✅ Sliced Sprites Detected: {_cachedSprites.Length} frame(s)", EditorStyles.miniBoldLabel);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Drag and drop a sliced Texture2D sprite sheet above to begin.", MessageType.Info);
                }
            }
        }

        private void DrawSpritePreviewStrip()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showPreviewStrip = EditorGUILayout.Foldout(_showPreviewStrip, $"Frame Sequence Preview ({_cachedSprites.Length} Frames)", true, EditorStyles.foldoutHeader);
                }

                if (!_showPreviewStrip) return;

                EditorGUILayout.Space(2);
                _spritePreviewScrollPos = EditorGUILayout.BeginScrollView(_spritePreviewScrollPos, GUILayout.Height(90));
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _cachedSprites.Length; i++)
                    {
                        var spr = _cachedSprites[i];
                        if (spr == null) continue;

                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(64)))
                        {
                            // Render frame index badge
                            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                alignment = TextAnchor.MiddleCenter,
                                fontStyle = FontStyle.Bold
                            };
                            EditorGUILayout.LabelField($"[{i}]", badgeStyle, GUILayout.Width(60));

                            // Sprite preview
                            Texture2D previewTex = AssetPreview.GetAssetPreview(spr);
                            if (previewTex != null)
                            {
                                GUILayout.Box(previewTex, GUILayout.Width(56), GUILayout.Height(56));
                            }
                            else
                            {
                                GUILayout.Box(spr.name, GUILayout.Width(56), GUILayout.Height(56));
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPresetSelectionSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Action Presets", EditorStyles.boldLabel);

                var presets = SpriteAnimationBuilder.GetStandardPresets();

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < presets.Count; i++)
                    {
                        if (GUILayout.Button(presets[i].presetName, EditorStyles.miniButton))
                        {
                            ApplyStandardPreset(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("⚡ Auto-Split Equal Frames (4 Actions)", EditorStyles.miniButtonLeft))
                    {
                        if (_cachedSprites.Length > 0)
                        {
                            actions = SpriteAnimationBuilder.CreateAutoSplitPreset(_cachedSprites.Length, 4);
                            GUIUtility.ExitGUI();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Notice", "Please assign a valid sliced sprite sheet first.", "OK");
                        }
                    }
                    if (GUILayout.Button("⚡ Auto-Split Equal Frames (6 Actions)", EditorStyles.miniButtonMid))
                    {
                        if (_cachedSprites.Length > 0)
                        {
                            actions = SpriteAnimationBuilder.CreateAutoSplitPreset(_cachedSprites.Length, 6);
                            GUIUtility.ExitGUI();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Notice", "Please assign a valid sliced sprite sheet first.", "OK");
                        }
                    }
                    if (GUILayout.Button("🗑️ Clear All Actions", EditorStyles.miniButtonRight))
                    {
                        actions.Clear();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void DrawActionConfigurationList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("3. Actions & Animation Events Schema", EditorStyles.boldLabel);
                    _showAdvancedEvents = GUILayout.Toggle(_showAdvancedEvents, "Show Event Details", EditorStyles.miniButton, GUILayout.Width(130));
                }

                EditorGUILayout.Space(2);

                if (actions == null || actions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No actions configured. Click a preset above or '+ Add Action' below.", MessageType.Warning);
                }
                else
                {
                    int totalSprites = _cachedSprites.Length;
                    int moveUpIndex = -1;
                    int moveDownIndex = -1;
                    int duplicateIndex = -1;
                    int removeIndex = -1;

                    for (int i = 0; i < actions.Count; i++)
                    {
                        var act = actions[i];
                        if (act == null) continue;

                        bool isValid = act.IsValid(totalSprites, out string actError);
                        Color prevColor = GUI.backgroundColor;
                        if (!isValid) GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);

                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            GUI.backgroundColor = prevColor;

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(24));
                                act.actionName = EditorGUILayout.TextField(act.actionName, GUILayout.Width(110));
                                
                                EditorGUILayout.LabelField("Start:", GUILayout.Width(36));
                                act.startFrame = EditorGUILayout.IntField(act.startFrame, GUILayout.Width(40));

                                EditorGUILayout.LabelField("Count:", GUILayout.Width(42));
                                act.frameCount = EditorGUILayout.IntField(act.frameCount, GUILayout.Width(40));

                                EditorGUILayout.LabelField("FPS:", GUILayout.Width(30));
                                act.frameRate = EditorGUILayout.FloatField(act.frameRate, GUILayout.Width(35));

                                act.isLooping = GUILayout.Toggle(act.isLooping, "Loop", GUILayout.Width(50));

                                // Frame Range Badge
                                int endFrame = act.startFrame + Mathf.Max(0, act.frameCount - 1);
                                string rangeStr = $"[{act.startFrame}..{endFrame}]";
                                var rangeStyle = new GUIStyle(EditorStyles.miniLabel)
                                {
                                    normal = { textColor = isValid ? (totalSprites > 0 ? Color.green : Color.gray) : Color.red },
                                    fontStyle = FontStyle.Bold
                                };
                                EditorGUILayout.LabelField(rangeStr, rangeStyle, GUILayout.Width(55));

                                GUILayout.FlexibleSpace();

                                // Control buttons - record operations to execute safely after loop
                                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20)) && i > 0)
                                {
                                    moveUpIndex = i;
                                }
                                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(20)) && i < actions.Count - 1)
                                {
                                    moveDownIndex = i;
                                }
                                if (GUILayout.Button("📋", EditorStyles.miniButtonMid, GUILayout.Width(22)))
                                {
                                    duplicateIndex = i;
                                }
                                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                                {
                                    removeIndex = i;
                                }
                            }

                            if (!isValid)
                            {
                                EditorGUILayout.LabelField($"⚠️ {actError}", EditorStyles.miniLabel);
                            }

                            // Event Details Sub-panel
                            if (_showAdvancedEvents || act.addEvents)
                            {
                                EditorGUILayout.Space(2);
                                using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
                                {
                                    act.addEvents = EditorGUILayout.ToggleLeft("Inject Events", act.addEvents, GUILayout.Width(95));

                                    if (act.addEvents)
                                    {
                                        EditorGUILayout.LabelField("Hit/Cast Frame:", GUILayout.Width(90));
                                        act.impactFrameOffset = EditorGUILayout.IntField(act.impactFrameOffset, GUILayout.Width(30));

                                        EditorGUILayout.LabelField("Fn:", GUILayout.Width(24));
                                        act.impactEventName = EditorGUILayout.TextField(act.impactEventName, GUILayout.Width(160));

                                        act.addCompleteEvent = EditorGUILayout.ToggleLeft("Complete Event", act.addCompleteEvent, GUILayout.Width(110));
                                        if (act.addCompleteEvent)
                                        {
                                            act.completeEventName = EditorGUILayout.TextField(act.completeEventName, GUILayout.Width(170));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Apply deferred list operations cleanly after loop
                    if (moveUpIndex > 0)
                    {
                        var temp = actions[moveUpIndex];
                        actions[moveUpIndex] = actions[moveUpIndex - 1];
                        actions[moveUpIndex - 1] = temp;
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (moveDownIndex >= 0 && moveDownIndex < actions.Count - 1)
                    {
                        var temp = actions[moveDownIndex];
                        actions[moveDownIndex] = actions[moveDownIndex + 1];
                        actions[moveDownIndex + 1] = temp;
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (duplicateIndex >= 0 && duplicateIndex < actions.Count)
                    {
                        actions.Insert(duplicateIndex + 1, actions[duplicateIndex].Clone());
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (removeIndex >= 0 && removeIndex < actions.Count)
                    {
                        actions.RemoveAt(removeIndex);
                        GUI.changed = true;
                        Repaint();
                    }
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+ Add Custom Action", GUILayout.Height(22)))
                {
                    int nextStart = 0;
                    if (actions.Count > 0)
                    {
                        var last = actions.Last();
                        nextStart = last.startFrame + last.frameCount;
                    }
                    actions.Add(new AnimationActionConfig($"Action_{actions.Count + 1}", nextStart, 4, 10f, false));
                    GUI.changed = true;
                    Repaint();
                }
            }
        }

        private void DrawOutputDestinationSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("4. Destination & Synthesis", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                outputFolderPath = EditorGUILayout.TextField(new GUIContent("Output Folder", "Assets destination path for clips and controller"), outputFolderPath);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string absPath = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(absPath))
                    {
                        if (absPath.StartsWith(Application.dataPath))
                        {
                            outputFolderPath = "Assets" + absPath.Substring(Application.dataPath.Length);
                        }
                        else
                        {
                            outputFolderPath = absPath;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                baseName = EditorGUILayout.TextField(new GUIContent("Base Name / Prefix", "Identifier prefix for clips and controller (e.g. MONSTER_N001)"), baseName);

                EditorGUILayout.Space(2);
                generateAnimatorController = EditorGUILayout.Toggle(new GUIContent("Generate Animator Controller", "Synthesizes .controller with state machine transitions"), generateAnimatorController);
                if (generateAnimatorController)
                {
                    EditorGUI.indentLevel++;
                    includeStandardParameters = EditorGUILayout.Toggle(new GUIContent("Standard Parameters", "Add IsMoving, Attack, CastSkill, Hit, IsDead & compatibility parameters"), includeStandardParameters);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawGenerationButton()
        {
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 38
            };

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);

            if (GUILayout.Button("🚀 Generate Animation Clips & Animator Controller", btnStyle))
            {
                ExecuteSingleGeneration();
            }

            GUI.backgroundColor = prevColor;
        }

        private void ExecuteSingleGeneration()
        {
            if (sourceTexture == null)
            {
                EditorUtility.DisplayDialog("Validation Error", "Please assign a Source Sprite Sheet Texture2D first.", "OK");
                return;
            }

            if (!_textureIsValid)
            {
                bool fix = EditorUtility.DisplayDialog(
                    "Texture Importer Issue",
                    $"The selected texture has invalid importer settings:\n\n{_textureValidationMessage}\n\nWould you like the tool to automatically configure it to Multiple Sprite mode?",
                    "Fix & Continue", "Cancel");

                if (fix)
                {
                    SpriteAnimationBuilder.FixTextureImporterSettings(sourceTexture);
                    RefreshTextureInfo(force: true);
                }
                else
                {
                    return;
                }
            }

            if (actions == null || actions.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Error", "Please configure at least one action before generating.", "OK");
                return;
            }

            var settings = new AnimationGenerationSettings
            {
                sourceTexture = sourceTexture,
                sortMode = sortMode,
                outputFolderPath = outputFolderPath,
                baseName = string.IsNullOrWhiteSpace(baseName) ? sourceTexture.name : baseName,
                actions = actions,
                generateAnimatorController = generateAnimatorController,
                includeStandardParameters = includeStandardParameters
            };

            EditorUtility.DisplayProgressBar("Generating Animations", "Creating AnimationClips and Animator Controller...", 0.4f);

            try
            {
                var result = SpriteAnimationBuilder.GenerateFromSettings(settings);

                EditorUtility.ClearProgressBar();

                if (result.Success)
                {
                    // If callback is registered (e.g. from Character Creator Wizard)
                    Sprite defaultSprite = _cachedSprites.Length > 0 ? _cachedSprites[0] : null;
                    OnGenerationCompleteCallback?.Invoke(result.CreatedController, defaultSprite);

                    if (result.CreatedController != null)
                    {
                        Selection.activeObject = result.CreatedController;
                        EditorGUIUtility.PingObject(result.CreatedController);
                    }
                    else if (result.CreatedClips.Count > 0)
                    {
                        Selection.activeObject = result.CreatedClips[0];
                        EditorGUIUtility.PingObject(result.CreatedClips[0]);
                    }

                    string summary = $"Successfully generated:\n• {result.CreatedClips.Count} AnimationClip(s)\n" +
                                     (result.CreatedController != null ? $"• AnimatorController '{result.CreatedController.name}'\n" : "") +
                                     $"\nDestination:\n{outputFolderPath}";

                    if (result.Warnings.Count > 0)
                    {
                        summary += $"\n\nWarnings:\n{string.Join("\n", result.Warnings)}";
                    }

                    EditorUtility.DisplayDialog("Generation Complete", summary, "Awesome!");
                }
                else
                {
                    EditorUtility.DisplayDialog("Generation Failed", $"Failed to generate animations:\n\n{result.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SpriteAnimationGeneratorWindow] Generation failed: {ex}");
                EditorUtility.DisplayDialog("Error", $"Generation encountered an exception:\n{ex.Message}", "OK");
            }
        }

        private void ApplyStandardPreset(int index)
        {
            var presets = SpriteAnimationBuilder.GetStandardPresets();
            if (index >= 0 && index < presets.Count)
            {
                actions.Clear();
                foreach (var act in presets[index].actions)
                {
                    actions.Add(act.Clone());
                }
            }
        }

        private void RefreshTextureInfo(bool force = false)
        {
            if (!force && _lastCheckedTexture == sourceTexture && _lastCheckedSortMode == sortMode) return;

            _lastCheckedTexture = sourceTexture;
            _lastCheckedSortMode = sortMode;

            if (sourceTexture == null)
            {
                _cachedSprites = Array.Empty<Sprite>();
                _textureIsValid = false;
                _textureValidationMessage = "No texture selected.";
                return;
            }

            _textureIsValid = SpriteAnimationBuilder.ValidateTextureImporter(sourceTexture, out _textureValidationMessage);
            if (_textureIsValid)
            {
                _cachedSprites = SpriteAnimationBuilder.ExtractSprites(sourceTexture, sortMode);
            }
            else
            {
                _cachedSprites = Array.Empty<Sprite>();
            }
        }

        #endregion

        #region Multi Sheet Mode GUI

        private void DrawMultiSheetTab()
        {
            _multiScrollPos = EditorGUILayout.BeginScrollView(_multiScrollPos);

            // 1. Output & Controller Setup
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. Character & Output Configuration", EditorStyles.boldLabel);

                multiBaseName = EditorGUILayout.TextField(new GUIContent("Character ID / Prefix", "Identifier for generated clips & controller (e.g. ADVENTURER_N001)"), multiBaseName);

                EditorGUILayout.BeginHorizontal();
                multiOutputFolderPath = EditorGUILayout.TextField("Output Destination", multiOutputFolderPath);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Animation Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        multiOutputFolderPath = path.StartsWith(Application.dataPath)
                            ? "Assets" + path.Substring(Application.dataPath.Length)
                            : path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                multiSortMode = (SpriteSortMode)EditorGUILayout.EnumPopup(new GUIContent("Sprite Sort Mode", "Sorting order for sliced frames within each sheet"), multiSortMode);

                multiGenerateAnimatorController = EditorGUILayout.Toggle(new GUIContent("Generate Controller", "Creates AnimatorController state machine wired for all actions"), multiGenerateAnimatorController);
                if (multiGenerateAnimatorController)
                {
                    multiIncludeStandardParameters = EditorGUILayout.Toggle(new GUIContent("Compatibility Parameters", "Includes Speed, MoveX, MoveY, GetHit compatibility params"), multiIncludeStandardParameters);
                }
            }

            EditorGUILayout.Space(6);

            // 2. Folder Auto-Match Bar
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Sprite Sheet Directory & Auto-Matching", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                multiSheetFolderPath = EditorGUILayout.TextField("Character Folder", multiSheetFolderPath);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Character Sprite Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        multiSheetFolderPath = path.StartsWith(Application.dataPath)
                            ? "Assets" + path.Substring(Application.dataPath.Length)
                            : path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔍 Auto-Match From Folder", GUILayout.Height(26)))
                {
                    if (string.IsNullOrWhiteSpace(multiSheetFolderPath) || !Directory.Exists(multiSheetFolderPath))
                    {
                        EditorUtility.DisplayDialog("Folder Error", $"Folder '{multiSheetFolderPath}' does not exist.", "OK");
                    }
                    else
                    {
                        multiActions = SpriteAnimationBuilder.AutoMatchMultiSheetActionsFromFolder(multiSheetFolderPath, multiActions);
                        if (string.IsNullOrWhiteSpace(multiBaseName))
                        {
                            multiBaseName = Path.GetFileName(multiSheetFolderPath.TrimEnd('/', '\\'));
                        }
                    }
                }

                if (GUILayout.Button("Reset Standard Slots", GUILayout.Height(26), GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog("Reset Slots", "Reset action slots to standard template (Idle, Move, Attack, CastSkill, Hit, Die)?", "Reset", "Cancel"))
                    {
                        multiActions = SpriteAnimationBuilder.GetDefaultMultiSheetActions();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);

            // 3. Multi-Sheet Action Slots
            DrawMultiSheetActionList();

            EditorGUILayout.Space(12);

            // 4. Generate Button
            DrawMultiSheetGenerateFooter();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMultiSheetActionList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("3. Action Sheets & Frame Configuration", EditorStyles.boldLabel);
                    multiShowAdvancedEvents = GUILayout.Toggle(multiShowAdvancedEvents, "Show Event Details", EditorStyles.miniButton, GUILayout.Width(130));
                }

                EditorGUILayout.Space(4);

                if (multiActions == null || multiActions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No action slots defined. Click 'Reset Standard Slots' or '+ Add Action Slot' below.", MessageType.Warning);
                }
                else
                {
                    int moveUpIndex = -1;
                    int moveDownIndex = -1;
                    int duplicateIndex = -1;
                    int removeIndex = -1;

                    for (int i = 0; i < multiActions.Count; i++)
                    {
                        var act = multiActions[i];
                        if (act == null) continue;

                        DrawSingleMultiSheetActionSlot(act, i, ref moveUpIndex, ref moveDownIndex, ref duplicateIndex, ref removeIndex);
                    }

                    // Apply deferred list operations cleanly after loop
                    if (moveUpIndex > 0)
                    {
                        var temp = multiActions[moveUpIndex];
                        multiActions[moveUpIndex] = multiActions[moveUpIndex - 1];
                        multiActions[moveUpIndex - 1] = temp;
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (moveDownIndex >= 0 && moveDownIndex < multiActions.Count - 1)
                    {
                        var temp = multiActions[moveDownIndex];
                        multiActions[moveDownIndex] = multiActions[moveDownIndex + 1];
                        multiActions[moveDownIndex + 1] = temp;
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (duplicateIndex >= 0 && duplicateIndex < multiActions.Count)
                    {
                        multiActions.Insert(duplicateIndex + 1, multiActions[duplicateIndex].Clone());
                        GUI.changed = true;
                        Repaint();
                    }
                    else if (removeIndex >= 0 && removeIndex < multiActions.Count)
                    {
                        multiActions.RemoveAt(removeIndex);
                        GUI.changed = true;
                        Repaint();
                    }
                }

                EditorGUILayout.Space(6);

                if (GUILayout.Button("➕ Add Action Slot", GUILayout.Height(24)))
                {
                    multiActions.Add(new MultiSheetActionConfig($"Action_{multiActions.Count + 1}", null, false, 10f));
                    GUI.changed = true;
                    Repaint();
                }
            }
        }

        private void DrawSingleMultiSheetActionSlot(
            MultiSheetActionConfig act,
            int index,
            ref int moveUpIndex,
            ref int moveDownIndex,
            ref int duplicateIndex,
            ref int removeIndex)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Top Row: Index, Name, Texture Field, Slot Controls
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(24));
                    act.actionName = EditorGUILayout.TextField(act.actionName, GUILayout.Width(110));

                    EditorGUILayout.LabelField("Sheet:", GUILayout.Width(42));
                    var newTex = (Texture2D)EditorGUILayout.ObjectField(act.sourceTexture, typeof(Texture2D), false);
                    if (newTex != act.sourceTexture)
                    {
                        act.sourceTexture = newTex;
                    }

                    // Sprite status badge
                    if (act.sourceTexture != null)
                    {
                        var subSprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(act.sourceTexture)).OfType<Sprite>().ToArray();
                        if (subSprites.Length > 0)
                        {
                            var countStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = Color.green },
                                fontStyle = FontStyle.Bold
                            };
                            EditorGUILayout.LabelField($"({subSprites.Length} sprites)", countStyle, GUILayout.Width(75));
                        }
                        else
                        {
                            var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = new Color(1f, 0.6f, 0.1f) },
                                fontStyle = FontStyle.Bold
                            };
                            EditorGUILayout.LabelField("(Not sliced)", warnStyle, GUILayout.Width(75));
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(Empty - skipped)", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(95));
                    }

                    GUILayout.FlexibleSpace();

                    // Slot reordering / removal - record operations to execute safely after loop
                    if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20)) && index > 0)
                    {
                        moveUpIndex = index;
                    }
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(20)) && index < multiActions.Count - 1)
                    {
                        moveDownIndex = index;
                    }
                    if (GUILayout.Button("📋", EditorStyles.miniButtonMid, GUILayout.Width(22)))
                    {
                        duplicateIndex = index;
                    }
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                    {
                        removeIndex = index;
                    }
                }

                // Second Row: Frame Options & Playback
                if (act.sourceTexture != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        act.useAllFrames = EditorGUILayout.ToggleLeft("Use All Sliced Frames", act.useAllFrames, GUILayout.Width(150));

                        if (!act.useAllFrames)
                        {
                            EditorGUILayout.LabelField("Start:", GUILayout.Width(36));
                            act.startFrame = EditorGUILayout.IntField(act.startFrame, GUILayout.Width(35));

                            EditorGUILayout.LabelField("Count:", GUILayout.Width(42));
                            act.frameCount = EditorGUILayout.IntField(act.frameCount, GUILayout.Width(35));
                        }

                        EditorGUILayout.LabelField("FPS:", GUILayout.Width(30));
                        act.frameRate = EditorGUILayout.FloatField(act.frameRate, GUILayout.Width(35));

                        act.isLooping = GUILayout.Toggle(act.isLooping, "Loop", GUILayout.Width(50));
                    }

                    // Event Details Sub-panel
                    if (multiShowAdvancedEvents || act.addEvents)
                    {
                        EditorGUILayout.Space(2);
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
                        {
                            act.addEvents = EditorGUILayout.ToggleLeft("Inject Events", act.addEvents, GUILayout.Width(95));

                            if (act.addEvents)
                            {
                                EditorGUILayout.LabelField("Impact Frame:", GUILayout.Width(82));
                                act.impactFrameOffset = EditorGUILayout.IntField(act.impactFrameOffset, GUILayout.Width(35));

                                EditorGUILayout.LabelField("Name:", GUILayout.Width(40));
                                act.impactEventName = EditorGUILayout.TextField(act.impactEventName, GUILayout.Width(150));

                                act.addCompleteEvent = EditorGUILayout.ToggleLeft("On Complete", act.addCompleteEvent, GUILayout.Width(88));
                                if (act.addCompleteEvent)
                                {
                                    act.completeEventName = EditorGUILayout.TextField(act.completeEventName, GUILayout.Width(150));
                                }
                            }
                        }
                    }

                    // Mini sprite preview strip
                    DrawTextureSpritePreviewStrip(act.sourceTexture, act.useAllFrames, act.startFrame, act.frameCount);
                }
            }
        }

        private void DrawTextureSpritePreviewStrip(Texture2D tex, bool useAll, int start, int count)
        {
            if (tex == null) return;
            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(assetPath)) return;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0) return;

            int displayCount = useAll ? sprites.Length : Mathf.Min(count, sprites.Length - start);
            if (displayCount <= 0) return;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                int startIndex = useAll ? 0 : Mathf.Clamp(start, 0, sprites.Length - 1);
                int endIndex = Mathf.Min(startIndex + displayCount, sprites.Length);

                for (int s = startIndex; s < endIndex && s < startIndex + 10; s++)
                {
                    var spr = sprites[s];
                    if (spr == null) continue;
                    Texture2D preview = AssetPreview.GetAssetPreview(spr);
                    if (preview != null)
                    {
                        GUILayout.Box(preview, GUILayout.Width(34), GUILayout.Height(34));
                    }
                }
                if (endIndex < sprites.Length && endIndex > startIndex + 10)
                {
                    EditorGUILayout.LabelField($"... +{sprites.Length - (startIndex + 10)} more", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawMultiSheetGenerateFooter()
        {
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            int activeCount = multiActions.Count(a => a != null && a.sourceTexture != null);
            bool canGenerate = activeCount > 0 && !string.IsNullOrWhiteSpace(multiOutputFolderPath);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = canGenerate ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);

            string btnText = canGenerate
                ? $"⚡ Generate Multi-Sheet Animations & Controller ({activeCount} Actions)"
                : "⚠️ Assign At Least One Action Sheet to Generate";

            if (GUILayout.Button(btnText, btnStyle) && canGenerate)
            {
                ExecuteMultiSheetGeneration();
            }

            GUI.backgroundColor = prevColor;
        }

        private void ExecuteMultiSheetGeneration()
        {
            string cleanId = string.IsNullOrWhiteSpace(multiBaseName) ? "Character" : multiBaseName.Trim();
            string outputDir = $"{multiOutputFolderPath.TrimEnd('/')}/{cleanId}";

            var settings = new MultiSheetGenerationSettings
            {
                baseName = cleanId,
                outputFolderPath = outputDir,
                sortMode = multiSortMode,
                generateAnimatorController = multiGenerateAnimatorController,
                includeStandardParameters = multiIncludeStandardParameters,
                actions = multiActions
            };

            var res = SpriteAnimationBuilder.GenerateFromMultiSheetSettings(settings);
            if (res.Success)
            {
                Sprite defaultSprite = null;
                var idleAction = multiActions.FirstOrDefault(a => a != null && a.sourceTexture != null && a.actionName.Equals("Idle", StringComparison.OrdinalIgnoreCase));
                if (idleAction == null)
                {
                    idleAction = multiActions.FirstOrDefault(a => a != null && a.sourceTexture != null);
                }
                if (idleAction != null && idleAction.sourceTexture != null)
                {
                    var sprList = SpriteAnimationBuilder.ExtractSprites(idleAction.sourceTexture, multiSortMode);
                    if (sprList.Length > 0) defaultSprite = sprList[0];
                }

                OnGenerationCompleteCallback?.Invoke(res.CreatedController, defaultSprite);

                string msg = $"Multi-Sheet Generation Successful!\n\n• Output: {outputDir}\n• Animation Clips: {res.CreatedClips.Count}";
                if (res.CreatedController != null)
                {
                    msg += $"\n• Animator Controller: {res.CreatedController.name}";
                }
                EditorUtility.DisplayDialog("Generation Complete", msg, "Great!");
            }
            else
            {
                EditorUtility.DisplayDialog("Generation Failed", $"Failed to generate multi-sheet animations:\n\n{res.Message}", "OK");
            }
        }

        #endregion

        #region Batch Mode GUI

        private void DrawBatchFolderTab()
        {
            _batchScrollPos = EditorGUILayout.BeginScrollView(_batchScrollPos);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. Batch Mode & Configuration", EditorStyles.boldLabel);

                batchMode = (BatchMode)EditorGUILayout.EnumPopup("Batch Mode", batchMode);

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                string sourceLabel = batchMode == BatchMode.SingleSheetPerTexture ? "Source Folder (Textures)" : "Source Folder (Character Subdirs)";
                batchSourceFolder = EditorGUILayout.TextField(sourceLabel, batchSourceFolder);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Batch Source Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        batchSourceFolder = path.StartsWith(Application.dataPath) ? "Assets" + path.Substring(Application.dataPath.Length) : path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (batchMode == BatchMode.SingleSheetPerTexture)
                {
                    batchFileFilter = EditorGUILayout.TextField("Search Pattern", batchFileFilter);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                batchOutputFolder = EditorGUILayout.TextField("Output Base Folder", batchOutputFolder);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Output Base Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        batchOutputFolder = path.StartsWith(Application.dataPath) ? "Assets" + path.Substring(Application.dataPath.Length) : path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                batchCreateSubfolders = EditorGUILayout.Toggle(new GUIContent("Subfolder Per Character", "Creates a subfolder '<Output>/<CharacterName>/' for each character"), batchCreateSubfolders);
                batchOverwriteExisting = EditorGUILayout.Toggle(new GUIContent("Overwrite Existing", "Replaces existing clips and controllers"), batchOverwriteExisting);

                if (batchMode == BatchMode.SingleSheetPerTexture)
                {
                    EditorGUILayout.Space(4);
                    var presets = SpriteAnimationBuilder.GetStandardPresets();
                    string[] presetNames = presets.Select(p => p.presetName).ToArray();
                    batchSelectedPresetIndex = EditorGUILayout.Popup("Batch Preset Schema", batchSelectedPresetIndex, presetNames);

                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("🔍 Scan Folder For Textures", GUILayout.Height(24)))
                    {
                        ScanBatchFolder();
                    }
                }
                else
                {
                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("🔍 Scan For Character Subfolders", GUILayout.Height(24)))
                    {
                        ScanBatchSubfolders();
                    }
                }
            }

            EditorGUILayout.Space(6);

            // Detected items list
            if (batchMode == BatchMode.SingleSheetPerTexture)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Detected Textures ({_batchFoundTexturePaths.Count})", EditorStyles.boldLabel);

                    if (_batchFoundTexturePaths.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Click 'Scan Folder For Textures' to locate sprite sheets in the source folder.", MessageType.Info);
                    }
                    else
                    {
                        for (int i = 0; i < _batchFoundTexturePaths.Count; i++)
                        {
                            string path = _batchFoundTexturePaths[i];
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"#{i + 1} {Path.GetFileName(path)}", EditorStyles.miniLabel);
                            EditorGUILayout.LabelField(path, EditorStyles.centeredGreyMiniLabel);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
            else
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Detected Character Subfolders ({_batchFoundSubfolderPaths.Count})", EditorStyles.boldLabel);

                    if (_batchFoundSubfolderPaths.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Click 'Scan For Character Subfolders' to locate character folders in the source directory.", MessageType.Info);
                    }
                    else
                    {
                        for (int i = 0; i < _batchFoundSubfolderPaths.Count; i++)
                        {
                            string path = _batchFoundSubfolderPaths[i];
                            int pngCount = Directory.Exists(path) ? Directory.GetFiles(path, "*.png", SearchOption.TopDirectoryOnly).Length : 0;
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"#{i + 1} {Path.GetFileName(path)} ({pngCount} sheets)", EditorStyles.miniLabel);
                            EditorGUILayout.LabelField(path, EditorStyles.centeredGreyMiniLabel);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }

            EditorGUILayout.Space(12);

            // Batch Generate Button
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 38
            };

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);

            string btnText = batchMode == BatchMode.SingleSheetPerTexture
                ? "⚡ Execute Single-Sheet Batch Generation"
                : "⚡ Execute Multi-Sheet Batch Generation";

            if (GUILayout.Button(btnText, btnStyle))
            {
                if (batchMode == BatchMode.SingleSheetPerTexture)
                {
                    ExecuteBatchGeneration();
                }
                else
                {
                    ExecuteMultiSheetBatchGeneration();
                }
            }

            GUI.backgroundColor = prevColor;
            EditorGUILayout.Space(12);

            EditorGUILayout.EndScrollView();
        }

        private void ScanBatchFolder()
        {
            _batchFoundTexturePaths.Clear();
            if (!Directory.Exists(batchSourceFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Folder '{batchSourceFolder}' does not exist.", "OK");
                return;
            }

            string[] files = Directory.GetFiles(batchSourceFolder, batchFileFilter, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string relPath = file.Replace("\\", "/");
                if (relPath.StartsWith(Application.dataPath))
                {
                    relPath = "Assets" + relPath.Substring(Application.dataPath.Length);
                }
                _batchFoundTexturePaths.Add(relPath);
            }
        }

        private void ScanBatchSubfolders()
        {
            _batchFoundSubfolderPaths.Clear();
            if (!Directory.Exists(batchSourceFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Folder '{batchSourceFolder}' does not exist.", "OK");
                return;
            }

            string[] subdirs = Directory.GetDirectories(batchSourceFolder, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir in subdirs)
            {
                if (Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    string rel = dir.Replace("\\", "/");
                    if (rel.StartsWith(Application.dataPath))
                    {
                        rel = "Assets" + rel.Substring(Application.dataPath.Length);
                    }
                    _batchFoundSubfolderPaths.Add(rel);
                }
            }
        }

        private void ExecuteMultiSheetBatchGeneration()
        {
            if (_batchFoundSubfolderPaths.Count == 0)
            {
                ScanBatchSubfolders();
            }

            if (_batchFoundSubfolderPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Notice", "No character subfolders containing textures were found in the specified source folder.", "OK");
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var errorLogs = new List<string>();

            for (int i = 0; i < _batchFoundSubfolderPaths.Count; i++)
            {
                string folderPath = _batchFoundSubfolderPaths[i];
                string charName = Path.GetFileName(folderPath.TrimEnd('/', '\\'));

                float progress = (float)i / _batchFoundSubfolderPaths.Count;
                EditorUtility.DisplayProgressBar("Batch Processing Multi-Sheet Characters", $"Processing ({i + 1}/{_batchFoundSubfolderPaths.Count}): {charName}", progress);

                var actions = SpriteAnimationBuilder.AutoMatchMultiSheetActionsFromFolder(folderPath);
                int activeSheets = actions.Count(a => a != null && a.sourceTexture != null);
                if (activeSheets == 0)
                {
                    failCount++;
                    errorLogs.Add($"{charName}: No valid sprite sheets found in folder.");
                    continue;
                }

                string charOutputDir = batchCreateSubfolders ? $"{batchOutputFolder.TrimEnd('/')}/{charName}" : batchOutputFolder;

                var settings = new MultiSheetGenerationSettings
                {
                    baseName = charName,
                    outputFolderPath = charOutputDir,
                    sortMode = multiSortMode,
                    generateAnimatorController = true,
                    includeStandardParameters = true,
                    actions = actions
                };

                var res = SpriteAnimationBuilder.GenerateFromMultiSheetSettings(settings);
                if (res.Success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    errorLogs.Add($"{charName}: {res.Message}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = $"Multi-Sheet Batch Processing Complete!\n\n• Successfully Processed Characters: {successCount}\n• Failed: {failCount}";
            if (errorLogs.Count > 0)
            {
                report += $"\n\nErrors / Warnings:\n{string.Join("\n", errorLogs.Take(10))}";
            }

            EditorUtility.DisplayDialog("Multi-Sheet Batch Complete", report, "OK");
        }

        private void ExecuteBatchGeneration()
        {
            if (_batchFoundTexturePaths.Count == 0)
            {
                ScanBatchFolder();
            }

            if (_batchFoundTexturePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Notice", "No textures found to process in the specified source folder.", "OK");
                return;
            }

            var presets = SpriteAnimationBuilder.GetStandardPresets();
            if (batchSelectedPresetIndex < 0 || batchSelectedPresetIndex >= presets.Count)
            {
                EditorUtility.DisplayDialog("Error", "Invalid preset selected.", "OK");
                return;
            }

            var selectedPreset = presets[batchSelectedPresetIndex];
            int successCount = 0;
            int failCount = 0;
            var errorLogs = new List<string>();

            for (int i = 0; i < _batchFoundTexturePaths.Count; i++)
            {
                string texPath = _batchFoundTexturePaths[i];
                float progress = (float)i / _batchFoundTexturePaths.Count;
                EditorUtility.DisplayProgressBar("Batch Processing Sprite Sheets", $"Processing ({i + 1}/{_batchFoundTexturePaths.Count}): {Path.GetFileName(texPath)}", progress);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    failCount++;
                    errorLogs.Add($"Failed to load Texture2D at '{texPath}'.");
                    continue;
                }

                if (!SpriteAnimationBuilder.ValidateTextureImporter(tex, out _))
                {
                    SpriteAnimationBuilder.FixTextureImporterSettings(tex);
                }

                string charName = Path.GetFileNameWithoutExtension(texPath);
                string charOutputDir = batchCreateSubfolders ? $"{batchOutputFolder.TrimEnd('/')}/{charName}" : batchOutputFolder;

                var settings = new AnimationGenerationSettings
                {
                    sourceTexture = tex,
                    sortMode = SpriteSortMode.NaturalNumericName,
                    outputFolderPath = charOutputDir,
                    baseName = charName,
                    actions = selectedPreset.actions.Select(a => a.Clone()).ToList(),
                    generateAnimatorController = true,
                    includeStandardParameters = true
                };

                var res = SpriteAnimationBuilder.GenerateFromSettings(settings);
                if (res.Success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    errorLogs.Add($"{charName}: {res.Message}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = $"Batch Processing Complete!\n\n• Successfully Processed: {successCount}\n• Failed: {failCount}";
            if (errorLogs.Count > 0)
            {
                report += $"\n\nErrors / Warnings:\n{string.Join("\n", errorLogs.Take(10))}";
            }

            EditorUtility.DisplayDialog("Batch Complete", report, "OK");
        }

        #endregion
    }
}
#endif
