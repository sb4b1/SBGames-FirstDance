using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ARTIFICE_TOOLKIT_PRESENT || true
using ArtificeToolkit.Attributes;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Consolidated development, debugging, and inspector toolkit for SBGames.
/// Provides both scene-bound inspector/HUD controls and project-wide static utilities.
/// 
/// Sub-Domains:
///  - DevTool.Log       : Tagged, channel-based logging and conditional console output.
///  - DevTool.Gizmo     : Reusable 2D/3D gizmo visualization (spheres, discs, arrows, boxes).
///  - DevTool.Profile   : Lightweight scoped profiling, frame timing, and memory diagnostics.
///  - DevTool.Time      : In-game timescale control, slow-motion, and frame stepping.
///  - DevTool.Assert    : Development assertions that strip cleanly in release builds.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SBGames/DevTool")]
public class DevTool : MonoBehaviour
{
    #region Inspector & Configuration

    [Header("HUD Overlay Settings")]
    [Tooltip("Enable keyboard shortcut (F1 or Tilde) to toggle the runtime OnGUI overlay.")]
    [SerializeField] private bool enableOverlayHotkey = true;

    [Tooltip("Show the on-screen debug HUD upon scene start.")]
    [SerializeField] private bool showOnScreenHUD = false;

#pragma warning disable CS0414
    [Tooltip("Key used to toggle the on-screen HUD (Legacy Input).")]
    [SerializeField] private KeyCode overlayToggleKey = KeyCode.F1;

    [Tooltip("Alternative key used to toggle the on-screen HUD (Legacy Input).")]
    [SerializeField] private KeyCode overlayToggleKeyAlt = KeyCode.BackQuote;
#pragma warning restore CS0414

    [Header("HUD Window Positioning")]
    [Tooltip("Initial position and dimension of the draggable HUD window.")]
    [SerializeField] private Rect hudWindowRect = new Rect(20f, 20f, 440f, 350f);

    [Header("Monitored Characters")]
    [SerializeField] private List<CharacterList> characterLists = new();

    #endregion

    #region Internal State

    private bool cmNullChecked = false;
    private int selectedTab = 0;
    private readonly string[] tabLabels = { "Status & Memory", "Time Controls", "Characters", "Gizmo Config" };
    private Vector2 characterScrollPos;
    private float fpsAccumulator = 0f;
    private int fpsFrameCount = 0;
    private float currentFps = 60f;
    private float fpsTimer = 0f;

    // Singleton-like scene access for HUD
    public static DevTool Instance { get; private set; }

    #endregion

    #region Unity Lifecycle & Character Tracking (Preserved Functionality)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void OnEnable()
    {
        if (CharacterManager.Instance == null)
        {
            cmNullChecked = true;
            return;
        }

        CharacterManager.Instance.OnCharacterListChanged += OnCharacterListChanged;
    }

    private void OnDisable()
    {
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterListChanged -= OnCharacterListChanged;
        }
    }

    private void Start()
    {
        if (cmNullChecked)
        {
            cmNullChecked = false;
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterListChanged += OnCharacterListChanged;
            }
        }

        if (CharacterManager.Instance != null)
        {
            var dict = CharacterManager.Instance.GetAllCharactersDict();
            if (dict != null)
            {
                OnCharacterListChanged(dict); // Initialize the list with current characters
            }
        }
    }

    private void Update()
    {
        // FPS calculation
        fpsAccumulator += UnityEngine.Time.unscaledDeltaTime;
        fpsFrameCount++;
        fpsTimer += UnityEngine.Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            currentFps = fpsFrameCount / fpsAccumulator;
            Profile.UpdateFpsSample(currentFps);
            fpsAccumulator = 0f;
            fpsFrameCount = 0;
            fpsTimer = 0f;
        }

        // Toggle HUD on hotkey
        if (enableOverlayHotkey && WasToggleHotkeyPressed())
        {
            showOnScreenHUD = !showOnScreenHUD;
        }
    }

    private bool WasToggleHotkeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.f1Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.Input.GetKeyDown(overlayToggleKey) || UnityEngine.Input.GetKeyDown(overlayToggleKeyAlt))
        {
            return true;
        }
#endif
        return false;
    }

    private void OnCharacterListChanged(Dictionary<string, CharacterBrain> newCharacters)
    {
        if (newCharacters == null) return;

        // Remove characters that are no longer in the new list
        characterLists.RemoveAll(cl => cl == null || !newCharacters.ContainsKey(cl.ID));

        // Add new characters that are not already in the list
        var existingIds = new HashSet<string>(characterLists.Where(cl => cl != null).Select(cl => cl.ID));
        foreach (var kvp in newCharacters)
        {
            if (!existingIds.Contains(kvp.Key))
            {
                characterLists.Add(new CharacterList(characterLists.Count.ToString(), kvp.Key, kvp.Value));
            }
        }
    }

    [Button(true)]
    private void GetCharacterList()
    {
        if (CharacterManager.Instance == null)
        {
            Log.Warning("DevTool", this, "CharacterManager instance is null. Cannot query character list.");
            return;
        }

        var dict = CharacterManager.Instance.GetAllCharactersDict();
        if (dict == null) return;

        int index = 0;
        characterLists = dict.Select(kvp => new CharacterList(index++.ToString(), kvp.Key, kvp.Value)).ToList();
    }

    [System.Serializable]
    public class CharacterList
    {
        [SerializeField] private string number;
        [SerializeField] private string id;
        [SerializeField] private CharacterBrain character;

        public string ID => id;
        public string Number => number;
        public CharacterBrain Character => character;

        public CharacterList(string number, string id, CharacterBrain character)
        {
            this.number = number;
            this.id = id;
            this.character = character;
        }

        [Button(true)]
        public void KillCharacter()
        {
            if (character != null)
            {
                var stats = character.GetComponent<CharacterStatsManager>();
                if (stats != null)
                {
                    stats.ForceDeath();
                }
                else
                {
                    Log.Warning("DevTool", character, $"Character {id} has no CharacterStatsManager component to ForceDeath().");
                }
            }
        }
    }

    #endregion

    #region On-Screen Runtime Debug Overlay (OnGUI)

    private void OnGUI()
    {
        if (!showOnScreenHUD) return;

        hudWindowRect = GUILayout.Window(
            0xDE07001,
            hudWindowRect,
            DrawHUDWindow,
            $"<b>SBGames DevTool</b>  [Drag to Move]  |  FPS: {currentFps:0.0}",
            GUILayout.Width(440f),
            GUILayout.Height(350f)
        );

        // Keep window within screen boundaries
        hudWindowRect.x = Mathf.Clamp(hudWindowRect.x, 0f, Mathf.Max(0f, Screen.width - hudWindowRect.width));
        hudWindowRect.y = Mathf.Clamp(hudWindowRect.y, 0f, Mathf.Max(0f, Screen.height - hudWindowRect.height));
    }

    private void DrawHUDWindow(int windowId)
    {
        // Navigation bar and quick close button
        GUILayout.BeginHorizontal();
        selectedTab = GUILayout.Toolbar(selectedTab, tabLabels, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("✕", GUILayout.Width(28f)))
        {
            showOnScreenHUD = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);

        switch (selectedTab)
        {
            case 0:
                DrawStatusTab();
                break;
            case 1:
                DrawTimeTab();
                break;
            case 2:
                DrawCharactersTab();
                break;
            case 3:
                DrawGizmoConfigTab();
                break;
        }

        // Enable window dragging by clicking and holding the top bar
        GUI.DragWindow(new Rect(0, 0, 10000, 30));
    }

    private void DrawStatusTab()
    {
        GUILayout.Label($"<b>Engine TimeScale:</b> {UnityEngine.Time.timeScale:0.00}");
        GUILayout.Label($"<b>Delta Time:</b> {UnityEngine.Time.deltaTime * 1000f:0.0} ms");
        GUILayout.Label($"<b>Allocated Memory:</b> {Profile.GetAllocatedMemoryMB():0.0} MB");
        GUILayout.Label($"<b>Reserved Memory:</b> {Profile.GetReservedMemoryMB():0.0} MB");
        GUILayout.Label($"<b>Mono Heap Used:</b> {Profile.GetMonoUsedMemoryMB():0.0} MB");

        GUILayout.Space(8f);
        if (GUILayout.Button("Trigger Garbage Collection"))
        {
            GC.Collect();
            Log.Info("DevTool", "Runtime", "Forced GC.Collect() invoked from HUD.");
        }
    }

    private void DrawTimeTab()
    {
        GUILayout.Label($"Current Time Scale: <b>{UnityEngine.Time.timeScale:0.00}x</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pause (0x)")) Time.Pause();
        if (GUILayout.Button("0.25x")) Time.SetTimeScale(0.25f);
        if (GUILayout.Button("0.5x")) Time.SetTimeScale(0.5f);
        if (GUILayout.Button("1.0x")) Time.Resume();
        if (GUILayout.Button("2.0x")) Time.SetTimeScale(2.0f);
        if (GUILayout.Button("4.0x")) Time.SetTimeScale(4.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        if (GUILayout.Button("Step 1 Frame (at 60fps delta)"))
        {
            Time.StepOneFrame();
        }
    }

    private void DrawCharactersTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Active Characters: <b>{characterLists.Count}</b>");
        if (GUILayout.Button("Refresh List", GUILayout.Width(100f)))
        {
            GetCharacterList();
        }
        GUILayout.EndHorizontal();

        characterScrollPos = GUILayout.BeginScrollView(characterScrollPos, GUILayout.Height(180f));
        for (int i = 0; i < characterLists.Count; i++)
        {
            var cl = characterLists[i];
            if (cl == null) continue;

            GUILayout.BeginHorizontal("box");
            string charName = cl.Character != null ? cl.Character.name : "null";
            GUILayout.Label($"[{cl.Number}] {cl.ID} ({charName})", GUILayout.Width(240f));
            if (GUILayout.Button("Kill", GUILayout.Width(60f)))
            {
                cl.KillCharacter();
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawGizmoConfigTab()
    {
        Gizmo.GlobalGizmosEnabled = GUILayout.Toggle(Gizmo.GlobalGizmosEnabled, "Enable DevTool Gizmos");
        Gizmo.DrawWireframeDefault = GUILayout.Toggle(Gizmo.DrawWireframeDefault, "Prefer Wireframes");
        GUILayout.Label($"Default Gizmo Color: {Gizmo.DefaultColor}");
    }

    #endregion

    #region Static Domain: DevTool.Log (Console & Logging Utilities)

    /// <summary>
    /// Unified logging system with channel tagging, conditional editor/dev execution, and zero-allocation string builders.
    /// </summary>
    public static class Log
    {
        public static bool EnableLogs = true;
        public static bool EnableDevLogs = true;

        private static readonly StringBuilder sb = new(256);

        public static void Info(object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.Log(Format(message));
        }

        public static void Info(object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.Log(Format(sender, message));
        }

        public static void Info(object tag, object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.Log(Format(tag, sender, message));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void Dev(object message)
        {
            if (!EnableDevLogs) return;
            UnityEngine.Debug.Log(Format("DEV", "Editor", message));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void Dev(object sender, object message)
        {
            if (!EnableDevLogs) return;
            UnityEngine.Debug.Log(Format("DEV", sender, message));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void Dev(object tag, object sender, object message)
        {
            if (!EnableDevLogs) return;
            UnityEngine.Debug.Log(Format(tag, sender, message));
        }

        public static void Warning(object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.LogWarning(Format(message));
        }

        public static void Warning(object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.LogWarning(Format(sender, message));
        }

        public static void Warning(object tag, object sender, object message)
        {
            if (!EnableLogs) return;
            UnityEngine.Debug.LogWarning(Format(tag, sender, message));
        }

        public static void Error(object message)
        {
            UnityEngine.Debug.LogError(Format(message));
        }

        public static void Error(object sender, object message)
        {
            UnityEngine.Debug.LogError(Format(sender, message));
        }

        public static void Error(object tag, object sender, object message)
        {
            UnityEngine.Debug.LogError(Format(tag, sender, message));
        }

        public static void Exception(Exception ex, object sender = null)
        {
            if (sender != null)
            {
                UnityEngine.Debug.LogError($"[DevTool/Exception] [{sender}] {ex.Message}\n{ex.StackTrace}");
            }
            else
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static string Format(object message)
        {
            return $"[SBGames] {message}";
        }

        public static string Format(object sender, object message)
        {
            return $"[{sender}] {message}";
        }

        public static string Format(object tag, object sender, object message)
        {
            lock (sb)
            {
                sb.Clear();
                sb.Append('[').Append(tag).Append('/').Append(sender).Append("] ");
                sb.Append(message);
                return sb.ToString();
            }
        }
    }

    #endregion

    #region Static Domain: DevTool.Gizmo (Debug Visualization)

    /// <summary>
    /// Reusable Gizmo visualization helpers for scene view and debug drawing.
    /// </summary>
    public static class Gizmo
    {
        public static bool GlobalGizmosEnabled = true;
        public static bool DrawWireframeDefault = true;
        public static Color DefaultColor = Color.green;

        public static void DrawWireSphere(Vector3 center, float radius, Color? color = null)
        {
            if (!GlobalGizmosEnabled || radius <= 0f) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = prev;
        }

        public static void DrawSphere(Vector3 center, float radius, Color? color = null)
        {
            if (!GlobalGizmosEnabled || radius <= 0f) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = prev;
        }

        public static void DrawWireBox(Vector3 center, Vector3 size, Color? color = null)
        {
            if (!GlobalGizmosEnabled) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = prev;
        }

        public static void DrawBox(Vector3 center, Vector3 size, Color? color = null)
        {
            if (!GlobalGizmosEnabled) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;
            Gizmos.DrawCube(center, size);
            Gizmos.color = prev;
        }

        public static void DrawWireCircle(Vector3 center, float radius, Color? color = null, Vector3? normal = null, int segments = 32)
        {
            if (!GlobalGizmosEnabled || radius <= 0f) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;

            Vector3 norm = (normal ?? Vector3.up).normalized;
            Vector3 tangent = (Mathf.Abs(norm.y) > 0.99f ? Vector3.right : Vector3.Cross(norm, Vector3.up)).normalized;
            Vector3 bitangent = Vector3.Cross(norm, tangent).normalized;

            float angleStep = 360f / segments;
            Vector3 prevPoint = center + tangent * radius;

            for (int i = 1; i <= segments; i++)
            {
                float rad = i * angleStep * Mathf.Deg2Rad;
                Vector3 currentPoint = center + (tangent * Mathf.Cos(rad) + bitangent * Mathf.Sin(rad)) * radius;
                Gizmos.DrawLine(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }

            Gizmos.color = prev;
        }

        public static void DrawRay(Vector3 from, Vector3 direction, float length, Color? color = null)
        {
            if (!GlobalGizmosEnabled) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;
            Gizmos.DrawRay(from, direction.normalized * length);
            Gizmos.color = prev;
        }

        public static void DrawArrow(Vector3 from, Vector3 to, Color? color = null, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            if (!GlobalGizmosEnabled) return;
            Color prev = Gizmos.color;
            Gizmos.color = color ?? DefaultColor;

            Gizmos.DrawLine(from, to);

            Vector3 direction = to - from;
            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
                Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
                Gizmos.DrawRay(to, right * arrowHeadLength);
                Gizmos.DrawRay(to, left * arrowHeadLength);
            }

            Gizmos.color = prev;
        }

        public static void DrawBounds(Bounds bounds, Color? color = null)
        {
            DrawWireBox(bounds.center, bounds.size, color);
        }

#if UNITY_EDITOR
        public static void DrawLabel(Vector3 position, string text, Color? color = null)
        {
            if (!GlobalGizmosEnabled || string.IsNullOrEmpty(text)) return;
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = color ?? DefaultColor }
            };
            Handles.Label(position, text, style);
        }
#endif
    }

    #endregion

    #region Static Domain: DevTool.Profile (Profiling & Performance Diagnostics)

    /// <summary>
    /// Scoped timing, GC allocation checks, and frame rate monitoring.
    /// Usage:
    ///   using (DevTool.Profile.Scope("PathfindingCalculation")) { ... }
    /// </summary>
    public static class Profile
    {
        private static float cachedFps = 60f;

        internal static void UpdateFpsSample(float fps)
        {
            cachedFps = fps;
        }

        public static float CurrentFPS => cachedFps;

        public static ProfileScope Scope(string blockName, float logWarningThresholdMs = -1f)
        {
            return new ProfileScope(blockName, logWarningThresholdMs);
        }

        public static void Time(Action action, string actionName = "Action", float logWarningThresholdMs = -1f)
        {
            if (action == null) return;
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            float ms = sw.ElapsedTicks / (float)TimeSpan.TicksPerMillisecond;
            if (logWarningThresholdMs > 0 && ms >= logWarningThresholdMs)
            {
                Log.Warning("DevTool.Profile", actionName, $"Execution exceeded threshold! Took {ms:0.00} ms (Threshold: {logWarningThresholdMs:0.00} ms)");
            }
            else
            {
                Log.Dev("DevTool.Profile", actionName, $"Completed in {ms:0.00} ms");
            }
        }

        public static float GetAllocatedMemoryMB()
        {
            return Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        }

        public static float GetReservedMemoryMB()
        {
            return Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);
        }

        public static float GetMonoUsedMemoryMB()
        {
            return Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);
        }
    }

    /// <summary>
    /// Disposable struct for zero-overhead profiling scopes.
    /// </summary>
    public readonly struct ProfileScope : IDisposable
    {
        private readonly string name;
        private readonly long startTicks;
        private readonly float thresholdMs;

        public ProfileScope(string name, float thresholdMs = -1f)
        {
            this.name = name;
            this.thresholdMs = thresholdMs;
            this.startTicks = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            double elapsedMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;

            if (thresholdMs > 0 && elapsedMs >= thresholdMs)
            {
                Log.Warning("DevTool.Profile", name, $"Scope exceeded threshold: {elapsedMs:0.00} ms (Threshold: {thresholdMs:0.00} ms)");
            }
        }
    }

    #endregion

    #region Static Domain: DevTool.Time (Timescale & Game Speed Control)

    /// <summary>
    /// In-game timescale manipulation for testing animations, slow-motion, and frame stepping.
    /// </summary>
    public static class Time
    {
        private static float previousTimeScale = 1f;

        public static float TimeScale
        {
            get => UnityEngine.Time.timeScale;
            set => SetTimeScale(value);
        }

        public static void SetTimeScale(float scale)
        {
            UnityEngine.Time.timeScale = Mathf.Max(0f, scale);
            Log.Dev("DevTool.Time", "Timescale set to " + UnityEngine.Time.timeScale);
        }

        public static void Pause()
        {
            if (UnityEngine.Time.timeScale > 0.0001f)
            {
                previousTimeScale = UnityEngine.Time.timeScale;
                UnityEngine.Time.timeScale = 0f;
                Log.Dev("DevTool.Time", "Paused");
            }
        }

        public static void Resume()
        {
            UnityEngine.Time.timeScale = previousTimeScale > 0.0001f ? previousTimeScale : 1f;
            Log.Dev("DevTool.Time", "Resumed to " + UnityEngine.Time.timeScale);
        }

        public static void TogglePause()
        {
            if (UnityEngine.Time.timeScale <= 0.0001f)
                Resume();
            else
                Pause();
        }

        public static void StepOneFrame(float frameTime = 1f / 60f)
        {
            // Advance unscaled time or invoke manual physics tick if paused
            if (UnityEngine.Time.timeScale <= 0.0001f)
            {
                Physics.Simulate(frameTime);
                Physics2D.Simulate(frameTime);
                Log.Dev("DevTool.Time", $"Stepped physics simulation by {frameTime * 1000f:0.0} ms");
            }
        }
    }

    #endregion

    #region Static Domain: DevTool.Assert (Safe Development Assertions)

    /// <summary>
    /// Runtime assertions active during Editor and Development Builds, stripped in release.
    /// </summary>
    public static class Assert
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void IsNotNull(object target, string message = "Object must not be null")
        {
            if (target == null)
            {
                Log.Error("DevTool.Assert", "NullCheck", message);
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void IsTrue(bool condition, string message = "Assertion failed")
        {
            if (!condition)
            {
                Log.Error("DevTool.Assert", "Condition", message);
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void Fail(string message = "Fatal assertion hit")
        {
            Log.Error("DevTool.Assert", "Fail", message);
        }
    }

    #endregion
}