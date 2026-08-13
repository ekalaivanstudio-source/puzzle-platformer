using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Confirmation / Quit Popup controller.
    /// </summary>
    public class ConfirmationPopupScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private Button m_CancelButton;

        private void OnEnable()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.onClick.AddListener(HandleConfirmClicked);
            if (m_CancelButton != null) m_CancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDisable()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (m_CancelButton != null) m_CancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        private void HandleConfirmClicked()
        {
            AudioManager.Instance?.PlayButton();
            Debug.Log("[ConfirmationPopupScreen] Confirming action (Exit)...");
            Application.Quit();
        }

        private void HandleCancelClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }
    }
}
