using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/Database/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    public List<CharacterEntry> characters;
}

[System.Serializable]
public class CharacterEntry
{
    public string id;
    public GameObject prefab;
}