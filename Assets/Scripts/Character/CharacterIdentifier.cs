using UnityEngine;

public class CharacterIdentifier : ObjectIdentifier
{
    private CharacterStatsManager cachedStatsManager;

    public string Name => gameObject.name;
    public string CharacterID => gameObject.name;

    public CharacterStatsManager StatsManager => cachedStatsManager != null
        ? cachedStatsManager
        : (cachedStatsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>());

    public AdventurerFunction CharacterFunction => StatsManager != null ? StatsManager.CharacterFunction : AdventurerFunction.None;
    public AdventurerFunction EffectiveFunction => StatsManager != null ? StatsManager.EffectiveFunction : AdventurerFunction.DPS;
    public AdventurerWealth CharacterWealth => StatsManager != null ? StatsManager.CharacterWealth : AdventurerWealth.Average;

#if UNITY_EDITOR
    [SerializeField] private string characterName;

    private void OnValidate()
    {
        characterName = gameObject.name;
    }
#endif
}