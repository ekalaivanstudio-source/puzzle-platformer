using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HomeUI
{
    /// <summary>
    /// Owns navigation between <see cref="UIPanel"/>s. It is the ONLY thing that decides which
    /// screen is visible, so panels and buttons stay ignorant of each other — they just ask the
    /// ScreenManager to "show Settings" or "go Back".
    ///
    /// • Panels are activated/deactivated, never destroyed (state is preserved).
    /// • A back-stack supports nested navigation (Home → Collections → … → Back).
    /// • On every change it sets the EventSystem's selected object, so keyboard/controller
    ///   navigation always has a sensible starting focus.
    ///
    /// A static <see cref="Instance"/> is exposed purely as a convenience locator for popup
    /// callbacks and decoupled buttons — it holds navigation state, not game state, and there is
    /// exactly one screen stack, so this is a legitimate use rather than singleton abuse.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [Tooltip("All panels managed by this screen. Found automatically in children if left empty.")]
        [SerializeField] private List<UIPanel> m_Panels = new List<UIPanel>();

        [Tooltip("Id of the panel shown on startup (usually the Home screen).")]
        [SerializeField] private string m_InitialPanelId;

        private readonly Dictionary<string, UIPanel> m_Lookup = new Dictionary<string, UIPanel>();
        private readonly List<UIPanel> m_History = new List<UIPanel>();

        /// <summary>The panel currently shown, or null before the first navigation.</summary>
        public UIPanel Current => m_History.Count > 0 ? m_History[m_History.Count - 1] : null;

        private void Awake()
        {
            Instance = this;

            if (m_Panels.Count == 0)
                GetComponentsInChildren(includeInactive: true, m_Panels);

            foreach (UIPanel panel in m_Panels)
            {
                if (panel == null) continue;
                m_Lookup[panel.PanelId] = panel;
                panel.Hide(instant: true);
            }
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(m_InitialPanelId))
                Show(m_InitialPanelId, instant: true, clearHistory: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows a panel by id, hiding the previous one and pushing it onto the back-stack.</summary>
        public void Show(string panelId) => Show(panelId, instant: false, clearHistory: false);

        /// <summary>Shows a panel by id with full control over transition and history behaviour.</summary>
        public void Show(string panelId, bool instant, bool clearHistory)
        {
            if (!m_Lookup.TryGetValue(panelId, out UIPanel target) || target == null)
            {
                Debug.LogError($"[ScreenManager] No panel registered with id '{panelId}'.", this);
                return;
            }
            Debug.Log($"[ScreenManager] Navigating to panel '{panelId}' (clearHistory={clearHistory}, instant={instant}).", this);
            UIPanel current = Current;
            if (current == target) return;

            if (clearHistory) m_History.Clear();
            if (current != null && current != target) current.Hide(instant);

            target.Show(instant);
            m_History.Add(target);
            FocusFirst(target);
        }

        /// <summary>
        /// Returns to the previous panel in the back-stack. Does nothing if already at the root,
        /// so it is safe to bind to an Escape/B-button handler.
        /// </summary>
        public void Back(bool instant = false)
        {
            if (m_History.Count < 2) return;

            UIPanel current = m_History[m_History.Count - 1];
            m_History.RemoveAt(m_History.Count - 1);
            current.Hide(instant);

            UIPanel previous = m_History[m_History.Count - 1];
            previous.Show(instant);
            FocusFirst(previous);
        }

        /// <summary>Sets the EventSystem focus so controllers/keyboards have a starting point.</summary>
        private void FocusFirst(UIPanel panel)
        {
            if (panel.FirstSelected == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(panel.FirstSelected);
        }
    }
}
