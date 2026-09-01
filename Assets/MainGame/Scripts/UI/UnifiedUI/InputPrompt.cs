using UnityEngine;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Placed on individual UI elements to display the correct prompt representation (e.g. keyboard key vs gamepad button).
    /// </summary>
    public class InputPrompt : MonoBehaviour
    {
        [Header("Prompt Views")]
        [Tooltip("Object to enable when Keyboard/Mouse layout is active.")]
        [SerializeField] private GameObject m_KeyboardPromptObject;

        [Tooltip("Object to enable when Xbox Layout is active.")]
        [SerializeField] private GameObject m_XboxPromptObject;

        [Tooltip("Object to enable when PS5 Layout is active.")]
        [SerializeField] private GameObject m_PS5PromptObject;

        [Tooltip("Object to enable when Mobile Layout is active.")]
        [SerializeField] private GameObject m_MobilePromptObject;

        // The manager we are actually subscribed to, so unsubscribing survives the singleton being replaced.
        private InputPromptManager m_SubscribedManager;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Update()
        {
            // The prompt manager may initialise after this prompt is enabled; keep trying until wired.
            if (m_SubscribedManager == null)
            {
                TrySubscribe();
            }
        }

        private void OnDisable()
        {
            if (m_SubscribedManager != null)
            {
                m_SubscribedManager.OnPromptStyleChanged -= RefreshPrompt;
                m_SubscribedManager = null;
            }
        }

        private void TrySubscribe()
        {
            InputPromptManager manager = InputPromptManager.Instance;
            if (manager == null) return;

            m_SubscribedManager = manager;
            manager.OnPromptStyleChanged += RefreshPrompt;
            RefreshPrompt(manager.GetCurrentDeviceStyle());
        }

        private void RefreshPrompt(DeviceType deviceType)
        {
            SetActiveSafe(m_KeyboardPromptObject, false);
            SetActiveSafe(m_XboxPromptObject, false);
            SetActiveSafe(m_PS5PromptObject, false);
            SetActiveSafe(m_MobilePromptObject, false);

            switch (deviceType)
            {
                case DeviceType.KeyboardMouse:
                    SetActiveSafe(m_KeyboardPromptObject, true);
                    break;

                case DeviceType.Xbox:
                    // Fall back to the other gamepad art rather than showing no prompt at all.
                    if (!SetActiveSafe(m_XboxPromptObject, true)) SetActiveSafe(m_PS5PromptObject, true);
                    break;

                case DeviceType.PS5:
                    if (!SetActiveSafe(m_PS5PromptObject, true)) SetActiveSafe(m_XboxPromptObject, true);
                    break;

                case DeviceType.Mobile:
                    SetActiveSafe(m_MobilePromptObject, true);
                    break;
            }
        }

        /// <summary>
        /// Sets the active state when the object is assigned. Returns false when there is nothing to show.
        /// </summary>
        private static bool SetActiveSafe(GameObject target, bool active)
        {
            if (target == null) return false;

            target.SetActive(active);
            return true;
        }
    }
}
