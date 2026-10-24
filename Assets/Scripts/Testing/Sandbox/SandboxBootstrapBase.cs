using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    public interface ISandboxBootstrap
    {
        string SandboxId { get; }
        bool IsReady { get; }
        void ExecuteBootstrap();
        void PurgeUnwantedMeta();
    }

    /// <summary>
    /// Common base bootstrap MonoBehaviour for isolated sandbox scenes.
    /// Runs at early execution order (-200) to ensure dependencies are initialized before other scripts Awake.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public abstract class SandboxBootstrapBase : MonoBehaviour, ISandboxBootstrap
    {
        [Header("Sandbox Bootstrap Base")]
        [SerializeField] private bool autoBootstrapOnAwake = true;
        [SerializeField] private bool autoPurgeMeta = true;

        public abstract string SandboxId { get; }
        public bool IsReady { get; protected set; }

        protected virtual void Awake()
        {
            Application.runInBackground = true;

            if (autoPurgeMeta)
            {
                PurgeUnwantedMeta();
            }

            if (autoBootstrapOnAwake)
            {
                ExecuteBootstrap();
            }
        }

        protected virtual void Start()
        {
            if (autoPurgeMeta)
            {
                PurgeUnwantedMeta();
            }
        }

        public abstract void ExecuteBootstrap();

        public virtual void PurgeUnwantedMeta()
        {
            string[] unwantedNames = {
                "[AltarEventUI]",
                "[CombatResultsUI]",
                "[DMAbilitiesUI]",
                "[DungeonMaster]",
                "[DMTalentTreeUI]",
                "[DMTalentTreeManager]",
                "[CoreGameLoop]",
                "[EscapeMenu]"
            };

            int removedCount = 0;
            foreach (var name in unwantedNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"<color=orange>[{SandboxId}Bootstrap] Purged {removedCount} unwanted meta objects.</color>", this);
            }
        }
    }
}
