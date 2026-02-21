using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterRuntimeDatabase : MonoBehaviour
{
    [SerializeField] private CharacterDatabase database;

    private Dictionary<string, CharacterEntry> characterDict;

    void Awake()
    {
        characterDict = database.characters
            .ToDictionary(c => c.id, c => c);

            GameServices.Characters = this;
    }

    public CharacterEntry Get(string id)
    {
        return characterDict.TryGetValue(id, out var entry)
            ? entry
            : null;
    }
}