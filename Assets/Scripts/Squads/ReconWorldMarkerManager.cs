using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Squads
{
    /// <summary>
    /// Spawns lightweight, non-allocating world markers indicating adventurer squad awareness of traps and enemies.
    /// Markers are pooled and automatically recycled when expiration occurs.
    /// </summary>
    public class ReconWorldMarkerManager : MonoBehaviour
    {
        public static ReconWorldMarkerManager Instance { get; private set; }

        private const int INITIAL_POOL_SIZE = 24;
        private readonly List<GameObject> markerPool = new(INITIAL_POOL_SIZE);
        private readonly List<ActiveMarker> activeMarkers = new(INITIAL_POOL_SIZE);

        private struct ActiveMarker
        {
            public GameObject Instance;
            public float ExpirationTime;
            public Transform FollowTarget; // null for stationary hazards
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            PreallocatePool();
        }

        private void PreallocatePool()
        {
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                var marker = CreateMarkerInstance($"ReconMarker_Pool_{i}");
                marker.SetActive(false);
                markerPool.Add(marker);
            }
        }

        private GameObject CreateMarkerInstance(string markerName)
        {
            var go = new GameObject(markerName);
            go.transform.SetParent(transform);

            // Sprite renderer with diamond/circle indicator
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.color = new Color(0.9f, 0.75f, 0.2f, 0.85f); // Gold / Recon color

            // Create a small procedural diamond sprite if no custom asset is provided
            sr.sprite = CreateProceduralMarkerSprite();

            return go;
        }

        private static Sprite CreateProceduralMarkerSprite()
        {
            int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - (size / 2));
                    int dy = Mathf.Abs(y - (size / 2));
                    if (dx + dy <= size / 2)
                    {
                        texture.SetPixel(x, y, fill);
                    }
                    else
                    {
                        texture.SetPixel(x, y, transparent);
                    }
                }
            }
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        public void SpawnHazardMarker(Vector3 worldPos, float duration = 30f)
        {
            GameObject marker = GetPooledMarker();
            marker.transform.position = worldPos;
            marker.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            var sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.95f, 0.3f, 0.2f, 0.85f); // Reddish-orange for trap hazards
            }

            marker.SetActive(true);
            activeMarkers.Add(new ActiveMarker
            {
                Instance = marker,
                ExpirationTime = Time.time + duration,
                FollowTarget = null
            });
        }

        public void SpawnEnemyMarker(Transform enemy, Vector3 lastKnownPos, float duration = 6f)
        {
            GameObject marker = GetPooledMarker();
            marker.transform.position = enemy != null ? enemy.position : lastKnownPos;
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.95f, 0.8f, 0.1f, 0.85f); // Yellow for spotted enemies
            }

            marker.SetActive(true);
            activeMarkers.Add(new ActiveMarker
            {
                Instance = marker,
                ExpirationTime = Time.time + duration,
                FollowTarget = enemy
            });
        }

        private GameObject GetPooledMarker()
        {
            if (markerPool.Count > 0)
            {
                int lastIdx = markerPool.Count - 1;
                var marker = markerPool[lastIdx];
                markerPool.RemoveAt(lastIdx);
                return marker;
            }

            return CreateMarkerInstance($"ReconMarker_Dynamic_{activeMarkers.Count}");
        }

        private void Update()
        {
            float currentTime = Time.time;

            for (int i = activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = activeMarkers[i];
                if (marker.Instance == null)
                {
                    activeMarkers.RemoveAt(i);
                    continue;
                }

                if (currentTime >= marker.ExpirationTime)
                {
                    marker.Instance.SetActive(false);
                    markerPool.Add(marker.Instance);
                    activeMarkers.RemoveAt(i);
                    continue;
                }

                // If following enemy, track position
                if (marker.FollowTarget != null)
                {
                    marker.Instance.transform.position = marker.FollowTarget.position + new Vector3(0f, 0.8f, 0f);
                }
            }
        }
    }
}
