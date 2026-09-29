using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SBGames.Input
{
    /// <summary>
    /// Foundational Input Manager responsible for detecting hardware inputs and dispatching them
    /// to registered UI systems (Inventory, Settings, Map, etc.).
    /// 
    /// Architecture Principles:
    /// 1. Strict Separation: InputManager knows NOTHING about how UIs are rendered, animated, or laid out.
    ///    It only detects raw button presses and dispatches toggle/open/close calls.
    /// 2. Registry Pattern: Uses an internal mapping table (UIButton -> IToggleableUI / Action)
    ///    enabling any gameplay or UI system to bind dynamically without modifying InputManager source code.
    /// 3. Edge-Case Resilience:
    ///    - Single-frame arbitration: Only allows one UI toggle per frame to prevent conflicting overlapping screens.
    ///    - Mutual exclusion option: Automatically closes existing open menus when a new one opens.
    ///    - Debounce guard: Protects against rapid button bouncing.
    ///    - Global input gating: Allows freezing UI toggling during cutscenes, dialogs, or loading screens.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("When true, opening any UI system automatically closes any other open UI systems.")]
        [SerializeField] private bool closeOtherMenusOnOpen = true;

        [Tooltip("Minimum time (in seconds) between successive UI toggles to prevent accidental double-inputs.")]
        [SerializeField, Range(0.05f, 0.5f)] private float toggleDebounceTime = 0.15f;

        [Header("Runtime State")]
        [SerializeField] private bool isInputBlocked = false;

        // Registry of button -> UI handler
        private readonly Dictionary<UIButton, IToggleableUI> _uiSystemRegistry = new();
        private readonly Dictionary<UIButton, Action> _callbackRegistry = new();

        // Hardware mapping: UIButton -> Key on Keyboard
        private readonly Dictionary<UIButton, Key> _keyboardBindings = new()
        {
            { UIButton.B, Key.B },
            { UIButton.Escape, Key.Escape },
            { UIButton.M, Key.M },
            { UIButton.Tab, Key.Tab },
            { UIButton.C, Key.C },
            { UIButton.J, Key.J },
            { UIButton.P, Key.P },
            { UIButton.H, Key.H }
        };

        // Edge-case tracking
        private float _lastToggleTimestamp = -1f;

        /// <summary>
        /// When true, all UI toggle inputs are ignored (e.g. during cutscenes, loading screens, or active dialogues).
        /// </summary>
        public bool IsInputBlocked
        {
            get => isInputBlocked;
            set => isInputBlocked = value;
        }

        /// <summary>
        /// Global event fired whenever any registered UI is successfully toggled.
        /// Useful for character movement controllers to freeze movement while a menu is open.
        /// </summary>
        public event Action<UIButton, bool> OnUISystemToggled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (isInputBlocked) return;

            // Debounce check to guard against physical key chatter
            if (Time.unscaledTime - _lastToggleTimestamp < toggleDebounceTime) return;

            ProcessUIInputs();
        }

        /// <summary>
        /// Polls registered buttons and dispatches to the corresponding UI handler.
        /// Processes at most ONE button press per frame to resolve simultaneous button press conflicts.
        /// </summary>
        private void ProcessUIInputs()
        {
            // Iterate over all registered buttons
            foreach (var kvp in _uiSystemRegistry)
            {
                UIButton button = kvp.Key;
                if (IsButtonPressedThisFrame(button))
                {
                    DispatchUIToggle(button, kvp.Value);
                    return; // Arbitrate: Only process the first detected button press this frame
                }
            }

            // Also check delegate-based registrations
            foreach (var kvp in _callbackRegistry)
            {
                UIButton button = kvp.Key;
                if (!_uiSystemRegistry.ContainsKey(button) && IsButtonPressedThisFrame(button))
                {
                    _lastToggleTimestamp = Time.unscaledTime;
                    kvp.Value?.Invoke();
                    return; // Arbitrate
                }
            }

            // Global Escape key fallback: Close any open UI systems if Escape is not explicitly bound
            if (!_uiSystemRegistry.ContainsKey(UIButton.Escape) && !_callbackRegistry.ContainsKey(UIButton.Escape) && IsButtonPressedThisFrame(UIButton.Escape))
            {
                foreach (var kvp in _uiSystemRegistry)
                {
                    if (kvp.Value != null && kvp.Value.IsOpen)
                    {
                        kvp.Value.Close();
                        OnUISystemToggled?.Invoke(kvp.Key, false);
                        _lastToggleTimestamp = Time.unscaledTime;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the execution of a UI toggle, enforcing mutual exclusion if configured.
        /// </summary>
        private void DispatchUIToggle(UIButton button, IToggleableUI uiSystem)
        {
            if (uiSystem == null) return;

            _lastToggleTimestamp = Time.unscaledTime;
            bool wasOpen = uiSystem.IsOpen;

            if (wasOpen)
            {
                // Closing current UI
                uiSystem.Close();
                OnUISystemToggled?.Invoke(button, false);
            }
            else
            {
                // Opening new UI: If mutual exclusion is enabled, close any other currently open UI first
                if (closeOtherMenusOnOpen)
                {
                    CloseAllUIExcept(uiSystem);
                }

                uiSystem.Open();
                OnUISystemToggled?.Invoke(button, true);
            }
        }

        #region Hardware Input Detection

        /// <summary>
        /// Detects whether the specified logical UIButton was pressed this frame across Keyboard and Gamepad.
        /// </summary>
        public bool IsButtonPressedThisFrame(UIButton button)
        {
            // 1. Keyboard Detection (via UnityEngine.InputSystem)
            var keyboard = Keyboard.current;
            if (keyboard != null && _keyboardBindings.TryGetValue(button, out Key key))
            {
                if (keyboard[key].wasPressedThisFrame)
                {
                    return true;
                }
            }

            // 2. Gamepad Detection (via UnityEngine.InputSystem)
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                switch (button)
                {
                    case UIButton.B:
                        // 'B' button (East face button on Xbox / Circle on PlayStation)
                        if (gamepad.buttonEast.wasPressedThisFrame) return true;
                        break;

                    case UIButton.Escape:
                        // 'Start' / Menu button on Gamepad
                        if (gamepad.startButton.wasPressedThisFrame) return true;
                        break;

                    case UIButton.M:
                        // 'Select' / View button on Gamepad
                        if (gamepad.selectButton.wasPressedThisFrame) return true;
                        break;
                }
            }

            return false;
        }

        #endregion

        #region Public Registration API

        /// <summary>
        /// Registers an IToggleableUI system (e.g. Inventory, Map, Settings) to a logical button.
        /// If the button was already registered, the new handler replaces the old one with a warning.
        /// </summary>
        public void RegisterUISystem(UIButton button, IToggleableUI uiSystem)
        {
            if (uiSystem == null)
            {
                Debug.LogWarning($"[InputManager] Attempted to register null UI system for button '{button}'.");
                return;
            }

            if (_uiSystemRegistry.ContainsKey(button))
            {
                Debug.LogWarning($"[InputManager] Rebinding button '{button}' to a new UI system ({uiSystem.GetType().Name}).");
            }

            _uiSystemRegistry[button] = uiSystem;
        }

        /// <summary>
        /// Registers a simple Action callback to be executed when the specified button is pressed.
        /// Ideal for simple one-off menus or scripts that do not implement IToggleableUI.
        /// </summary>
        public void RegisterUISystem(UIButton button, Action toggleAction)
        {
            if (toggleAction == null)
            {
                Debug.LogWarning($"[InputManager] Attempted to register null callback for button '{button}'.");
                return;
            }

            _callbackRegistry[button] = toggleAction;
        }

        /// <summary>
        /// Unregisters a UI system from the specified button.
        /// </summary>
        public void UnregisterUISystem(UIButton button)
        {
            _uiSystemRegistry.Remove(button);
            _callbackRegistry.Remove(button);
        }

        /// <summary>
        /// Unregisters a UI system across any button it may be bound to.
        /// </summary>
        public void UnregisterUISystem(IToggleableUI uiSystem)
        {
            if (uiSystem == null) return;

            UIButton? targetButton = null;
            foreach (var kvp in _uiSystemRegistry)
            {
                if (kvp.Value == uiSystem)
                {
                    targetButton = kvp.Key;
                    break;
                }
            }

            if (targetButton.HasValue)
            {
                _uiSystemRegistry.Remove(targetButton.Value);
            }
        }

        /// <summary>
        /// Rebinds a UI system from an old button to a new button.
        /// </summary>
        public bool RebindButton(UIButton oldButton, UIButton newButton)
        {
            if (!_uiSystemRegistry.TryGetValue(oldButton, out var uiSystem))
            {
                return false;
            }

            _uiSystemRegistry.Remove(oldButton);
            _uiSystemRegistry[newButton] = uiSystem;
            return true;
        }

        /// <summary>
        /// Customizes the physical keyboard key assigned to an abstract UIButton.
        /// Allows player key rebinding from settings menus without modifying code.
        /// </summary>
        public void SetKeyboardBinding(UIButton button, Key newKey)
        {
            _keyboardBindings[button] = newKey;
        }

        #endregion

        #region Window Management Queries & Utilities

        /// <summary>
        /// Returns true if a UI system or callback is registered to the specified button.
        /// </summary>
        public bool IsUISystemRegistered(UIButton button)
        {
            return _uiSystemRegistry.ContainsKey(button) || _callbackRegistry.ContainsKey(button);
        }

        /// <summary>
        /// Returns true if the UI system registered to the given button is currently open.
        /// </summary>
        public bool IsUIOpen(UIButton button)
        {
            return _uiSystemRegistry.TryGetValue(button, out var system) && system.IsOpen;
        }

        /// <summary>
        /// Returns true if ANY registered UI system is currently open.
        /// Extremely useful for pausing gameplay, disabling player combat controls, or showing cursor.
        /// </summary>
        public bool IsAnyUIOpen()
        {
            foreach (var system in _uiSystemRegistry.Values)
            {
                if (system.IsOpen) return true;
            }
            return false;
        }

        /// <summary>
        /// Closes all open registered UI systems.
        /// Useful when dying, entering combat, starting cutscenes, or switching levels.
        /// </summary>
        public void CloseAllUI()
        {
            foreach (var system in _uiSystemRegistry.Values)
            {
                if (system.IsOpen)
                {
                    system.Close();
                }
            }
        }

        /// <summary>
        /// Closes all registered UI systems except the specified instance.
        /// </summary>
        public void CloseAllUIExcept(IToggleableUI activeSystem)
        {
            foreach (var system in _uiSystemRegistry.Values)
            {
                if (system != activeSystem && system.IsOpen)
                {
                    system.Close();
                }
            }
        }

        #endregion
    }
}
