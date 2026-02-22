#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class DevTool : MonoBehaviour
{
    [SerializeField] private List<CharacterList> characterLists = new();

    bool cmNullChecked = false;

    private void OnEnable()
    {
        if (CharacterManager.Instance == null)
        {
            cmNullChecked = true;
            return;
        }
        CharacterManager.Instance.OnCharacterListChanged += OnCharacterListChanged;
    }

    private void OnDisable()
    {
        CharacterManager.Instance.OnCharacterListChanged -= OnCharacterListChanged;
    }

    private void Start()
    {
        if (cmNullChecked)
        {
            cmNullChecked = false;
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterListChanged += OnCharacterListChanged;
            }
        }

        OnCharacterListChanged(CharacterManager.Instance.GetAllCharactersDict()); // Initialize the list with current characters
    }

    private void OnCharacterListChanged(Dictionary<string, CharacterBrain> newCharacters)
    {
        // Remove characters that are no longer in the new list
        characterLists.RemoveAll(cl => !newCharacters.ContainsKey(cl.ID));

        // Add new characters that are not already in the list
        var existingIds = new HashSet<string>(characterLists.Select(cl => cl.ID));
        foreach (var kvp in newCharacters)
        {
            if (!existingIds.Contains(kvp.Key))
            {
                characterLists.Add(new CharacterList(characterLists.Count.ToString(), kvp.Key, kvp.Value));
            }
        }
    }

    [System.Serializable]
    public class CharacterList
    {
        [SerializeField] private string number;
        [SerializeField] private string id;
        [SerializeField] private CharacterBrain character;

        public string ID => id;

        public CharacterList(string number,string id, CharacterBrain character)
        {
            this.number = number;
            this.id = id;
            this.character = character;
        }

        [Button(true)]
        public void KillCharacter()
        {
            if (character != null)
            {
                character.GetComponent<CharacterStatsManager>().ForceDeath();
            }
        }
    }

    [Button(true)]
    private void GetCharacterList()
    {
        int index = 0;
        characterLists = CharacterManager.Instance.GetAllCharactersDict().Select(kvp => new CharacterList(index++.ToString(), kvp.Key, kvp.Value)).ToList();
    }
}
#endif