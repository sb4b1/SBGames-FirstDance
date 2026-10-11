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
            BatchFolder
        }

        // Toolbar
        [SerializeField] private WindowTab activeTab = WindowTab.SingleSheet;
        private readonly string[] tabTitles = new[] { "🖼️ Single Sprite Sheet", "📂 Batch Folder Generator" };

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
        // Batch Mode State
        // ----------------------------------------------------
        [SerializeField] private string batchSourceFolder = "Assets/Resources/Sprites/Characters";
        [SerializeField] private string batchOutputFolder = "Assets/Animators/Characters";
        [SerializeField] private string batchFileFilter = "*.png";
        [SerializeField] private int batchSelectedPresetIndex = 0;
        [SerializeField] private bool batchCreateSubfolders = true;
        [SerializeField] private bool batchOverwriteExisting = true;

        private Vector2 _batchScrollPos;
        private List<string> _batchFoundTexturePaths = new();

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

        private void OnEnable()
        {
            if (actions == null || actions.Count == 0)
            {
                ApplyStandardPreset(0); // Default to 6-action full set
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
                EditorGUILayout.BeginHorizontal();
                _showPreviewStrip = EditorGUILayout.Foldout(_showPreviewStrip, $"Frame Sequence Preview ({_cachedSprites.Length} Frames)", true, EditorStyles.foldoutHeader);
                EditorGUILayout.EndHorizontal();

                if (!_showPreviewStrip) return;

                EditorGUILayout.Space(2);
                _spritePreviewScrollPos = EditorGUILayout.BeginScrollView(_spritePreviewScrollPos, GUILayout.Height(90));
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < _cachedSprites.Length; i++)
                {
                    var spr = _cachedSprites[i];
                    if (spr == null) continue;

                    EditorGUILayout.BeginVertical(GUILayout.Width(64));
                    
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

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPresetSelectionSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Action Presets", EditorStyles.boldLabel);

                var presets = SpriteAnimationBuilder.GetStandardPresets();

                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < presets.Count; i++)
                {
                    if (GUILayout.Button(presets[i].presetName, EditorStyles.miniButton))
                    {
                        ApplyStandardPreset(i);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("⚡ Auto-Split Equal Frames (4 Actions)", EditorStyles.miniButtonLeft))
                {
                    if (_cachedSprites.Length > 0)
                    {
                        actions = SpriteAnimationBuilder.CreateAutoSplitPreset(_cachedSprites.Length, 4);
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
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Notice", "Please assign a valid sliced sprite sheet first.", "OK");
                    }
                }
                if (GUILayout.Button("🗑️ Clear All Actions", EditorStyles.miniButtonRight))
                {
                    actions.Clear();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawActionConfigurationList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("3. Actions & Animation Events Schema", EditorStyles.boldLabel);
                _showAdvancedEvents = GUILayout.Toggle(_showAdvancedEvents, "Show Event Details", EditorStyles.miniButton, GUILayout.Width(130));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                if (actions == null || actions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No actions configured. Click a preset above or '+ Add Action' below.", MessageType.Warning);
                }
                else
                {
                    int totalSprites = _cachedSprites.Length;

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

                            EditorGUILayout.BeginHorizontal();
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

                            // Control buttons
                            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(20)) && i > 0)
                            {
                                var temp = actions[i];
                                actions[i] = actions[i - 1];
                                actions[i - 1] = temp;
                                break;
                            }
                            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(20)) && i < actions.Count - 1)
                            {
                                var temp = actions[i];
                                actions[i] = actions[i + 1];
                                actions[i + 1] = temp;
                                break;
                            }
                            if (GUILayout.Button("📋", EditorStyles.miniButtonMid, GUILayout.Width(22)))
                            {
                                actions.Insert(i + 1, act.Clone());
                                break;
                            }
                            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22)))
                            {
                                actions.RemoveAt(i);
                                break;
                            }
                            EditorGUILayout.EndHorizontal();

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

        #region Batch Mode GUI

        private void DrawBatchFolderTab()
        {
            _batchScrollPos = EditorGUILayout.BeginScrollView(_batchScrollPos);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Batch Sprite Sheets Ingestion", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                batchSourceFolder = EditorGUILayout.TextField("Source Folder", batchSourceFolder);
                if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Sprite Sheets Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        batchSourceFolder = path.StartsWith(Application.dataPath) ? "Assets" + path.Substring(Application.dataPath.Length) : path;
                    }
                }
                EditorGUILayout.EndHorizontal();

                batchFileFilter = EditorGUILayout.TextField("Search Pattern", batchFileFilter);

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

                batchCreateSubfolders = EditorGUILayout.Toggle(new GUIContent("Subfolder Per Character", "Creates a subfolder '<Output>/<TextureName>/' for each sheet"), batchCreateSubfolders);
                batchOverwriteExisting = EditorGUILayout.Toggle(new GUIContent("Overwrite Existing", "Replaces existing clips and controllers"), batchOverwriteExisting);

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

            EditorGUILayout.Space(6);

            // Found Textures List
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

            if (GUILayout.Button("⚡ Execute Batch Generation", btnStyle))
            {
                ExecuteBatchGeneration();
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
