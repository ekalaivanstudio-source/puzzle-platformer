using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Setting.Menu
{
    /// <summary>
    /// UI component for a settings row using 10 images to show values instead of a slider.
    /// Inherits from Selectable to support EventSystem selection focus (WASD/Controller).
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingStepControl : Selectable, IMoveHandler, IPointerClickHandler
    {
        #region Inspector Fields

        [Header("Controls (Optional/Visual only)")]
        [SerializeField] private RectTransform decreaseButtonTransform;
        [SerializeField] private RectTransform increaseButtonTransform;

        [Header("Visual Blocks")]
        [Tooltip("Exactly 10 image components representing the steps.")]
        [SerializeField] private Image[] stepImages = new Image[MaxValue];

        [Header("Sprites")]
        [Tooltip("Sprite used for active (filled) blocks (e.g., yellow sprite).")]
        [SerializeField] private Sprite activeSprite;
        [Tooltip("Sprite used for inactive (unfilled) blocks (e.g., white sprite).")]
        [SerializeField] private Sprite inactiveSprite;

        [Header("Colors (Optional tint fallback)")]
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = Color.white;

        [Header("Header Label Sprite Swap")]
        [Tooltip("The stylized label Image on the left (e.g. Button name).")]
        [SerializeField] private Image labelImage;
        [Tooltip("Sprite used for the label when this setting row is selected/focused.")]
        [SerializeField] private Sprite labelSelectedSprite;
        [Tooltip("Sprite used for the label in normal state.")]
        [SerializeField] private Sprite labelNormalSprite;

        [Header("Physical Selection Motion")]
        [Tooltip("Scale factor of the row when selected/focused.")]
        [SerializeField] private float selectScale = 1.03f;
        [Tooltip("Horizontal shift of the label when selected/focused.")]
        [SerializeField] private float selectShiftX = 8f;

        #endregion

        #region Constants

        /// <summary>
        /// Highest value this control can hold; also the number of visual blocks.
        /// Callers converting to/from a 0..1 range scale by this value.
        /// </summary>
        public const int MaxValue = 10;

        #endregion

        #region Private Fields

        private int currentValue = 0; // Range: 0 to MaxValue
        private Coroutine m_BlockPunchCoroutine;
        private Coroutine m_SelectMotionCoroutine;
        private Vector2 m_OriginalLabelPos;
        private bool m_CapturedLabelPos;

        #endregion

        #region Events

        /// <summary>
        /// Triggered when the value changes. Passes the new integer value (0 to 10).
        /// </summary>
        public event Action<int> OnValueChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current step value (0 to <see cref="MaxValue"/>).
        /// </summary>
        public int Value => currentValue;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            // Ensure there is a graphic on this GameObject so it can receive focus
            if (targetGraphic == null)
            {
                Image img = GetComponent<Image>();
                if (img == null)
                {
                    img = gameObject.AddComponent<Image>();
                    // Make it fully transparent so it doesn't block the visual design
                    img.color = new Color(1f, 1f, 1f, 0f);
                }
                img.raycastTarget = true;
                targetGraphic = img;
            }

            // Disable raycast target on all children so clicks pass through to this parent script
            if (stepImages != null)
            {
                foreach (var stepImg in stepImages)
                {
                    if (stepImg != null)
                    {
                        stepImg.raycastTarget = false;
                    }
                }
            }

            if (decreaseButtonTransform != null)
            {
                var images = decreaseButtonTransform.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    img.raycastTarget = false;
                }
            }

            if (increaseButtonTransform != null)
            {
                var images = increaseButtonTransform.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    img.raycastTarget = false;
                }
            }

            if (labelImage != null)
            {
                labelImage.raycastTarget = false;
            }

            // Navigation is intentionally left as authored in the inspector. OptionsScreen rebuilds it
            // explicitly once the screen opens; overwriting it here would discard the designer setup and
            // let Automatic navigation jump sideways out of the settings list for a frame.

            // Ensure start visual matches normal state
            UpdateLabelSprite(false);
        }

        #endregion

        #region Selection Visual Feedback Override

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            UpdateLabelSprite(true);
            AnimateSelectionFocus(true);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            UpdateLabelSprite(false);
            AnimateSelectionFocus(false);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            Select();
        }

        private void AnimateSelectionFocus(bool focused)
        {
            if (labelImage != null)
            {
                RectTransform labelRt = labelImage.rectTransform;
                if (!m_CapturedLabelPos)
                {
                    m_OriginalLabelPos = labelRt.anchoredPosition;
                    m_CapturedLabelPos = true;
                }

                if (m_SelectMotionCoroutine != null) StopCoroutine(m_SelectMotionCoroutine);
                m_SelectMotionCoroutine = StartCoroutine(SelectionMotionRoutine(labelRt, focused));
            }
        }

        private IEnumerator SelectionMotionRoutine(RectTransform labelRt, bool focused)
        {
            float duration = 0.12f;
            float elapsed = 0f;
            Vector2 startPos = labelRt.anchoredPosition;
            Vector2 targetPos = focused 
                ? new Vector2(m_OriginalLabelPos.x + selectShiftX, m_OriginalLabelPos.y) 
                : m_OriginalLabelPos;

            Vector3 startScale = transform.localScale;
            Vector3 targetScale = focused 
                ? new Vector3(selectScale, selectScale, 1f) 
                : Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                labelRt.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                transform.localScale = Vector3.Lerp(startScale, targetScale, ease);
                yield return null;
            }

            labelRt.anchoredPosition = targetPos;
            transform.localScale = targetScale;
            m_SelectMotionCoroutine = null;
        }

        private void UpdateLabelSprite(bool selected)
        {
            if (labelImage != null)
            {
                Sprite targetSprite = selected ? labelSelectedSprite : labelNormalSprite;
                if (targetSprite != null)
                {
                    labelImage.sprite = targetSprite;
                }
            }
        }

        #endregion

        #region IPointerClickHandler Implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            // Only change the value if clicking the decrease/increase button areas
            if (decreaseButtonTransform != null && IsPointInRect(decreaseButtonTransform, eventData.position, eventData.pressEventCamera))
            {
                DecreaseValue();
            }
            else if (increaseButtonTransform != null && IsPointInRect(increaseButtonTransform, eventData.position, eventData.pressEventCamera))
            {
                IncreaseValue();
            }
        }

        #endregion

        #region IMoveHandler Implementation

        /// <summary>
        /// Intercepts navigation inputs. If the direction is horizontal, changes values and consumes the event.
        /// If the direction is vertical, lets the event continue so that EventSystem moves focus to other UI elements.
        /// </summary>
        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    DecreaseValue();
                    eventData.Use(); // Consume event
                    break;
                case MoveDirection.Right:
                    IncreaseValue();
                    eventData.Use(); // Consume event
                    break;
                // Let the base Selectable handle Up and Down directions to move focus
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the value and updates the visual blocks without triggering the OnValueChanged event.
        /// </summary>
        /// <param name="value">New value (clamped between 0 and <see cref="MaxValue"/>).</param>
        public void SetValueWithoutNotify(int value)
        {
            currentValue = Mathf.Clamp(value, 0, MaxValue);
            UpdateVisuals();
        }

        /// <summary>
        /// Sets the value, updates visual blocks, and triggers the OnValueChanged event.
        /// </summary>
        /// <param name="value">New value (clamped between 0 and <see cref="MaxValue"/>).</param>
        public void SetValue(int value)
        {
            int newValue = Mathf.Clamp(value, 0, MaxValue);
            if (currentValue != newValue)
            {
                int changedIndex = (newValue > currentValue) ? newValue - 1 : currentValue - 1;
                currentValue = newValue;
                UpdateVisuals();
                PunchBlock(changedIndex);
                OnValueChanged?.Invoke(currentValue);
            }
        }

        private void PunchBlock(int index)
        {
            if (stepImages == null || index < 0 || index >= stepImages.Length) return;
            Image img = stepImages[index];
            if (img == null) return;

            if (m_BlockPunchCoroutine != null) StopCoroutine(m_BlockPunchCoroutine);
            m_BlockPunchCoroutine = StartCoroutine(BlockPunchRoutine(img.rectTransform));
        }

        private IEnumerator BlockPunchRoutine(RectTransform target)
        {
            if (target == null) yield break;
            float duration = 0.12f;
            float elapsed = 0f;
            Vector3 startScale = new Vector3(1.25f, 1.25f, 1f);
            Vector3 endScale = Vector3.one;

            target.localScale = startScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                target.localScale = Vector3.Lerp(startScale, endScale, ease);
                yield return null;
            }
            target.localScale = endScale;
            m_BlockPunchCoroutine = null;
        }

        #endregion

        #region Private Methods

        private void IncreaseValue()
        {
            SetValue(currentValue + 1);
        }

        private void DecreaseValue()
        {
            SetValue(currentValue - 1);
        }

        private bool IsPointInRect(RectTransform rect, Vector2 screenPoint, Camera cam)
        {
            if (rect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
        }

        /// <summary>
        /// Updates the sprites and colors of the step images based on the currentValue.
        /// </summary>
        private void UpdateVisuals()
        {
            if (stepImages == null) return;

            for (int i = 0; i < stepImages.Length; i++)
            {
                if (stepImages[i] == null) continue;

                // Active blocks (i < currentValue)
                if (i < currentValue)
                {
                    if (activeSprite != null)
                    {
                        stepImages[i].sprite = activeSprite;
                    }
                    stepImages[i].color = activeColor;
                }
                // Inactive blocks (i >= currentValue)
                else
                {
                    if (inactiveSprite != null)
                    {
                        stepImages[i].sprite = inactiveSprite;
                    }
                    stepImages[i].color = inactiveColor;
                }
            }
        }

        #endregion
    }
}
