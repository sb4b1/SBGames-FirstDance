using System.Collections.Generic;
using UnityEngine;
using CrashKonijn.Goap.Runtime;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;

namespace SBGames.Testing
{
    /// <summary>
    /// Self-contained bootstrap component that initializes and validates all required runtime dependencies
    /// for standalone battle simulation scenes. Bypasses meta singletons and avoids NullReferenceExceptions.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public class CombatTestEnvironmentBootstrap : MonoBehaviour
    {
        [Header("Bootstrap Configurations")]
        [Tooltip("Automatically execute environment validation and dependency mocking on Awake.")]
        [SerializeField] private bool autoBootstrapOnAwake = true;

        [Tooltip("Ensure ObjectPoolManager exists (critical for skill projectiles/casters).")]
        [SerializeField] private bool ensureObjectPool = true;

        [Tooltip("Ensure CharacterManager exists (used for unit registration).")]
        [SerializeField] private bool ensureCharacterManager = true;

        [Tooltip("Ensure GameServices singleton exists.")]
        [SerializeField] private bool ensureGameServices = true;

        [Tooltip("Ensure GOAP runner and archetypes exist in scene.")]
        [SerializeField] private bool ensureGoapSystem = true;

        [Tooltip("Verify presence and bake state of NavMeshPlus 2D navigation surface.")]
        [SerializeField] private bool validateNavMesh = true;

        [Header("Meta Systems Suppression")]
        [Tooltip("Automatically purge unwanted meta loop and UI singletons (EscapeMenu, CoreGameLoop, DungeonMaster, etc.).")]
        [SerializeField] private bool suppressMetaGameSystems = true;

        [Header("Arena Boundary Builder")]
        [Tooltip("When enabled, builds a boundary enclosure collider to confine 2D combatants.")]
        [SerializeField] private bool generateArenaWalls = false;
        [SerializeField] private Vector2 arenaDimensions = new Vector2(20f, 14f);
        [SerializeField] private float wallThickness = 1f;

        private void Awake()
        {
            if (suppressMetaGameSystems)
            {
                PurgeUnwantedMetaGameObjects();
            }

            if (autoBootstrapOnAwake)
            {
                ExecuteBootstrap();
            }
        }

        private void Start()
        {
            // Second-pass purge to catch any late AfterSceneLoad auto-bootstraps
            if (suppressMetaGameSystems)
            {
                PurgeUnwantedMetaGameObjects();
            }
        }

        /// <summary>
        /// Global guard used by AfterSceneLoad bootstrap routines to determine if meta loops/UI should be skipped.
        /// </summary>
        public static bool ShouldBypassMetaAutoBootstrap()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string sceneName = activeScene.name;

            if (!string.IsNullOrEmpty(sceneName))
            {
                if (sceneName.IndexOf("Sandbox", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("Test", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (FindAnyObjectByType<BattleSimulationTestRunner>() != null ||
                FindAnyObjectByType<CombatTestEnvironmentBootstrap>() != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Destroys auto-instantiated meta loop/UI GameObjects if they exist in the scene.
        /// </summary>
        [ContextMenu("Purge Unwanted Meta UI")]
        public void PurgeUnwantedMetaGameObjects()
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
                Debug.Log($"<color=orange>[CombatBootstrap] Purged {removedCount} unwanted meta game/UI objects from sandbox.</color>", this);
            }
        }

        /// <summary>
        /// Validates scene setup and creates any missing essential singletons or systems.
        /// Safe to call in both Edit Mode and Play Mode.
        /// </summary>
        [ContextMenu("Execute Environment Bootstrap")]
        public void ExecuteBootstrap()
        {
            Debug.Log("<color=cyan><b>[CombatBootstrap]</b> Initializing Combat Sandbox Environment...</color>", this);

            if (suppressMetaGameSystems)
            {
                PurgeUnwantedMetaGameObjects();
            }

            if (ensureGameServices)
            {
                SetupGameServices();
            }

            if (ensureCharacterManager)
            {
                SetupCharacterManager();
            }

            if (ensureObjectPool)
            {
                SetupObjectPoolManager();
            }

            if (ensureGoapSystem)
            {
                SetupGoapSystem();
            }

            if (validateNavMesh)
            {
                CheckNavMeshIntegrity();
            }

            EnsureCameraAndAudioListener();

            if (generateArenaWalls)
            {
                BuildArenaBoundaries();
            }

            Debug.Log("<color=green><b>[CombatBootstrap]</b> Environment Bootstrap complete. Sandbox is ready for simulation.</color>", this);
        }

        #region Dependency Mocking & Singletons

        private void SetupGameServices()
        {
            if (GameServices.Instance != null) return;

            var existing = FindAnyObjectByType<GameServices>();
            if (existing != null) return;

            var gsGo = new GameObject("[Bootstrap] GameServices");
            gsGo.AddComponent<GameServices>();
            gsGo.transform.SetParent(transform);
            Debug.Log("[CombatBootstrap] Spawned fallback GameServices singleton.", gsGo);
        }

        private void SetupCharacterManager()
        {
            if (CharacterManager.Instance != null) return;

            var existing = FindAnyObjectByType<CharacterManager>();
            if (existing != null) return;

            var cmGo = new GameObject("[Bootstrap] CharacterManager");
            cmGo.AddComponent<CharacterManager>();
            cmGo.transform.SetParent(transform);
            Debug.Log("[CombatBootstrap] Spawned fallback CharacterManager singleton.", cmGo);
        }

        private void SetupObjectPoolManager()
        {
            if (ObjectPoolManager.Instance != null) return;

            var existing = FindAnyObjectByType<ObjectPoolManager>();
            if (existing != null) return;

            var opGo = new GameObject("[Bootstrap] ObjectPoolManager");
            opGo.AddComponent<ObjectPoolManager>();
            opGo.transform.SetParent(transform);
            Debug.Log("[CombatBootstrap] Spawned fallback ObjectPoolManager (Required for skills/projectiles).", opGo);
        }

        private void SetupGoapSystem()
        {
            var goap = FindAnyObjectByType<GoapBehaviour>();
            if (goap != null)
            {
                EnsureAgentFactoriesOnExistingGoap(goap);
                return;
            }

            var goapGo = new GameObject("[Bootstrap] GOAP_System");
            goapGo.transform.SetParent(transform);

            // 1. Add IGoapController
            goapGo.AddComponent<ReactiveControllerBehaviour>();

            // 2. Add GoapBehaviour
            var goapComp = goapGo.AddComponent<GoapBehaviour>();

            // 3. Add Archetype Factories as child components
            var factories = new List<AgentTypeFactoryBase>();

            var normalFactory = goapGo.AddComponent<NormalAgentTypeFactory>();
            factories.Add(normalFactory);

            var mageFactory = goapGo.AddComponent<MageAgentTypeFactory>();
            if (mageFactory != null) factories.Add(mageFactory);

            var minionFactory = goapGo.AddComponent<MinionAgentTypeFactory>();
            if (minionFactory != null) factories.Add(minionFactory);

            goapComp.agentTypeConfigFactories = factories;

            Debug.Log("[CombatBootstrap] Created fallback GOAP runner with Normal, Mage, and Minion agent type factories.", goapGo);
        }

        private void EnsureAgentFactoriesOnExistingGoap(GoapBehaviour goap)
        {
            if (goap.agentTypeConfigFactories == null)
            {
                goap.agentTypeConfigFactories = new List<AgentTypeFactoryBase>();
            }

            // Ensure NormalAgentTypeFactory is registered
            bool hasNormal = false;
            for (int i = 0; i < goap.agentTypeConfigFactories.Count; i++)
            {
                if (goap.agentTypeConfigFactories[i] is NormalAgentTypeFactory)
                {
                    hasNormal = true;
                    break;
                }
            }

            if (!hasNormal)
            {
                var factory = goap.GetComponent<NormalAgentTypeFactory>() ?? goap.gameObject.AddComponent<NormalAgentTypeFactory>();
                goap.agentTypeConfigFactories.Add(factory);
                Debug.Log("[CombatBootstrap] Appended missing NormalAgentTypeFactory to GoapBehaviour.", goap);
            }
        }

        #endregion

        #region NavMesh & Navigation Integrity

        private void CheckNavMeshIntegrity()
        {
            var navSurface = FindAnyObjectByType<NavMeshSurface>();
            if (navSurface == null)
            {
                Debug.LogWarning("<b>[CombatBootstrap] ⚠ No NavMeshPlus NavMeshSurface detected in scene!</b> Units will not be able to navigate. Use 'Create 2D NavMesh Surface' context menu.", this);
                return;
            }

            if (navSurface.navMeshData == null)
            {
                Debug.LogWarning("<b>[CombatBootstrap] ⚠ NavMeshSurface found, but NavMeshData is unbaked!</b> Right click CombatTestEnvironmentBootstrap -> 'Bake 2D NavMesh'.", navSurface);
            }
            else
            {
                Debug.Log("[CombatBootstrap] 2D NavMeshSurface validated with active baked NavMeshData.", navSurface);
            }
        }

        /// <summary>
        /// Attempts to bake the NavMeshPlus surface directly via script.
        /// </summary>
        [ContextMenu("Bake 2D NavMesh")]
        public void BakeNavMesh()
        {
            var navSurface = FindAnyObjectByType<NavMeshSurface>();
            if (navSurface == null)
            {
                Debug.LogError("[CombatBootstrap] Cannot bake: No NavMeshSurface found in scene.", this);
                return;
            }

            navSurface.BuildNavMesh();
            Debug.Log("<color=green>[CombatBootstrap] 2D NavMesh baked successfully!</color>", navSurface);
        }

        /// <summary>
        /// Creates a fully configured NavMeshPlus surface GameObject in the scene.
        /// </summary>
        [ContextMenu("Create 2D NavMesh Surface")]
        public void CreateNavMeshSurface()
        {
            var existing = FindAnyObjectByType<NavMeshSurface>();
            if (existing != null)
            {
                Debug.LogWarning("[CombatBootstrap] A NavMeshSurface already exists in the scene.", existing);
                return;
            }

            var surfaceGo = new GameObject("NavMesh_Surface_2D");
            surfaceGo.transform.SetParent(transform);

            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            var collectSources = surfaceGo.AddComponent<CollectSources2d>();

            // Configure 2D defaults
            surface.hideEditorLogs = true;

            Debug.Log("[CombatBootstrap] Created NavMesh_Surface_2D with CollectSources2d. You can now bake the surface.", surfaceGo);
        }

        #endregion

        #region Camera & Visuals

        private void EnsureCameraAndAudioListener()
        {
            if (Camera.main != null) return;

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            cam.orthographic = true;
            cam.orthographicSize = 8f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.transform.SetParent(transform);

            Debug.Log("[CombatBootstrap] Spawned 2D Orthographic Main Camera.", camGo);
        }

        #endregion

        #region Arena Enclosure Generator

        /// <summary>
        /// Builds static 2D boundary colliders around the combat area to prevent units from leaving the walkable field.
        /// </summary>
        [ContextMenu("Build Arena Boundaries")]
        public void BuildArenaBoundaries()
        {
            Transform existingWalls = transform.Find("ArenaBoundaries");
            if (existingWalls != null)
            {
                if (Application.isPlaying) Destroy(existingWalls.gameObject);
                else DestroyImmediate(existingWalls.gameObject);
            }

            var root = new GameObject("ArenaBoundaries");
            root.transform.SetParent(transform);

            float halfW = arenaDimensions.x * 0.5f;
            float halfH = arenaDimensions.y * 0.5f;
            float t = wallThickness;

            // Top Wall
            CreateWall(root.transform, "Wall_Top", new Vector2(0f, halfH + t * 0.5f), new Vector2(arenaDimensions.x + t * 2f, t));
            // Bottom Wall
            CreateWall(root.transform, "Wall_Bottom", new Vector2(0f, -halfH - t * 0.5f), new Vector2(arenaDimensions.x + t * 2f, t));
            // Left Wall
            CreateWall(root.transform, "Wall_Left", new Vector2(-halfW - t * 0.5f, 0f), new Vector2(t, arenaDimensions.y));
            // Right Wall
            CreateWall(root.transform, "Wall_Right", new Vector2(halfW + t * 0.5f, 0f), new Vector2(t, arenaDimensions.y));

            Debug.Log($"[CombatBootstrap] Built 2D arena walls enclosure ({arenaDimensions.x}x{arenaDimensions.y}).", root);
        }

        private void CreateWall(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.localPosition = pos;

            var col = wall.AddComponent<BoxCollider2D>();
            col.size = size;

            // Mark wall static for 2D NavMesh obstacle collection
            wall.isStatic = true;
        }

        #endregion
    }
}
