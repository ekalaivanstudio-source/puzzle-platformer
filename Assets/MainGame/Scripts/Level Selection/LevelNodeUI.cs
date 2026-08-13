using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace LevelSelection
{
    /// <summary>
    /// Component representing a single level node in the level selection screen UI.
    /// Supports dynamic selection arrow toggles on focus.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelNodeUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
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

        private Coroutine m_PulseCoroutine;

        public void OnSelect(BaseEventData eventData)
        {
            SetArrowActive(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetArrowActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hovering sets selection in EventSystem
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                SetArrowActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                SetArrowActive(false);
            }
        }

        private void SetArrowActive(bool active)
        {
            if (m_PulseCoroutine != null)
            {
                StopCoroutine(m_PulseCoroutine);
                m_PulseCoroutine = null;
            }

            if (selectionArrow != null)
            {
                selectionArrow.SetActive(active);
                if (active)
                {
                    m_PulseCoroutine = StartCoroutine(ArrowPulseRoutine());
                }
            }
        }

        private IEnumerator ArrowPulseRoutine()
        {
            if (selectionArrow == null) yield break;

            Vector3 originalScale = selectionArrow.transform.localScale;
            Vector3 originalLocalPos = selectionArrow.transform.localPosition;
            float elapsed = 0f;

            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                
                // Gentle pulse: sine wave oscillating scale between 95% and 105% at 6Hz frequency
                float scaleOffset = Mathf.Sin(elapsed * 6f) * 0.05f;
                selectionArrow.transform.localScale = originalScale * (1f + scaleOffset);

                // Bobbing: vertical shift up and down by 8 units using sine wave
                float bobOffset = Mathf.Sin(elapsed * 6f) * 8f;
                selectionArrow.transform.localPosition = new Vector3(originalLocalPos.x, originalLocalPos.y + bobOffset, originalLocalPos.z);

                yield return null;
            }
        }

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
