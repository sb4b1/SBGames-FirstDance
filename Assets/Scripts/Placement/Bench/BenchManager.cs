using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Placement.Bench
{
    /// <summary>
    /// Manages the player's unit and trap bench (reserve storage outside the combat grid).
    /// Provides methods for adding, removing, and swapping bench units at zero gold cost during Planning.
    /// </summary>
    [DisallowMultipleComponent]
    public class BenchManager : MonoBehaviour
    {
        public static BenchManager Instance { get; private set; }

        [Header("Bench Configuration")]
        [SerializeField, Min(1)] private int benchCapacity = 8;
        [SerializeField] private Vector3 benchOrigin = new Vector3(-4f, -4f, 0f);
        [SerializeField] private float slotSpacing = 1.2f;
        [SerializeField] private bool horizontalLayout = true;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private List<BenchSlot> slots = new();

        [Header("Unity Events for UI")]
        public UnityEvent onBenchChanged = new();
        public UnityEvent<int, PlacedUnitInstance> onUnitAddedToBench = new();
        public UnityEvent<int, PlacedUnitInstance> onUnitRemovedFromBench = new();

        public int Capacity
        {
            get
            {
                if (slots == null || slots.Count == 0) InitializeSlots();
                return benchCapacity;
            }
        }

        public IReadOnlyList<BenchSlot> Slots
        {
            get
            {
                if (slots == null || slots.Count == 0) InitializeSlots();
                return slots;
            }
        }

        public int OccupiedCount
        {
            get
            {
                var currentSlots = Slots;
                int count = 0;
                for (int i = 0; i < currentSlots.Count; i++)
                {
                    if (!currentSlots[i].IsEmpty) count++;
                }
                return count;
            }
        }
        public bool HasFreeSlot => OccupiedCount < benchCapacity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;

            InitializeSlots();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void InitializeSlots()
        {
            if (slots == null) slots = new List<BenchSlot>();
            if (slots.Count == benchCapacity) return;

            slots.Clear();
            for (int i = 0; i < benchCapacity; i++)
            {
                slots.Add(new BenchSlot(i));
            }
        }

        /// <summary>
        /// Attempts to add a unit to the first available bench slot.
        /// </summary>
        public bool TryAddToBench(PlacedUnitInstance unit, out int assignedSlotIndex)
        {
            assignedSlotIndex = -1;
            if (unit == null) return false;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i].Assign(unit);
                    assignedSlotIndex = i;

                    if (unit.GameObjectInstance != null)
                    {
                        unit.GameObjectInstance.transform.position = GetSlotWorldPosition(i);
                    }

                    onUnitAddedToBench?.Invoke(i, unit);
                    onBenchChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Assigns a unit to a specific bench slot index. If the slot was occupied, returns the previous unit.
        /// </summary>
        public bool TryAssignToSlot(int slotIndex, PlacedUnitInstance unit, out PlacedUnitInstance previousUnit)
        {
            previousUnit = null;
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;

            previousUnit = slots[slotIndex].Clear();
            slots[slotIndex].Assign(unit);

            if (unit != null && unit.GameObjectInstance != null)
            {
                unit.GameObjectInstance.transform.position = GetSlotWorldPosition(slotIndex);
            }

            if (previousUnit != null)
            {
                onUnitRemovedFromBench?.Invoke(slotIndex, previousUnit);
            }

            if (unit != null)
            {
                onUnitAddedToBench?.Invoke(slotIndex, unit);
            }

            onBenchChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes and retrieves the unit occupying the specified bench slot.
        /// </summary>
        public bool TryRemoveFromBench(int slotIndex, out PlacedUnitInstance unit)
        {
            unit = null;
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;
            if (slots[slotIndex].IsEmpty) return false;

            unit = slots[slotIndex].Clear();
            onUnitRemovedFromBench?.Invoke(slotIndex, unit);
            onBenchChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Swaps units between two bench slots.
        /// </summary>
        public bool SwapBenchSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= slots.Count || indexB < 0 || indexB >= slots.Count) return false;
            if (indexA == indexB) return true;

            var unitA = slots[indexA].UnitInstance;
            var unitB = slots[indexB].UnitInstance;

            slots[indexA].Assign(unitB);
            slots[indexB].Assign(unitA);

            if (unitA != null && unitA.GameObjectInstance != null)
            {
                unitA.GameObjectInstance.transform.position = GetSlotWorldPosition(indexB);
            }

            if (unitB != null && unitB.GameObjectInstance != null)
            {
                unitB.GameObjectInstance.transform.position = GetSlotWorldPosition(indexA);
            }

            onBenchChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Gets the world space position for a given bench slot index.
        /// </summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (horizontalLayout)
            {
                return benchOrigin + new Vector3(slotIndex * slotSpacing, 0f, 0f);
            }
            return benchOrigin + new Vector3(0f, slotIndex * slotSpacing, 0f);
        }

        /// <summary>
        /// Finds the nearest bench slot index to the given world position. Returns -1 if beyond threshold.
        /// </summary>
        public int GetNearestSlotIndex(Vector3 worldPos, float maxDistance = 1.0f)
        {
            int nearest = -1;
            float minSqrDist = maxDistance * maxDistance;

            for (int i = 0; i < slots.Count; i++)
            {
                Vector3 slotPos = GetSlotWorldPosition(i);
                float sqrDist = (slotPos - worldPos).sqrMagnitude;
                if (sqrDist <= minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = i;
                }
            }

            return nearest;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 0.5f);
            for (int i = 0; i < benchCapacity; i++)
            {
                Vector3 pos = GetSlotWorldPosition(i);
                Gizmos.DrawWireSphere(pos, 0.4f);
            }
        }
    }
}
