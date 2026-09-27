using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Procedural in-game grid visualizer and cell hover highlight for DungeonPlacementSystem.
    /// Automatically renders grid lines and cell boundaries on the XY or XZ plane during Build Mode,
    /// and seamlessly hides during Combat Mode.
    /// </summary>
    [RequireComponent(typeof(DungeonPlacementManager))]
    [DisallowMultipleComponent]
    public class DungeonGridVisualizer : MonoBehaviour
    {
        [Header("Visibility Settings")]
        [Tooltip("If true, grid lines are ONLY visible when Build Mode is active (Build Menu open [G], or actively placing/selling units).")]
        [SerializeField] private bool onlyShowInBuildMode = true;

        [Tooltip("If true, grid lines are hidden during CombatPhase even if Build Menu was open.")]
        [SerializeField] private bool hideGridInCombatPhase = true;

        [Header("Grid Line Appearance")]
        [Tooltip("Color of inner cell separating lines.")]
        [SerializeField] private Color innerLineColor = new Color(1f, 1f, 1f, 0.22f);

        [Tooltip("Color of the outer bounding box surrounding the entire grid.")]
        [SerializeField] private Color borderLineColor = new Color(0.3f, 0.8f, 1f, 0.7f);

        [Tooltip("Thickness of inner cell lines in world units.")]
        [SerializeField, Range(0.01f, 0.2f)] private float innerLineWidth = 0.035f;

        [Tooltip("Thickness of the outer border lines in world units.")]
        [SerializeField, Range(0.01f, 0.3f)] private float borderLineWidth = 0.07f;

        [Header("Hover Cell Highlight")]
        [Tooltip("Whether to render a soft highlight quad over the tile currently under the mouse cursor.")]
        [SerializeField] private bool showHoverHighlight = true;

        [Tooltip("Highlight color when hovering over an empty grid cell without active unit selection.")]
        [SerializeField] private Color hoverIdleColor = new Color(1f, 1f, 1f, 0.18f);

        [Tooltip("Highlight color when hovering over a valid placement cell with a selected unit.")]
        [SerializeField] private Color hoverValidColor = new Color(0.2f, 0.9f, 0.2f, 0.35f);

        [Tooltip("Highlight color when hovering over an invalid/occupied cell with a selected unit.")]
        [SerializeField] private Color hoverInvalidColor = new Color(0.95f, 0.2f, 0.2f, 0.35f);

        [Tooltip("Highlight color when hovering over a unit in Sell Mode (Press E).")]
        [SerializeField] private Color hoverSellColor = new Color(1f, 0.45f, 0.1f, 0.4f);

        [Header("Rendering & Sorting Layer")]
        [Tooltip("Sorting layer name for 2D rendering (e.g. Ground or Default).")]
        [SerializeField] private string sortingLayerName = "Ground";

        [Tooltip("Sorting order on the sorting layer (placed above ground tiles order 0).")]
        [SerializeField] private int sortingOrder = 5;

        [Tooltip("Offset along perpendicular axis (Z offset for 2D XY plane, Y offset for 3D XZ plane).")]
        [SerializeField] private float depthOffset = -0.01f;

        [Tooltip("Optional custom material. If null, a clean unlit sprite material is created automatically.")]
        [SerializeField] private Material customGridMaterial;

        // References
        private DungeonPlacementManager placementManager;
        private PlacementCursorController cursorController;

        // Visualizer GameObjects & components
        private GameObject linesContainer;
        private MeshFilter linesMeshFilter;
        private MeshRenderer linesMeshRenderer;
        private Mesh linesMesh;

        private GameObject hoverContainer;
        private MeshFilter hoverMeshFilter;
        private MeshRenderer hoverMeshRenderer;
        private Mesh hoverMesh;

        private Material runtimeMaterial;
        private MaterialPropertyBlock hoverPropertyBlock;
        private bool isGridVisible = true;

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");

        public bool IsGridVisible => isGridVisible;

        private void Awake()
        {
            placementManager = GetComponent<DungeonPlacementManager>();
            cursorController = GetComponent<PlacementCursorController>();
            if (cursorController == null)
            {
                cursorController = FindAnyObjectByType<PlacementCursorController>();
            }

            hoverPropertyBlock = new MaterialPropertyBlock();
            InitializeMaterial();
            BuildGridHierarchy();
        }

        private void Start()
        {
            if (placementManager != null)
            {
                placementManager.onPhaseChanged.AddListener(HandlePhaseChanged);
            }

            RebuildGridMesh();
            RebuildHoverMesh();

            // Initial visibility state based on current phase
            UpdateVisibilityState(force: true);
        }

        private void OnDestroy()
        {
            if (placementManager != null)
            {
                placementManager.onPhaseChanged.RemoveListener(HandlePhaseChanged);
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            if (linesMesh != null) Destroy(linesMesh);
            if (hoverMesh != null) Destroy(hoverMesh);
        }

        private void LateUpdate()
        {
            UpdateVisibilityState();
            UpdateHoverHighlight();
        }

        /// <summary>
        /// Creates an unlit sprite material compatible with Universal Render Pipeline and Built-in RP.
        /// </summary>
        private void InitializeMaterial()
        {
            if (customGridMaterial != null)
            {
                runtimeMaterial = new Material(customGridMaterial);
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "DungeonGrid_RuntimeMaterial",
                hideFlags = HideFlags.DontSave
            };
        }

        /// <summary>
        /// Creates child GameObjects for the lines and hover mesh.
        /// </summary>
        private void BuildGridHierarchy()
        {
            // Lines container
            linesContainer = new GameObject("Visualizer_GridLines");
            linesContainer.transform.SetParent(transform, false);

            linesMeshFilter = linesContainer.AddComponent<MeshFilter>();
            linesMeshRenderer = linesContainer.AddComponent<MeshRenderer>();
            linesMeshRenderer.sharedMaterial = runtimeMaterial;
            linesMeshRenderer.sortingLayerName = sortingLayerName;
            linesMeshRenderer.sortingOrder = sortingOrder;

            // Hover container
            hoverContainer = new GameObject("Visualizer_HoverCell");
            hoverContainer.transform.SetParent(transform, false);

            hoverMeshFilter = hoverContainer.AddComponent<MeshFilter>();
            hoverMeshRenderer = hoverContainer.AddComponent<MeshRenderer>();
            hoverMeshRenderer.sharedMaterial = runtimeMaterial;
            hoverMeshRenderer.sortingLayerName = sortingLayerName;
            hoverMeshRenderer.sortingOrder = sortingOrder + 1; // Slightly in front of lines
            hoverContainer.SetActive(false);
        }

        /// <summary>
        /// Constructs procedural quads for inner lines and border lines.
        /// </summary>
        [ContextMenu("Rebuild Grid Mesh")]
        public void RebuildGridMesh()
        {
            if (placementManager == null) return;

            Vector2Int size = placementManager.GridSize;
            float cell = placementManager.CellSize;
            Vector3 origin = placementManager.GridOrigin;
            var orientation = placementManager.PlaneOrientation;

            if (linesMesh == null)
            {
                linesMesh = new Mesh { name = "DungeonGrid_LinesMesh" };
            }
            else
            {
                linesMesh.Clear();
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color32>();

            float totalWidth = size.x * cell;
            float totalHeight = size.y * cell;

            // Cell (0,0) is centered at origin.
            // Half-extents from origin:
            float minX = origin.x - (cell * 0.5f);
            float maxX = minX + totalWidth;

            float minY, maxY;
            if (orientation == GridPlaneOrientation.XY_2D)
            {
                minY = origin.y - (cell * 0.5f);
                maxY = minY + totalHeight;
            }
            else
            {
                minY = origin.z - (cell * 0.5f);
                maxY = minY + totalHeight;
            }

            float zDepth = orientation == GridPlaneOrientation.XY_2D ? depthOffset : origin.y + depthOffset;

            // --- Vertical Grid Lines ---
            for (int x = 0; x <= size.x; x++)
            {
                bool isBorder = (x == 0 || x == size.x);
                float curX = minX + (x * cell);
                float thickness = isBorder ? borderLineWidth : innerLineWidth;
                Color32 col = isBorder ? (Color32)borderLineColor : (Color32)innerLineColor;

                Vector3 start, end;
                if (orientation == GridPlaneOrientation.XY_2D)
                {
                    start = new Vector3(curX, minY - (borderLineWidth * 0.5f), zDepth);
                    end = new Vector3(curX, maxY + (borderLineWidth * 0.5f), zDepth);
                }
                else
                {
                    start = new Vector3(curX, zDepth, minY - (borderLineWidth * 0.5f));
                    end = new Vector3(curX, zDepth, maxY + (borderLineWidth * 0.5f));
                }

                AddLineQuad(vertices, triangles, colors, start, end, thickness, col, orientation);
            }

            // --- Horizontal Grid Lines ---
            for (int y = 0; y <= size.y; y++)
            {
                bool isBorder = (y == 0 || y == size.y);
                float curY = minY + (y * cell);
                float thickness = isBorder ? borderLineWidth : innerLineWidth;
                Color32 col = isBorder ? (Color32)borderLineColor : (Color32)innerLineColor;

                Vector3 start, end;
                if (orientation == GridPlaneOrientation.XY_2D)
                {
                    start = new Vector3(minX - (borderLineWidth * 0.5f), curY, zDepth);
                    end = new Vector3(maxX + (borderLineWidth * 0.5f), curY, zDepth);
                }
                else
                {
                    start = new Vector3(minX - (borderLineWidth * 0.5f), zDepth, curY);
                    end = new Vector3(maxX + (borderLineWidth * 0.5f), zDepth, curY);
                }

                AddLineQuad(vertices, triangles, colors, start, end, thickness, col, orientation);
            }

            linesMesh.SetVertices(vertices);
            linesMesh.SetTriangles(triangles, 0);
            linesMesh.SetColors(colors);
            linesMesh.RecalculateBounds();

            linesMeshFilter.sharedMesh = linesMesh;
        }

        /// <summary>
        /// Builds the 1-cell quad mesh used for dynamic hover highlight.
        /// </summary>
        private void RebuildHoverMesh()
        {
            if (placementManager == null) return;

            float cell = placementManager.CellSize;
            float half = (cell * 0.95f) * 0.5f; // Inset slightly so grid lines remain visible
            var orientation = placementManager.PlaneOrientation;

            if (hoverMesh == null)
            {
                hoverMesh = new Mesh { name = "DungeonGrid_HoverMesh" };
            }
            else
            {
                hoverMesh.Clear();
            }

            Vector3[] v;
            if (orientation == GridPlaneOrientation.XY_2D)
            {
                v = new Vector3[]
                {
                    new Vector3(-half, -half, 0f),
                    new Vector3(half, -half, 0f),
                    new Vector3(half, half, 0f),
                    new Vector3(-half, half, 0f)
                };
            }
            else
            {
                v = new Vector3[]
                {
                    new Vector3(-half, 0f, -half),
                    new Vector3(half, 0f, -half),
                    new Vector3(half, 0f, half),
                    new Vector3(-half, 0f, half)
                };
            }

            int[] t = new int[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 }; // Double-sided
            Color32[] c = new Color32[] { Color.white, Color.white, Color.white, Color.white };

            hoverMesh.vertices = v;
            hoverMesh.triangles = t;
            hoverMesh.colors32 = c;
            hoverMesh.RecalculateBounds();

            hoverMeshFilter.sharedMesh = hoverMesh;
        }

        /// <summary>
        /// Adds a quad representing a thick line segment with both front and back-face triangles for robustness.
        /// </summary>
        private void AddLineQuad(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors,
            Vector3 p0,
            Vector3 p1,
            float thickness,
            Color32 col,
            GridPlaneOrientation orientation)
        {
            int baseIdx = vertices.Count;
            float halfThick = thickness * 0.5f;

            Vector3 normal;
            if (orientation == GridPlaneOrientation.XY_2D)
            {
                Vector3 dir = (p1 - p0).normalized;
                normal = new Vector3(-dir.y, dir.x, 0f) * halfThick;
            }
            else
            {
                Vector3 dir = (p1 - p0).normalized;
                normal = new Vector3(-dir.z, 0f, dir.x) * halfThick;
            }

            vertices.Add(p0 - normal);
            vertices.Add(p1 - normal);
            vertices.Add(p1 + normal);
            vertices.Add(p0 + normal);

            colors.Add(col);
            colors.Add(col);
            colors.Add(col);
            colors.Add(col);

            // Double-sided winding order ensures visibility regardless of camera angle or cull mode
            triangles.Add(baseIdx + 0);
            triangles.Add(baseIdx + 1);
            triangles.Add(baseIdx + 2);

            triangles.Add(baseIdx + 0);
            triangles.Add(baseIdx + 2);
            triangles.Add(baseIdx + 3);

            triangles.Add(baseIdx + 0);
            triangles.Add(baseIdx + 2);
            triangles.Add(baseIdx + 1);

            triangles.Add(baseIdx + 0);
            triangles.Add(baseIdx + 3);
            triangles.Add(baseIdx + 2);
        }

        private void HandlePhaseChanged(DungeonPhase phase)
        {
            UpdateVisibilityState(force: true);
        }

        /// <summary>
        /// Evaluates current phase and cursor interaction state to toggle visualizer elements.
        /// </summary>
        private void UpdateVisibilityState(bool force = false)
        {
            bool inCombatPhase = placementManager?.PhaseManager != null &&
                                placementManager.PhaseManager.CurrentPhase == DungeonPhase.CombatPhase;

            // Combat mode overrides and hides grid if hideGridInCombatPhase is enabled
            if (inCombatPhase && hideGridInCombatPhase)
            {
                if (force || isGridVisible) SetGridVisible(false);
                return;
            }

            bool inBuildMode;
            if (cursorController != null)
            {
                // In build mode when Build Menu is open (toggled by G), or placing, or in sell mode (E)
                inBuildMode = cursorController.IsBuildMenuOpen ||
                              cursorController.IsPlacementActive ||
                              cursorController.IsSellModeActive;
            }
            else
            {
                inBuildMode = placementManager?.PhaseManager == null ||
                              placementManager.PhaseManager.CurrentPhase == DungeonPhase.BuildPhase;
            }

            bool targetVisible = onlyShowInBuildMode ? inBuildMode : true;

            if (force || targetVisible != isGridVisible)
            {
                SetGridVisible(targetVisible);
            }
        }

        /// <summary>
        /// Explicitly toggles grid and hover visibility.
        /// </summary>
        public void SetGridVisible(bool visible)
        {
            isGridVisible = visible;
            if (linesContainer != null)
            {
                linesContainer.SetActive(visible);
            }

            if (!visible && hoverContainer != null)
            {
                hoverContainer.SetActive(false);
            }
        }

        /// <summary>
        /// Updates the hover highlight position and validation tint.
        /// </summary>
        private void UpdateHoverHighlight()
        {
            if (!showHoverHighlight || !isGridVisible || cursorController == null || placementManager == null)
            {
                if (hoverContainer != null && hoverContainer.activeSelf)
                {
                    hoverContainer.SetActive(false);
                }
                return;
            }

            if (!cursorController.IsCursorOnGrid)
            {
                if (hoverContainer.activeSelf) hoverContainer.SetActive(false);
                return;
            }

            if (!hoverContainer.activeSelf)
            {
                hoverContainer.SetActive(true);
            }

            Vector2Int hoveredCell = cursorController.HoveredCell;
            Vector3 worldPos = placementManager.GridToWorldPosition(hoveredCell);

            // Slightly closer to camera than lines mesh
            if (placementManager.PlaneOrientation == GridPlaneOrientation.XY_2D)
            {
                worldPos.z = depthOffset - 0.005f;
            }
            else
            {
                worldPos.y = placementManager.GridOrigin.y + depthOffset + 0.005f;
            }

            hoverContainer.transform.position = worldPos;

            // Pick color according to cursor context
            Color tint;
            if (cursorController.IsSellModeActive)
            {
                tint = hoverSellColor;
            }
            else if (cursorController.IsPlacementActive)
            {
                tint = cursorController.CurrentValidationResult.IsValid ? hoverValidColor : hoverInvalidColor;
            }
            else
            {
                tint = hoverIdleColor;
            }

            hoverPropertyBlock.SetColor(ColorPropId, tint);
            hoverPropertyBlock.SetColor(BaseColorPropId, tint);
            hoverMeshRenderer.SetPropertyBlock(hoverPropertyBlock);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && linesMesh != null)
            {
                RebuildGridMesh();
                RebuildHoverMesh();
            }
        }
    }
}
