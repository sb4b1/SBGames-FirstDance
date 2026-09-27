using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// ScriptableObject asset defining a placeable unit type for the dungeon builder.
    /// Designers can author unit costs, capacity footprints, and associated prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUnitPlacementDefinition", menuName = "SBGames/Placement/Unit Placement Definition", order = 10)]
    public class UnitPlacementDefinitionSO : ScriptableObject, IUnitPlacementDefinition
    {
        [Header("Identity")]
        [SerializeField] private string unitId = "unit_id";
        [SerializeField] private string displayName = "Unit Name";

        [Header("Placement Constraints")]
        [Tooltip("Resource cost (gold, soul essence, etc.) deducted upon placement.")]
        [SerializeField, Min(0)] private int baseCost = 10;

        [Tooltip("Base dungeon capacity consumed by this unit type before combat power scaling.")]
        [SerializeField, Min(1)] private int baseCapacity = 1;

        [Tooltip("Spatial footprint in grid tiles (default 1x1).")]
        [SerializeField] private Vector2Int footprint = Vector2Int.one;

        [Header("Visual / Prefab")]
        [Tooltip("Character prefab asset from Project window (e.g. Assets/Prefabs/.../NC002.prefab).")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Optional CharacterBrain component reference on the prefab (auto-syncs with Prefab).")]
        [SerializeField] private CharacterBrain characterBrain;

        public string UnitId => unitId;
        public string DisplayName => displayName;
        public int BaseCost => baseCost;
        public int BaseCapacity => baseCapacity;
        public Vector2Int Footprint => footprint.x < 1 || footprint.y < 1 ? Vector2Int.one : footprint;
        public GameObject Prefab => prefab != null ? prefab : (characterBrain != null ? characterBrain.gameObject : null);
        public CharacterBrain CharacterBrain => characterBrain;

        private void OnValidate()
        {
            if (characterBrain != null && prefab == null)
            {
                prefab = characterBrain.gameObject;
            }
            else if (prefab != null && characterBrain == null)
            {
                characterBrain = prefab.GetComponent<CharacterBrain>();
            }

            if (string.IsNullOrEmpty(unitId))
            {
                unitId = name.ToLower().Replace(" ", "_");
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = name;
            }
        }
    }
}
