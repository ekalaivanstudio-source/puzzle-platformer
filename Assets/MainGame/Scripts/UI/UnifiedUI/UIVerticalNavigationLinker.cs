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

        private void OnEnable()
        {
            RefreshNavigationLinks();
        }

        private void Start()
        {
            // Run a delayed refresh to wait for LevelGenerator script visibility setup
            StartCoroutine(DelayedRefresh());
        }

        private System.Collections.IEnumerator DelayedRefresh()
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

            // Gather all active and interactable buttons
            var activeList = new System.Collections.Generic.List<Button>();
            foreach (var btn in m_Buttons)
            {
                if (btn != null && btn.gameObject.activeInHierarchy && btn.interactable)
                {
                    activeList.Add(btn);
                }
            }

            int count = activeList.Count;
            if (count <= 1) return;

            for (int i = 0; i < count; i++)
            {
                Button current = activeList[i];
                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // Determine Up neighbor
                if (i > 0)
                {
                    nav.selectOnUp = activeList[i - 1];
                }
                else
                {
                    nav.selectOnUp = m_LoopNavigation ? activeList[count - 1] : null;
                }

                // Determine Down neighbor
                if (i < count - 1)
                {
                    nav.selectOnDown = activeList[i + 1];
                }
                else
                {
                    nav.selectOnDown = m_LoopNavigation ? activeList[0] : null;
                }

                // Keep horizontal/sides clear or map them accordingly
                nav.selectOnLeft = null;
                nav.selectOnRight = null;

                current.navigation = nav;
            }

            Debug.Log($"[UIVerticalNavigationLinker] Re-linked {count} buttons vertically.");
        }
    }
}
