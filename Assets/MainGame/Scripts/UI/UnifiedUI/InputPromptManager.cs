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

        private InputDeviceManager m_SubscribedDeviceManager;

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
            TrySubscribe();
        }

        private void Update()
        {
            // InputDeviceManager may come up after this component (script execution order, or a
            // manager prefab spawned later). Keep looking until we are wired to it exactly once.
            if (m_SubscribedDeviceManager == null)
            {
                TrySubscribe();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void TrySubscribe()
        {
            InputDeviceManager deviceManager = InputDeviceManager.Instance;
            if (deviceManager == null) return;

            m_SubscribedDeviceManager = deviceManager;
            deviceManager.OnDeviceChanged += HandleDeviceChanged;

            // Initial update so prompts match the device already in use
            HandleDeviceChanged(deviceManager.CurrentDevice);
        }

        private void Unsubscribe()
        {
            if (m_SubscribedDeviceManager != null)
            {
                m_SubscribedDeviceManager.OnDeviceChanged -= HandleDeviceChanged;
                m_SubscribedDeviceManager = null;
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
            InputDeviceManager deviceManager = InputDeviceManager.Instance;
            return deviceManager != null ? deviceManager.CurrentDevice : DeviceType.KeyboardMouse;
        }
    }
}
