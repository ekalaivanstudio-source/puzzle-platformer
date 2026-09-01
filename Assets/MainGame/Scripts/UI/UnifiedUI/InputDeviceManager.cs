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

        /// <summary>Substrings that identify a PlayStation pad from its name / product / interface.</summary>
        private static readonly string[] PlayStationIdentifiers =
        {
            "dualshock", "dualsense", "playstation", "sony", "ps4", "ps5"
        };

        [Header("Settings")]
        [Tooltip("Threshold pointer movement to register mouse activity instead of accidental jitter.")]
        [SerializeField] private float m_MouseMovementThreshold = 1.0f;

        [Header("Debugging")]
        [Tooltip("Log every device switch. Off by default to keep the console readable.")]
        [SerializeField] private bool m_VerboseLogging;

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
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
                HandleMouseEvent(mouse);
            }
            else if (device is Gamepad)
            {
                UpdateDevice(IsPlayStationPad(device) ? DeviceType.PS5 : DeviceType.Xbox);
            }
            else if (device is Touchscreen)
            {
                UpdateDevice(DeviceType.Mobile);
            }
        }

        /// <summary>
        /// Treats the mouse as active on a button press, a scroll, or a movement large enough to
        /// clear the jitter threshold, so a nudged desk does not steal focus from a gamepad.
        /// </summary>
        private void HandleMouseEvent(Mouse mouse)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed
                || mouse.scroll.ReadValue().sqrMagnitude > 0.01f)
            {
                UpdateDevice(DeviceType.KeyboardMouse);
                return;
            }

            Vector2 currentPos = mouse.position.ReadValue();
            if (Vector2.Distance(currentPos, m_LastMousePosition) > m_MouseMovementThreshold)
            {
                m_LastMousePosition = currentPos;
                UpdateDevice(DeviceType.KeyboardMouse);
            }
        }

        private static bool IsPlayStationPad(InputDevice device)
        {
            return MatchesPlayStation(device.name)
                || MatchesPlayStation(device.description.product)
                || MatchesPlayStation(device.description.manufacturer)
                || MatchesPlayStation(device.description.interfaceName);
        }

        private static bool MatchesPlayStation(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            for (int i = 0; i < PlayStationIdentifiers.Length; i++)
            {
                if (value.IndexOf(PlayStationIdentifiers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateDevice(DeviceType newDevice)
        {
            if (m_CurrentDevice == newDevice) return;

            m_CurrentDevice = newDevice;
            if (m_VerboseLogging)
            {
                Debug.Log($"[InputDeviceManager] Last used device switched to: {m_CurrentDevice}");
            }
            OnDeviceChanged?.Invoke(m_CurrentDevice);
        }
    }
}
