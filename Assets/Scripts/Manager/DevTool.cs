using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SBGames.Database.Runtime;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.User;
using SBGames.Loop;
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
///  - DevTool.Loop      : Core game loop, combat simulation, round stepping, and DM resources.
///  - DevTool.Item      : Item spawning, preset catalog, and UserInventory/DungeonMaster artifact sync diagnostics.
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
    [SerializeField] private Rect hudWindowRect = new Rect(20f, 20f, 480f, 380f);

    [Header("Monitored Characters")]
    [SerializeField] private List<CharacterList> characterLists = new();

    [Header("Game Loop & Combat Controls")]
    [Tooltip("Target round number to jump to via Inspector or HUD.")]
    [SerializeField] private int debugTargetRound = 1;

    [Tooltip("Target game phase to force via Inspector.")]
    [SerializeField] private GamePhase debugTargetPhase = GamePhase.Planning;

    [Tooltip("Target Game Over reason when testing defeat conditions.")]
    [SerializeField] private GameOverReason debugGameOverReason = GameOverReason.EssenceDepleted;

    [Tooltip("Adventurer kill count when running battle simulation.")]
    [SerializeField] private int debugSimulateKills = 3;

    [Tooltip("Adventurer retreat count when running battle simulation.")]
    [SerializeField] private int debugSimulateRetreats = 1;

    [Tooltip("Energy amount to grant / deduct in debug operations.")]
    [SerializeField] private int debugEnergyDelta = 25;

    [Tooltip("Essence amount to grant / deduct in debug operations.")]
    [SerializeField] private int debugEssenceDelta = 10;

    [Header("Item & Inventory Spawner")]
    [Tooltip("Target ItemDefinition asset to spawn via Inspector.")]
    [SerializeField] private ItemDefinition debugItemAsset;

    [Tooltip("Quantity of items to spawn.")]
    [SerializeField, Min(1)] private int debugSpawnAmount = 1;

    [Header("Frame Rate & Performance Settings")]
    [Tooltip("Target frame rate cap (-1 for uncapped / platform default, or specific values like 30, 60, 120, 144, 240).")]
    [SerializeField] private int targetFrameRate = -1;

    [Tooltip("Enable VSync. Note: When VSync is enabled (>0), Application.targetFrameRate is ignored by Unity.")]
    [SerializeField] private bool enableVSync = false;

    #endregion

    #region Internal State

    private bool cmNullChecked = false;
    private int selectedTab = 0;
    private readonly string[] tabLabels = { "Status & Memory", "Time Controls", "Game Loop", "Characters", "Items & Sync", "Gizmo Config" };
    private Vector2 characterScrollPos;
    private Vector2 gameLoopScrollPos;
    private Vector2 itemScrollPos;
    private Vector2 itemBrowserScrollPos;
    private int itemCategoryIndex = 0;
    private readonly string[] itemCategoryTabs = { "All", "Artifacts", "Runes", "Consumables", "Other" };
    private string itemSearchFilter = "";
    private List<ItemDefinition> scannedDatabaseItems = new();
    private bool isDatabaseScanned = false;
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

        ApplyFrameRateSettings();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyFrameRateSettings();
        }
    }

    public void ApplyFrameRateSettings()
    {
        Time.SetVSync(enableVSync);
        if (!enableVSync)
        {
            Time.SetTargetFPS(targetFrameRate, false);
        }
    }

    public void SetTargetFPS(int fps)
    {
        targetFrameRate = fps;
        enableVSync = false;
        Time.SetTargetFPS(fps, true);
    }

    public void SetVSync(bool enabled)
    {
        enableVSync = enabled;
        Time.SetVSync(enabled);
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

    #region Inspector Debug: Game Loop & Combat

    [ContextMenu("Debug/Game Loop/Start Game")]
    [Button(true)]
    public void DebugStartGame()
    {
        Loop.StartGame();
    }

    [ContextMenu("Debug/Game Loop/Start Planning Phase")]
    [Button(true)]
    public void DebugStartPlanningPhase()
    {
        Loop.StartPlanning();
    }

    [ContextMenu("Debug/Game Loop/Start Battle Phase")]
    [Button(true)]
    public void DebugStartBattlePhase()
    {
        Loop.StartBattle();
    }

    [ContextMenu("Debug/Game Loop/Next Round")]
    [Button(true)]
    public void DebugNextRound()
    {
        Loop.NextRound();
    }

    [ContextMenu("Debug/Game Loop/Previous Round")]
    [Button(true)]
    public void DebugPreviousRound()
    {
        Loop.PreviousRound();
    }

    [ContextMenu("Debug/Game Loop/Jump To Target Round")]
    [Button(true)]
    public void DebugJumpToTargetRound()
    {
        Loop.JumpToRound(debugTargetRound);
    }

    [ContextMenu("Debug/Game Loop/Simulate Victory")]
    [Button(true)]
    public void DebugSimulateVictory()
    {
        Loop.WinBattle(debugSimulateKills, debugSimulateRetreats);
    }

    [ContextMenu("Debug/Game Loop/Simulate Defeat")]
    [Button(true)]
    public void DebugSimulateDefeat()
    {
        Loop.LoseBattle();
    }

    [ContextMenu("Debug/Game Loop/Kill All Adventurers")]
    [Button(true)]
    public void DebugKillAllAdventurers()
    {
        Loop.KillAllAdventurers();
    }

    [ContextMenu("Debug/Game Loop/Kill Boss")]
    [Button(true)]
    public void DebugKillBoss()
    {
        Loop.KillBoss();
    }

    [ContextMenu("Debug/Game Loop/Acknowledge Results & Proceed")]
    [Button(true)]
    public void DebugAcknowledgeAndProceed()
    {
        Loop.AcknowledgeResults();
    }

    [ContextMenu("Debug/Game Loop/Add Energy")]
    [Button(true)]
    public void DebugAddEnergy()
    {
        Loop.AddEnergy(debugEnergyDelta);
    }

    [ContextMenu("Debug/Game Loop/Add Essence")]
    [Button(true)]
    public void DebugAddEssence()
    {
        Loop.AddEssence(debugEssenceDelta);
    }

    [ContextMenu("Debug/Game Loop/Trigger Game Over")]
    [Button(true)]
    public void DebugTriggerGameOver()
    {
        Loop.TriggerGameOver(debugGameOverReason);
    }

    [ContextMenu("Debug/Game Loop/Reset Game Loop")]
    [Button(true)]
    public void DebugResetGameLoop()
    {
        Loop.ResetGame();
    }

    [ContextMenu("Debug/Performance/Apply Target FPS Settings")]
    [Button(true)]
    public void DebugApplyFpsSettings()
    {
        ApplyFrameRateSettings();
    }

    [ContextMenu("Debug/Performance/Set 60 FPS")]
    public void DebugSet60Fps() => SetTargetFPS(60);

    [ContextMenu("Debug/Performance/Set 120 FPS")]
    public void DebugSet120Fps() => SetTargetFPS(120);

    [ContextMenu("Debug/Performance/Set Uncapped FPS")]
    public void DebugSetUncappedFps() => SetTargetFPS(-1);

    #endregion

    #region Inspector Debug: Items & Inventory Spawner

    [ContextMenu("Debug/Items/Scan Database Items")]
    [Button(true)]
    public void DebugScanDatabase()
    {
        var items = Item.ScanDatabaseItems(true);
        scannedDatabaseItems = items;
        isDatabaseScanned = true;
    }

    [ContextMenu("Debug/Items/Spawn Item Asset")]
    [Button(true)]
    public void DebugSpawnItemAsset()
    {
        if (debugItemAsset != null)
        {
            Item.Spawn(debugItemAsset, debugSpawnAmount);
        }
        else
        {
            Log.Warning("DevTool.Item", this, "DebugItemAsset is not assigned in the Inspector.");
        }
    }

    [ContextMenu("Debug/Items/Clear User Inventory")]
    [Button(true)]
    public void DebugClearUserInventory() => Item.ClearUserInventory();

    [ContextMenu("Debug/Items/Log Artifact Sync Report")]
    [Button(true)]
    public void DebugLogArtifactSyncReport() => Item.LogSyncReport();

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
            GUILayout.Width(480f),
            GUILayout.Height(380f)
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
                DrawGameLoopTab();
                break;
            case 3:
                DrawCharactersTab();
                break;
            case 4:
                DrawItemsTab();
                break;
            case 5:
                DrawGizmoConfigTab();
                break;
        }

        // Enable window dragging by clicking and holding the top bar
        GUI.DragWindow(new Rect(0, 0, 10000, 30));
    }

    private void DrawStatusTab()
    {
        GUILayout.Label($"<b>FPS:</b> {currentFps:0.0} (Target: {(Application.targetFrameRate <= 0 ? "Uncapped" : Application.targetFrameRate.ToString())} | VSync: {(QualitySettings.vSyncCount > 0 ? "ON" : "OFF")})");
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

        GUILayout.Space(10f);
        GUILayout.Label($"<b>Target Frame Rate Cap:</b> {(Application.targetFrameRate <= 0 ? "Uncapped (-1)" : Application.targetFrameRate + " FPS")}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Uncapped")) SetTargetFPS(-1);
        if (GUILayout.Button("30")) SetTargetFPS(30);
        if (GUILayout.Button("60")) SetTargetFPS(60);
        if (GUILayout.Button("120")) SetTargetFPS(120);
        if (GUILayout.Button("144")) SetTargetFPS(144);
        if (GUILayout.Button("240")) SetTargetFPS(240);
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        bool currentVsync = QualitySettings.vSyncCount > 0;
        bool newVsync = GUILayout.Toggle(currentVsync, " Enable VSync (Locks to Monitor Refresh)");
        if (newVsync != currentVsync)
        {
            SetVSync(newVsync);
        }
    }

    private void DrawGameLoopTab()
    {
        var loop = Loop.GetGameLoop();
        var combat = Loop.GetCombatResultHandler();
        var dm = Loop.GetDungeonMaster();

        gameLoopScrollPos = GUILayout.BeginScrollView(gameLoopScrollPos, GUILayout.Height(280f));

        // 1. Status Section
        GUILayout.Label("<b>[ Game Loop State ]</b>");
        if (loop != null)
        {
            GUILayout.Label($"Phase: <b>{loop.CurrentPhase}</b> | Round: <b>{loop.CurrentRound}</b> | IsGameOver: <b>{loop.IsGameOver}</b> {(loop.IsGameOver ? $"({loop.ReasonForGameOver})" : "")}");
        }
        else
        {
            GUILayout.Label("<color=yellow>CoreGameLoop instance not found in scene.</color>");
        }

        if (dm != null)
        {
            GUILayout.Label($"DM Essence: <b>{dm.Essence}/{dm.MaxEssence}</b> | Energy: <b>{dm.Energy}/{dm.MaxEnergy}</b> | Artifacts: <b>{dm.ArtifactCount}</b>");
        }

        if (combat != null)
        {
            GUILayout.Label($"Combat State: <b>{combat.CurrentCombatState}</b> | Active Units: <b>{combat.ActiveAdventurersCount}/{combat.TotalAdventurersCount}</b> | Boss Alive: <b>{combat.IsBossAlive}</b>");
        }

        GUILayout.Space(6f);

        // 2. Round & Phase Flow Controls
        GUILayout.Label("<b>[ Phase & Round Controls ]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Game")) Loop.StartGame();
        if (GUILayout.Button("Planning Phase")) Loop.StartPlanning();
        if (GUILayout.Button("Battle Phase")) Loop.StartBattle();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀ Prev Round")) Loop.PreviousRound();
        if (GUILayout.Button("Next Round (+Energy) ▶")) Loop.NextRound();
        if (GUILayout.Button("Reset Loop")) Loop.ResetGame();
        GUILayout.EndHorizontal();

        // Round Jump row
        GUILayout.BeginHorizontal();
        GUILayout.Label("Jump to Round:", GUILayout.Width(100f));
        if (int.TryParse(GUILayout.TextField(debugTargetRound.ToString(), GUILayout.Width(50f)), out int parsedRound))
        {
            debugTargetRound = Mathf.Max(1, parsedRound);
        }
        if (GUILayout.Button("Jump", GUILayout.Width(60f)))
        {
            Loop.JumpToRound(debugTargetRound);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);

        // 3. Combat Simulation Controls
        GUILayout.Label("<b>[ Combat Simulation & Actions ]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Simulate Win (3K/1R)")) Loop.WinBattle(debugSimulateKills, debugSimulateRetreats);
        if (GUILayout.Button("Simulate Defeat")) Loop.LoseBattle();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill All Adventurers")) Loop.KillAllAdventurers();
        if (GUILayout.Button("Kill Final Boss")) Loop.KillBoss();
        if (GUILayout.Button("Acknowledge & Advance")) Loop.AcknowledgeResults();
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);

        // 4. Dungeon Master Resources
        GUILayout.Label("<b>[ Dungeon Master Resources ]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+10 Energy")) Loop.AddEnergy(10);
        if (GUILayout.Button("+50 Energy")) Loop.AddEnergy(50);
        if (GUILayout.Button("+10 Essence")) Loop.AddEssence(10);
        if (GUILayout.Button("-10 Essence")) Loop.AddEssence(-10);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Trigger Game Over (Essence 0)")) Loop.TriggerGameOver(GameOverReason.EssenceDepleted);
        if (GUILayout.Button("Trigger Game Over (No Artifacts)")) Loop.TriggerGameOver(GameOverReason.ArtifactsLost);
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
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

    private void DrawItemsTab()
    {
        var userInv = Item.GetUserInventory();
        var dm = Item.GetDungeonMaster();
        var report = Item.GenerateSyncReport();

        itemScrollPos = GUILayout.BeginScrollView(itemScrollPos, GUILayout.Height(280f));

        // 1. Sync & Diagnostics Summary
        GUILayout.Label("<b>[ Artifact Inventory & DM Sync Status ]</b>");
        GUILayout.BeginVertical("box");
        GUILayout.Label($"User Inventory: <b>{report.UserInventoryTotalItems}</b> items (<b>{report.UserInventoryArtifacts}</b> Artifacts) | Capacity: <b>{report.UserInventoryCapacity}</b>");
        GUILayout.Label($"Equipped on Characters: <b>{report.EquippedArtifacts}</b> Artifacts");
        GUILayout.Label($"Dungeon Master Artifacts: <b>{report.DungeonMasterArtifacts}</b> {(report.IsDungeonMasterAlive ? "<color=green>(Alive)</color>" : "<color=red>(Defeated)</color>")}");

        if (report.HasMismatch)
        {
            GUILayout.Label("<color=red><b>⚠ [SYNC WARNING]</b> (Inventory + Equipped) Artifacts != DM Artifact Count!</color>");
        }
        else
        {
            GUILayout.Label("<color=green><b>✔ [SYNC OK]</b> Artifact counts are aligned.</color>");
        }

        if (GUILayout.Button("Print Full Sync Report to Console"))
        {
            Item.LogSyncReport();
        }
        GUILayout.EndVertical();

        GUILayout.Space(6f);

        // 2. Database Scanned Item Browser
        if (!isDatabaseScanned)
        {
            scannedDatabaseItems = Item.ScanDatabaseItems();
            isDatabaseScanned = true;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>[ Database Items ({scannedDatabaseItems.Count} Scanned) ]</b>");
        if (GUILayout.Button("Rescan Database", GUILayout.Width(130f)))
        {
            scannedDatabaseItems = Item.ScanDatabaseItems(true);
            isDatabaseScanned = true;
        }
        GUILayout.EndHorizontal();

        // Category Filter Tabs
        itemCategoryIndex = GUILayout.Toolbar(itemCategoryIndex, itemCategoryTabs, GUILayout.ExpandWidth(true));

        // Search & Quantity Controls
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(50f));
        itemSearchFilter = GUILayout.TextField(itemSearchFilter, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("✕", GUILayout.Width(22f)))
        {
            itemSearchFilter = string.Empty;
        }
        GUILayout.Label("Qty:", GUILayout.Width(30f));
        if (int.TryParse(GUILayout.TextField(debugSpawnAmount.ToString(), GUILayout.Width(40f)), out int parsedQty))
        {
            debugSpawnAmount = Mathf.Max(1, parsedQty);
        }
        GUILayout.EndHorizontal();

        // Filter and display items
        var filteredItems = scannedDatabaseItems.Where(item =>
        {
            if (item == null) return false;
            if (itemCategoryIndex == 1 && item.ItemType != ItemType.Artifact) return false;
            if (itemCategoryIndex == 2 && item.ItemType != ItemType.Rune) return false;
            if (itemCategoryIndex == 3 && item.ItemType != ItemType.Consumable) return false;
            if (itemCategoryIndex == 4 && (item.ItemType == ItemType.Artifact || item.ItemType == ItemType.Rune || item.ItemType == ItemType.Consumable)) return false;

            if (!string.IsNullOrWhiteSpace(itemSearchFilter))
            {
                bool nameMatch = !string.IsNullOrEmpty(item.ItemName) && item.ItemName.IndexOf(itemSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool idMatch = !string.IsNullOrEmpty(item.Id) && item.Id.IndexOf(itemSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!nameMatch && !idMatch) return false;
            }
            return true;
        }).ToList();

        itemBrowserScrollPos = GUILayout.BeginScrollView(itemBrowserScrollPos, GUILayout.Height(150f));
        if (filteredItems.Count == 0)
        {
            GUILayout.Label("<color=grey><i>No matching items found in scanned database.</i></color>");
        }
        else
        {
            for (int i = 0; i < filteredItems.Count; i++)
            {
                var item = filteredItems[i];
                if (item == null) continue;

                GUILayout.BeginHorizontal("box");
                string typeTag = item.ItemType == ItemType.Artifact ? "<color=cyan>[Artifact]</color>" :
                                 item.ItemType == ItemType.Rune ? "<color=yellow>[Rune]</color>" :
                                 item.ItemType == ItemType.Consumable ? "<color=lime>[Consumable]</color>" : "[Misc]";
                GUILayout.Label($"{typeTag} <b>{item.ItemName}</b> <color=grey>({item.Id})</color>", GUILayout.Width(240f));

                if (GUILayout.Button($"+{debugSpawnAmount}", GUILayout.Width(45f)))
                {
                    Item.Spawn(item, debugSpawnAmount);
                }
                if (debugSpawnAmount != 1 && GUILayout.Button("+1", GUILayout.Width(35f)))
                {
                    Item.Spawn(item, 1);
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();

        if (debugItemAsset != null)
        {
            if (GUILayout.Button($"Spawn Assigned Asset: '{debugItemAsset.ItemName}' x{debugSpawnAmount}"))
            {
                Item.Spawn(debugItemAsset, debugSpawnAmount);
            }
        }

        GUILayout.Space(6f);

        // 3. Inventory Utilities & Contents
        GUILayout.Label("<b>[ Inventory Controls & Slot Contents ]</b>");
        if (GUILayout.Button("Clear All User Inventory Slots"))
        {
            Item.ClearUserInventory();
        }

        if (userInv != null && userInv.Inventory != null)
        {
            GUILayout.Space(4f);
            GUILayout.BeginVertical("box");
            bool anyOccupied = false;
            for (int i = 0; i < userInv.Capacity; i++)
            {
                var slot = userInv.Inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    anyOccupied = true;
                    string typeTag = slot.Item.ItemType == ItemType.Artifact ? "<color=cyan>[Artifact]</color>" :
                                     slot.Item.ItemType == ItemType.Rune ? "<color=yellow>[Rune]</color>" :
                                     slot.Item.ItemType == ItemType.Consumable ? "<color=lime>[Consumable]</color>" : "[Misc]";
                    GUILayout.Label($"Slot {i:02}: {typeTag} <b>{slot.Item.ItemName}</b> x{slot.Amount} (ID: {slot.Item.Id})");
                }
            }
            if (!anyOccupied)
            {
                GUILayout.Label("<color=grey><i>(User Inventory is currently empty)</i></color>");
            }
            GUILayout.EndVertical();
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

        public static int TargetFPS
        {
            get => Application.targetFrameRate;
            set => SetTargetFPS(value);
        }

        public static bool VSync
        {
            get => QualitySettings.vSyncCount > 0;
            set => SetVSync(value);
        }

        public static void SetTargetFPS(int targetFps, bool disableVSyncIfCapped = true)
        {
            if (disableVSyncIfCapped && targetFps > 0)
            {
                QualitySettings.vSyncCount = 0;
            }
            Application.targetFrameRate = targetFps;
            Log.Dev("DevTool.Time", $"Target FPS set to {(targetFps <= 0 ? "Uncapped (-1)" : targetFps.ToString())} (VSync: {QualitySettings.vSyncCount})");
        }

        public static void SetVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            Log.Dev("DevTool.Time", $"VSync set to {(enabled ? "ON (1)" : "OFF (0)")}");
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

    #region Static Domain: DevTool.Loop (Game Loop & Combat Debugging)

    /// <summary>
    /// Centralized Game Loop, Combat Simulation, and Dungeon Master runtime debugging utilities.
    /// Provides auto-discovery of CoreGameLoop, CombatResultHandler, and DungeonMaster across scenes.
    /// </summary>
    public static class Loop
    {
        private static CoreGameLoop cachedLoop;
        private static CombatResultHandler cachedCombat;
        private static DungeonMaster cachedDM;

        public static CoreGameLoop GetGameLoop()
        {
            if (cachedLoop == null)
            {
                cachedLoop = UnityEngine.Object.FindAnyObjectByType<CoreGameLoop>();
            }
            return cachedLoop;
        }

        public static CombatResultHandler GetCombatResultHandler()
        {
            if (cachedCombat == null)
            {
                cachedCombat = UnityEngine.Object.FindAnyObjectByType<CombatResultHandler>();
            }
            return cachedCombat;
        }

        public static DungeonMaster GetDungeonMaster()
        {
            if (cachedDM == null)
            {
                cachedDM = UnityEngine.Object.FindAnyObjectByType<DungeonMaster>();
            }
            return cachedDM;
        }

        public static void StartGame()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "StartGame", "Starting game loop...");
                loop.StartGame();
            }
            else
            {
                Log.Warning("DevTool.Loop", "StartGame", "No CoreGameLoop found in scene.");
            }
        }

        public static void StartPlanning()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "StartPlanning", "Switching to Planning phase.");
                loop.DebugStartPlanningPhase();
            }
            else
            {
                Log.Warning("DevTool.Loop", "StartPlanning", "No CoreGameLoop found in scene.");
            }
        }

        public static void StartBattle()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "StartBattle", "Switching to Battle phase.");
                loop.DebugStartBattlePhase();
            }
            else
            {
                Log.Warning("DevTool.Loop", "StartBattle", "No CoreGameLoop found in scene.");
            }
        }

        public static void NextRound()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "NextRound", $"Advancing from Round {loop.CurrentRound} to Round {loop.CurrentRound + 1}.");
                loop.DebugNextRound();
            }
            else
            {
                Log.Warning("DevTool.Loop", "NextRound", "No CoreGameLoop found in scene.");
            }
        }

        public static void PreviousRound()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "PreviousRound", $"Reverting from Round {loop.CurrentRound}.");
                loop.DebugPreviousRound();
            }
            else
            {
                Log.Warning("DevTool.Loop", "PreviousRound", "No CoreGameLoop found in scene.");
            }
        }

        public static void JumpToRound(int targetRound)
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "JumpToRound", $"Jumping to Round {targetRound}.");
                loop.DebugSetRound(targetRound, true);
            }
            else
            {
                Log.Warning("DevTool.Loop", "JumpToRound", "No CoreGameLoop found in scene.");
            }
        }

        public static void WinBattle(int kills = 3, int retreats = 1)
        {
            var loop = GetGameLoop();
            if (loop != null && loop.CurrentPhase != GamePhase.Battle)
            {
                Log.Info("DevTool.Loop", "WinBattle", "Not currently in Battle phase; forcing Battle phase first.");
                loop.DebugStartBattlePhase();
            }

            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                Log.Info("DevTool.Loop", "WinBattle", $"Simulating Victory with {kills} kills and {retreats} retreats via CombatResultHandler.");
                combat.DebugSimulateVictory(kills, retreats);
            }
            else
            {
                if (loop != null)
                {
                    Log.Info("DevTool.Loop", "WinBattle", "No CombatResultHandler found; resolving directly via CoreGameLoop.");
                    loop.DebugWinBattle();
                }
                else
                {
                    Log.Warning("DevTool.Loop", "WinBattle", "Neither CombatResultHandler nor CoreGameLoop found in scene.");
                }
            }
        }

        public static void LoseBattle()
        {
            var loop = GetGameLoop();
            if (loop != null && loop.CurrentPhase != GamePhase.Battle)
            {
                Log.Info("DevTool.Loop", "LoseBattle", "Not currently in Battle phase; forcing Battle phase first.");
                loop.DebugStartBattlePhase();
            }

            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                Log.Info("DevTool.Loop", "LoseBattle", "Simulating Defeat (Boss Death) via CombatResultHandler.");
                combat.DebugSimulateDefeat();
            }
            else
            {
                if (loop != null)
                {
                    Log.Info("DevTool.Loop", "LoseBattle", "No CombatResultHandler found; resolving directly via CoreGameLoop.");
                    loop.DebugLoseBattle();
                }
                else
                {
                    Log.Warning("DevTool.Loop", "LoseBattle", "Neither CombatResultHandler nor CoreGameLoop found in scene.");
                }
            }
        }

        public static void KillAllAdventurers()
        {
            var loop = GetGameLoop();
            if (loop != null && loop.CurrentPhase != GamePhase.Battle)
            {
                Log.Info("DevTool.Loop", "KillAllAdventurers", "Not currently in Battle phase; forcing Battle phase first.");
                loop.DebugStartBattlePhase();
            }

            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                if (combat.CurrentCombatState != CombatState.CombatActive)
                {
                    combat.BeginCombat(loop != null ? loop.CurrentRound : 1);
                }

                var tracked = combat.TrackedAdventurers;
                var keys = new List<string>(tracked.Keys);
                foreach (var id in keys)
                {
                    combat.ReportAdventurerDeath(id);
                }
                Log.Info("DevTool.Loop", "KillAllAdventurers", $"Reported death for {keys.Count} registered adventurers.");
            }
            else
            {
                Log.Warning("DevTool.Loop", "KillAllAdventurers", "No CombatResultHandler found in scene.");
            }
        }

        public static void KillBoss()
        {
            var loop = GetGameLoop();
            if (loop != null && loop.CurrentPhase != GamePhase.Battle)
            {
                Log.Info("DevTool.Loop", "KillBoss", "Not currently in Battle phase; forcing Battle phase first.");
                loop.DebugStartBattlePhase();
            }

            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                if (combat.CurrentCombatState != CombatState.CombatActive)
                {
                    combat.BeginCombat(loop != null ? loop.CurrentRound : 1);
                }

                Log.Info("DevTool.Loop", "KillBoss", "Reporting Boss Death via CombatResultHandler.");
                combat.ReportBossDeath();
            }
            else
            {
                Log.Warning("DevTool.Loop", "KillBoss", "No CombatResultHandler found in scene.");
            }
        }

        public static void AcknowledgeResults()
        {
            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                Log.Info("DevTool.Loop", "AcknowledgeResults", "Acknowledging combat results and proceeding to next phase.");
                combat.AcknowledgeResultsAndProceed();
            }
            else
            {
                Log.Warning("DevTool.Loop", "AcknowledgeResults", "No CombatResultHandler found in scene.");
            }
        }

        public static void AddEnergy(int amount)
        {
            var dm = GetDungeonMaster();
            if (dm != null)
            {
                if (amount >= 0)
                {
                    dm.GainEnergy(amount);
                    Log.Info("DevTool.Loop", "AddEnergy", $"Added {amount} Energy. Current: {dm.Energy}/{dm.MaxEnergy}");
                }
                else
                {
                    dm.SpendEnergy(-amount);
                    Log.Info("DevTool.Loop", "AddEnergy", $"Spent {-amount} Energy. Current: {dm.Energy}/{dm.MaxEnergy}");
                }
            }
            else
            {
                Log.Warning("DevTool.Loop", "AddEnergy", "No DungeonMaster found in scene.");
            }
        }

        public static void AddEssence(int amount)
        {
            var dm = GetDungeonMaster();
            if (dm != null)
            {
                if (amount >= 0)
                {
                    dm.RecoverEssence(amount, 0);
                    Log.Info("DevTool.Loop", "AddEssence", $"Restored {amount} Essence. Current: {dm.Essence}/{dm.MaxEssence}");
                }
                else
                {
                    dm.LoseEssence(-amount);
                    Log.Info("DevTool.Loop", "AddEssence", $"Reduced {-amount} Essence. Current: {dm.Essence}/{dm.MaxEssence}");
                }
            }
            else
            {
                Log.Warning("DevTool.Loop", "AddEssence", "No DungeonMaster found in scene.");
            }
        }

        public static void TriggerGameOver(GameOverReason reason = GameOverReason.EssenceDepleted)
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Warning("DevTool.Loop", "TriggerGameOver", $"Triggering Game Over with reason: {reason}");
                loop.DebugTriggerGameOver(reason);
            }
            else
            {
                var dm = GetDungeonMaster();
                if (dm != null)
                {
                    if (reason == GameOverReason.ArtifactsLost)
                    {
                        dm.ClearArtifacts();
                    }
                    else
                    {
                        dm.LoseEssence(dm.Essence);
                    }
                }
                else
                {
                    Log.Warning("DevTool.Loop", "TriggerGameOver", "No CoreGameLoop or DungeonMaster found in scene.");
                }
            }
        }

        public static void ResetGame()
        {
            var loop = GetGameLoop();
            if (loop != null)
            {
                Log.Info("DevTool.Loop", "ResetGame", "Resetting CoreGameLoop...");
                loop.DebugResetGame();
            }

            var combat = GetCombatResultHandler();
            if (combat != null)
            {
                Log.Info("DevTool.Loop", "ResetGame", "Resetting CombatResultHandler...");
                combat.ResetCombatState();
            }
        }
    }

    #endregion

    #region Static Domain: DevTool.Item (Item Spawner & Inventory Sync Diagnostics)

    /// <summary>
    /// Centralized Item Spawning, runtime item generation, and Artifact Inventory synchronization utilities.
    /// Provides auto-discovery of UserInventoryManager, CharacterInventoryManager instances, and DungeonMaster.
    /// </summary>
    public static class Item
    {
        private static UserInventoryManager cachedUserInventory;
        private static DungeonMaster cachedDM;
        private static List<ItemDefinition> cachedScannedItems;

        public static UserInventoryManager GetUserInventory()
        {
            if (cachedUserInventory == null)
            {
                cachedUserInventory = UnityEngine.Object.FindAnyObjectByType<UserInventoryManager>();
            }
            return cachedUserInventory;
        }

        public static DungeonMaster GetDungeonMaster()
        {
            if (cachedDM == null)
            {
                cachedDM = UnityEngine.Object.FindAnyObjectByType<DungeonMaster>();
            }
            return cachedDM;
        }

        /// <summary>
        /// Scans GameRegistry, Resources, and Editor AssetDatabase to discover all ItemDefinition assets.
        /// </summary>
        public static List<ItemDefinition> ScanDatabaseItems(bool forceRescan = false)
        {
            if (cachedScannedItems != null && !forceRescan)
            {
                return cachedScannedItems;
            }

            var list = new List<ItemDefinition>();
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Scan via GameRegistry / MasterGameDatabase
            try
            {
                var allItems = GameRegistry.GetAllItems();
                if (allItems != null)
                {
                    foreach (var item in allItems)
                    {
                        if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !addedIds.Contains(item.Id))
                        {
                            list.Add(item);
                            addedIds.Add(item.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Dev("DevTool.Item", "GameRegistryScan", $"GameRegistry item query failed: {ex.Message}");
            }

            // 2. Scan via Resources
            try
            {
                var resourcesItems = Resources.LoadAll<ItemDefinition>("");
                if (resourcesItems != null)
                {
                    foreach (var item in resourcesItems)
                    {
                        if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !addedIds.Contains(item.Id))
                        {
                            list.Add(item);
                            addedIds.Add(item.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Dev("DevTool.Item", "ResourcesScan", $"Resources item query failed: {ex.Message}");
            }

#if UNITY_EDITOR
            // 3. Editor-only AssetDatabase scan to find all ItemDefinition assets across the project
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !addedIds.Contains(item.Id))
                    {
                        list.Add(item);
                        addedIds.Add(item.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Dev("DevTool.Item", "AssetDatabaseScan", $"AssetDatabase query failed: {ex.Message}");
            }
#endif

            cachedScannedItems = list;
            Log.Info("DevTool.Item", "Scan", $"Scanned database: Found {list.Count} unique ItemDefinitions.");
            return list;
        }

        /// <summary>
        /// Spawns the specified ItemDefinition into the player's UserInventoryManager.
        /// </summary>
        public static bool Spawn(ItemDefinition item, int count = 1)
        {
            if (item == null)
            {
                Log.Warning("DevTool.Item", "Spawn", "Cannot spawn null ItemDefinition.");
                return false;
            }

            var userInv = GetUserInventory();
            if (userInv == null)
            {
                Log.Warning("DevTool.Item", "Spawn", "No UserInventoryManager instance found in the scene.");
                return false;
            }

            int leftover = userInv.AddItem(item, count);
            int added = count - leftover;
            if (added > 0)
            {
                Log.Info("DevTool.Item", "Spawn", $"Successfully added {added}x '{item.ItemName}' (ID: {item.Id}) to User Inventory. (Leftover: {leftover})");
                return true;
            }
            else
            {
                Log.Warning("DevTool.Item", "Spawn", $"User Inventory is full! Could not add '{item.ItemName}'.");
                return false;
            }
        }

        /// <summary>
        /// Attempts to look up an item by ID from GameRegistry and spawns it into UserInventoryManager.
        /// </summary>
        public static bool SpawnById(string itemId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Log.Warning("DevTool.Item", "SpawnById", "Item ID must not be empty.");
                return false;
            }

            if (GameRegistry.TryGetItem(itemId, out var item))
            {
                return Spawn(item, count);
            }

            // Fallback lookup from scanned database
            var scanned = ScanDatabaseItems();
            var matched = scanned.Find(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                return Spawn(matched, count);
            }

            Log.Warning("DevTool.Item", "SpawnById", $"Item with ID '{itemId}' was not found in database or scanned assets.");
            return false;
        }

        public static void ClearUserInventory()
        {
            var userInv = GetUserInventory();
            if (userInv != null && userInv.Inventory != null)
            {
                userInv.Inventory.Clear();
                Log.Info("DevTool.Item", "Clear", "Cleared all slots in UserInventoryManager.");
            }
            else
            {
                Log.Warning("DevTool.Item", "Clear", "No active UserInventoryManager found to clear.");
            }
        }

        #region Sync & Diagnostics

        public struct SyncReport
        {
            public int UserInventoryTotalItems;
            public int UserInventoryCapacity;
            public int UserInventoryArtifacts;
            public List<string> UserArtifactNames;

            public int EquippedArtifacts;
            public List<string> EquippedArtifactNames;

            public int DungeonMasterArtifacts;
            public List<string> DungeonMasterArtifactNames;

            public bool IsDungeonMasterAlive;
            public bool HasMismatch;
        }

        public static SyncReport GenerateSyncReport()
        {
            var report = new SyncReport
            {
                UserArtifactNames = new List<string>(),
                EquippedArtifactNames = new List<string>(),
                DungeonMasterArtifactNames = new List<string>()
            };

            var userInv = GetUserInventory();
            if (userInv != null && userInv.Inventory != null)
            {
                report.UserInventoryCapacity = userInv.Capacity;
                for (int i = 0; i < userInv.Capacity; i++)
                {
                    var slot = userInv.Inventory.GetSlot(i);
                    if (slot != null && !slot.IsEmpty)
                    {
                        report.UserInventoryTotalItems += slot.Amount;
                        if (slot.Item.ItemType == ItemType.Artifact)
                        {
                            report.UserInventoryArtifacts += slot.Amount;
                            report.UserArtifactNames.Add(slot.Item.ItemName);
                        }
                    }
                }
            }

            var charManagers = UnityEngine.Object.FindObjectsByType<CharacterInventoryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (charManagers != null)
            {
                foreach (var cm in charManagers)
                {
                    if (cm == null || cm.Inventory == null) continue;
                    for (int i = 0; i < cm.Inventory.SlotCount; i++)
                    {
                        var slot = cm.Inventory.GetSlot(i);
                        if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                        {
                            if (slot.SlotType == EquipmentSlotType.Artifact || slot.EquippedItem.ItemType == ItemType.Artifact)
                            {
                                report.EquippedArtifacts++;
                                report.EquippedArtifactNames.Add($"{cm.CharacterDisplayName}: {slot.EquippedItem.ItemName}");
                            }
                        }
                    }
                }
            }

            var dm = GetDungeonMaster();
            if (dm != null)
            {
                report.DungeonMasterArtifacts = dm.ArtifactCount;
                report.IsDungeonMasterAlive = !dm.IsDefeated;
                if (dm.Artifacts != null)
                {
                    foreach (var art in dm.Artifacts)
                    {
                        if (art != null)
                        {
                            report.DungeonMasterArtifactNames.Add($"{art.ArtifactName} (Lvl {art.PowerLevel}{(art.IsEmpowered ? ", Empowered" : "")})");
                        }
                    }
                }
            }

            int totalInventoryAndEquippedArtifacts = report.UserInventoryArtifacts + report.EquippedArtifacts;
            report.HasMismatch = dm != null && totalInventoryAndEquippedArtifacts != report.DungeonMasterArtifacts;

            return report;
        }

        public static void LogSyncReport()
        {
            var r = GenerateSyncReport();
            var sb = new StringBuilder();
            sb.AppendLine("=== [Artifact & Inventory Sync Diagnostic Report] ===");
            sb.AppendLine($"User Inventory: {r.UserInventoryTotalItems} items ({r.UserInventoryArtifacts} Artifacts) / Capacity {r.UserInventoryCapacity}");
            if (r.UserArtifactNames.Count > 0)
                sb.AppendLine($"  - Inventory Artifacts: {string.Join(", ", r.UserArtifactNames)}");

            sb.AppendLine($"Character Equipped Artifacts: {r.EquippedArtifacts}");
            if (r.EquippedArtifactNames.Count > 0)
                sb.AppendLine($"  - Equipped: {string.Join(", ", r.EquippedArtifactNames)}");

            sb.AppendLine($"Dungeon Master Artifacts: {r.DungeonMasterArtifacts} (Defeated: {!r.IsDungeonMasterAlive})");
            if (r.DungeonMasterArtifactNames.Count > 0)
                sb.AppendLine($"  - DM Collection: {string.Join(", ", r.DungeonMasterArtifactNames)}");

            if (r.HasMismatch)
            {
                sb.AppendLine("⚠ [SYNC WARNING] Count mismatch between (Inventory + Equipped) artifacts and Dungeon Master artifact collection!");
            }
            else
            {
                sb.AppendLine("✔ [SYNC OK] Artifact counts match.");
            }

            Log.Info("DevTool.Item", "SyncReport", sb.ToString());
        }

        #endregion
    }

    #endregion
}