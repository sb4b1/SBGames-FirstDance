using UnityEngine;

namespace SBGames.Dungeon.Markers
{
    [AddComponentMenu("SBGames/Dungeon/Safe Room Marker")]
    [RequireComponent(typeof(RoomVolume))]
    public class SafeRoomMarker : MonoBehaviour
    {
        private void Awake()
        {
            var room = GetComponent<RoomVolume>();
            if (room != null)
            {
                room.IsSafeRoom = true;
            }
        }
    }
}
