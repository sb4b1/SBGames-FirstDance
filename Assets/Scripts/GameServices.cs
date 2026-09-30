using SBGames.Database;
using UnityEngine;

public class GameServices : MonoBehaviour
{
    public static GameServices Instance { get; private set; }

    public static CharacterManager CharacterManager { get; set; }

    public static CharacterRuntimeDatabase CharacterRuntimeDatabase { get;  set; }

    public static UtilitySkillRuntimeDatabase UtilitySkillRuntimeDatabase { get; set; }

    public static MasterGameDatabaseSO MasterGameDatabase { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Register<T>(T service)
    {
        if (service == null)
        {
            Debug.LogWarning($"[GameServices] Attempted to register null service of type {typeof(T).Name}");
            return;
        }

        if (Instance == null)
        {
            Debug.LogError($"[GameServices] No GameServices instance found when registering service of type {typeof(T).Name}");
            return;
        }

        var property = typeof(GameServices).GetProperty(typeof(T).Name);
        if (property == null || !property.CanWrite)
        {
            Debug.LogError($"[GameServices] No writable property found for service type {typeof(T).Name}");
            return;
        }

        property.SetValue(Instance, service);
    }
}