using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Dungeon.Markers
{
    [ExecuteAlways]
    [AddComponentMenu("SBGames/Dungeon/Room Volume")]
    public class RoomVolume : MonoBehaviour
    {
        [SerializeField] private int roomId;
        [SerializeField] private Bounds bounds;
        [SerializeField] private bool isSafeRoom;
        [SerializeField] private bool isBossRoom;
        [SerializeField] private bool isHazardous;
        [SerializeField] private List<DoorMarker> exits = new List<DoorMarker>();

        public int RoomId
        {
            get => roomId;
            set => roomId = value;
        }

        public Bounds RoomBounds => bounds;

        public bool IsSafeRoom
        {
            get => isSafeRoom;
            set => isSafeRoom = value;
        }

        public bool IsBossRoom
        {
            get => isBossRoom;
            set => isBossRoom = value;
        }

        public bool IsHazardous
        {
            get => isHazardous;
            set => isHazardous = value;
        }

        public IReadOnlyList<DoorMarker> Exits => exits;

        public void AddExit(DoorMarker door)
        {
            if (door != null && !exits.Contains(door))
            {
                exits.Add(door);
            }
        }

        public void SetBounds(Bounds newBounds)
        {
            bounds = newBounds;
        }

        private void OnEnable()
        {
            if (bounds.size == Vector3.zero)
            {
                var col2d = GetComponent<Collider2D>();
                if (col2d != null)
                {
                    bounds = col2d.bounds;
                }
                else
                {
                    bounds = new Bounds(transform.position, Vector3.one * 10f);
                }
            }
            DungeonRoomRegistry.RegisterRoom(this);
        }

        private void OnDisable()
        {
            DungeonRoomRegistry.UnregisterRoom(this);
        }

        public bool Contains(Vector3 position)
        {
            return bounds.Contains(position);
        }
    }
}
