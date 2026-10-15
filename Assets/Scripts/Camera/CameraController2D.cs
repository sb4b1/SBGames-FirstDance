using System;
using System.Collections.Generic;
using SBGames.Dungeon.Markers;
using SBGames.Dungeon.Unity.Pieces;
using SBGames.Placement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SBGames.Cameras
{
    /// <summary>
    /// Orthographic 2D camera controller providing right-click drag panning and scroll wheel zooming.
    /// Clamps position viewport within dungeon arena bounds retrieved dynamically from
    /// DungeonRoomRegistry, DungeonPiece objects, or DungeonPlacementManager.
    /// Distinguishes between pan drags and stationary right clicks so placement cancellation/selling
    /// continues to function seamlessly.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraController2D : MonoBehaviour
    {
        public static CameraController2D Instance { get; private set; }

        [Header("Pan Settings")]
        [Tooltip("Minimum pixel movement before RMB hold is treated as a camera drag.")]
        [SerializeField] private float dragThresholdPixels = 5f;

        [Tooltip("Multiplier applied to pan drag motion.")]
        [SerializeField] private float panSensitivity = 1f;

        [Header("Zoom Settings")]
        [Tooltip("Minimum allowed orthographic size (closest zoom).")]
        [SerializeField] private float minZoom = 10f;

        [Tooltip("Maximum allowed orthographic size (furthest zoom).")]
        [SerializeField] private float maxZoom = 60f;

        [Tooltip("Zoom sensitivity multiplier for scroll wheel.")]
        [SerializeField] private float zoomSensitivity = 5f;

        [Tooltip("Zoom smoothing speed. Set high (e.g. 20) for near-instant response.")]
        [SerializeField] private float zoomSmoothSpeed = 15f;

        [Header("Bounds & Clamping")]
        [Tooltip("Automatically clamp camera viewport to detected dungeon arena bounds.")]
        [SerializeField] private bool clampToArenaBounds = true;

        [Tooltip("Margin padding in world units added beyond the dungeon geometry edges (allows panning beyond borders).")]
        [SerializeField] private Vector2 boundsPadding = new Vector2(16f, 16f);

        [Tooltip("Fallback manual bounds if no dungeon rooms or pieces are present in the scene.")]
        [SerializeField] private Rect fallbackBounds = new Rect(0f, 0f, 80f, 80f);

        private Camera _targetCamera;
        private float _targetZoom;
        private bool _isPotentialDrag;
        private bool _isDragging;
        private bool _hasDraggedThisPress;
        private Vector2 _dragStartMousePos;
        private Vector2 _lastMousePos;

        /// <summary>
        /// True while the user is actively dragging the camera with RMB.
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// True if the current or most recent RMB press exceeded the drag threshold.
        /// Checked on RMB release by placement/cursor controllers to avoid cancelling/selling.
        /// </summary>
        public bool HasDraggedThisPress => _hasDraggedThisPress;

        /// <summary>
        /// Margin padding in world units beyond dungeon geometry edges.
        /// </summary>
        public Vector2 BoundsPadding
        {
            get => boundsPadding;
            set => boundsPadding = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _targetCamera = GetComponent<Camera>();
            if (_targetCamera != null && _targetCamera.orthographic)
            {
                _targetZoom = _targetCamera.orthographicSize;
            }
        }

        private void Start()
        {
            // Center the camera on the dungeon initially if bounds are detected
            if (clampToArenaBounds && _targetCamera != null)
            {
                Rect bounds = GetCurrentBounds();
                Vector3 centerPos = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
                transform.position = centerPos;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (_targetCamera == null) return;

            HandlePanInput();
            HandleZoomInput();
            ClampPosition();
        }

        #region Pan Logic

        private void HandlePanInput()
        {
            Vector2 currentMousePos = GetMouseScreenPosition();

            // 1. Right Mouse Button Down
            if (IsRightMouseButtonDown())
            {
                // Gate: Block camera drag if the cursor started over an interactive UI element
                if (IsPointerOverUI())
                {
                    _isPotentialDrag = false;
                    _isDragging = false;
                    _hasDraggedThisPress = false;
                    return;
                }

                _isPotentialDrag = true;
                _isDragging = false;
                _hasDraggedThisPress = false;
                _dragStartMousePos = currentMousePos;
                _lastMousePos = currentMousePos;
            }

            // 2. Right Mouse Button Held
            if (IsRightMouseButtonHeld() && _isPotentialDrag)
            {
                if (!_isDragging)
                {
                    if (Vector2.Distance(currentMousePos, _dragStartMousePos) >= dragThresholdPixels)
                    {
                        _isDragging = true;
                        _hasDraggedThisPress = true;
                        _lastMousePos = currentMousePos;
                    }
                }

                if (_isDragging)
                {
                    Vector2 mouseDelta = currentMousePos - _lastMousePos;
                    _lastMousePos = currentMousePos;

                    if (mouseDelta.sqrMagnitude > 0.0001f)
                    {
                        // "Grab World" 1:1 calculation:
                        // Convert screen pixel delta to world units based on orthographic size & screen height
                        float worldUnitsPerPixel = (_targetCamera.orthographicSize * 2f) / Screen.height;
                        Vector3 worldDelta = new Vector3(
                            mouseDelta.x * worldUnitsPerPixel * panSensitivity,
                            mouseDelta.y * worldUnitsPerPixel * panSensitivity,
                            0f
                        );

                        // Moving mouse right/up pulls the world under cursor, so camera moves opposite
                        transform.position -= worldDelta;
                    }
                }
            }

            // 3. Right Mouse Button Released
            if (IsRightMouseButtonUp())
            {
                _isPotentialDrag = false;
                _isDragging = false;
                // Note: _hasDraggedThisPress remains true this frame so listeners can query it on mouse release
            }
        }

        #endregion

        #region Zoom Logic

        private void HandleZoomInput()
        {
            // Do not zoom if mouse is over UI
            if (IsPointerOverUI()) return;

            Vector2 mousePos = GetMouseScreenPosition();
            float scrollY = GetMouseScrollDelta();
            if (Mathf.Abs(scrollY) > 0.001f)
            {
                _targetZoom -= scrollY * zoomSensitivity;
                _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
            }

            if (_targetCamera.orthographic)
            {
                float currentSize = _targetCamera.orthographicSize;
                float newSize = Mathf.Lerp(
                    currentSize,
                    _targetZoom,
                    Time.unscaledDeltaTime * zoomSmoothSpeed
                );

                float sizeDelta = currentSize - newSize;
                if (Mathf.Abs(sizeDelta) > 0.0001f)
                {
                    // Zoom towards mouse cursor:
                    // Maintain the world point currently under the mouse cursor
                    float halfHeightPixels = Screen.height * 0.5f;
                    float halfWidthPixels = Screen.width * 0.5f;

                    if (halfHeightPixels > 0f)
                    {
                        float ux = (mousePos.x - halfWidthPixels) / halfHeightPixels;
                        float uy = (mousePos.y - halfHeightPixels) / halfHeightPixels;

                        Vector3 pos = transform.position;
                        pos.x += ux * sizeDelta;
                        pos.y += uy * sizeDelta;
                        transform.position = pos;
                    }

                    _targetCamera.orthographicSize = newSize;
                }
            }
        }

        #endregion

        #region Bounds Clamping

        /// <summary>
        /// Clamps the camera position so the view remains centered within the dungeon bounds + margin padding.
        /// Operates consistently at all zoom levels without sudden locking or snapping.
        /// </summary>
        private void ClampPosition()
        {
            if (!clampToArenaBounds) return;

            Rect worldBounds = GetCurrentBounds();
            Vector3 pos = transform.position;

            float minX = worldBounds.xMin - boundsPadding.x;
            float maxX = worldBounds.xMax + boundsPadding.x;
            float minY = worldBounds.yMin - boundsPadding.y;
            float maxY = worldBounds.yMax + boundsPadding.y;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            transform.position = pos;
        }

        /// <summary>
        /// Retrieves the active arena/dungeon bounds in world space across available sources:
        /// 1. DungeonRoomRegistry registered room volumes
        /// 2. Active DungeonPiece components
        /// 3. DungeonPlacementManager grid
        /// 4. Inspector fallback bounds
        /// </summary>
        public Rect GetCurrentBounds()
        {
            // Source 1: Registered room volumes in DungeonRoomRegistry
            var rooms = DungeonRoomRegistry.Rooms;
            if (rooms != null && rooms.Count > 0)
            {
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                int validCount = 0;

                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    if (r == null) continue;
                    Bounds rb = r.RoomBounds;
                    if (rb.size.sqrMagnitude > 0.01f)
                    {
                        min = Vector2.Min(min, new Vector2(rb.min.x, rb.min.y));
                        max = Vector2.Max(max, new Vector2(rb.max.x, rb.max.y));
                        validCount++;
                    }
                }

                if (validCount > 0)
                {
                    return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                }
            }

            // Source 2: Active DungeonPiece instances in the scene
            var pieces = FindObjectsByType<DungeonPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (pieces != null && pieces.Length > 0)
            {
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                int validCount = 0;

                for (int i = 0; i < pieces.Length; i++)
                {
                    var p = pieces[i];
                    if (p == null) continue;
                    var renderers = p.GetComponentsInChildren<Renderer>();
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        Bounds b = renderers[r].bounds;
                        min = Vector2.Min(min, new Vector2(b.min.x, b.min.y));
                        max = Vector2.Max(max, new Vector2(b.max.x, b.max.y));
                        validCount++;
                    }
                }

                if (validCount > 0)
                {
                    return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                }
            }

            // Source 3: DungeonPlacementManager grid
            if (DungeonPlacementManager.Instance != null && DungeonPlacementManager.Instance.GridSize.sqrMagnitude > 0)
            {
                Vector3 origin = DungeonPlacementManager.Instance.GridOrigin;
                Vector2Int size = DungeonPlacementManager.Instance.GridSize;
                float cellSize = DungeonPlacementManager.Instance.CellSize;
                return new Rect(origin.x, origin.y, size.x * cellSize, size.y * cellSize);
            }

            return fallbackBounds;
        }

        private void OnDrawGizmosSelected()
        {
            Rect bounds = GetCurrentBounds();
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            Vector3 size = new Vector3(bounds.width + boundsPadding.x * 2f, bounds.height + boundsPadding.y * 2f, 1f);
            Gizmos.DrawWireCube(center, size);
        }

        #endregion

        #region Input Helpers

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private float GetMouseScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float rawY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(rawY) > 0f)
                {
                    return Mathf.Sign(rawY);
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            float legacy = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(legacy) > 0.001f)
            {
                return Mathf.Sign(legacy);
            }
#endif
            return 0f;
        }

        private bool IsRightMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private bool IsRightMouseButtonHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private bool IsRightMouseButtonUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.wasReleasedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonUp(1);
#else
            return false;
#endif
        }

        #endregion
    }
}
