using System.Collections.Generic;
using UnityEngine;

public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private Transform poolRoot;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        

        poolRoot = new GameObject("Object Pools").transform;
        poolRoot.SetParent(transform);
    }

    // =========================
    // SPAWN
    // =========================
    public GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation
    )
    {
        if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools.Add(prefab, pool);
        }

        GameObject obj = pool.Count > 0
            ? pool.Dequeue()
            : CreateNew(prefab);

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        if (obj.TryGetComponent(out IPoolable poolable))
            poolable.OnSpawn();

        return obj;
    }

    // =========================
    // DESPAWN
    // =========================
    public void Despawn(GameObject obj)
    {
        if (!obj.TryGetComponent(out PooledObject po))
        {
            Debug.LogError(
                $"[ObjectPoolManager] {obj.name} has no PooledObject component",
                obj
            );
            Destroy(obj);
            return;
        }

        if (!pools.TryGetValue(po.Prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools.Add(po.Prefab, pool);
        }

        if (obj.TryGetComponent(out IPoolable poolable))
            poolable.OnDespawn();

        obj.SetActive(false);
        obj.transform.SetParent(poolRoot);
        pool.Enqueue(obj);
    }

    // =========================
    // INTERNAL
    // =========================
    private GameObject CreateNew(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        obj.AddComponent<PooledObject>().Prefab = prefab;
        obj.transform.SetParent(poolRoot);
        return obj;
    }
}

// =========================
// INTERNAL HELPER
// =========================
internal class PooledObject : MonoBehaviour
{
    public GameObject Prefab;
}
