using UnityEngine;

namespace SBGames.Database
{
    /// <summary>
    /// Master database asset aggregating all domain-specific sub-catalogs.
    /// Provides central initialization and lifecycle management.
    /// </summary>
    [CreateAssetMenu(fileName = "MasterGameDatabase", menuName = "SBGames/Database/Master Game Database")]
    public class MasterGameDatabaseSO : ScriptableObject
    {
        [Header("Sub-Catalogs")]
        [SerializeField] private ItemDatabaseSO itemDatabase;
        [SerializeField] private SkillDatabaseSO skillDatabase;
        [SerializeField] private CharacterDatabaseSO characterDatabase;

        public ItemDatabaseSO Items => itemDatabase;
        public SkillDatabaseSO Skills => skillDatabase;
        public CharacterDatabaseSO Characters => characterDatabase;

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Initializes all underlying sub-databases for runtime querying.
        /// </summary>
        public void InitializeAll()
        {
            if (itemDatabase != null)
            {
                itemDatabase.Initialize();
            }
            else
            {
                Debug.LogWarning("[MasterGameDatabase] ItemDatabase sub-catalog is unassigned.", this);
            }

            if (skillDatabase != null)
            {
                skillDatabase.Initialize();
            }
            else
            {
                Debug.LogWarning("[MasterGameDatabase] SkillDatabase sub-catalog is unassigned.", this);
            }

            if (characterDatabase != null)
            {
                characterDatabase.Initialize();
            }
            else
            {
                Debug.LogWarning("[MasterGameDatabase] CharacterDatabase sub-catalog is unassigned.", this);
            }

            IsInitialized = true;
        }

        /// <summary>
        /// Editor helper for auto-linking sub-catalogs.
        /// </summary>
        public void SetSubCatalogsEditor(
            ItemDatabaseSO items,
            SkillDatabaseSO skills,
            CharacterDatabaseSO characters)
        {
            itemDatabase = items;
            skillDatabase = skills;
            characterDatabase = characters;
        }
    }
}
