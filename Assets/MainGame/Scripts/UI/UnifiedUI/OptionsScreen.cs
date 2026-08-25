using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
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

        private readonly List<Selectable> m_NavigationChain = new List<Selectable>();
        private Coroutine m_BuildNavigationCoroutine;

        /// <summary>
        /// Focus starts on the first settings row, falling back to the inspector-assigned default.
        /// </summary>
        public override GameObject DefaultSelectedObject
        {
            get
            {
                SettingStepControl firstRow = GetFirstUsableRow();
                return firstRow != null ? firstRow.gameObject : base.DefaultSelectedObject;
            }
        }

        private void OnEnable()
        {
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
            m_BuildNavigationCoroutine = StartCoroutine(BuildExplicitNavigationNextFrame());
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);

            if (m_BuildNavigationCoroutine != null)
            {
                StopCoroutine(m_BuildNavigationCoroutine);
                m_BuildNavigationCoroutine = null;
            }
        }

        private SettingStepControl GetFirstUsableRow()
        {
            if (m_SettingsRows == null) return null;

            for (int i = 0; i < m_SettingsRows.Length; i++)
            {
                if (m_SettingsRows[i] != null) return m_SettingsRows[i];
            }
            return null;
        }

        private IEnumerator BuildExplicitNavigationNextFrame()
        {
            yield return null; // Wait one frame for UI layouts to settle

            BuildExplicitNavigation();
            m_BuildNavigationCoroutine = null;
        }

        /// <summary>
        /// Chains every assigned settings row plus the back button into one wrapping vertical loop.
        /// Null slots in the inspector array are skipped rather than breaking the chain.
        /// </summary>
        private void BuildExplicitNavigation()
        {
            m_NavigationChain.Clear();

            if (m_SettingsRows != null)
            {
                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    if (m_SettingsRows[i] != null) m_NavigationChain.Add(m_SettingsRows[i]);
                }
            }

            if (m_BackButton != null) m_NavigationChain.Add(m_BackButton);

            int count = m_NavigationChain.Count;
            if (count <= 1) return;

            for (int i = 0; i < count; i++)
            {
                Selectable current = m_NavigationChain[i];

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = m_NavigationChain[(i - 1 + count) % count];
                nav.selectOnDown = m_NavigationChain[(i + 1) % count];
                current.navigation = nav;
            }
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
