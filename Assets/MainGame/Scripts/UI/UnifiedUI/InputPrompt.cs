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

        private void OnEnable()
        {
            if (InputPromptManager.Instance != null)
            {
                InputPromptManager.Instance.OnPromptStyleChanged += RefreshPrompt;
                RefreshPrompt(InputPromptManager.Instance.GetCurrentDeviceStyle());
            }
        }

        private void OnDisable()
        {
            if (InputPromptManager.Instance != null)
            {
                InputPromptManager.Instance.OnPromptStyleChanged -= RefreshPrompt;
            }
        }

        private void RefreshPrompt(DeviceType deviceType)
        {
            if (m_KeyboardPromptObject != null) m_KeyboardPromptObject.SetActive(false);
            if (m_XboxPromptObject != null) m_XboxPromptObject.SetActive(false);
            if (m_PS5PromptObject != null) m_PS5PromptObject.SetActive(false);
            if (m_MobilePromptObject != null) m_MobilePromptObject.SetActive(false);

            switch (deviceType)
            {
                case DeviceType.KeyboardMouse:
                    if (m_KeyboardPromptObject != null) m_KeyboardPromptObject.SetActive(true);
                    break;

                case DeviceType.Xbox:
                    if (m_XboxPromptObject != null) m_XboxPromptObject.SetActive(true);
                    else if (m_PS5PromptObject != null) m_PS5PromptObject.SetActive(true);
                    break;

                case DeviceType.PS5:
                    if (m_PS5PromptObject != null) m_PS5PromptObject.SetActive(true);
                    else if (m_XboxPromptObject != null) m_XboxPromptObject.SetActive(true);
                    break;

                case DeviceType.Mobile:
                    if (m_MobilePromptObject != null) m_MobilePromptObject.SetActive(true);
                    break;
            }
        }
    }
}
