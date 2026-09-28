using SBGames.Loop;
using SBGames.Loop.UI;
using SBGames.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Editor
{
    public static class SetupDungeonGameLoop
    {
        [MenuItem("SBGames/Setup Dungeon Game Loop & Escape Menu in Scene")]
        public static void SetupInActiveScene()
        {
            // 1. Ensure DungeonMaster exists
            var dm = Object.FindAnyObjectByType<DungeonMaster>();
            if (dm == null)
            {
                var dmGo = new GameObject("[DungeonMaster]");
                dm = dmGo.AddComponent<DungeonMaster>();
                dm.Initialize(startingEssence: 100, startingEnergy: 50);
                Undo.RegisterCreatedObjectUndo(dmGo, "Create DungeonMaster");
                Debug.Log("[Setup] Created [DungeonMaster] GameObject.");
            }

            // 2. Ensure CoreGameLoop exists
            var loop = Object.FindAnyObjectByType<CoreGameLoop>();
            if (loop == null)
            {
                var loopGo = new GameObject("[CoreGameLoop]");
                loop = loopGo.AddComponent<CoreGameLoop>();
                loop.Initialize(dm: dm, initialRound: 1, planningAllowance: 15, victoryBonus: 25);
                Undo.RegisterCreatedObjectUndo(loopGo, "Create CoreGameLoop");
                Debug.Log("[Setup] Created [CoreGameLoop] GameObject.");
            }

            // 3. Ensure Escape Menu UI exists
            var menuController = Object.FindAnyObjectByType<EscapeMenuUIController>();
            if (menuController == null)
            {
                var menuGo = new GameObject("[EscapeMenu]");
                var uiDoc = menuGo.AddComponent<UIDocument>();
                uiDoc.sortingOrder = 999;

                // Assign default PanelSettings
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Resources/UI/MainPanelSettings.asset");
                if (panelSettings == null)
                {
                    panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
                }
                if (panelSettings != null)
                {
                    uiDoc.panelSettings = panelSettings;
                }

                menuController = menuGo.AddComponent<EscapeMenuUIController>();
                Undo.RegisterCreatedObjectUndo(menuGo, "Create EscapeMenu");
                Debug.Log("[Setup] Created [EscapeMenu] with UIDocument and EscapeMenuUIController.");
            }
            else
            {
                var uiDoc = menuController.GetComponent<UIDocument>();
                if (uiDoc != null)
                {
                    uiDoc.sortingOrder = 999;
                    if (uiDoc.panelSettings == null)
                    {
                        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Resources/UI/MainPanelSettings.asset");
                        if (panelSettings == null)
                        {
                            panelSettings = Resources.Load<PanelSettings>("UI/MainPanelSettings");
                        }
                        if (panelSettings != null)
                        {
                            uiDoc.panelSettings = panelSettings;
                            EditorUtility.SetDirty(uiDoc);
                        }
                    }
                }
            }

            // 4. Ensure DungeonPlacementManager connects to DungeonMaster & CoreGameLoop
            var placementManager = Object.FindAnyObjectByType<DungeonPlacementManager>();
            if (placementManager != null)
            {
                var so = new SerializedObject(placementManager);
                var dmProp = so.FindProperty("dungeonMaster");
                var loopProp = so.FindProperty("coreGameLoop");
                var useEnergyProp = so.FindProperty("useDungeonMasterEnergy");
                var refundRatioProp = so.FindProperty("removalRefundRatio");

                if (dmProp != null) dmProp.objectReferenceValue = dm;
                if (loopProp != null) loopProp.objectReferenceValue = loop;
                if (useEnergyProp != null) useEnergyProp.boolValue = true;
                if (refundRatioProp != null) refundRatioProp.floatValue = 1.0f; // 100% refund during planning

                so.ApplyModifiedProperties();
                Debug.Log("[Setup] Configured DungeonPlacementManager to spend DM Energy with 100% refund.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[Setup Completed Successfully]</color> Dungeon Game Loop, Escape Menu, and Energy Build Wallet are configured!");
        }
    }
}
