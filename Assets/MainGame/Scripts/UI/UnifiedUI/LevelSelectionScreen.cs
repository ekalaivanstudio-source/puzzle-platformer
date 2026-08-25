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

        public override GameObject DefaultSelectedObject
        {
            get
            {
                LevelSelection.LevelSelectionManager manager = GetComponent<LevelSelection.LevelSelectionManager>();
                if (manager == null) manager = GetComponentInChildren<LevelSelection.LevelSelectionManager>();
                if (manager != null)
                {
                    manager.InitializeAndFocusCurrentLevel();
                    GameObject selectTarget = manager.GetCurrentUnlockedLevelNodeObject();
                    if (selectTarget != null)
                    {
                        return selectTarget;
                    }
                }
                return base.DefaultSelectedObject;
            }
        }

        public override void Open()
        {
            base.Open();
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
