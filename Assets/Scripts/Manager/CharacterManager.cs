using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using ArtificeToolkit.Runtime.SerializedDictionary;
using UnityEngine;

/// <summary>
/// Manages and tracks all currently spawned characters in the scene.
/// Provides easy access to active characters and their identifiers.
/// </summary>
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [SerializeField, ReadOnly] private SerializedDictionary<string, CharacterBrain> characters = new();
    private readonly List<CharacterBrain> charactersList = new();

    public event System.Action<Dictionary<string, CharacterBrain>> OnCharacterListChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GameServices.Register(this);
    }

    /// <summary>
    /// Register a spawned character
    /// </summary>
    public void RegisterCharacter(CharacterBrain character)
    {
        if (character == null) return;

        var identifier = character.GetComponent<CharacterIdentifier>();
        if (identifier == null)
        {
            Debug.LogWarning("[CharacterManager] Character has no CharacterIdentifier", character.gameObject);
            return;
        }

        string id = identifier.ID;

        if (characters.ContainsKey(id))
        {
            Debug.LogWarning($"[CharacterManager] Character with ID {id} already registered", character.gameObject);
            return;
        }

        characters.Add(id, character);
        charactersList.Add(character);
    }

    /// <summary>
    /// Unregister a despawned character
    /// </summary>
    public void UnregisterCharacter(CharacterBrain character)
    {
        if (character == null) return;

        if (!character.TryGetComponent<CharacterIdentifier>(out var identifier)) return;

        string id = identifier.ID;

        if (characters.Remove(id))
        {
            charactersList.Remove(character);
        }
    }

    /// <summary>
    /// Get a character by its unique ID
    /// </summary>
    public CharacterBrain GetCharacterByID(string id)
    {
        characters.TryGetValue(id, out var character);
        return character;
    }

    /// <summary>
    /// Get all spawned characters as a list
    /// </summary>
    public List<CharacterBrain> GetAllCharacters()
    {
        return new List<CharacterBrain>(charactersList);
    }

    /// <summary>
    /// Get all spawned characters as a dictionary (ID -> Character)
    /// </summary>
    public Dictionary<string, CharacterBrain> GetAllCharactersDict()
    {
        return new Dictionary<string, CharacterBrain>(characters);
    }

    /// <summary>
    /// Get the number of spawned characters
    /// </summary>
    public int GetCharacterCount()
    {
        return charactersList.Count;
    }

    /// <summary>
    /// Check if a character is currently spawned
    /// </summary>
    public bool IsCharacterSpawned(string id)
    {
        return characters.ContainsKey(id);
    }

    /// <summary>
    /// Clear all spawned characters from tracking (called on scene unload)
    /// </summary>
    public void ClearAll()
    {
        characters.Clear();
        charactersList.Clear();
    }

    /// <summary>
    /// Notify listeners that the character list has changed (e.g. after spawn/despawn)
    /// </summary>
    public void NotifyCharacterListChanged()
    {
        OnCharacterListChanged?.Invoke(GetAllCharactersDict());
    }
}
