using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Credits Screen controller.
    /// </summary>
    public class CreditsScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        private void OnEnable()
        {
            if (m_BackButton != null)
            {
                m_BackButton.onClick.AddListener(HandleBackClicked);
                m_BackButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        private void HandleBackClicked()
        {
            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }
    }
}
