using UnityEngine;

public class GameServices : MonoBehaviour
{
    public static GameServices Instance { get; private set; }

    public static CharacterRuntimeDatabase Characters { get;  set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}