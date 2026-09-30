using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace SBGames.Database.Hybrid
{
    /// <summary>
    /// Hybrid asset reference container supporting both instant direct object references
    /// and memory-efficient Addressables lazy loading.
    /// </summary>
    [Serializable]
    public class HybridAssetReference<T> where T : Object
    {
        [Tooltip("Direct object reference (instant loading in Editor/RAM).")]
        [SerializeField] private T directAsset;

        [Tooltip("Addressable asset reference (on-demand asynchronous loading).")]
        [SerializeField] private AssetReferenceT<T> addressableAsset;

        public T DirectAsset
        {
            get => directAsset;
            set => directAsset = value;
        }

        public AssetReferenceT<T> AddressableAsset
        {
            get => addressableAsset;
            set => addressableAsset = value;
        }

        public bool HasDirectAsset => directAsset != null;
        public bool HasAddressableAsset => addressableAsset != null && addressableAsset.RuntimeKeyIsValid();
        public bool HasAnyAsset => HasDirectAsset || HasAddressableAsset;

        /// <summary>
        /// Instantly returns the direct asset if available, or null.
        /// </summary>
        public T LoadDirect() => directAsset;

        /// <summary>
        /// Loads the asset asynchronously. If a direct asset is already assigned,
        /// it invokes the callback immediately. Otherwise, it loads via Addressables.
        /// </summary>
        public void LoadAssetAsync(Action<T> onComplete)
        {
            if (HasDirectAsset)
            {
                onComplete?.Invoke(directAsset);
                return;
            }

            if (HasAddressableAsset)
            {
                addressableAsset.LoadAssetAsync<T>().Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        onComplete?.Invoke(handle.Result);
                    }
                    else
                    {
                        Debug.LogError($"[HybridAssetReference] Failed to load Addressable asset for {typeof(T).Name}: {handle.OperationException}");
                        onComplete?.Invoke(null);
                    }
                };
                return;
            }

            onComplete?.Invoke(null);
        }

        /// <summary>
        /// Releases any Addressable handle if it was loaded.
        /// </summary>
        public void ReleaseAsset()
        {
            if (HasAddressableAsset && addressableAsset.IsValid())
            {
                addressableAsset.ReleaseAsset();
            }
        }
    }

    [Serializable]
    public class HybridGameObjectReference : HybridAssetReference<GameObject> { }

    [Serializable]
    public class HybridSpriteReference : HybridAssetReference<Sprite> { }

    [Serializable]
    public class HybridAudioClipReference : HybridAssetReference<AudioClip> { }
}
