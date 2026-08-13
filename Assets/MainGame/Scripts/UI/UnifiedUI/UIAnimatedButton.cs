using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates buttons on focus/selection and hover state. Handles positioning shift,
    /// scale emphasis, and left/right indicators using a clean interruptible animation loop.
    /// Works automatically with Keyboard, Gamepad (EventSystem selection) and Mouse (Pointer events).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIAnimatedButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Animation Settings")]
        [Tooltip("Horizontal shift when selected.")]
        [SerializeField] private float m_SelectedOffsetX = 15f;

        [Tooltip("Scale modifier when selected.")]
        [SerializeField] private float m_SelectedScale = 1.04f;

        [Tooltip("Duration of focus in transition (seconds).")]
        [SerializeField] private float m_FocusInDuration = 0.12f;

        [Tooltip("Duration of focus out transition (seconds).")]
        [SerializeField] private float m_FocusOutDuration = 0.05f;

        [Header("Visual References")]
        [Tooltip("The main visual container transform of the button that will be offset/scaled.")]
        [SerializeField] private RectTransform m_ButtonVisual;

        [Tooltip("The left selection pointer (blue triangle).")]
        [SerializeField] private RectTransform m_LeftPointer;

        private Vector2 m_OriginalVisualPos;
        private Vector2 m_OriginalPointerPos;
        private Coroutine m_AnimationCoroutine;
        private bool m_IsFocused = false;

        private void Awake()
        {
            if (m_ButtonVisual == null)
            {
                // Fallback to own RectTransform if not assigned
                m_ButtonVisual = GetComponent<RectTransform>();
            }

            m_OriginalVisualPos = m_ButtonVisual.anchoredPosition;

            if (m_LeftPointer != null)
            {
                m_OriginalPointerPos = m_LeftPointer.anchoredPosition;
                // Initially hide pointers
                m_LeftPointer.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Reset state instantly on disable to avoid stuck visual issues
            ResetToNormalState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hovering over button sets it as the active selected object in the EventSystem
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                SetFocused(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Deselect if mouse leaves and it is currently the selected object
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                SetFocused(false);
            }
        }

        private void SetFocused(bool focused)
        {
            if (m_IsFocused == focused) return;
            m_IsFocused = focused;

            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
            }

            m_AnimationCoroutine = StartCoroutine(AnimateTransition(focused));
        }

        private IEnumerator AnimateTransition(bool focusIn)
        {
            float elapsed = 0f;
            float duration = focusIn ? m_FocusInDuration : m_FocusOutDuration;

            // Target visual positions
            Vector2 startPos = m_ButtonVisual.anchoredPosition;
            Vector2 targetPos = focusIn 
                ? new Vector2(m_OriginalVisualPos.x + m_SelectedOffsetX, m_OriginalVisualPos.y) 
                : m_OriginalVisualPos;

            // Target scales
            Vector3 startScale = m_ButtonVisual.localScale;
            Vector3 targetScale = focusIn 
                ? new Vector3(m_SelectedScale, m_SelectedScale, 1f) 
                : Vector3.one;

            // Enable or disable indicator objects immediately to ensure visual cleanliness during rapid navigation
            if (focusIn)
            {
                if (m_LeftPointer != null) m_LeftPointer.gameObject.SetActive(true);
            }
            else
            {
                if (m_LeftPointer != null) m_LeftPointer.gameObject.SetActive(false);
            }

            // Left Pointer sliding transitions
            Vector2 startPointerPos = m_LeftPointer != null ? m_LeftPointer.anchoredPosition : Vector2.zero;
            Vector2 targetPointerPos = m_OriginalPointerPos;
            Vector2 slidePointerFrom = new Vector2(m_OriginalPointerPos.x - 10f, m_OriginalPointerPos.y);

            // If entering focus, slide the pointer from the left, otherwise return to default anchor
            Vector2 pointerStart = focusIn ? slidePointerFrom : startPointerPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Ease out cubic evaluation: 1 - (1 - t)^3
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                m_ButtonVisual.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                m_ButtonVisual.localScale = Vector3.Lerp(startScale, targetScale, ease);

                // Only animate position if the pointer is active/visible
                if (m_LeftPointer != null && focusIn)
                {
                    m_LeftPointer.anchoredPosition = Vector2.Lerp(pointerStart, targetPointerPos, ease);
                }

                yield return null;
            }

            // Final snap
            m_ButtonVisual.anchoredPosition = targetPos;
            m_ButtonVisual.localScale = targetScale;
            if (m_LeftPointer != null && focusIn) m_LeftPointer.anchoredPosition = targetPointerPos;
        }

        private void ResetToNormalState()
        {
            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
                m_AnimationCoroutine = null;
            }

            m_IsFocused = false;

            if (m_ButtonVisual != null)
            {
                m_ButtonVisual.anchoredPosition = m_OriginalVisualPos;
                m_ButtonVisual.localScale = Vector3.one;
            }

            if (m_LeftPointer != null)
            {
                m_LeftPointer.anchoredPosition = m_OriginalPointerPos;
                m_LeftPointer.gameObject.SetActive(false);
            }
        }
    }
}
