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
            bool isPc = deviceType == DeviceType.KeyboardMouse;

            if (m_KeyboardPromptObject != null)
            {
                m_KeyboardPromptObject.SetActive(isPc);
            }

            if (m_XboxPromptObject != null)
            {
                m_XboxPromptObject.SetActive(!isPc);
            }
        }
    }
}
