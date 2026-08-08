using System;
using UnityEngine;
using UnityEngine.UI;

namespace Setting.Menu
{
    /// <summary>
    /// UI component for a settings row using 10 images to show values instead of a slider.
    /// Handles increase and decrease buttons and updates box sprites/colors accordingly.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingStepControl : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Controls")]
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;

        [Header("Visual Blocks")]
        [Tooltip("Exactly 10 image components representing the steps.")]
        [SerializeField] private Image[] stepImages = new Image[10];

        [Header("Sprites")]
        [Tooltip("Sprite used for active (filled) blocks (e.g., yellow sprite).")]
        [SerializeField] private Sprite activeSprite;
        [Tooltip("Sprite used for inactive (unfilled) blocks (e.g., white sprite).")]
        [SerializeField] private Sprite inactiveSprite;

        [Header("Colors (Optional tint fallback)")]
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = Color.white;

        #endregion

        #region Private Fields

        private int currentValue = 0; // Range: 0 to 10

        #endregion

        #region Events

        /// <summary>
        /// Triggered when the value changes. Passes the new integer value (0 to 10).
        /// </summary>
        public event Action<int> OnValueChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current step value (0 to 10).
        /// </summary>
        public int Value => currentValue;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (decreaseButton != null)
            {
                decreaseButton.onClick.AddListener(DecreaseValue);
            }

            if (increaseButton != null)
            {
                increaseButton.onClick.AddListener(IncreaseValue);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the value and updates the visual blocks without triggering the OnValueChanged event.
        /// </summary>
        /// <param name="value">New value (clamped between 0 and 10).</param>
        public void SetValueWithoutNotify(int value)
        {
            currentValue = Mathf.Clamp(value, 0, 10);
            UpdateVisuals();
        }

        /// <summary>
        /// Sets the value, updates visual blocks, and triggers the OnValueChanged event.
        /// </summary>
        /// <param name="value">New value (clamped between 0 and 10).</param>
        public void SetValue(int value)
        {
            int newValue = Mathf.Clamp(value, 0, 10);
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
            if (currentValue < 10)
            {
                SetValue(currentValue + 1);
            }
        }

        private void DecreaseValue()
        {
            if (currentValue > 0)
            {
                SetValue(currentValue - 1);
            }
        }

        /// <summary>
        /// Updates the sprites and colors of the 10 images based on the currentValue.
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
