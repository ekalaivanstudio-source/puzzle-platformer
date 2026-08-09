using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Represents a single segment of the level path between two nodes.
    /// Supports color changing (yellow/white) and horizontal filling transitions.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class UIPathSegment : MonoBehaviour
    {
        #region Public Fields

        [Tooltip("The level index that unlocks this path segment (e.g. 2 represents the path leading to Level 2).")]
        public int targetLevelIndex;

        [Header("Color Customization")]
        [SerializeField] private Color activeColor = new Color(1f, 0.78f, 0f); // Yellow
        [SerializeField] private Color inactiveColor = Color.white;            // White

        [Header("Animation Settings")]
        [Tooltip("If true, uses Image FillAmount to grow the path. (Requires a separate background image on the prefab to see the locked state).")]
        [SerializeField] private bool useFillAmountAnimation = false;

        #endregion

        #region Private Fields

        private Image lineImage;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            lineImage = GetComponent<Image>();
            if (lineImage != null && useFillAmountAnimation)
            {
                lineImage.type = Image.Type.Filled;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Instantly sets the filled/active state of the segment.
        /// </summary>
        public void SetFilled(bool isFilled)
        {
            if (lineImage == null) lineImage = GetComponent<Image>();
            if (lineImage != null)
            {
                if (useFillAmountAnimation)
                {
                    lineImage.type = Image.Type.Filled;
                    lineImage.fillAmount = isFilled ? 1f : 0f;
                    lineImage.color = activeColor;
                }
                else
                {
                    lineImage.type = Image.Type.Simple;
                    lineImage.color = isFilled ? activeColor : inactiveColor;
                }
            }
        }

        /// <summary>
        /// Coroutine to animate the line filling up or changing color over time.
        /// </summary>
        public IEnumerator AnimateFill(float duration)
        {
            if (lineImage == null) lineImage = GetComponent<Image>();
            if (lineImage == null) yield break;

            float elapsed = 0f;
            Color startColor = lineImage.color;

            if (useFillAmountAnimation)
            {
                lineImage.type = Image.Type.Filled;
                lineImage.color = activeColor;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (useFillAmountAnimation)
                {
                    lineImage.fillAmount = progress;
                }
                else
                {
                    lineImage.color = Color.Lerp(startColor, activeColor, progress);
                }

                yield return null;
            }

            if (useFillAmountAnimation)
            {
                lineImage.fillAmount = 1f;
            }
            else
            {
                lineImage.color = activeColor;
            }
        }

        #endregion
    }
}
