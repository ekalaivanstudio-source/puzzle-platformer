using System;
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
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            UpdateLabelSprite(false);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            // Select on mouse hover
            Select();
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
                currentValue = newValue;
                UpdateVisuals();
                OnValueChanged?.Invoke(currentValue);
            }
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
