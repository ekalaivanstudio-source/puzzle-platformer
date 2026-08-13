using System;
using UnityEngine;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Acts as a central hub and distributor for device changes, updating all visual prompts.
    /// </summary>
    public class InputPromptManager : MonoBehaviour
    {
        public static InputPromptManager Instance { get; private set; }

        /// <summary>
        /// Triggered when the prompt style needs to refresh.
        /// </summary>
        public event Action<DeviceType> OnPromptStyleChanged;

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

        private void Start()
        {
            if (InputDeviceManager.Instance != null)
            {
                InputDeviceManager.Instance.OnDeviceChanged += HandleDeviceChanged;
                // Initial update
                HandleDeviceChanged(InputDeviceManager.Instance.CurrentDevice);
            }
        }

        private void OnDestroy()
        {
            if (InputDeviceManager.Instance != null)
            {
                InputDeviceManager.Instance.OnDeviceChanged -= HandleDeviceChanged;
            }
        }

        private void HandleDeviceChanged(DeviceType deviceType)
        {
            OnPromptStyleChanged?.Invoke(deviceType);
        }

        /// <summary>
        /// Gets the current active device style.
        /// </summary>
        public DeviceType GetCurrentDeviceStyle()
        {
            if (InputDeviceManager.Instance != null)
            {
                return InputDeviceManager.Instance.CurrentDevice;
            }
            return DeviceType.KeyboardMouse;
        }
    }
}
