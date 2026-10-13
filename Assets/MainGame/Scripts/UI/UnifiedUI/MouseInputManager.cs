using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Global manager that enforces keyboard-only (and gamepad) navigation by disabling
    /// mouse hardware devices, locking and hiding the mouse cursor, and stripping pointer
    /// actions from all UI input modules across every scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseInputManager : MonoBehaviour
    {
        private static MouseInputManager s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInstance();
            ApplyMouseSuppression();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            ApplyMouseSuppression();
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;

            GameObject go = new GameObject("[MouseInputManager]");
            s_Instance = go.AddComponent<MouseInputManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyMouseSuppression();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                InputSystem.onDeviceChange -= OnDeviceChange;
                s_Instance = null;
            }
        }

        private void Update()
        {
            EnforceCursorHidden();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyMouseSuppression();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyMouseSuppression();
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Mouse)
            {
                if (change == InputDeviceChange.Added ||
                    change == InputDeviceChange.Reconnected ||
                    change == InputDeviceChange.Enabled)
                {
                    try
                    {
                        InputSystem.DisableDevice(device);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MouseInputManager] Failed to disable mouse device: {ex.Message}");
                    }
                }
            }
        }

        public static void ApplyMouseSuppression()
        {
            DisableAllMouseDevices();
            EnforceCursorHidden();
            StripPointerActionsFromUIModules();
        }

        private static void DisableAllMouseDevices()
        {
            try
            {
                var devices = InputSystem.devices;
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i] is Mouse mouse && mouse.enabled)
                    {
                        InputSystem.DisableDevice(mouse);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MouseInputManager] Failed to disable mice: {ex.Message}");
            }
        }

        private static void EnforceCursorHidden()
        {
            if (Cursor.visible)
            {
                Cursor.visible = false;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private static void StripPointerActionsFromUIModules()
        {
            try
            {
                var uiModules = FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include);
                foreach (var module in uiModules)
                {
                    if (module == null) continue;

                    module.point = null;
                    module.leftClick = null;
                    module.rightClick = null;
                    module.middleClick = null;
                    module.scrollWheel = null;
                    module.deselectOnBackgroundClick = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MouseInputManager] Failed to strip pointer actions: {ex.Message}");
            }
        }
    }
}
