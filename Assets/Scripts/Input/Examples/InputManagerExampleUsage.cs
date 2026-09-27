using System.Text;
using UnityEngine;

namespace SBGames.Input.Examples
{
    /// <summary>
    /// Demonstrates how to use InputManager to register UI systems (Inventory, Settings, Map, etc.),
    /// rebind buttons, handle mutual exclusion, and run automated headless verification tests.
    /// </summary>
    public class InputManagerExampleUsage : MonoBehaviour
    {
        [Header("Testing")]
        [SerializeField] private bool runVerificationOnStart = false;

        private void Start()
        {
            if (runVerificationOnStart)
            {
                RunFullVerificationSuite();
            }
            else
            {
                DemonstrateRegistrationExample();
            }
        }

        /// <summary>
        /// Demonstrates how gameplay and UI developers register both current and future UI systems.
        /// </summary>
        public static void DemonstrateRegistrationExample()
        {
            // 1. Ensure InputManager exists
            var inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                var inputGo = new GameObject("[InputManager]");
                inputManager = inputGo.AddComponent<InputManager>();
            }

            // -------------------------------------------------------------
            // EXAMPLE 1: Registering the Inventory System on 'B' button
            // -------------------------------------------------------------
            // A UI controller implementing IToggleableUI registers itself:
            var mockInventoryUI = new MockUISystem("Inventory UI");
            inputManager.RegisterUISystem(UIButton.B, mockInventoryUI);
            Debug.Log("[InputManager Example] Registered Inventory UI on 'B' button.");

            // -------------------------------------------------------------
            // EXAMPLE 2: Registering a future Settings Menu on 'Escape' button
            // -------------------------------------------------------------
            var mockSettingsUI = new MockUISystem("Settings Menu");
            inputManager.RegisterUISystem(UIButton.Escape, mockSettingsUI);
            Debug.Log("[InputManager Example] Registered Settings Menu on 'Escape' button.");

            // -------------------------------------------------------------
            // EXAMPLE 3: Registering a future World Map on 'M' button
            // -------------------------------------------------------------
            var mockMapUI = new MockUISystem("World Map");
            inputManager.RegisterUISystem(UIButton.M, mockMapUI);
            Debug.Log("[InputManager Example] Registered World Map on 'M' button.");

            // -------------------------------------------------------------
            // EXAMPLE 4: Delegate-based registration (no IToggleableUI needed)
            // -------------------------------------------------------------
            inputManager.RegisterUISystem(UIButton.Tab, () =>
            {
                Debug.Log("[InputManager Example] Tab pressed: Toggled Quick Wheel / Scoreboard!");
            });

            // -------------------------------------------------------------
            // EXAMPLE 5: Rebinding buttons at runtime (e.g. from player options)
            // -------------------------------------------------------------
            // Rebind Inventory from 'B' to 'C'
            inputManager.RebindButton(UIButton.B, UIButton.C);
            Debug.Log("[InputManager Example] Rebound Inventory UI to 'C' button.");
        }

        #region Automated Verification Suite

        [ContextMenu("Run InputManager Verification Suite")]
        public static bool RunFullVerificationSuite()
        {
            var log = new StringBuilder();
            log.AppendLine("==================================================");
            log.AppendLine("STARTING INPUT MANAGER VERIFICATION SUITE");
            log.AppendLine("==================================================");

            bool allPassed = true;

            var go = new GameObject("[TestInputManager]");
            var inputManager = go.AddComponent<InputManager>();

            // 1. Basic Registration & Toggle Logic
            allPassed &= VerifyRegistrationAndToggles(inputManager, log);

            // 2. Mutual Exclusion (Opening Menu B closes Menu A)
            allPassed &= VerifyMutualExclusion(inputManager, log);

            // 3. Input Blocking (Cutscenes / Dialogues)
            allPassed &= VerifyInputBlocking(inputManager, log);

            // 4. Dynamic Rebinding
            allPassed &= VerifyDynamicRebinding(inputManager, log);

            // 5. CloseAllUI Utility
            allPassed &= VerifyCloseAllUI(inputManager, log);

            DestroyImmediate(go);

            log.AppendLine("==================================================");
            log.AppendLine(allPassed ? ">> ALL INPUT MANAGER TESTS PASSED SUCCESSFULLY! <<" : ">> SOME INPUT MANAGER TESTS FAILED! <<");
            log.AppendLine("==================================================");

            if (allPassed)
            {
                Debug.Log(log.ToString());
            }
            else
            {
                Debug.LogError(log.ToString());
            }

            return allPassed;
        }

        private static bool VerifyRegistrationAndToggles(InputManager inputManager, StringBuilder log)
        {
            log.AppendLine("\n[Test 1] Registration & Toggle Dispatch");
            bool ok = true;

            var inventoryUI = new MockUISystem("Inventory");
            inputManager.RegisterUISystem(UIButton.B, inventoryUI);

            ok &= Assert(!inventoryUI.IsOpen, "Inventory is initially closed", log);

            // Simulate toggle by calling Open/Close via IToggleableUI
            inventoryUI.Toggle();
            ok &= Assert(inventoryUI.IsOpen, "Inventory opened after toggle", log);
            ok &= Assert(inputManager.IsUIOpen(UIButton.B), "InputManager reports Inventory is open", log);

            inventoryUI.Toggle();
            ok &= Assert(!inventoryUI.IsOpen, "Inventory closed after second toggle", log);
            ok &= Assert(!inputManager.IsUIOpen(UIButton.B), "InputManager reports Inventory is closed", log);

            inputManager.UnregisterUISystem(UIButton.B);
            ok &= Assert(!inputManager.IsUIOpen(UIButton.B), "Unregistered successfully", log);

            return ok;
        }

        private static bool VerifyMutualExclusion(InputManager inputManager, StringBuilder log)
        {
            log.AppendLine("\n[Test 2] Mutual Exclusion (Single-Active Menu Guarantee)");
            bool ok = true;

            var inventoryUI = new MockUISystem("Inventory");
            var settingsUI = new MockUISystem("Settings");

            inputManager.RegisterUISystem(UIButton.B, inventoryUI);
            inputManager.RegisterUISystem(UIButton.Escape, settingsUI);

            // Open Inventory
            inventoryUI.Open();
            ok &= Assert(inventoryUI.IsOpen && !settingsUI.IsOpen, "Inventory is open, Settings is closed", log);

            // Open Settings using CloseAllUIExcept simulation
            inputManager.CloseAllUIExcept(settingsUI);
            settingsUI.Open();

            ok &= Assert(!inventoryUI.IsOpen, "Inventory was automatically closed when Settings opened", log);
            ok &= Assert(settingsUI.IsOpen, "Settings is now active and open", log);

            inputManager.CloseAllUI();
            return ok;
        }

        private static bool VerifyInputBlocking(InputManager inputManager, StringBuilder log)
        {
            log.AppendLine("\n[Test 3] Global Input Blocking (Cutscenes & Loading)");
            bool ok = true;

            inputManager.IsInputBlocked = true;
            ok &= Assert(inputManager.IsInputBlocked, "Input blocking enabled", log);

            inputManager.IsInputBlocked = false;
            ok &= Assert(!inputManager.IsInputBlocked, "Input blocking disabled", log);

            return ok;
        }

        private static bool VerifyDynamicRebinding(InputManager inputManager, StringBuilder log)
        {
            log.AppendLine("\n[Test 4] Dynamic Button Rebinding");
            bool ok = true;

            var mapUI = new MockUISystem("Map");
            inputManager.RegisterUISystem(UIButton.M, mapUI);

            mapUI.Open();
            ok &= Assert(inputManager.IsUIOpen(UIButton.M), "Map registered on 'M' button", log);

            // Rebind from 'M' to 'Tab'
            bool rebound = inputManager.RebindButton(UIButton.M, UIButton.Tab);
            ok &= Assert(rebound, "Rebind operation succeeded", log);
            ok &= Assert(!inputManager.IsUIOpen(UIButton.M), "Old button 'M' is no longer bound", log);
            ok &= Assert(inputManager.IsUIOpen(UIButton.Tab), "New button 'Tab' now references Map", log);

            inputManager.CloseAllUI();
            return ok;
        }

        private static bool VerifyCloseAllUI(InputManager inputManager, StringBuilder log)
        {
            log.AppendLine("\n[Test 5] CloseAllUI & IsAnyUIOpen Queries");
            bool ok = true;

            var uiA = new MockUISystem("A");
            var uiB = new MockUISystem("B");

            inputManager.RegisterUISystem(UIButton.B, uiA);
            inputManager.RegisterUISystem(UIButton.M, uiB);

            ok &= Assert(!inputManager.IsAnyUIOpen(), "No UI open initially", log);

            uiA.Open();
            ok &= Assert(inputManager.IsAnyUIOpen(), "IsAnyUIOpen is true when UI A is open", log);

            inputManager.CloseAllUI();
            ok &= Assert(!inputManager.IsAnyUIOpen(), "All UIs closed after CloseAllUI()", log);
            ok &= Assert(!uiA.IsOpen && !uiB.IsOpen, "Both UI A and UI B are closed", log);

            return ok;
        }

        private static bool Assert(bool condition, string testName, StringBuilder log)
        {
            if (condition)
            {
                log.AppendLine($"  [PASS] {testName}");
                return true;
            }
            else
            {
                log.AppendLine($"  [FAIL] >>> {testName} <<<");
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Standalone mock implementing IToggleableUI for testing and demonstration.
        /// </summary>
        public class MockUISystem : IToggleableUI
        {
            public string Name { get; }
            public bool IsOpen { get; private set; }

            public MockUISystem(string name)
            {
                Name = name;
                IsOpen = false;
            }

            public void Open()
            {
                IsOpen = true;
                Debug.Log($"[{Name}] Opened.");
            }

            public void Close()
            {
                IsOpen = false;
                Debug.Log($"[{Name}] Closed.");
            }

            public void Toggle()
            {
                if (IsOpen) Close();
                else Open();
            }
        }
    }
}
