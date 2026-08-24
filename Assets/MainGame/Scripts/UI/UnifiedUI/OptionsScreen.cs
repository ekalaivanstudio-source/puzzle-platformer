using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Setting.Menu;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Options Screen controller.
    /// Explicitly binds the vertical selection paths for settings rows and the back button.
    /// </summary>
    public class OptionsScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("Settings Rows (Order Top to Bottom)")]
        [SerializeField] private SettingStepControl[] m_SettingsRows;

        private void OnEnable()
        {
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
            StartCoroutine(BuildExplicitNavigationNextFrame());
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        private IEnumerator BuildExplicitNavigationNextFrame()
        {
            yield return null; // Wait one frame for UI layouts to settle

            if (m_SettingsRows == null || m_SettingsRows.Length == 0) yield break;

            int totalRows = m_SettingsRows.Length;

            for (int i = 0; i < totalRows; i++)
            {
                SettingStepControl current = m_SettingsRows[i];
                if (current == null) continue;

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // Bind SelectOnUp
                if (i > 0)
                {
                    nav.selectOnUp = m_SettingsRows[i - 1];
                }
                else
                {
                    // Loop back to the bottom settings row from the top row
                    nav.selectOnUp = m_SettingsRows[totalRows - 1];
                }

                // Bind SelectOnDown
                if (i < totalRows - 1)
                {
                    nav.selectOnDown = m_SettingsRows[i + 1];
                }
                else
                {
                    // Loop back to the top settings row from the bottom row
                    nav.selectOnDown = m_SettingsRows[0];
                }

                current.navigation = nav;
            }

            // Explicitly bind the Back Button to None
            if (m_BackButton != null)
            {
                Navigation backNav = m_BackButton.navigation;
                backNav.mode = Navigation.Mode.None;
                m_BackButton.navigation = backNav;
            }

            // FORCE focus to the first row to ensure keyboard/controller takes over immediately
            if (UINavigationManager.Instance != null && m_SettingsRows[0] != null)
            {
                UINavigationManager.Instance.RestoreSelectedElement(m_SettingsRows[0].gameObject);
            }

            Debug.Log("[OptionsScreen] Explicit settings vertical navigation built successfully.");
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
