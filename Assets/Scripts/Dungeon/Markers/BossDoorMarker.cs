using UnityEngine;

namespace SBGames.Dungeon.Markers
{
    [AddComponentMenu("SBGames/Dungeon/Boss Door Marker")]
    [RequireComponent(typeof(DoorMarker))]
    public class BossDoorMarker : MonoBehaviour
    {
        private void Awake()
        {
            var door = GetComponent<DoorMarker>();
            if (door != null)
            {
                door.IsBossDoor = true;
            }
        }
    }
}
