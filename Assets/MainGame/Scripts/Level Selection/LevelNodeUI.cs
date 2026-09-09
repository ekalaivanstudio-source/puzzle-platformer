using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System;

namespace LevelSelection
{
    /// <summary>
    /// Component representing a single level node in the level selection screen UI.
    /// Supports dynamic selection arrow toggles on focus, map activation boot routines,
    /// selection micro-lifts, and punch feedback.
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

        #region Public Properties

        public RectTransform RectTransform => (RectTransform)transform;
        public bool IsUnlocked => m_IsUnlocked;
        public bool HasMarker
        {
            get
            {
                LevelSelectionManager mgr = ResolveManager();
                return mgr != null && mgr.Pointer != null && mgr.Pointer.CurrentTargetNode == this && mgr.Pointer.IsVisible;
            }
        }

        #endregion

        #region Private Fields

        private static readonly Color UnlockedColor = Color.white;

        private bool m_IsUnlocked;
        private Button m_Button;
        private LevelSelectionManager m_Manager;
        private Coroutine m_PulseCoroutine;
        private Coroutine m_LiftCoroutine;
        private Coroutine m_BootCoroutine;
        private bool m_IsLoading;

        // Resting transform of the arrow, captured before any pulse runs so the animation
        // can always be rewound exactly instead of drifting a little further each time.
        private Vector3 m_ArrowRestScale = Vector3.one;
        private Vector3 m_ArrowRestLocalPos;

        // Node base transform for micro-lift and punch routines
        private Vector3 m_NodeBasePos;
        private Vector3 m_NodeBaseScale = Vector3.one;
        private bool m_BaseCaptured;

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
                selectionArrow.SetActive(false);
            }

            CaptureBaseTransform();
        }

        private void OnEnable()
        {
            if (m_Button != null) m_Button.onClick.AddListener(OnNodeClicked);
        }

        private void OnDisable()
        {
            if (m_Button != null) m_Button.onClick.RemoveListener(OnNodeClicked);
            StopPulse();
            if (m_LiftCoroutine != null)
            {
                StopCoroutine(m_LiftCoroutine);
                m_LiftCoroutine = null;
            }
            if (m_BootCoroutine != null)
            {
                StopCoroutine(m_BootCoroutine);
                m_BootCoroutine = null;
            }
        }

        #endregion

        #region Base Transform Capture

        public void CaptureBaseTransform()
        {
            m_NodeBasePos = transform.localPosition;
            m_NodeBaseScale = Vector3.one;
            m_BaseCaptured = true;
        }

        #endregion

        #region Selection Handling

        public void OnSelect(BaseEventData eventData)
        {
            SetLift(true);
            LevelSelectionManager manager = ResolveManager();
            if (manager != null)
            {
                manager.OnNodeSelected(this);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetLift(false);
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
                SetLift(true);
                ResolveManager()?.OnNodeSelected(this);
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

            // Strict lock: if the manager is currently playing an entrance or arc transition, ignore edge paging
            if (manager.IsTransitioning)
            {
                eventData.Use();
                return;
            }

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

        #region Micro-Lift Animation

        private void SetLift(bool lifted)
        {
            if (!m_BaseCaptured) CaptureBaseTransform();

            if (m_LiftCoroutine != null) StopCoroutine(m_LiftCoroutine);
            if (isActiveAndEnabled)
            {
                m_LiftCoroutine = StartCoroutine(LiftRoutine(lifted));
            }
            else
            {
                transform.localPosition = lifted ? (m_NodeBasePos + new Vector3(0f, 5f, 0f)) : m_NodeBasePos;
                transform.localScale = lifted ? (m_NodeBaseScale * 1.04f) : m_NodeBaseScale;
            }
        }

        private IEnumerator LiftRoutine(bool lifted)
        {
            Vector3 startPos = transform.localPosition;
            Vector3 targetPos = lifted ? (m_NodeBasePos + new Vector3(0f, 5f, 0f)) : m_NodeBasePos;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = lifted ? (m_NodeBaseScale * 1.04f) : m_NodeBaseScale;

            float elapsed = 0f;
            float duration = 0.12f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localPosition = targetPos;
            transform.localScale = targetScale;
            m_LiftCoroutine = null;
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

        #region Map Network Boot & Marker Drop Routines

        /// <summary>
        /// Resets the node to pre-boot state for the map network activation sequence.
        /// </summary>
        public void SetInitialBootState()
        {
            StopAllCoroutines();
            m_BootCoroutine = null;
            m_LiftCoroutine = null;
            m_PulseCoroutine = null;

            CaptureBaseTransform();
            StopPulse();
            if (selectionArrow != null) selectionArrow.SetActive(false);

            transform.localScale = Vector3.zero;
            transform.localPosition = m_NodeBasePos;
            m_IsLoading = false;
        }

        /// <summary>
        /// Boots up this level node during the sequential map network activation.
        /// </summary>
        public IEnumerator PlayBootUpRoutine(bool isUnlocked, float delay)
        {
            CaptureBaseTransform();
            transform.localScale = Vector3.zero;
            transform.localPosition = m_NodeBasePos;

            if (delay > 0f)
            {
                float timer = 0f;
                while (timer < delay)
                {
                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            float duration = isUnlocked ? 0.28f : 0.18f;
            Color origColor = unlockedImage != null ? unlockedImage.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (isUnlocked)
                {
                    // Overshoot scale: 0 -> 1.15 -> 1.0
                    float s = (t < 0.6f) ? Mathf.LerpUnclamped(0f, 1.15f, EaseOutQuad(t / 0.6f)) : Mathf.Lerp(1.15f, 1f, EaseOutQuad((t - 0.6f) / 0.4f));
                    transform.localScale = m_NodeBaseScale * s;

                    // Vertical micro lift: +6px -> 0px
                    float lift = (t < 0.5f) ? Mathf.Lerp(0f, 6f, t / 0.5f) : Mathf.Lerp(6f, 0f, (t - 0.5f) / 0.5f);
                    transform.localPosition = m_NodeBasePos + new Vector3(0f, lift, 0f);

                    // Flash effect
                    if (unlockedImage != null && t < 0.4f)
                    {
                        unlockedImage.color = Color.Lerp(Color.white * 1.5f, origColor, t / 0.4f);
                    }
                }
                else
                {
                    // Locked snap in with dim snap: 0 -> 1.0
                    float s = Mathf.LerpUnclamped(0f, 1f, EaseOutBack(t));
                    transform.localScale = m_NodeBaseScale * s;
                }

                yield return null;
            }

            transform.localScale = m_NodeBaseScale;
            transform.localPosition = m_NodeBasePos;
            if (unlockedImage != null) unlockedImage.color = origColor;
            m_BootCoroutine = null;
        }

        /// <summary>
        /// Drops the single authoritative level pointer from above onto this node with physics bounce.
        /// </summary>
        public IEnumerator PlayMarkerDropRoutine(Action onDone = null)
        {
            StopPulse();
            if (selectionArrow != null) selectionArrow.SetActive(false);

            LevelSelectionManager mgr = ResolveManager();
            if (mgr != null && mgr.Pointer != null)
            {
                mgr.Pointer.PlayEntranceDrop(this, onDone);
            }
            else
            {
                onDone?.Invoke();
            }
            yield break;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the visual state of the level node.
        /// </summary>
        public void SetupNode(bool isUnlocked, bool isCompleted, bool isSelected)
        {
            m_IsUnlocked = isUnlocked;

            if (m_Button != null)
            {
                m_Button.interactable = true;
            }

            if (selectionArrow != null)
            {
                selectionArrow.SetActive(false);
            }

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

        #region Private Methods & Easing

        private void OnNodeClicked()
        {
            if (!m_IsUnlocked || m_IsLoading) return;

            StartCoroutine(ConfirmPunchAndLoadRoutine());
        }

        private IEnumerator ConfirmPunchAndLoadRoutine()
        {
            m_IsLoading = true;
            CaptureBaseTransform();

            float elapsed = 0f;
            float duration = 0.14f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Fast squash and punch out
                float s = (t < 0.35f) ? Mathf.Lerp(1.04f, 0.9f, t / 0.35f) : Mathf.Lerp(0.9f, 1.15f, (t - 0.35f) / 0.65f);
                transform.localScale = m_NodeBaseScale * s;
                yield return null;
            }

            // Load the scene corresponding to the level number
            UnityEngine.SceneManagement.SceneManager.LoadScene(levelNumber);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        #endregion
    }
}
