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
    public class LevelNodeUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IMoveHandler
    {
        #region Inspector Fields

        public int levelNumber;

        [Header("UI References")]
        [SerializeField] private GameObject lockedStateObject;   // GameObject for locked state
        [SerializeField] private GameObject unlockedStateObject; // GameObject for unlocked state
        [SerializeField] private Image unlockedImage;            // Image on unlocked state to tint yellow if completed
        [SerializeField] private GameObject selectionArrow;      // Arrow for active level

        [Header("Arrow Pulse")]
        [Tooltip("Oscillations per second of the selection arrow pulse.")]
        [SerializeField] private float arrowPulseSpeed = 6f;
        [Tooltip("Peak scale deviation of the pulse, as a fraction of the arrow's resting scale.")]
        [SerializeField] private float arrowPulseScale = 0.05f;
        [Tooltip("Peak vertical bob of the arrow, in local units.")]
        [SerializeField] private float arrowBobDistance = 8f;

        [Header("Completed Tint")]
        [SerializeField] private Color completedColor = new Color(1f, 0.92f, 0.016f);

        #endregion

        #region Private Fields

        private static readonly Color UnlockedColor = Color.white;

        private bool m_IsUnlocked;
        private Button m_Button;
        private LevelSelectionManager m_Manager;
        private Coroutine m_PulseCoroutine;

        // Resting transform of the arrow, captured before any pulse runs so the animation
        // can always be rewound exactly instead of drifting a little further each time.
        private Vector3 m_ArrowRestScale = Vector3.one;
        private Vector3 m_ArrowRestLocalPos;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Button = GetComponent<Button>();
            if (m_Button == null)
            {
                m_Button = GetComponentInChildren<Button>(true);
            }

            if (selectionArrow != null)
            {
                m_ArrowRestScale = selectionArrow.transform.localScale;
                m_ArrowRestLocalPos = selectionArrow.transform.localPosition;
            }
        }

        private void OnEnable()
        {
            if (m_Button != null) m_Button.onClick.AddListener(OnNodeClicked);
        }

        private void OnDisable()
        {
            if (m_Button != null) m_Button.onClick.RemoveListener(OnNodeClicked);
            StopPulse();
        }

        #endregion

        #region Selection Handling

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
            // Do not clear the selected GameObject on hover exit to keep focus persistent (like main menu buttons)
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir != MoveDirection.Right && eventData.moveDir != MoveDirection.Left)
            {
                return;
            }

            LevelSelectionManager manager = ResolveManager();
            if (manager == null) return;

            // Moving past either end of the arc pages to the neighbouring arc instead of dead-ending.
            if (eventData.moveDir == MoveDirection.Right)
            {
                if (manager.IsLastLevelOfCurrentArc(levelNumber) && manager.CanGoToNextArc())
                {
                    manager.GoToNextArc();
                    eventData.Use();
                }
            }
            else if (manager.IsFirstLevelOfCurrentArc(levelNumber) && manager.CanGoToPrevArc())
            {
                manager.GoToPrevArc();
                eventData.Use();
            }
        }

        private LevelSelectionManager ResolveManager()
        {
            if (m_Manager == null)
            {
                // Nodes are spawned under the manager's containers, so the parent lookup normally hits.
                m_Manager = GetComponentInParent<LevelSelectionManager>();
            }
            if (m_Manager == null)
            {
                m_Manager = FindAnyObjectByType<LevelSelectionManager>();
            }
            return m_Manager;
        }

        #endregion

        #region Arrow Animation

        private void SetArrowActive(bool active)
        {
            StopPulse();

            if (selectionArrow == null) return;

            selectionArrow.SetActive(active);
            if (active && isActiveAndEnabled)
            {
                m_PulseCoroutine = StartCoroutine(ArrowPulseRoutine());
            }
        }

        /// <summary>
        /// Stops any running pulse and rewinds the arrow to its resting transform, so repeated
        /// select/deselect cycles never accumulate scale or position drift.
        /// </summary>
        private void StopPulse()
        {
            if (m_PulseCoroutine != null)
            {
                StopCoroutine(m_PulseCoroutine);
                m_PulseCoroutine = null;
            }

            if (selectionArrow != null)
            {
                selectionArrow.transform.localScale = m_ArrowRestScale;
                selectionArrow.transform.localPosition = m_ArrowRestLocalPos;
            }
        }

        private IEnumerator ArrowPulseRoutine()
        {
            float elapsed = 0f;

            while (selectionArrow != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(elapsed * arrowPulseSpeed);

                selectionArrow.transform.localScale = m_ArrowRestScale * (1f + wave * arrowPulseScale);
                selectionArrow.transform.localPosition = new Vector3(
                    m_ArrowRestLocalPos.x,
                    m_ArrowRestLocalPos.y + wave * arrowBobDistance,
                    m_ArrowRestLocalPos.z);

                yield return null;
            }

            m_PulseCoroutine = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the visual state of the level node.
        /// </summary>
        /// <remarks>
        /// Locked nodes stay interactable on purpose: they remain reachable by keyboard/controller
        /// navigation so the player can see what is coming. <see cref="OnNodeClicked"/> is what
        /// refuses to load a locked level.
        /// </remarks>
        public void SetupNode(bool isUnlocked, bool isCompleted, bool isSelected)
        {
            m_IsUnlocked = isUnlocked;

            if (m_Button != null)
            {
                m_Button.interactable = true;
            }

            SetArrowActive(isSelected);

            if (lockedStateObject != null)
            {
                lockedStateObject.SetActive(!isUnlocked);
            }
            if (unlockedStateObject != null)
            {
                unlockedStateObject.SetActive(isUnlocked);
            }

            if (!isUnlocked) return;

            // Auto-retrieve Image component if not assigned
            if (unlockedImage == null && unlockedStateObject != null)
            {
                unlockedImage = unlockedStateObject.GetComponent<Image>();
                if (unlockedImage == null)
                {
                    unlockedImage = unlockedStateObject.GetComponentInChildren<Image>(true);
                }
            }

            if (unlockedImage != null)
            {
                // Completed levels are tinted; the current (unlocked, unbeaten) level stays default.
                unlockedImage.color = isCompleted ? completedColor : UnlockedColor;
            }
        }

        #endregion

        #region Private Methods

        private void OnNodeClicked()
        {
            if (!m_IsUnlocked) return;

            // Load the scene corresponding to the level number
            UnityEngine.SceneManagement.SceneManager.LoadScene(levelNumber);
        }

        #endregion
    }
}
