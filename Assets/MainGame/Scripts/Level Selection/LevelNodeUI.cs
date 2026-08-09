using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Component representing a single level node in the level selection screen UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelNodeUI : MonoBehaviour
    {
        #region Inspector Fields

        public int levelNumber;
        
        [Header("UI References")]
        [SerializeField] private GameObject lockedStateObject;   // GameObject for locked state
        [SerializeField] private GameObject unlockedStateObject; // GameObject for unlocked state
        [SerializeField] private Image unlockedImage;            // Image on unlocked state to tint yellow if completed
        [SerializeField] private GameObject selectionArrow;      // Arrow for active level

        private bool m_IsUnlocked;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Button button = GetComponent<Button>();
            if (button == null)
            {
                button = GetComponentInChildren<Button>();
            }
            if (button != null)
            {
                button.onClick.AddListener(OnNodeClicked);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the visual state of the level node.
        /// </summary>
        public void SetupNode(bool isUnlocked, bool isCompleted, bool isSelected)
        {
            m_IsUnlocked = isUnlocked;

            if (selectionArrow != null)
            {
                selectionArrow.SetActive(isSelected);
            }

            if (!isUnlocked)
            {
                // Locked State
                if (lockedStateObject != null)
                {
                    lockedStateObject.SetActive(true);
                }
                if (unlockedStateObject != null)
                {
                    unlockedStateObject.SetActive(false);
                }
            }
            else
            {
                // Unlocked State
                if (lockedStateObject != null)
                {
                    lockedStateObject.SetActive(false);
                }
                if (unlockedStateObject != null)
                {
                    unlockedStateObject.SetActive(true);
                }

                // Auto-retrieve Image component if not assigned
                if (unlockedImage == null && unlockedStateObject != null)
                {
                    unlockedImage = unlockedStateObject.GetComponent<Image>();
                    if (unlockedImage == null)
                    {
                        unlockedImage = unlockedStateObject.GetComponentInChildren<Image>();
                    }
                }

                if (unlockedImage != null)
                {
                    if (isCompleted)
                    {
                        // Completed: tint yellow
                        unlockedImage.color = new Color(1f, 0.92f, 0.016f);
                    }
                    else
                    {
                        // Unlocked but not completed (active level): keep white/default
                        unlockedImage.color = Color.white;
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private void OnNodeClicked()
        {
            if (m_IsUnlocked)
            {
                // Load the scene corresponding to the level number
                UnityEngine.SceneManagement.SceneManager.LoadScene(levelNumber);
            }
        }

        #endregion
    }
}
