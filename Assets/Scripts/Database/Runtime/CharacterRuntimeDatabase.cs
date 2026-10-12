using System.Collections.Generic;
using System.Linq;
using SBGames.Database;
using UnityEngine;

public class CharacterRuntimeDatabase : MonoBehaviour
{
    [SerializeField] private CharacterDatabaseSO database;

    private Dictionary<string, CharacterEntry> characterDict;

    public static CharacterRuntimeDatabase Instance { get; private set; }

    void Awake()
    {
        characterDict = database.characters
            .ToDictionary(c => c.id, c => c);
    }

    void Start()
    {
        GameServices.Register(this);

        if (characterDict == null || characterDict.Count == 0)
        {
            Debug.LogWarning("[CharacterRuntimeDatabase] Character dictionary is empty. Check if the database is assigned and populated.");
        }
    }

    public CharacterEntry Get(string id)
    {
        return characterDict.TryGetValue(id, out var entry)
            ? entry
            : null;
    }
}