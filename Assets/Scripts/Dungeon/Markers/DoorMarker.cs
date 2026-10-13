using System;
using UnityEngine;

namespace SBGames.Dungeon.Markers
{
    public enum DoorState
    {
        Unexplored = 0,
        Explored = 1,
        Sealed = 2
    }

    [ExecuteAlways]
    [AddComponentMenu("SBGames/Dungeon/Door Marker")]
    public class DoorMarker : MonoBehaviour
    {
        [SerializeField] private int doorId;
        [SerializeField] private DoorState doorState = DoorState.Unexplored;
        [SerializeField] private bool isBossDoor;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private RoomVolume roomA;
        [SerializeField] private RoomVolume roomB;

        public int DoorId
        {
            get => doorId;
            set => doorId = value;
        }

        public DoorState State
        {
            get => doorState;
            set => doorState = value;
        }

        public bool IsBossDoor
        {
            get => isBossDoor;
            set => isBossDoor = value;
        }

        public RoomVolume RoomA
        {
            get => roomA;
            set => roomA = value;
        }

        public RoomVolume RoomB
        {
            get => roomB;
            set => roomB = value;
        }

        public Vector3 Position => interactionPoint != null ? interactionPoint.position : transform.position;

        private void OnEnable()
        {
            DungeonRoomRegistry.RegisterDoor(this);
        }

        private void OnDisable()
        {
            DungeonRoomRegistry.UnregisterDoor(this);
        }

        public RoomVolume GetOppositeRoom(RoomVolume fromRoom)
        {
            if (fromRoom == roomA) return roomB;
            if (fromRoom == roomB) return roomA;
            return roomA != null ? roomA : roomB;
        }

        [SerializeField] private Collider2D physicalBlockerCollider;
        [SerializeField] private GameObject visualObstacle;

        public event Action<DoorState> OnDoorStateChanged;
        public event Action OnDoorClosed;
        public event Action OnDoorOpened;

        public void Close()
        {
            doorState = DoorState.Sealed;
            if (physicalBlockerCollider != null)
            {
                physicalBlockerCollider.enabled = true;
            }
            if (visualObstacle != null)
            {
                visualObstacle.SetActive(true);
            }
            OnDoorClosed?.Invoke();
            OnDoorStateChanged?.Invoke(doorState);
        }

        public void Open()
        {
            doorState = DoorState.Explored;
            if (physicalBlockerCollider != null)
            {
                physicalBlockerCollider.enabled = false;
            }
            if (visualObstacle != null)
            {
                visualObstacle.SetActive(false);
            }
            OnDoorOpened?.Invoke();
            OnDoorStateChanged?.Invoke(doorState);
        }

        public void MarkExplored()
        {
            if (doorState != DoorState.Sealed)
            {
                doorState = DoorState.Explored;
                OnDoorStateChanged?.Invoke(doorState);
            }
        }
    }
}
