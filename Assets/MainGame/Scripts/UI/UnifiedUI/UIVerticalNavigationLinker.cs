using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Automatically links a list of buttons in a strict vertical loop.
    /// Excludes buttons that are disabled or inactive, ensuring navigation never skips unexpectedly
    /// and always moves sequentially (1 -> 2 -> 3 -> 4 -> 5 -> 6) using W/S or controller.
    /// </summary>
    public class UIVerticalNavigationLinker : MonoBehaviour
    {
        [Header("Menu Buttons in Order")]
        [Tooltip("Add the main menu buttons in order from top to bottom.")]
        [SerializeField] private Button[] m_Buttons;

        [Header("Looping")]
        [Tooltip("If true, pressing Up on the top button wraps to the bottom button, and vice versa.")]
        [SerializeField] private bool m_LoopNavigation = true;

        // Reused between refreshes so relinking on every screen open does not allocate.
        private readonly List<Button> m_ActiveButtons = new List<Button>();

        private void OnEnable()
        {
            RefreshNavigationLinks();

            // Run a delayed refresh too: unlock states (e.g. the Continue button) are applied by other
            // scripts during the same frame this screen is enabled.
            StartCoroutine(DelayedRefresh());
        }

        private IEnumerator DelayedRefresh()
        {
            yield return null; // Wait 1 frame
            RefreshNavigationLinks();
        }

        /// <summary>
        /// Scans the buttons list, filters only active/interactable ones, and links them vertically.
        /// Call this at runtime if button unlock states change.
        /// </summary>
        public void RefreshNavigationLinks()
        {
            if (m_Buttons == null || m_Buttons.Length == 0) return;

            m_ActiveButtons.Clear();
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                Button btn = m_Buttons[i];
                if (btn != null && btn.gameObject.activeInHierarchy && btn.interactable)
                {
                    m_ActiveButtons.Add(btn);
                }
            }

            int count = m_ActiveButtons.Count;
            if (count <= 1) return;

            for (int i = 0; i < count; i++)
            {
                Button current = m_ActiveButtons[i];

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;

                bool isFirst = i == 0;
                bool isLast = i == count - 1;

                nav.selectOnUp = isFirst
                    ? (m_LoopNavigation ? m_ActiveButtons[count - 1] : null)
                    : m_ActiveButtons[i - 1];

                nav.selectOnDown = isLast
                    ? (m_LoopNavigation ? m_ActiveButtons[0] : null)
                    : m_ActiveButtons[i + 1];

                // This linker owns vertical movement only; horizontal is left unbound.
                nav.selectOnLeft = null;
                nav.selectOnRight = null;

                current.navigation = nav;
            }
        }
    }
}
