#if UNITY_EDITOR
using SBGames.Input;
using SBGames.Inventory.Character;
using SBGames.Inventory.UI;
using SBGames.Inventory.User;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Inventory.Editor
{
    /// <summary>
    /// Editor utility providing 1-click setup for the Inventory and InputManager in the active scene.
    /// Access via: Tools > SBGames > Setup Inventory & Input System
    /// </summary>
    public static class InventorySceneSetup
    {
        [MenuItem("Tools/SBGames/Setup Inventory & Input Demo Scene", false, 10)]
        public static void SetupScene()
        {
            // 1. Setup InputManager
            var inputManager = Object.FindAnyObjectByType<InputManager>();
            if (inputManager == null)
            {
                var inputGo = new GameObject("[InputManager]");
                inputManager = inputGo.AddComponent<InputManager>();
                Undo.RegisterCreatedObjectUndo(inputGo, "Create InputManager");
            }

            // 2. Setup UserInventoryManager
            var userInv = Object.FindAnyObjectByType<UserInventoryManager>();
            if (userInv == null)
            {
                var userInvGo = new GameObject("[UserInventoryManager]");
                userInv = userInvGo.AddComponent<UserInventoryManager>();
                Undo.RegisterCreatedObjectUndo(userInvGo, "Create UserInventoryManager");
            }

            // 3. Setup CharacterInventoryManager on any Character in the scene
            var statsManager = Object.FindAnyObjectByType<CharacterStatsManager>();
            if (statsManager != null)
            {
                var charInv = statsManager.GetComponent<CharacterInventoryManager>();
                if (charInv == null)
                {
                    charInv = statsManager.gameObject.AddComponent<CharacterInventoryManager>();
                    Undo.RegisterCreatedObjectUndo(charInv, "Add CharacterInventoryManager");
                }
            }

            // 4. Setup Inventory UI Document & Controller
            var uiController = Object.FindAnyObjectByType<InventoryUIController>();
            if (uiController == null)
            {
                var uiGo = new GameObject("[InventoryUI]");
                var uiDoc = uiGo.AddComponent<UIDocument>();
                uiController = uiGo.AddComponent<InventoryUIController>();
                Undo.RegisterCreatedObjectUndo(uiGo, "Create InventoryUI");
            }

            Selection.activeGameObject = uiController.gameObject;
            Debug.Log("<color=green>[SBGames] Inventory & InputManager setup successfully! Press PLAY and hit 'B' to toggle the inventory.</color>");
        }
    }
}
#endif
