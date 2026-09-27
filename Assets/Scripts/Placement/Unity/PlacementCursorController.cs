using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SBGames.Placement
{
    /// <summary>
    /// Interactive grid cursor, visual ghost preview, and Build Menu controller.
    /// Press 'G' to open/close the Build Menu to select units.
    /// Press 'E' (while Build Menu is open) to toggle Sell Mode — click placed units to sell them.
    /// Manages raycasting, tile snapping, real-time validation tinting (green/red),
    /// floating diagnostics tooltip, and click-to-place / sell interactions.
    /// </summary>
    public class PlacementCursorController : MonoBehaviour
    {
        [Header("Camera & Plane")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Offset along perpendicular axis (Z offset for 2D XY plane, Y elevation for 3D XZ plane).")]
        [SerializeField] private float planeOffset = 0f;

        [Header("Colors & Visuals")]
        [SerializeField] private Color validColor = new Color(0.2f, 0.9f, 0.2f, 0.65f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.15f, 0.15f, 0.65f);

        [Header("Build Menu & Unit Catalogue")]
        [Tooltip("Press G to open/close the Build Menu in game.")]
        [SerializeField] private bool isBuildMenuOpen = false;
        [SerializeField] private List<UnitPlacementDefinitionSO> availableUnits = new();

        [Header("Active Selection")]
        [SerializeField] private UnitPlacementDefinitionSO activeUnitAsset;

        [Header("Debug / Quick Test Controls")]
        [Tooltip("Press Space to toggle between Build and Combat phases.")]
        [SerializeField] private bool enablePhaseHotkeys = true;

        [Header("Debug Info (Read-Only)")]
        [Tooltip("True while Sell Mode is active (press E in Build Menu to toggle).")]
        [SerializeField] private bool isSellModeActive = false;

        private IUnitPlacementDefinition currentUnit;
        private GameObject ghostObject;
        private Renderer[] ghostRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Vector2Int currentHoveredCell;
        private bool isCursorOnGrid;
        private PlacementValidationResult currentValidationResult;

        public IUnitPlacementDefinition CurrentUnit => currentUnit;
        public Vector2Int HoveredCell => currentHoveredCell;
        public bool IsPlacementActive => currentUnit != null;
        public bool IsBuildMenuOpen => isBuildMenuOpen;
        public bool IsSellModeActive => isSellModeActive;
        public bool IsCursorOnGrid => isCursorOnGrid;
        public PlacementValidationResult CurrentValidationResult => currentValidationResult;
        public IReadOnlyList<UnitPlacementDefinitionSO> AvailableUnits => availableUnits;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // Auto-populate availableUnits if empty
            if (availableUnits.Count == 0 && activeUnitAsset != null)
            {
                availableUnits.Add(activeUnitAsset);
            }

            // Only auto-select unit if Build Menu is open at Start
            if (isBuildMenuOpen && activeUnitAsset != null)
            {
                SelectUnit(activeUnitAsset);
            }
        }

        private void Update()
        {
            HandleBuildMenuInput();
            HandleSellModeInput();
            HandlePhaseToggleInput();
            UpdateCursorAndRaycast();
            HandlePlacementInput();
        }

        /// <summary>
        /// Toggles the Build Menu open / closed.
        /// </summary>
        public void ToggleBuildMenu()
        {
            SetBuildMenuOpen(!isBuildMenuOpen);
        }

        /// <summary>
        /// Explicitly opens or closes the Build Menu.
        /// Closing the menu exits Sell Mode, but preserves unit selection
        /// so the player can place the unit on the grid.
        /// </summary>
        public void SetBuildMenuOpen(bool open)
        {
            isBuildMenuOpen = open;
            if (!isBuildMenuOpen)
            {
                isSellModeActive = false;
                DeselectUnit();
            }
        }

        /// <summary>
        /// Selects a unit type to place and spawns its preview ghost.
        /// Also exits Sell Mode if it was active.
        /// </summary>
        public void SelectUnit(IUnitPlacementDefinition unitDefinition)
        {
            isSellModeActive = false; // Selecting a unit exits sell mode
            currentUnit = unitDefinition;
            RebuildGhost();
        }

        /// <summary>
        /// Cancels active placement mode and destroys ghost preview.
        /// </summary>
        public void DeselectUnit()
        {
            currentUnit = null;
            SafeDestroyGhost();
        }

        private void SafeDestroyGhost()
        {
            if (ghostObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(ghostObject);
                }
                else
                {
                    DestroyImmediate(ghostObject);
                }
                ghostObject = null;
                ghostRenderers = null;
            }
        }

        private void UpdateCursorAndRaycast()
        {
            var manager = DungeonPlacementManager.Instance;
            if (targetCamera == null || manager == null)
            {
                isCursorOnGrid = false;
                if (ghostObject != null) ghostObject.SetActive(false);
                return;
            }

            // Only track raycast when in build menu, placement mode, or sell mode
            if (!isBuildMenuOpen && !IsPlacementActive && !isSellModeActive)
            {
                isCursorOnGrid = false;
                if (ghostObject != null) ghostObject.SetActive(false);
                return;
            }

            Vector2 mousePos = GetMousePosition();
            Vector3 hitPoint;

            if (targetCamera.orthographic && manager.PlaneOrientation == GridPlaneOrientation.XY_2D)
            {
                // Direct world-point transformation for 2D orthographic camera
                float zDistance = -targetCamera.transform.position.z + planeOffset;
                hitPoint = targetCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, zDistance));
                hitPoint.z = planeOffset;
            }
            else
            {
                Ray ray = targetCamera.ScreenPointToRay(mousePos);
                Plane raycastPlane = manager.PlaneOrientation == GridPlaneOrientation.XY_2D
                    ? new Plane(Vector3.back, new Vector3(0f, 0f, planeOffset))
                    : new Plane(Vector3.up, new Vector3(0f, planeOffset, 0f));

                if (!raycastPlane.Raycast(ray, out float enter))
                {
                    isCursorOnGrid = false;
                    return;
                }
                hitPoint = ray.GetPoint(enter);
            }

            Vector2Int cell = manager.WorldToGridPosition(hitPoint);
            currentHoveredCell = cell;

            var tracker = manager.OccupancyTracker;
            isCursorOnGrid = tracker != null && tracker.IsWithinBounds(cell);

            // In sell mode: hide ghost but keep tracking cursor position for hover tooltip
            if (isSellModeActive)
            {
                if (ghostObject != null) ghostObject.SetActive(false);
                return;
            }

            if (ghostObject != null)
            {
                if (isCursorOnGrid && IsPlacementActive)
                {
                    ghostObject.SetActive(true);

                    Vector3 snappedPos = manager.GridToWorldPosition(cell);
                    // Slightly closer to camera in Z so it does not get occluded by background tiles
                    snappedPos.z = planeOffset - 0.05f;
                    ghostObject.transform.position = snappedPos;
                    ghostObject.transform.rotation = Quaternion.identity;

                    // Pre-validate without side effects
                    currentValidationResult = manager.ValidatePlacementWithCombatPower(
                        currentUnit,
                        cell
                    );

                    ApplyGhostColor(currentValidationResult.IsValid ? validColor : invalidColor);
                }
                else
                {
                    // Hide ghost sprite when mouse is not hovering over the placable grid
                    ghostObject.SetActive(false);

                    currentValidationResult = PlacementValidationResult.FailureResult(
                        PlacementStatus.OutOfBounds,
                        "Outside dungeon grid boundaries.",
                        currentUnit.BaseCost,
                        0,
                        manager.Wallet?.CurrentResources ?? 0,
                        manager.CapacityPool?.AvailableCapacity ?? 0
                    );
                }
            }
        }

        private void HandlePlacementInput()
        {
            if (DungeonPlacementManager.Instance == null) return;

            // --- SELL MODE: Left-click to sell a placed unit ---
            if (isSellModeActive)
            {
                if (WasLeftMouseButtonPressed() && isCursorOnGrid)
                {
                    if (DungeonPlacementManager.Instance.OccupancyTracker.TryGetUnitAt(currentHoveredCell, out var placed))
                    {
                        bool removed = DungeonPlacementManager.Instance.TryRemoveUnit(currentHoveredCell);
                        if (removed)
                        {
                            Debug.Log($"[Sell] Sold {placed.Definition.DisplayName} at {currentHoveredCell}. Refunded {placed.CostPaid} gold.");
                        }
                        else
                        {
                            Debug.LogWarning($"[Sell] Could not remove unit at {currentHoveredCell} (phase locked or no unit found).");
                        }
                    }
                }
                return; // Skip normal placement input while in sell mode
            }

            // Left Click: Commit placement if valid
            if (WasLeftMouseButtonPressed())
            {
                if (IsPlacementActive && isCursorOnGrid)
                {
                    if (currentValidationResult.IsValid)
                    {
                        bool success = DungeonPlacementManager.Instance.TryPlaceUnitWithCombatPower(
                            currentUnit,
                            currentHoveredCell,
                            out var placedUnit,
                            out var result
                        );

                        if (success)
                        {
                            // Re-evaluate validation immediately after placement
                            currentValidationResult = DungeonPlacementManager.Instance.ValidatePlacementWithCombatPower(
                                currentUnit,
                                currentHoveredCell
                            );
                            ApplyGhostColor(currentValidationResult.IsValid ? validColor : invalidColor);
                        }
                    }
                }
            }

            // Right Click: Cancel selection, or if nothing selected, sell/remove unit on hovered tile
            if (WasRightMouseButtonPressed())
            {
                if (IsPlacementActive)
                {
                    DeselectUnit();
                }
                else if (isCursorOnGrid)
                {
                    // Attempt removal during Build Phase
                    DungeonPlacementManager.Instance.TryRemoveUnit(currentHoveredCell);
                }
            }
        }

        private void HandleBuildMenuInput()
        {
            if (WasGKeyPressed())
            {
                ToggleBuildMenu();
            }

            if (isBuildMenuOpen)
            {
                // Number hotkeys 1-9 to select units
                for (int i = 0; i < availableUnits.Count && i < 9; i++)
                {
                    if (WasAlphaKeyPressed(i + 1))
                    {
                        SelectUnit(availableUnits[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Toggles Sell Mode on/off when pressing E while the Build Menu is open.
        /// Entering Sell Mode deselects any active unit (no ghost shown).
        /// Exiting Sell Mode restores the cursor to normal idle.
        /// </summary>
        private void HandleSellModeInput()
        {
            if (!WasEKeyPressed()) return;
            if (!isBuildMenuOpen) return; // E only works inside Build Menu

            if (isSellModeActive)
            {
                // Exit sell mode
                isSellModeActive = false;
            }
            else
            {
                // Enter sell mode — deselect any unit first so ghost disappears
                DeselectUnit();
                isSellModeActive = true;
            }
        }

        private void HandlePhaseToggleInput()
        {
            if (!enablePhaseHotkeys || DungeonPlacementManager.Instance == null || DungeonPlacementManager.Instance.PhaseManager == null) return;

            if (WasSpaceKeyPressed())
            {
                var current = DungeonPlacementManager.Instance.PhaseManager.CurrentPhase;
                var next = current == DungeonPhase.BuildPhase ? DungeonPhase.CombatPhase : DungeonPhase.BuildPhase;
                DungeonPlacementManager.Instance.SetPhase(next);
            }
        }

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private bool WasLeftMouseButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private bool WasRightMouseButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private bool WasSpaceKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private bool WasGKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.gKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.G);
#else
            return false;
#endif
        }

        private bool WasEKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.eKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private bool WasAlphaKeyPressed(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switch (digit)
                {
                    case 1: return Keyboard.current.digit1Key.wasPressedThisFrame;
                    case 2: return Keyboard.current.digit2Key.wasPressedThisFrame;
                    case 3: return Keyboard.current.digit3Key.wasPressedThisFrame;
                    case 4: return Keyboard.current.digit4Key.wasPressedThisFrame;
                    case 5: return Keyboard.current.digit5Key.wasPressedThisFrame;
                    case 6: return Keyboard.current.digit6Key.wasPressedThisFrame;
                    case 7: return Keyboard.current.digit7Key.wasPressedThisFrame;
                    case 8: return Keyboard.current.digit8Key.wasPressedThisFrame;
                    case 9: return Keyboard.current.digit9Key.wasPressedThisFrame;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Alpha0 + digit);
#else
            return false;
#endif
        }

        private void RebuildGhost()
        {
            SafeDestroyGhost();

            if (currentUnit == null) return;

            if (currentUnit.Prefab != null)
            {
                ghostObject = Instantiate(currentUnit.Prefab);
                ghostObject.name = $"Ghost_{currentUnit.DisplayName}";
            }
            else
            {
                // Fallback primitive ghost if no prefab is assigned
                ghostObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghostObject.name = $"Ghost_{currentUnit.DisplayName}_Primitive";

                var manager = DungeonPlacementManager.Instance;
                if (manager != null && manager.PlaneOrientation == GridPlaneOrientation.XY_2D)
                {
                    ghostObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.05f);
                }
                else
                {
                    ghostObject.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
                }
            }

            ghostObject.transform.rotation = Quaternion.identity;

            // CRITICAL: Disable NavMeshAgent so it does not auto-align with 3D NavMesh or rotate into wrong plane!
            var navAgents = ghostObject.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
            foreach (var na in navAgents)
            {
                na.enabled = false;
            }

            // CRITICAL: Disable all functional Behaviours (AI, brain, movement, audio) so they don't run!
            var behaviours = ghostObject.GetComponentsInChildren<Behaviour>(true);
            foreach (var b in behaviours)
            {
                b.enabled = false;
            }

            // Disable Animators so idle animations don't override or hide the sprite
            var animators = ghostObject.GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators)
            {
                anim.enabled = false;
            }

            // Disable physics simulation and colliders
            var bodies2D = ghostObject.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var rb in bodies2D)
            {
                rb.simulated = false;
            }

            var colliders3D = ghostObject.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders3D)
            {
                col.enabled = false;
            }

            var colliders2D = ghostObject.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders2D)
            {
                col.enabled = false;
            }

            // Cache renderers and ensure 2D SpriteRenderers sort on top
            ghostRenderers = ghostObject.GetComponentsInChildren<Renderer>(true);
            foreach (var r in ghostRenderers)
            {
                if (r is SpriteRenderer sr)
                {
                    sr.sortingLayerName = "Character";
                    sr.sortingOrder = 5000; // Force to render on top of ground & characters
                }
            }

            ApplyGhostColor(validColor);

            // Initially hide ghost until the cursor is placed over a valid grid cell
            ghostObject.SetActive(false);
        }

        private void ApplyGhostColor(Color color)
        {
            if (ghostRenderers == null) return;

            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                var r = ghostRenderers[i];
                if (r == null) continue;

                if (r is SpriteRenderer sr)
                {
                    sr.color = color;
                    sr.sortingLayerName = "Character";
                    sr.sortingOrder = 5000;
                }
                else
                {
                    r.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", color);
                    propertyBlock.SetColor("_BaseColor", color);
                    r.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private void OnGUI()
        {
            if (DungeonPlacementManager.Instance == null) return;

            var manager = DungeonPlacementManager.Instance;
            var phase = manager.PhaseManager?.CurrentPhase ?? DungeonPhase.BuildPhase;
            int gold = manager.Wallet?.CurrentResources ?? 0;
            int usedCap = manager.CapacityPool?.CurrentCapacityUsed ?? 0;
            int maxCap = manager.CapacityPool?.MaxCapacity ?? 0;

            // 1. Top-Left Economy & Status HUD
            GUI.Box(new Rect(10, 10, 310, 52), "");
            GUI.Label(new Rect(16, 12, 300, 22), $"<b>Phase:</b> {phase} (Press Space to toggle)");
            GUI.Label(new Rect(16, 32, 300, 22), $"Gold: {gold}   |   Capacity: {usedCap}/{maxCap}");

            // 2. Build Menu Toggle Button
            string toggleBtnText = isBuildMenuOpen ? "<b>[G] Close Build Menu</b>" : "<b>[G] Open Build Menu</b>";
            if (GUI.Button(new Rect(10, 68, 175, 30), toggleBtnText))
            {
                ToggleBuildMenu();
            }

            // 2b. Sell Mode active banner (shown below the toggle button when sell mode is on)
            float nextBannerY = 104f;
            if (isSellModeActive)
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.25f, 0.25f, 0.9f);
                GUI.Box(new Rect(10, nextBannerY, 290, 26), "");
                GUI.Label(new Rect(16, nextBannerY + 2, 280, 22), "<b>SELL MODE</b>  —  Click a unit to sell it.  [E] to exit.");
                GUI.backgroundColor = prev;
                nextBannerY += 30f;
            }

            // 2c. Placing unit active banner
            if (IsPlacementActive && !isSellModeActive)
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f, 0.95f);
                GUI.Box(new Rect(10, nextBannerY, 360, 26), "");
                GUI.Label(new Rect(16, nextBannerY + 2, 350, 22), $"<b>PLACING:</b> {currentUnit.DisplayName} ({currentUnit.BaseCost} Gold)  |  [Right-Click to Cancel]");
                GUI.backgroundColor = prev;
            }

            // 3. Build Menu Tray (Rendered at bottom when open)
            if (isBuildMenuOpen)
            {
                DrawBuildMenu(gold, usedCap, maxCap);
            }

            // 4. Cursor Hover Tooltip
            if (isSellModeActive && isCursorOnGrid)
            {
                // Sell mode: show refund preview for unit under cursor
                if (manager.OccupancyTracker.TryGetUnitAt(currentHoveredCell, out var placed))
                {
                    Vector2 mousePos = GetMousePosition();
                    float guiX = mousePos.x + 18f;
                    float guiY = Screen.height - mousePos.y + 12f;

                    string info = "<b>SELL: " + placed.Definition.DisplayName + "</b>\n" +
                                  "Refund: " + placed.CostPaid + " Gold\n" +
                                  "Capacity freed: " + placed.CapacityConsumed + " Slots\n" +
                                  "<b>[Left-Click to Sell]</b>";

                    GUIStyle sellStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 12,
                        richText = true,
                        wordWrap = true
                    };
                    sellStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
                    GUI.Box(new Rect(guiX, guiY, 230, 80), info, sellStyle);
                }
            }
            else if (IsPlacementActive && isCursorOnGrid)
            {
                // Placement tooltip — displayed when hovering over the grid
                Vector2 mousePos = GetMousePosition();
                float guiX = mousePos.x + 18f;
                float guiY = Screen.height - mousePos.y + 12f;

                string text;
                Color textColor;

                if (currentValidationResult.IsValid)
                {
                    text = "<b>" + currentUnit.DisplayName + "</b>\n" +
                           "Cost: " + currentValidationResult.CostRequired + " Gold\n" +
                           "Capacity: " + currentValidationResult.CapacityRequired + " Slots\n" +
                           "<b>[Left-Click to Place]</b>\n[Right-Click to Cancel]";
                    textColor = Color.green;
                }
                else
                {
                    text = "<b>" + currentUnit.DisplayName + "</b>\n" +
                           "<color=#ff5555>X " + currentValidationResult.Message + "</color>\n" +
                           "[Right-Click to Cancel]";
                    textColor = Color.white;
                }

                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    richText = true,
                    wordWrap = true
                };
                style.normal.textColor = textColor;

                GUI.Box(new Rect(guiX, guiY, 240, 85), text, style);
            }
            else if (isCursorOnGrid)
            {
                // Inspect placed unit under cursor if any
                if (manager.OccupancyTracker.TryGetUnitAt(currentHoveredCell, out var placed))
                {
                    Vector2 mousePos = GetMousePosition();
                    float guiX = mousePos.x + 18f;
                    float guiY = Screen.height - mousePos.y + 12f;
                    string info = "<b>" + placed.Definition.DisplayName + "</b>\n" +
                                  "CP Factor: " + placed.CombatPowerFactor.ToString("F2") + "\n" +
                                  "Cap: " + placed.CapacityConsumed + " | Cost: " + placed.CostPaid + "\n" +
                                  "[Right-Click to Sell]";
                    GUI.Box(new Rect(guiX, guiY, 200, 75), info);
                }
            }
        }

        private void DrawBuildMenu(int gold, int usedCap, int maxCap)
        {
            float menuWidth = Mathf.Max(420f, availableUnits.Count * 170f + 30f);
            menuWidth = Mathf.Min(menuWidth, Screen.width - 40f);
            float menuHeight = 140f;
            float menuX = (Screen.width - menuWidth) / 2f;
            float menuY = Screen.height - menuHeight - 15f;

            // Background tray
            GUI.Box(new Rect(menuX, menuY, menuWidth, menuHeight), "");

            string menuTitle = isSellModeActive
                ? "<b>BUILD MENU</b>  [E] EXIT SELL MODE  |  Click units below to select for placement:"
                : "<b>BUILD MENU</b>  —  Select a unit to place (1, 2, 3...):    [E] Sell Mode";
            GUI.Label(new Rect(menuX + 15, menuY + 6, menuWidth - 30, 20), menuTitle);

            // Sell Mode toggle button inside Build Menu
            string sellBtnText = isSellModeActive ? "<b>[E] Exit Sell Mode</b>" : "<b>[E] Sell Mode</b>";
            Color prevBg = GUI.backgroundColor;
            if (isSellModeActive)
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUI.Button(new Rect(menuX + menuWidth - 145f, menuY + 4f, 135f, 24f), sellBtnText))
            {
                HandleSellModeButton();
            }
            GUI.backgroundColor = prevBg;

            // Horizontal card buttons
            float cardWidth = 155f;
            float cardHeight = 82f;
            float startX = menuX + 15f;
            float startY = menuY + 32f;

            for (int i = 0; i < availableUnits.Count; i++)
            {
                var unit = availableUnits[i];
                if (unit == null) continue;

                float cardX = startX + i * (cardWidth + 10f);
                if (cardX + cardWidth > menuX + menuWidth) break; // Avoid overflowing screen

                bool isSelected = currentUnit != null && currentUnit.UnitId == unit.UnitId;
                bool canAfford = gold >= unit.BaseCost;
                bool hasCapacity = (usedCap + unit.BaseCapacity) <= maxCap;

                string statusPrefix = isSelected ? "▶ <b>[PLACING]</b>\n" : "";
                string cardText = statusPrefix + "<b>[" + (i + 1) + "] " + unit.DisplayName + "</b>\n" +
                                  unit.BaseCost + " Gold  |  " + unit.BaseCapacity + " Cap";

                Color origColor = GUI.backgroundColor;
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
                }
                else if (!canAfford || !hasCapacity)
                {
                    GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                }

                if (GUI.Button(new Rect(cardX, startY, cardWidth, cardHeight), cardText))
                {
                    SelectUnit(unit);
                }

                GUI.backgroundColor = origColor;
            }
        }

        private void HandleSellModeButton()
        {
            if (!isBuildMenuOpen) return;

            if (isSellModeActive)
            {
                isSellModeActive = false;
            }
            else
            {
                DeselectUnit();
                isSellModeActive = true;
            }
        }
    }
}
