#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SBGames.Editor.Animation
{
    /// <summary>
    /// Sorting modes for extracted sprites from a sliced texture.
    /// </summary>
    public enum SpriteSortMode
    {
        /// <summary>
        /// Natural numeric sorting by sprite name (e.g. 'Hero_0', 'Hero_1', ..., 'Hero_10').
        /// </summary>
        NaturalNumericName,

        /// <summary>
        /// Reads sprite sheet row-by-row: Top-to-Bottom, Left-to-Right.
        /// </summary>
        GridTopLeftToBottomRight,

        /// <summary>
        /// Reads sprite sheet column-by-column: Left-to-Right, Top-to-Bottom.
        /// </summary>
        GridColumnByColumn,

        /// <summary>
        /// Raw order returned by AssetDatabase.LoadAllAssetsAtPath.
        /// </summary>
        AssetDatabaseOrder
    }

    /// <summary>
    /// Configuration schema for a single character animation action.
    /// </summary>
    [Serializable]
    public class AnimationActionConfig
    {
        [SerializeField] public string actionName = "Idle";
        [SerializeField] public int startFrame = 0;
        [SerializeField] public int frameCount = 4;
        [SerializeField] public float frameRate = 10f;
        [SerializeField] public bool isLooping = true;

        [Header("Animation Events")]
        [SerializeField] public bool addEvents = false;
        [SerializeField] public int impactFrameOffset = 2;
        [SerializeField] public string impactEventName = "AnimationEvent_Attack";
        [SerializeField] public bool addCompleteEvent = true;
        [SerializeField] public string completeEventName = "AnimationEvent_AttackComplete";

        public AnimationActionConfig() { }

        public AnimationActionConfig(string name, int start, int count, float fps, bool loop, bool events = false, int impactFrame = 0, string impactName = "", bool complete = false, string completeName = "")
        {
            actionName = name;
            startFrame = start;
            frameCount = count;
            frameRate = fps;
            isLooping = loop;
            addEvents = events;
            impactFrameOffset = impactFrame;
            impactEventName = impactName;
            addCompleteEvent = complete;
            completeEventName = completeName;
        }

        public AnimationActionConfig Clone()
        {
            return new AnimationActionConfig
            {
                actionName = this.actionName,
                startFrame = this.startFrame,
                frameCount = this.frameCount,
                frameRate = this.frameRate,
                isLooping = this.isLooping,
                addEvents = this.addEvents,
                impactFrameOffset = this.impactFrameOffset,
                impactEventName = this.impactEventName,
                addCompleteEvent = this.addCompleteEvent,
                completeEventName = this.completeEventName
            };
        }

        public bool IsValid(int totalAvailableSprites, out string error)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                error = "Action name cannot be empty.";
                return false;
            }

            if (startFrame < 0)
            {
                error = $"Start frame ({startFrame}) cannot be negative.";
                return false;
            }

            if (frameCount <= 0)
            {
                error = $"Frame count ({frameCount}) must be greater than zero.";
                return false;
            }

            if (totalAvailableSprites > 0 && startFrame + frameCount > totalAvailableSprites)
            {
                error = $"Frame range [{startFrame}..{startFrame + frameCount - 1}] exceeds total sliced sprites ({totalAvailableSprites}).";
                return false;
            }

            if (frameRate <= 0)
            {
                error = $"Frame rate ({frameRate}) must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Slicing preset template.
    /// </summary>
    [Serializable]
    public class ActionPresetTemplate
    {
        public string presetName;
        public string description;
        public List<AnimationActionConfig> actions;

        public ActionPresetTemplate(string name, string desc, List<AnimationActionConfig> actionList)
        {
            presetName = name;
            description = desc;
            actions = actionList;
        }
    }

    /// <summary>
    /// Encapsulated settings for animation and controller generation.
    /// </summary>
    [Serializable]
    public class AnimationGenerationSettings
    {
        public Texture2D sourceTexture;
        public SpriteSortMode sortMode = SpriteSortMode.NaturalNumericName;
        public string outputFolderPath = "Assets/Animators/Characters/Monsters/Normal";
        public string baseName = "MONSTER_N001";
        public List<AnimationActionConfig> actions = new();
        public bool generateAnimatorController = true;
        public bool includeStandardParameters = true;
        public bool setIdleAsDefaultState = true;
    }

    /// <summary>
    /// Configuration schema for an action mapped to its own individual sprite sheet texture.
    /// </summary>
    [Serializable]
    public class MultiSheetActionConfig
    {
        [SerializeField] public string actionName = "Idle";
        [SerializeField] public Texture2D sourceTexture;
        [SerializeField] public bool useAllFrames = true;
        [SerializeField] public int startFrame = 0;
        [SerializeField] public int frameCount = 4;
        [SerializeField] public float frameRate = 10f;
        [SerializeField] public bool isLooping = true;

        [Header("Animation Events")]
        [SerializeField] public bool addEvents = false;
        [SerializeField] public int impactFrameOffset = 2;
        [SerializeField] public string impactEventName = "AnimationEvent_Attack";
        [SerializeField] public bool addCompleteEvent = true;
        [SerializeField] public string completeEventName = "AnimationEvent_AttackComplete";

        public MultiSheetActionConfig() { }

        public MultiSheetActionConfig(string name, Texture2D texture, bool loop, float fps = 10f, bool events = false, int impactFrame = 0, string impactName = "", bool complete = false, string completeName = "")
        {
            actionName = name;
            sourceTexture = texture;
            useAllFrames = true;
            startFrame = 0;
            frameCount = 4;
            frameRate = fps;
            isLooping = loop;
            addEvents = events;
            impactFrameOffset = impactFrame;
            impactEventName = impactName;
            addCompleteEvent = complete;
            completeEventName = completeName;
        }

        public MultiSheetActionConfig Clone()
        {
            return new MultiSheetActionConfig
            {
                actionName = this.actionName,
                sourceTexture = this.sourceTexture,
                useAllFrames = this.useAllFrames,
                startFrame = this.startFrame,
                frameCount = this.frameCount,
                frameRate = this.frameRate,
                isLooping = this.isLooping,
                addEvents = this.addEvents,
                impactFrameOffset = this.impactFrameOffset,
                impactEventName = this.impactEventName,
                addCompleteEvent = this.addCompleteEvent,
                completeEventName = this.completeEventName
            };
        }

        public bool IsValid(int totalAvailableSprites, out string error)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                error = "Action name cannot be empty.";
                return false;
            }

            if (sourceTexture == null)
            {
                error = "No texture assigned.";
                return false;
            }

            if (!useAllFrames)
            {
                if (startFrame < 0)
                {
                    error = $"Start frame ({startFrame}) cannot be negative.";
                    return false;
                }

                if (frameCount <= 0)
                {
                    error = $"Frame count ({frameCount}) must be greater than zero.";
                    return false;
                }

                if (totalAvailableSprites > 0 && startFrame + frameCount > totalAvailableSprites)
                {
                    error = $"Frame range [{startFrame}..{startFrame + frameCount - 1}] exceeds total sliced sprites ({totalAvailableSprites}).";
                    return false;
                }
            }

            if (frameRate <= 0)
            {
                error = $"Frame rate ({frameRate}) must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Encapsulated settings for generating character animation clips & controller from multiple sprite sheets.
    /// </summary>
    [Serializable]
    public class MultiSheetGenerationSettings
    {
        public string baseName = "ADVENTURER_N001";
        public string outputFolderPath = "Assets/Animators/Characters/Adventurers/N001";
        public List<MultiSheetActionConfig> actions = new();
        public SpriteSortMode sortMode = SpriteSortMode.NaturalNumericName;
        public bool generateAnimatorController = true;
        public bool includeStandardParameters = true;
    }

    /// <summary>
    /// Result information returned from generation process.
    /// </summary>
    public class GenerationResult
    {
        public bool Success;
        public string Message;
        public AnimatorController CreatedController;
        public List<AnimationClip> CreatedClips = new();
        public List<string> Warnings = new();
        public List<string> Errors = new();
    }

    /// <summary>
    /// Core builder engine that creates AnimationClips, injects AnimationEvents,
    /// and synthesizes AnimatorControllers with state machine wiring.
    /// </summary>
    public static class SpriteAnimationBuilder
    {
        // Standard Animator Parameter Names across SBGames character systems
        public const string PARAM_IS_MOVING = "IsMoving";
        public const string PARAM_ATTACK = "Attack";
        public const string PARAM_CAST_SKILL = "CastSkill";
        public const string PARAM_HIT = "Hit";
        public const string PARAM_IS_DEAD = "IsDead";

        // Compatibility parameters for legacy/runtime animation controllers
        public const string PARAM_SPEED = "Speed";
        public const string PARAM_MOVE_X = "MoveX";
        public const string PARAM_MOVE_Y = "MoveY";
        public const string PARAM_GET_HIT = "GetHit";
        public const string PARAM_IS_ATTACKING = "IsAttacking";
        public const string PARAM_IS_CASTING_SKILL = "IsCastingSkill";

        // Standard Event Names
        public const string EVENT_ATTACK = "AnimationEvent_Attack";
        public const string EVENT_ATTACK_COMPLETE = "AnimationEvent_AttackComplete";
        public const string EVENT_CAST_SKILL = "AnimationEvent_CastSkill";
        public const string EVENT_CAST_SKILL_COMPLETE = "AnimationEvent_CastSkillComplete";

        #region Sprite Extraction & Validation

        /// <summary>
        /// Validates that a texture is properly imported as a Multiple Sprite asset.
        /// </summary>
        public static bool ValidateTextureImporter(Texture2D texture, out string errorMessage)
        {
            if (texture == null)
            {
                errorMessage = "Source texture is null.";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                errorMessage = "Texture is not saved as an asset in the project.";
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                errorMessage = "Could not retrieve TextureImporter for asset.";
                return false;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                errorMessage = $"Texture type is '{importer.textureType}', expected 'Sprite (2D and UI)'.";
                return false;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                errorMessage = $"Sprite Mode is '{importer.spriteImportMode}', expected 'Multiple' with sliced sprites.";
                return false;
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int spriteCount = subAssets.OfType<Sprite>().Count();
            if (spriteCount == 0)
            {
                errorMessage = "Texture has no sliced sub-sprites. Please slice the sprite sheet in the Sprite Editor.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Automatically adjusts TextureImporter settings to Sprite (2D and UI) Multiple and reimports.
        /// </summary>
        public static bool FixTextureImporterSettings(Texture2D texture)
        {
            if (texture == null) return false;
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath)) return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        /// <summary>
        /// Extracts and sorts all sliced Sprites from the given texture asset according to the specified sort mode.
        /// </summary>
        public static Sprite[] ExtractSprites(Texture2D texture, SpriteSortMode sortMode = SpriteSortMode.NaturalNumericName)
        {
            if (texture == null) return Array.Empty<Sprite>();

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath)) return Array.Empty<Sprite>();

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var sprites = subAssets.OfType<Sprite>().ToArray();

            if (sprites.Length == 0) return sprites;

            switch (sortMode)
            {
                case SpriteSortMode.NaturalNumericName:
                    return SortSpritesNaturally(sprites);

                case SpriteSortMode.GridTopLeftToBottomRight:
                    return SortSpritesByGrid(sprites, horizontalFirst: true);

                case SpriteSortMode.GridColumnByColumn:
                    return SortSpritesByGrid(sprites, horizontalFirst: false);

                case SpriteSortMode.AssetDatabaseOrder:
                default:
                    return sprites;
            }
        }

        /// <summary>
        /// Natural alphanumeric sort (e.g. Sprite_1, Sprite_2, ..., Sprite_10 instead of Sprite_1, Sprite_10, Sprite_2).
        /// </summary>
        private static Sprite[] SortSpritesNaturally(Sprite[] sprites)
        {
            return sprites.OrderBy(s => s.name, new NaturalStringComparer()).ToArray();
        }

        /// <summary>
        /// Grid coordinate sort based on sprite rect position in texture coordinates.
        /// </summary>
        private static Sprite[] SortSpritesByGrid(Sprite[] sprites, bool horizontalFirst)
        {
            if (sprites.Length <= 1) return sprites;

            float avgHeight = sprites.Average(s => s.rect.height);
            float rowTolerance = Mathf.Max(2f, avgHeight * 0.4f);

            if (horizontalFirst)
            {
                // Top-to-Bottom (Y descending), Left-to-Right (X ascending)
                return sprites.OrderByDescending(s => Mathf.Round(s.rect.y / rowTolerance))
                              .ThenBy(s => s.rect.x)
                              .ToArray();
            }
            else
            {
                // Left-to-Right (X ascending), Top-to-Bottom (Y descending)
                float avgWidth = sprites.Average(s => s.rect.width);
                float colTolerance = Mathf.Max(2f, avgWidth * 0.4f);
                return sprites.OrderBy(s => Mathf.Round(s.rect.x / colTolerance))
                              .ThenByDescending(s => s.rect.y)
                              .ToArray();
            }
        }

        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return EditorUtility.NaturalCompare(x, y);
            }
        }

        #endregion

        #region AnimationClip Generation

        /// <summary>
        /// Creates and serializes an AnimationClip asset for SpriteRenderer.m_Sprite with loop settings and animation events.
        /// </summary>
        public static AnimationClip CreateAnimationClip(
            string clipPath,
            Sprite[] frames,
            float frameRate,
            bool isLooping,
            List<AnimationEvent> events = null)
        {
            if (frames == null || frames.Length == 0)
            {
                Debug.LogError($"[SpriteAnimationBuilder] Cannot create clip at '{clipPath}' with empty frames.");
                return null;
            }

            EnsureDirectoryForFile(clipPath);

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool isNew = clip == null;
            if (isNew)
            {
                clip = new AnimationClip();
            }
            else
            {
                // Clear existing curves and events if overwriting
                clip.ClearCurves();
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            }

            clip.frameRate = frameRate > 0 ? frameRate : 12f;

            // Target SpriteRenderer.m_Sprite property on the root GameObject
            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty, // Target the GameObject itself
                propertyName = "m_Sprite"
            };

            float frameDuration = 1f / clip.frameRate;
            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * frameDuration,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

            // Configure loop settings
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = isLooping;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Configure Animation Events
            if (events != null && events.Count > 0)
            {
                AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        /// <summary>
        /// Constructs the list of AnimationEvents for a given action config based on frame duration.
        /// </summary>
        public static List<AnimationEvent> BuildAnimationEventsForAction(AnimationActionConfig config, int totalFramesInClip)
        {
            var events = new List<AnimationEvent>();
            if (!config.addEvents || totalFramesInClip <= 0) return events;

            float fps = config.frameRate > 0 ? config.frameRate : 12f;
            float frameDuration = 1f / fps;

            // 1. Impact / Cast Event
            if (!string.IsNullOrWhiteSpace(config.impactEventName))
            {
                int impactFrame = Mathf.Clamp(config.impactFrameOffset, 0, totalFramesInClip - 1);
                float impactTime = impactFrame * frameDuration;

                events.Add(new AnimationEvent
                {
                    functionName = config.impactEventName.Trim(),
                    time = impactTime,
                    messageOptions = SendMessageOptions.DontRequireReceiver
                });
            }

            // 2. Complete Event on Final Frame
            if (config.addCompleteEvent && !string.IsNullOrWhiteSpace(config.completeEventName))
            {
                // Place complete event at the final frame onset or total clip length
                int lastFrameIndex = Mathf.Max(0, totalFramesInClip - 1);
                float completeTime = lastFrameIndex * frameDuration;

                events.Add(new AnimationEvent
                {
                    functionName = config.completeEventName.Trim(),
                    time = completeTime,
                    messageOptions = SendMessageOptions.DontRequireReceiver
                });
            }

            return events;
        }

        #endregion

        #region AnimatorController Synthesis

        /// <summary>
        /// Synthesizes an AnimatorController asset with standard SBGames parameters,
        /// state machine states corresponding to action clips, and standard transition graphs.
        /// </summary>
        public static AnimatorController CreateAnimatorController(
            string controllerPath,
            Dictionary<string, AnimationClip> actionClips,
            bool includeCompatibilityParams = true)
        {
            EnsureDirectoryForFile(controllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                Debug.LogError($"[SpriteAnimationBuilder] Failed to create AnimatorController at '{controllerPath}'.");
                return null;
            }

            // 1. Add Standard & Compatibility Parameters
            AddControllerParameter(controller, PARAM_IS_MOVING, AnimatorControllerParameterType.Bool, false);
            AddControllerParameter(controller, PARAM_ATTACK, AnimatorControllerParameterType.Trigger);
            AddControllerParameter(controller, PARAM_CAST_SKILL, AnimatorControllerParameterType.Trigger);
            AddControllerParameter(controller, PARAM_HIT, AnimatorControllerParameterType.Trigger);
            AddControllerParameter(controller, PARAM_IS_DEAD, AnimatorControllerParameterType.Bool, false);

            if (includeCompatibilityParams)
            {
                AddControllerParameter(controller, PARAM_SPEED, AnimatorControllerParameterType.Float, 0f);
                AddControllerParameter(controller, PARAM_MOVE_X, AnimatorControllerParameterType.Float, 0f);
                AddControllerParameter(controller, PARAM_MOVE_Y, AnimatorControllerParameterType.Float, -1f);
                AddControllerParameter(controller, PARAM_GET_HIT, AnimatorControllerParameterType.Trigger);
                AddControllerParameter(controller, PARAM_IS_ATTACKING, AnimatorControllerParameterType.Bool, false);
                AddControllerParameter(controller, PARAM_IS_CASTING_SKILL, AnimatorControllerParameterType.Bool, false);
            }

            // 2. Build State Machine
            var rootStateMachine = controller.layers[0].stateMachine;
            rootStateMachine.anyStatePosition = new Vector3(50, -60, 0);
            rootStateMachine.entryPosition = new Vector3(50, 0, 0);
            rootStateMachine.exitPosition = new Vector3(800, 0, 0);

            // Dictionary to store created AnimatorStates
            var states = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);

            int customStateYOffset = 0;

            // Create states for all provided clips
            foreach (var kvp in actionClips)
            {
                string actionKey = kvp.Key;
                AnimationClip clip = kvp.Value;
                string stateName = actionKey;

                // Position cleanly on grid
                Vector3 statePos;
                if (string.Equals(actionKey, "Idle", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(250, 0, 0);
                }
                else if (string.Equals(actionKey, "Walk", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Move", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Run", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(250, 140, 0);
                }
                else if (string.Equals(actionKey, "Attack", StringComparison.OrdinalIgnoreCase) || actionKey.StartsWith("Attack", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(550, -60, 0);
                }
                else if (string.Equals(actionKey, "CastSkill", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Cast", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(550, 60, 0);
                }
                else if (string.Equals(actionKey, "Hit", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "GetHit", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Hurt", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(550, 180, 0);
                }
                else if (string.Equals(actionKey, "Die", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Death", StringComparison.OrdinalIgnoreCase) || string.Equals(actionKey, "Dead", StringComparison.OrdinalIgnoreCase))
                {
                    statePos = new Vector3(550, 300, 0);
                }
                else
                {
                    statePos = new Vector3(250, 260 + customStateYOffset, 0);
                    customStateYOffset += 80;
                }

                var state = rootStateMachine.AddState(stateName, statePos);
                state.motion = clip;
                states[actionKey] = state;
            }

            // Set Idle as Default State if present
            AnimatorState idleState = null;
            if (states.TryGetValue("Idle", out idleState) || states.TryGetValue("idle", out idleState))
            {
                rootStateMachine.defaultState = idleState;
            }
            else if (states.Count > 0)
            {
                idleState = states.Values.First();
                rootStateMachine.defaultState = idleState;
            }

            // 3. Setup Transitions
            AnimatorState walkState = FindState(states, "Walk", "Move", "Run");
            AnimatorState attackState = FindState(states, "Attack", "Attack01");
            AnimatorState castState = FindState(states, "CastSkill", "Cast", "Skill");
            AnimatorState hitState = FindState(states, "Hit", "GetHit", "Hurt");
            AnimatorState dieState = FindState(states, "Die", "Death", "Dead");

            // A. Idle <-> Walk Bidirectional
            if (idleState != null && walkState != null)
            {
                // Idle -> Walk
                var idleToWalk = idleState.AddTransition(walkState);
                idleToWalk.hasExitTime = false;
                idleToWalk.duration = 0f;
                idleToWalk.AddCondition(AnimatorConditionMode.If, 0, PARAM_IS_MOVING);

                // Walk -> Idle
                var walkToIdle = walkState.AddTransition(idleState);
                walkToIdle.hasExitTime = false;
                walkToIdle.duration = 0f;
                walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, PARAM_IS_MOVING);
            }

            // B. AnyState -> Attack
            if (attackState != null)
            {
                var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
                anyToAttack.hasExitTime = false;
                anyToAttack.duration = 0f;
                anyToAttack.canTransitionToSelf = false;
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0, PARAM_ATTACK);

                // Attack -> Return to Idle (Has Exit Time)
                if (idleState != null)
                {
                    var attackToIdle = attackState.AddTransition(idleState);
                    attackToIdle.hasExitTime = true;
                    attackToIdle.exitTime = 1.0f;
                    attackToIdle.duration = 0f;
                }
            }

            // C. AnyState -> CastSkill
            if (castState != null)
            {
                var anyToCast = rootStateMachine.AddAnyStateTransition(castState);
                anyToCast.hasExitTime = false;
                anyToCast.duration = 0f;
                anyToCast.canTransitionToSelf = false;
                anyToCast.AddCondition(AnimatorConditionMode.If, 0, PARAM_CAST_SKILL);

                // Cast -> Return to Idle (Has Exit Time)
                if (idleState != null)
                {
                    var castToIdle = castState.AddTransition(idleState);
                    castToIdle.hasExitTime = true;
                    castToIdle.exitTime = 1.0f;
                    castToIdle.duration = 0f;
                }
            }

            // D. AnyState -> Hit
            if (hitState != null)
            {
                var anyToHit = rootStateMachine.AddAnyStateTransition(hitState);
                anyToHit.hasExitTime = false;
                anyToHit.duration = 0f;
                anyToHit.canTransitionToSelf = false;
                anyToHit.AddCondition(AnimatorConditionMode.If, 0, PARAM_HIT);

                // Hit -> Return to Idle (Has Exit Time)
                if (idleState != null)
                {
                    var hitToIdle = hitState.AddTransition(idleState);
                    hitToIdle.hasExitTime = true;
                    hitToIdle.exitTime = 1.0f;
                    hitToIdle.duration = 0f;
                }
            }

            // E. AnyState -> Die
            if (dieState != null)
            {
                var anyToDie = rootStateMachine.AddAnyStateTransition(dieState);
                anyToDie.hasExitTime = false;
                anyToDie.duration = 0f;
                anyToDie.canTransitionToSelf = false;
                anyToDie.AddCondition(AnimatorConditionMode.If, 0, PARAM_IS_DEAD);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindState(Dictionary<string, AnimatorState> states, params string[] candidateNames)
        {
            foreach (var name in candidateNames)
            {
                if (states.TryGetValue(name, out var state)) return state;
            }
            return null;
        }

        private static void AddControllerParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type, object defaultValue = null)
        {
            if (controller.parameters.Any(p => p.name == paramName)) return;

            controller.AddParameter(paramName, type);

            if (defaultValue != null)
            {
                var param = controller.parameters.First(p => p.name == paramName);
                if (type == AnimatorControllerParameterType.Bool && defaultValue is bool b)
                {
                    param.defaultBool = b;
                }
                else if (type == AnimatorControllerParameterType.Float && defaultValue is float f)
                {
                    param.defaultFloat = f;
                }
                else if (type == AnimatorControllerParameterType.Int && defaultValue is int i)
                {
                    param.defaultInt = i;
                }
            }
        }

        #endregion

        #region Master Pipeline Execution

        /// <summary>
        /// Executes full generation pipeline from settings: slices sprites, generates AnimationClips with events,
        /// creates AnimatorController, and refreshes the AssetDatabase.
        /// </summary>
        public static GenerationResult GenerateFromSettings(AnimationGenerationSettings settings)
        {
            var result = new GenerationResult();

            if (settings.sourceTexture == null)
            {
                result.Success = false;
                result.Message = "Source texture is missing.";
                result.Errors.Add(result.Message);
                return result;
            }

            if (!ValidateTextureImporter(settings.sourceTexture, out string validationError))
            {
                result.Success = false;
                result.Message = $"Texture validation failed: {validationError}";
                result.Errors.Add(result.Message);
                return result;
            }

            if (settings.actions == null || settings.actions.Count == 0)
            {
                result.Success = false;
                result.Message = "No animation actions configured to generate.";
                result.Errors.Add(result.Message);
                return result;
            }

            // Extract sliced sprites
            Sprite[] allSprites = ExtractSprites(settings.sourceTexture, settings.sortMode);
            if (allSprites.Length == 0)
            {
                result.Success = false;
                result.Message = "No sprites extracted from the sprite sheet.";
                result.Errors.Add(result.Message);
                return result;
            }

            // Normalize destination folder
            string cleanOutputDir = settings.outputFolderPath.Replace("\\", "/").TrimEnd('/');
            EnsureDirectory(cleanOutputDir);

            string characterPrefix = string.IsNullOrWhiteSpace(settings.baseName) ? settings.sourceTexture.name : settings.baseName.Trim();

            var actionClipsMap = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. Generate Animation Clips
                for (int i = 0; i < settings.actions.Count; i++)
                {
                    var action = settings.actions[i];
                    if (!action.IsValid(allSprites.Length, out string actionError))
                    {
                        result.Warnings.Add($"Action '{action.actionName}' skipped: {actionError}");
                        continue;
                    }

                    int count = Mathf.Min(action.frameCount, allSprites.Length - action.startFrame);
                    Sprite[] actionFrames = new Sprite[count];
                    Array.Copy(allSprites, action.startFrame, actionFrames, 0, count);

                    // Build events
                    var events = BuildAnimationEventsForAction(action, count);

                    // File path: e.g. Assets/Animators/.../MONSTER_N001_Attack.anim or Attack.anim
                    string clipFileName = $"{characterPrefix}_{action.actionName}.anim";
                    string clipPath = $"{cleanOutputDir}/{clipFileName}";

                    var clip = CreateAnimationClip(clipPath, actionFrames, action.frameRate, action.isLooping, events);
                    if (clip != null)
                    {
                        result.CreatedClips.Add(clip);
                        actionClipsMap[action.actionName] = clip;
                    }
                }

                if (result.CreatedClips.Count == 0)
                {
                    result.Success = false;
                    result.Message = "Failed to generate any animation clips. Check action frame ranges.";
                    return result;
                }

                // 2. Generate Animator Controller
                if (settings.generateAnimatorController)
                {
                    string controllerFileName = $"{characterPrefix}.controller";
                    string controllerPath = $"{cleanOutputDir}/{controllerFileName}";

                    result.CreatedController = CreateAnimatorController(controllerPath, actionClipsMap, settings.includeStandardParameters);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.Success = true;
                result.Message = $"Generated {result.CreatedClips.Count} AnimationClip(s)" +
                                 (result.CreatedController != null ? $" and AnimatorController '{result.CreatedController.name}'" : "") +
                                 $" in '{cleanOutputDir}'.";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Exception during generation: {ex.Message}";
                result.Errors.Add(ex.ToString());
                return result;
            }
        }

        /// <summary>
        /// Constructs standard multi-sheet action slots (Idle, Move, Attack, CastSkill, Hit, Die)
        /// with standard loop and event settings pre-configured.
        /// </summary>
        public static List<MultiSheetActionConfig> GetDefaultMultiSheetActions()
        {
            return new List<MultiSheetActionConfig>
            {
                new("Idle", null, true, 10f),
                new("Move", null, true, 12f),
                new("Attack", null, false, 12f, events: true, impactFrame: 2, impactName: EVENT_ATTACK, complete: true, completeName: EVENT_ATTACK_COMPLETE),
                new("CastSkill", null, false, 12f, events: true, impactFrame: 2, impactName: EVENT_CAST_SKILL, complete: true, completeName: EVENT_CAST_SKILL_COMPLETE),
                new("Hit", null, false, 10f),
                new("Die", null, false, 10f)
            };
        }

        /// <summary>
        /// Given a collection of textures, matches each texture to an appropriate action by keyword.
        /// Unmatched textures are appended as custom actions.
        /// </summary>
        public static List<MultiSheetActionConfig> AutoMatchMultiSheetActions(IEnumerable<Texture2D> textures, List<MultiSheetActionConfig> existingActions = null)
        {
            var result = (existingActions != null && existingActions.Count > 0)
                ? existingActions.Select(a => a.Clone()).ToList()
                : GetDefaultMultiSheetActions();

            if (textures == null) return result;

            var textureList = textures.Where(t => t != null).Distinct().ToList();
            var usedTextures = new HashSet<Texture2D>();

            // Find matching slot for each standard action
            foreach (var act in result)
            {
                string name = act.actionName.ToLowerInvariant();
                Texture2D match = null;

                if (name == "idle")
                {
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) && t.name.ToLowerInvariant().Contains("idle"));
                }
                else if (name == "move" || name == "walk" || name == "run")
                {
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                        (t.name.ToLowerInvariant().Contains("move") || t.name.ToLowerInvariant().Contains("walk") || t.name.ToLowerInvariant().Contains("run")));
                }
                else if (name == "castskill" || name == "skill" || name == "cast")
                {
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                        (t.name.ToLowerInvariant().Contains("skill") || t.name.ToLowerInvariant().Contains("magic") || t.name.ToLowerInvariant().Contains("charge") || t.name.ToLowerInvariant().Contains("cast")));
                }
                else if (name.StartsWith("attack") || name == "atk")
                {
                    // Prefer normal attack over skill/magic
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                        (t.name.ToLowerInvariant().Contains("attack_normal") || t.name.ToLowerInvariant().Contains("normal_attack")));
                    if (match == null)
                    {
                        match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                            (t.name.ToLowerInvariant().Contains("attack") || t.name.ToLowerInvariant().Contains("atk")) &&
                            !t.name.ToLowerInvariant().Contains("skill") && !t.name.ToLowerInvariant().Contains("charge"));
                    }
                }
                else if (name == "hit" || name == "gethit" || name == "hurt" || name == "damage")
                {
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                        (t.name.ToLowerInvariant().Contains("damage") || t.name.ToLowerInvariant().Contains("hurt") || t.name.ToLowerInvariant().Contains("hit")));
                }
                else if (name == "die" || name == "death" || name == "dead")
                {
                    match = textureList.FirstOrDefault(t => !usedTextures.Contains(t) &&
                        (t.name.ToLowerInvariant().Contains("die") || t.name.ToLowerInvariant().Contains("death") || t.name.ToLowerInvariant().Contains("dead")));
                }

                if (match != null)
                {
                    act.sourceTexture = match;
                    usedTextures.Add(match);
                }
            }

            // Append any remaining unused textures as custom actions
            foreach (var unused in textureList)
            {
                if (!usedTextures.Contains(unused))
                {
                    string actionName = unused.name;
                    int lastUnder = actionName.LastIndexOf('_');
                    if (lastUnder > 0 && lastUnder < actionName.Length - 1)
                    {
                        actionName = actionName.Substring(lastUnder + 1);
                    }
                    if (string.IsNullOrWhiteSpace(actionName)) actionName = unused.name;

                    result.Add(new MultiSheetActionConfig(actionName, unused, false, 10f));
                    usedTextures.Add(unused);
                }
            }

            return result;
        }

        /// <summary>
        /// Auto-matches multi-sheet actions by scanning all textures in a folder path.
        /// </summary>
        public static List<MultiSheetActionConfig> AutoMatchMultiSheetActionsFromFolder(string folderPath, List<MultiSheetActionConfig> existingActions = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return existingActions ?? GetDefaultMultiSheetActions();
            }

            string[] filePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
            var textures = new List<Texture2D>();
            foreach (var file in filePaths)
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string rel = file.Replace("\\", "/");
                if (rel.StartsWith(Application.dataPath))
                {
                    rel = "Assets" + rel.Substring(Application.dataPath.Length);
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
                if (tex != null) textures.Add(tex);
            }

            return AutoMatchMultiSheetActions(textures, existingActions);
        }

        /// <summary>
        /// Executes full generation pipeline from multi-sheet settings: slices sprites per assigned sheet,
        /// generates AnimationClips with events, creates AnimatorController, and refreshes the AssetDatabase.
        /// </summary>
        public static GenerationResult GenerateFromMultiSheetSettings(MultiSheetGenerationSettings settings)
        {
            var result = new GenerationResult();

            if (settings.actions == null || settings.actions.Count == 0)
            {
                result.Success = false;
                result.Message = "No animation actions configured to generate.";
                result.Errors.Add(result.Message);
                return result;
            }

            // Filter actions that have a texture assigned
            var activeActions = settings.actions.Where(a => a != null && a.sourceTexture != null).ToList();
            if (activeActions.Count == 0)
            {
                result.Success = false;
                result.Message = "No sprite sheet textures assigned to any action slots.";
                result.Errors.Add(result.Message);
                return result;
            }

            string cleanOutputDir = settings.outputFolderPath.Replace("\\", "/").TrimEnd('/');
            EnsureDirectory(cleanOutputDir);

            string characterPrefix = string.IsNullOrWhiteSpace(settings.baseName) ? "Character" : settings.baseName.Trim();
            var actionClipsMap = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int i = 0; i < activeActions.Count; i++)
                {
                    var action = activeActions[i];
                    var tex = action.sourceTexture;

                    // Ensure multiple sprite importer mode
                    if (!ValidateTextureImporter(tex, out _))
                    {
                        FixTextureImporterSettings(tex);
                    }

                    Sprite[] allSprites = ExtractSprites(tex, settings.sortMode);
                    if (allSprites.Length == 0)
                    {
                        result.Warnings.Add($"Texture '{tex.name}' for action '{action.actionName}' has no sliced sprites. Skipping.");
                        continue;
                    }

                    Sprite[] actionFrames;
                    if (action.useAllFrames)
                    {
                        actionFrames = allSprites;
                    }
                    else
                    {
                        int start = Mathf.Clamp(action.startFrame, 0, allSprites.Length - 1);
                        int count = Mathf.Min(action.frameCount, allSprites.Length - start);
                        actionFrames = new Sprite[count];
                        Array.Copy(allSprites, start, actionFrames, 0, count);
                    }

                    if (actionFrames.Length == 0)
                    {
                        result.Warnings.Add($"Action '{action.actionName}' produced 0 frames. Skipping.");
                        continue;
                    }

                    // Build events
                    var dummySingleConfig = new AnimationActionConfig
                    {
                        actionName = action.actionName,
                        frameRate = action.frameRate,
                        isLooping = action.isLooping,
                        addEvents = action.addEvents,
                        impactFrameOffset = action.impactFrameOffset,
                        impactEventName = action.impactEventName,
                        addCompleteEvent = action.addCompleteEvent,
                        completeEventName = action.completeEventName
                    };
                    var events = BuildAnimationEventsForAction(dummySingleConfig, actionFrames.Length);

                    string clipFileName = $"{characterPrefix}_{action.actionName}.anim";
                    string clipPath = $"{cleanOutputDir}/{clipFileName}";

                    var clip = CreateAnimationClip(clipPath, actionFrames, action.frameRate, action.isLooping, events);
                    if (clip != null)
                    {
                        result.CreatedClips.Add(clip);
                        actionClipsMap[action.actionName] = clip;
                    }
                }

                if (result.CreatedClips.Count == 0)
                {
                    result.Success = false;
                    result.Message = "Failed to generate any animation clips from the assigned sheets.";
                    return result;
                }

                // Generate Animator Controller
                if (settings.generateAnimatorController)
                {
                    string controllerFileName = $"{characterPrefix}.controller";
                    string controllerPath = $"{cleanOutputDir}/{controllerFileName}";

                    result.CreatedController = CreateAnimatorController(controllerPath, actionClipsMap, settings.includeStandardParameters);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.Success = true;
                result.Message = $"Generated {result.CreatedClips.Count} AnimationClip(s)" +
                                 (result.CreatedController != null ? $" and AnimatorController '{result.CreatedController.name}'" : "") +
                                 $" in '{cleanOutputDir}'.";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Exception during multi-sheet generation: {ex.Message}";
                result.Errors.Add(ex.ToString());
                return result;
            }
        }

        #endregion

        #region Presets Provider

        /// <summary>
        /// Standard built-in layout presets for 2D character sprite sheets.
        /// </summary>
        public static List<ActionPresetTemplate> GetStandardPresets()
        {
            return new List<ActionPresetTemplate>
            {
                new ActionPresetTemplate(
                    "6-Action Full Set (Top-Down Battler)",
                    "Standard 6-state configuration: Idle(4), Walk(6), Attack(4 with events), CastSkill(4 with events), Hit(3), Die(4).",
                    new List<AnimationActionConfig>
                    {
                        new("Idle", 0, 4, 10f, true),
                        new("Walk", 4, 6, 12f, true),
                        new("Attack", 10, 4, 12f, false, events: true, impactFrame: 2, impactName: EVENT_ATTACK, complete: true, completeName: EVENT_ATTACK_COMPLETE),
                        new("CastSkill", 14, 4, 12f, false, events: true, impactFrame: 2, impactName: EVENT_CAST_SKILL, complete: true, completeName: EVENT_CAST_SKILL_COMPLETE),
                        new("Hit", 18, 3, 10f, false),
                        new("Die", 21, 4, 10f, false)
                    }
                ),

                new ActionPresetTemplate(
                    "4-Action Combat Basic (Idle, Walk, Attack, Die)",
                    "Common 4-state layout: Idle(4), Walk(6), Attack(4 with events), Die(4).",
                    new List<AnimationActionConfig>
                    {
                        new("Idle", 0, 4, 10f, true),
                        new("Walk", 4, 6, 12f, true),
                        new("Attack", 10, 4, 12f, false, events: true, impactFrame: 2, impactName: EVENT_ATTACK, complete: true, completeName: EVENT_ATTACK_COMPLETE),
                        new("Die", 14, 4, 10f, false)
                    }
                ),

                new ActionPresetTemplate(
                    "Minion / Simple Monster (Idle, Walk, Attack)",
                    "Lightweight 3-state monster: Idle(4), Walk(4), Attack(4 with events).",
                    new List<AnimationActionConfig>
                    {
                        new("Idle", 0, 4, 10f, true),
                        new("Walk", 4, 4, 10f, true),
                        new("Attack", 8, 4, 12f, false, events: true, impactFrame: 2, impactName: EVENT_ATTACK, complete: true, completeName: EVENT_ATTACK_COMPLETE)
                    }
                ),

                new ActionPresetTemplate(
                    "Idle & Walk Only",
                    "Passive movement only: Idle(4), Walk(6).",
                    new List<AnimationActionConfig>
                    {
                        new("Idle", 0, 4, 10f, true),
                        new("Walk", 4, 6, 12f, true)
                    }
                )
            };
        }

        /// <summary>
        /// Generates action configurations dynamically by dividing total frames equally.
        /// </summary>
        public static List<AnimationActionConfig> CreateAutoSplitPreset(int totalSprites, int actionCount = 4)
        {
            var actions = new List<AnimationActionConfig>();
            if (totalSprites <= 0 || actionCount <= 0) return actions;

            string[] defaultNames = { "Idle", "Walk", "Attack", "CastSkill", "Hit", "Die" };
            int framesPerAction = Mathf.Max(1, totalSprites / actionCount);

            for (int i = 0; i < actionCount; i++)
            {
                int start = i * framesPerAction;
                if (start >= totalSprites) break;

                int count = (i == actionCount - 1) ? (totalSprites - start) : framesPerAction;
                string name = (i < defaultNames.Length) ? defaultNames[i] : $"Action_{i + 1}";
                bool loop = i <= 1; // Idle and Walk loop by default
                bool isAttack = name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isCast = name.IndexOf("Cast", StringComparison.OrdinalIgnoreCase) >= 0;

                actions.Add(new AnimationActionConfig
                {
                    actionName = name,
                    startFrame = start,
                    frameCount = count,
                    frameRate = loop ? 10f : 12f,
                    isLooping = loop,
                    addEvents = isAttack || isCast,
                    impactFrameOffset = Mathf.Max(0, count / 2),
                    impactEventName = isAttack ? EVENT_ATTACK : (isCast ? EVENT_CAST_SKILL : ""),
                    addCompleteEvent = isAttack || isCast,
                    completeEventName = isAttack ? EVENT_ATTACK_COMPLETE : (isCast ? EVENT_CAST_SKILL_COMPLETE : "")
                });
            }

            return actions;
        }

        #endregion

        #region Helper IO Utilities

        public static void EnsureDirectory(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            string normalized = folderPath.Replace("\\", "/").TrimEnd('/');
            if (!Directory.Exists(normalized))
            {
                Directory.CreateDirectory(normalized);
            }
        }

        public static void EnsureDirectoryForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                EnsureDirectory(dir);
            }
        }

        #endregion
    }
}
#endif
