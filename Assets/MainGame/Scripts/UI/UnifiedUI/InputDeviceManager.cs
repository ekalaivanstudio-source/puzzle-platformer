using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Singleton manager that monitors control/input activity to detect which device was last used.
    /// Does not poll or run manual selection loops; simply provides the last-used device type.
    /// </summary>
    public class InputDeviceManager : MonoBehaviour
    {
        public static InputDeviceManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Threshold pointer movement to register mouse activity instead of accidental jitter.")]
        [SerializeField] private float m_MouseMovementThreshold = 1.0f;

        private Vector2 m_LastMousePosition;
        private DeviceType m_CurrentDevice = DeviceType.KeyboardMouse;

        /// <summary>
        /// Gets the current active device type.
        /// </summary>
        public DeviceType CurrentDevice => m_CurrentDevice;

        /// <summary>
        /// Triggered when the last-used input device type changes.
        /// </summary>
        public event Action<DeviceType> OnDeviceChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[InputDeviceManager] Awake completed. Current Device: " + m_CurrentDevice);
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            // Check what device generated this input event
            if (device is Keyboard)
            {
                UpdateDevice(DeviceType.KeyboardMouse);
            }
            else if (device is Mouse mouse)
            {
                // Check if the mouse actually moved past a threshold to ignore jitter
                Vector2 currentPos = mouse.position.ReadValue();
                if (Vector2.Distance(currentPos, m_LastMousePosition) > m_MouseMovementThreshold)
                {
                    m_LastMousePosition = currentPos;
                    UpdateDevice(DeviceType.KeyboardMouse);
                }
                
                // If any mouse buttons are pressed, count as active KeyboardMouse device immediately
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0.01f)
                {
                    UpdateDevice(DeviceType.KeyboardMouse);
                }
            }
            else if (device is Gamepad)
            {
                // Detect PlayStation controllers (DualShock, DualSense, etc.)
                string deviceName = device.name != null ? device.name.ToLower() : "";
                string deviceProduct = device.description.product != null ? device.description.product.ToLower() : "";
                string deviceInterface = device.description.interfaceName != null ? device.description.interfaceName.ToLower() : "";

                if (deviceName.Contains("dualshock") || deviceName.Contains("dualsense") || deviceName.Contains("playstation") || deviceName.Contains("sony") || deviceName.Contains("ps4") || deviceName.Contains("ps5") ||
                    deviceProduct.Contains("dualshock") || deviceProduct.Contains("dualsense") || deviceProduct.Contains("playstation") || deviceProduct.Contains("sony") ||
                    deviceInterface.Contains("playstation") || deviceInterface.Contains("sony"))
                {
                    UpdateDevice(DeviceType.PS5);
                }
                else
                {
                    UpdateDevice(DeviceType.Xbox);
                }
            }
            else if (device is Touchscreen)
            {
                UpdateDevice(DeviceType.Mobile);
            }
        }

        private void UpdateDevice(DeviceType newDevice)
        {
            if (m_CurrentDevice != newDevice)
            {
                m_CurrentDevice = newDevice;
                Debug.Log($"[InputDeviceManager] Last used device switched to: {m_CurrentDevice}");
                OnDeviceChanged?.Invoke(m_CurrentDevice);
            }
        }
    }
}
