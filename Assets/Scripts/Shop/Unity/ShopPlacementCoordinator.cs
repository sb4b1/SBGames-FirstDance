using System;
using System.Collections.Generic;
using SBGames.Loop;
using SBGames.Loop.Integration;
using SBGames.Placement;
using SBGames.Shop.Core;
using SBGames.Shop.Data;
using SBGames.Shop.Mock;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Shop.Unity
{
    /// <summary>
    /// Backend coordinator mediating interaction between the Monster Shop system and the Dungeon Placement engine.
    /// Manages shop lifecycle, energy wallet sharing, phase transitions, and monster GameObject instantiation/decoration.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopPlacementCoordinator : MonoBehaviour
    {
        public static ShopPlacementCoordinator Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private DungeonPlacementManager placementManager;
        [SerializeField] private PlacementCursorController cursorController;
        [SerializeField] private DungeonMaster dungeonMaster;
        [SerializeField] private CoreGameLoop coreGameLoop;

        [Header("Shop Settings")]
        [SerializeField] private ShopConfig shopConfig = new ShopConfig(5, 2, 0.5f);
        [SerializeField] private bool autoRefreshOnPlanningPhase = true;

        [Header("Unity Events for UI & Audio")]
        public UnityEvent<MonsterShopItem, PlacedUnitInstance> onShopMonsterSummoned;
        public UnityEvent<IReadOnlyList<ShopSlot>, bool> onShopRefreshed;

        // Core systems
        public MonsterShopSystem Shop { get; private set; }
        public IMonsterCatalog Catalog { get; private set; }
        public IResourceWallet SharedWallet { get; private set; }

        // Pending placement state
        private int _pendingSlotIndex = -1;
        private MonsterShopItem _pendingShopItem;

        public MonsterShopItem PendingShopItem => _pendingShopItem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ResolveDependencies();
            InitializeShop();
        }

        private void Start()
        {
            // Initial refresh for starting round
            if (autoRefreshOnPlanningPhase && Shop != null)
            {
                int startRound = coreGameLoop != null ? coreGameLoop.CurrentRound : 1;
                DungeonRank startRank = DungeonRank.E;
                Shop.TryRefresh(startRound, startRank, isManualReroll: false, out _);
            }
        }

        private void OnDestroy()
        {
            if (placementManager != null && placementManager.System != null)
            {
                placementManager.System.OnUnitPlaced -= HandlePlacementSystemUnitPlaced;
            }

            if (placementManager != null)
            {
                placementManager.onPhaseChanged.RemoveListener(HandlePhaseChanged);
            }
        }

        private void Update()
        {
            // If user deselected placement or entered sell mode, cancel pending shop summon
            if (_pendingShopItem != null && cursorController != null)
            {
                if (!cursorController.IsPlacementActive || cursorController.CurrentUnit != _pendingShopItem)
                {
                    CancelPendingSummon();
                }
            }
        }

        private void ResolveDependencies()
        {
            if (placementManager == null) placementManager = FindAnyObjectByType<DungeonPlacementManager>();
            if (cursorController == null) cursorController = FindAnyObjectByType<PlacementCursorController>();
            if (dungeonMaster == null) dungeonMaster = FindAnyObjectByType<DungeonMaster>();
            if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
        }

        private void InitializeShop()
        {
            Catalog = new MockMonsterCatalog();

            // Create shared wallet connected directly to DungeonMaster Energy
            if (dungeonMaster != null)
            {
                SharedWallet = new DungeonMasterEnergyWallet(dungeonMaster);
            }
            else if (placementManager != null && placementManager.DungeonMaster != null)
            {
                SharedWallet = new DungeonMasterEnergyWallet(placementManager.DungeonMaster);
            }
            else
            {
                SharedWallet = new ResourceWallet(initialResources: 50);
            }

            IDungeonPhaseManager phaseMgr = placementManager != null ? placementManager.PhaseManager : null;

            Shop = new MonsterShopSystem(
                catalog: Catalog,
                wallet: SharedWallet,
                config: shopConfig,
                rng: new SystemRandomProvider(),
                phaseManager: phaseMgr
            );

            // Hook internal shop events
            Shop.OnShopRefreshed += (slots, isManual) => onShopRefreshed?.Invoke(slots, isManual);

            // Hook placement manager events
            if (placementManager != null)
            {
                placementManager.onPhaseChanged.AddListener(HandlePhaseChanged);

                if (placementManager.System != null)
                {
                    placementManager.System.OnUnitPlaced += HandlePlacementSystemUnitPlaced;
                }
            }
        }

        private void HandlePhaseChanged(DungeonPhase phase)
        {
            if (phase == DungeonPhase.BuildPhase && autoRefreshOnPlanningPhase)
            {
                int round = coreGameLoop != null ? coreGameLoop.CurrentRound : Shop.CurrentRound + 1;
                DungeonRank rank = DungeonRank.E;
                Shop.TryRefresh(round, rank, isManualReroll: false, out _);
            }
            else if (phase == DungeonPhase.CombatPhase)
            {
                CancelPendingSummon();
            }
        }

        /// <summary>
        /// Initiates the summoning workflow for a monster in the shop.
        /// Enters placement mode with the monster's ghost preview without deducting currency yet.
        /// </summary>
        /// <param name="slotIndex">Index of the shop slot to summon.</param>
        public bool SelectShopSlotForSummon(int slotIndex)
        {
            if (Shop == null || cursorController == null) return false;

            var val = Shop.ValidatePurchase(slotIndex);
            if (!val.IsValid)
            {
                Debug.LogWarning($"[ShopPlacementCoordinator] Cannot summon from slot {slotIndex}: {val.Message}");
                return false;
            }

            var slot = Shop.Slots[slotIndex];
            _pendingSlotIndex = slotIndex;
            _pendingShopItem = slot.CurrentItem;

            // Hand off to PlacementCursorController to show ghost & handle grid clicks
            cursorController.SelectUnit(_pendingShopItem);
            return true;
        }

        /// <summary>
        /// Cancels active pending shop summon without deducting currency or consuming slot.
        /// </summary>
        public void CancelPendingSummon()
        {
            _pendingSlotIndex = -1;
            _pendingShopItem = null;
        }

        /// <summary>
        /// Triggered when DungeonPlacementSystem successfully places any unit.
        /// If the unit placed was our pending shop monster, marks slot as purchased and decorates the instance.
        /// </summary>
        private void HandlePlacementSystemUnitPlaced(PlacedUnitInstance placedUnit)
        {
            if (_pendingShopItem != null && placedUnit != null && placedUnit.Definition is MonsterShopItem shopItem)
            {
                if (_pendingSlotIndex >= 0 && _pendingSlotIndex < Shop.Slots.Count)
                {
                    // Mark shop slot as purchased
                    var slot = Shop.Slots[_pendingSlotIndex];
                    if (slot.CurrentItem == shopItem)
                    {
                        slot.MarkPurchased();
                    }
                }

                // If no prefab was attached to the definition, create a stylized visual placeholder
                if (placedUnit.GameObjectInstance == null && placementManager != null)
                {
                    GameObject placeholder = CreateFallbackMonsterVisual(shopItem, placedUnit.GridPosition);
                    placedUnit.GameObjectInstance = placeholder;
                }

                // Attach runtime monster instance metadata
                if (placedUnit.GameObjectInstance != null)
                {
                    var runtimeComp = placedUnit.GameObjectInstance.GetComponent<MonsterRuntimeInstance>();
                    if (runtimeComp == null)
                    {
                        runtimeComp = placedUnit.GameObjectInstance.AddComponent<MonsterRuntimeInstance>();
                    }
                    runtimeComp.Initialize(shopItem);
                }

                onShopMonsterSummoned?.Invoke(shopItem, placedUnit);
                CancelPendingSummon();
            }
        }

        private GameObject CreateFallbackMonsterVisual(MonsterShopItem item, Vector2Int gridPos)
        {
            Vector3 worldPos = placementManager.GridToWorldPosition(gridPos);
            Transform parent = placementManager.transform;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Monster_{item.DisplayName}_{item.Rank}_L{item.Level}";
            go.transform.position = worldPos;
            go.transform.SetParent(parent);

            if (placementManager.PlaneOrientation == GridPlaneOrientation.XY_2D)
            {
                go.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                go.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && ColorUtility.TryParseHtmlString(item.Rank.GetColorHex(), out Color rankCol))
            {
                renderer.material.color = rankCol;
            }

            return go;
        }
    }
}
