using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Level Selection Screen controller.
    /// </summary>
    public class LevelSelectionScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("References")]
        [Tooltip("Manager that generates the arc pages. Resolved from this object or its children when left empty.")]
        [SerializeField] private LevelSelection.LevelSelectionManager m_LevelSelectionManager;

        protected override void Awake()
        {
            base.Awake();

            if (m_LevelSelectionManager == null)
            {
                m_LevelSelectionManager = GetComponent<LevelSelection.LevelSelectionManager>();
            }
            if (m_LevelSelectionManager == null)
            {
                m_LevelSelectionManager = GetComponentInChildren<LevelSelection.LevelSelectionManager>(true);
            }
        }

        /// <summary>
        /// Returns the node for the player's current level. This is a pure lookup: the arc itself is
        /// (re)generated in <see cref="Open"/>, because the navigation manager may read this property
        /// repeatedly while restoring lost focus.
        /// </summary>
        public override GameObject DefaultSelectedObject
        {
            get
            {
                GameObject selectTarget = m_LevelSelectionManager != null
                    ? m_LevelSelectionManager.GetCurrentUnlockedLevelNodeObject()
                    : null;

                return selectTarget != null ? selectTarget : base.DefaultSelectedObject;
            }
        }

        public override void Open()
        {
            base.Open();

            // Rebuild the arc page around the player's latest progress before focus is restored.
            if (m_LevelSelectionManager != null)
            {
                m_LevelSelectionManager.InitializeAndFocusCurrentLevel();
            }
        }

        private void OnEnable()
        {
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
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
