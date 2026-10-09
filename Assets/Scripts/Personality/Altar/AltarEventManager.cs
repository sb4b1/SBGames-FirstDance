using System;
using System.Collections.Generic;
using SBGames.Events;
using SBGames.Events.Types;
using SBGames.Loop;
using SBGames.Personality.Data;
using SBGames.Personality.UI;
using SBGames.Placement;
using SBGames.Placement.Bench;
using SBGames.Shop.Unity;
using UnityEngine;

namespace SBGames.Personality.Altar
{
    /// <summary>
    /// Model wrapping a player-owned unit from either the Dungeon Grid or Bench for Altar selection.
    /// </summary>
    public class AltarUnitTarget
    {
        public string DisplayName { get; }
        public bool IsOnGrid { get; }
        public Vector2Int GridPosition { get; }
        public int BenchSlotIndex { get; }
        public CharacterPersonality Personality { get; }
        public MonsterRuntimeInstance RuntimeInstance { get; }
        public PlacedUnitInstance PlacedUnit { get; }

        public RaceInstinctSO Race => Personality != null ? Personality.RaceInstinct : RuntimeInstance != null ? RuntimeInstance.RaceInstinct : null;
        public CharacterTraitSO CurrentTrait => Personality != null ? Personality.ActiveTrait : RuntimeInstance != null ? RuntimeInstance.ActiveTrait : null;

        public AltarUnitTarget(
            string displayName,
            bool isOnGrid,
            Vector2Int gridPosition,
            int benchSlotIndex,
            CharacterPersonality personality,
            MonsterRuntimeInstance runtimeInstance,
            PlacedUnitInstance placedUnit = null)
        {
            DisplayName = displayName;
            IsOnGrid = isOnGrid;
            GridPosition = gridPosition;
            BenchSlotIndex = benchSlotIndex;
            Personality = personality;
            RuntimeInstance = runtimeInstance;
            PlacedUnit = placedUnit;
        }
    }

    /// <summary>
    /// Coordinates Altar ritual events:
    /// - Aggregates all player units across Grid and Bench.
    /// - Manages Blood vs. Arcane cost determination.
    /// - Rolls 1-of-3 trait draft choices.
    /// - Applies immediate trait transmutation to the unit's runtime instance and AI.
    /// </summary>
    [DisallowMultipleComponent]
    public class AltarEventManager : MonoBehaviour
    {
        public static AltarEventManager Instance { get; private set; }

        [Header("Runtime State (Read Only)")]
        [SerializeField] private AltarEventSO _currentConfig;
        [SerializeField] private AltarCurrencyType _activeCurrencyType;
        [SerializeField] private int _activeCost;
        private readonly List<AltarUnitTarget> _availableUnits = new();
        private AltarUnitTarget _selectedUnit;
        private List<CharacterTraitSO> _draftedTraits = new();
        private Action _onCompleteCallback;

        public AltarEventSO CurrentConfig => _currentConfig;
        public AltarCurrencyType ActiveCurrencyType => _activeCurrencyType;
        public int ActiveCost => _activeCost;
        public IReadOnlyList<AltarUnitTarget> AvailableUnits => _availableUnits;
        public AltarUnitTarget SelectedUnit => _selectedUnit;
        public IReadOnlyList<CharacterTraitSO> DraftedTraits => _draftedTraits;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Initiates the Altar ritual session from an AltarEventSO trigger.
        /// </summary>
        public void OpenAltar(AltarEventSO config, EventTriggerContext ctx, Action onComplete)
        {
            _currentConfig = config;
            _onCompleteCallback = onComplete;

            // 1. Determine Altar Type (Blood vs Arcane)
            float roll = UnityEngine.Random.value;
            _activeCurrencyType = roll <= config.BloodAltarChance ? AltarCurrencyType.Blood : AltarCurrencyType.Arcane;
            _activeCost = _activeCurrencyType == AltarCurrencyType.Blood ? config.BloodCost : config.EnergyCost;

            // 2. Scan and aggregate all owned units from Grid & Bench
            AggregateOwnedUnits(ctx);

            if (_availableUnits.Count == 0)
            {
                Debug.LogWarning("[AltarEventManager] No owned units available for Altar transmutation.");
                onComplete?.Invoke();
                return;
            }

            // 3. Select first unit and roll initial draft
            SelectUnit(_availableUnits[0]);

            // 4. Show UI Toolkit modal
            if (AltarEventUIController.Instance != null)
            {
                AltarEventUIController.Instance.OpenModal(this);
            }
            else
            {
                Debug.LogWarning("[AltarEventManager] AltarEventUIController.Instance not found!");
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Selects a specific unit in the Altar and rolls 3 fresh traits for it.
        /// </summary>
        public void SelectUnit(AltarUnitTarget unit)
        {
            if (unit == null) return;
            _selectedUnit = unit;

            // Roll draft traits for this unit
            if (_currentConfig != null && _currentConfig.PersonalityDatabase != null)
            {
                _draftedTraits = _currentConfig.PersonalityDatabase.RollDraftTraits(
                    _currentConfig.DraftOptionCount,
                    unit.CurrentTrait
                );
            }
            else
            {
                _draftedTraits = new List<CharacterTraitSO>();
            }

            if (AltarEventUIController.Instance != null && AltarEventUIController.Instance.IsOpen)
            {
                AltarEventUIController.Instance.RefreshView();
            }
        }

        private void AggregateOwnedUnits(EventTriggerContext ctx)
        {
            _availableUnits.Clear();

            // A. Scan Grid Units
            if (ctx.PlacementManager != null && ctx.PlacementManager.OccupancyTracker != null)
            {
                var placedUnits = ctx.PlacementManager.OccupancyTracker.GetAllPlacedUnits();
                foreach (var placed in placedUnits)
                {
                    if (placed == null || placed.GameObjectInstance == null) continue;

                    var pers = placed.GameObjectInstance.GetComponent<CharacterPersonality>();
                    var runtime = placed.GameObjectInstance.GetComponent<MonsterRuntimeInstance>();

                    if (pers == null && runtime != null)
                    {
                        pers = placed.GameObjectInstance.AddComponent<CharacterPersonality>();
                        pers.Initialize(runtime.RaceInstinct, runtime.ActiveTrait);
                    }

                    if (pers != null)
                    {
                        string name = placed.Definition != null ? placed.Definition.DisplayName : placed.GameObjectInstance.name;
                        _availableUnits.Add(new AltarUnitTarget(name, true, placed.GridPosition, -1, pers, runtime, placed));
                    }
                }
            }

            // B. Scan Bench Units
            if (ctx.BenchManager != null)
            {
                var slots = ctx.BenchManager.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.IsEmpty || slot.UnitInstance == null) continue;

                    var placed = slot.UnitInstance;
                    var go = placed.GameObjectInstance;
                    if (go == null) continue;

                    var pers = go.GetComponent<CharacterPersonality>();
                    var runtime = go.GetComponent<MonsterRuntimeInstance>();

                    if (pers == null && runtime != null)
                    {
                        pers = go.AddComponent<CharacterPersonality>();
                        pers.Initialize(runtime.RaceInstinct, runtime.ActiveTrait);
                    }

                    if (pers != null)
                    {
                        string name = placed.Definition != null ? placed.Definition.DisplayName : go.name;
                        _availableUnits.Add(new AltarUnitTarget(name, false, Vector2Int.zero, i, pers, runtime, placed));
                    }
                }
            }
        }

        /// <summary>
        /// Validates if the player can afford the altar cost.
        /// </summary>
        public bool CanAffordCost(out string failureReason)
        {
            var dm = DungeonMaster.Instance ?? FindAnyObjectByType<DungeonMaster>();
            if (dm == null)
            {
                failureReason = "Dungeon Master unavailable";
                return false;
            }

            if (_activeCurrencyType == AltarCurrencyType.Blood)
            {
                if (dm.Essence <= _activeCost)
                {
                    failureReason = $"Requires {_activeCost} Essence (Sacrifice would be fatal)";
                    return false;
                }
            }
            else
            {
                if (dm.Energy < _activeCost)
                {
                    failureReason = $"Requires {_activeCost} Energy (Current: {dm.Energy})";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies the selected trait to the unit and deducts resources.
        /// If keepCurrentTrait is true, completes without deducting cost.
        /// </summary>
        public bool ConfirmTransmutation(CharacterTraitSO chosenTrait, bool keepCurrentTrait)
        {
            if (_selectedUnit == null) return false;

            if (keepCurrentTrait)
            {
                CloseAndResolve();
                return true;
            }

            if (chosenTrait == null) return false;

            if (!CanAffordCost(out var reason))
            {
                Debug.LogWarning($"[AltarEventManager] Cannot confirm: {reason}");
                return false;
            }

            var dm = DungeonMaster.Instance;
            if (dm != null)
            {
                if (_activeCurrencyType == AltarCurrencyType.Blood)
                {
                    dm.LoseEssence(_activeCost);
                }
                else
                {
                    dm.SpendEnergy(_activeCost);
                }
            }

            // Apply immediately to Personality and MonsterRuntimeInstance
            _selectedUnit.Personality.SetTrait(chosenTrait);
            if (_selectedUnit.RuntimeInstance != null)
            {
                _selectedUnit.RuntimeInstance.UpdateTrait(chosenTrait);
            }

            Debug.Log($"[AltarEventManager] Successfully transmuted {_selectedUnit.DisplayName}'s trait to {chosenTrait.DisplayName}!");

            CloseAndResolve();
            return true;
        }

        /// <summary>
        /// Skips the Altar event without deducting any resources.
        /// </summary>
        public void SkipAltar()
        {
            CloseAndResolve();
        }

        private void CloseAndResolve()
        {
            if (AltarEventUIController.Instance != null)
            {
                AltarEventUIController.Instance.CloseModal();
            }

            var cb = _onCompleteCallback;
            _onCompleteCallback = null;
            cb?.Invoke();
        }
    }
}
