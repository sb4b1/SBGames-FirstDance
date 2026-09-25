using UnityEngine;

public class CharacterIdentifier : ObjectIdentifier
{
    public string Name => gameObject.name;
    public string CharacterID => gameObject.name;

#if UNITY_EDITOR
    [SerializeField] private string characterName;

    private void OnValidate()
    {
        characterName = gameObject.name;
    }
#endif
}