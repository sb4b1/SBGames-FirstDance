using UnityEngine;

public class GameAssets : MonoBehaviour
{
    public static GameAssets I { get; private set; }

    [Header("Skill Prefabs")]
    // Fire Skill Prefabs
    public GameObject FS001;

    // Water Skill Prefabs
    public GameObject WS001;

    private void Awake()
    {
        if (I != null)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
    }

    public T GetPrefabComponent<T>(GameObject prefab) where T : Component
    {
        return prefab.GetComponent<T>();
    }
}