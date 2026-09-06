using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MainGame.UI.Animation;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Adds controller-first selection motion, living heartbeat pulse, and spotlight isolation
    /// (dimming unselected cards) to a robot collection slot card.
    /// Inherits from Selectable to integrate directly into EventSystem keyboard & gamepad navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectionCardSelectable : Selectable
    {
        [Header("Physical Selection Motion")]
        [Tooltip("Upward shift in pixels when this card is focused/selected.")]
        [SerializeField] private float m_SelectMoveY = 14f;

        [Tooltip("Scale factor when focused/selected.")]
        [SerializeField] private float m_SelectScale = 1.08f;

        [Tooltip("Duration of the focus transition (seconds).")]
        [SerializeField] private float m_TransitionDuration = 0.08f;

        [Header("Visual Container")]
        [Tooltip("Transform of the card visual that shifts and scales. Defaults to self if not set.")]
        [SerializeField] private RectTransform m_CardVisual;

        private Vector2 m_RestPosition;
        private Vector3 m_RestScale = Vector3.one;
        private Coroutine m_MotionCoroutine;
        private Coroutine m_LivingIdleCoroutine;
        private CanvasGroup m_CanvasGroup;
        private bool m_HasCapturedRest = false;

        protected override void Awake()
        {
            base.Awake();

            if (m_CardVisual == null)
            {
                m_CardVisual = GetComponent<RectTransform>();
            }

            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Ensure there is a transparent Graphic target so EventSystem can select this element
            if (targetGraphic == null)
            {
                Graphic graphic = GetComponent<Graphic>();
                if (graphic == null)
                {
                    Image img = gameObject.AddComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0f);
                    img.raycastTarget = true;
                    targetGraphic = img;
                }
                else
                {
                    targetGraphic = graphic;
                }
            }

            CaptureRestState();
        }

        protected override void Start()
        {
            base.Start();
            CaptureRestState();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest || m_CardVisual == null) return;

            m_RestPosition = m_CardVisual.anchoredPosition;
            m_RestScale = m_CardVisual.localScale;
            m_HasCapturedRest = true;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            AnimateFocus(true);
            SetSpotlightIsolation(true);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            AnimateFocus(false);
            SetSpotlightIsolation(false);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                AnimateFocus(true);
                SetSpotlightIsolation(true);
            }
        }

        private void SetSpotlightIsolation(bool isolated)
        {
            if (transform.parent == null) return;

            var siblingCards = transform.parent.GetComponentsInChildren<CollectionCardSelectable>(true);
            if (siblingCards == null) return;

            for (int i = 0; i < siblingCards.Length; i++)
            {
                CollectionCardSelectable sibling = siblingCards[i];
                if (sibling == null) continue;

                CanvasGroup cg = sibling.GetComponent<CanvasGroup>();
                if (cg == null) continue;

                if (isolated)
                {
                    cg.alpha = (sibling == this) ? 1.0f : 0.65f;
                }
                else
                {
                    cg.alpha = 1.0f;
                }
            }
        }

        private void AnimateFocus(bool focused)
        {
            CaptureRestState();

            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }

            if (m_MotionCoroutine != null)
            {
                StopCoroutine(m_MotionCoroutine);
            }

            m_MotionCoroutine = StartCoroutine(FocusRoutine(focused));
        }

        private IEnumerator FocusRoutine(bool focused)
        {
            float elapsed = 0f;
            float duration = m_TransitionDuration;

            Vector2 startPos = m_CardVisual.anchoredPosition;
            Vector2 targetPos = focused 
                ? new Vector2(m_RestPosition.x, m_RestPosition.y + m_SelectMoveY) 
                : m_RestPosition;

            Vector3 startScale = m_CardVisual.localScale;
            Vector3 targetScale = focused 
                ? new Vector3(m_SelectScale, m_SelectScale, 1f) 
                : m_RestScale;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = focused
                    ? UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f)
                    : UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                m_CardVisual.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                m_CardVisual.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
                yield return null;
            }

            m_CardVisual.anchoredPosition = targetPos;
            m_CardVisual.localScale = targetScale;
            m_MotionCoroutine = null;

            if (focused)
            {
                m_LivingIdleCoroutine = StartCoroutine(LivingIdleRoutine());
            }
        }

        private IEnumerator LivingIdleRoutine()
        {
            Vector3 baseScale = new Vector3(m_SelectScale, m_SelectScale, 1f);

            while (true)
            {
                float time = Time.unscaledTime;
                float pulse = Mathf.Sin(time * 4.5f) * 0.015f;
                if (m_CardVisual != null)
                {
                    m_CardVisual.localScale = baseScale + new Vector3(pulse, pulse, 0f);
                }
                yield return null;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_LivingIdleCoroutine != null)
            {
                StopCoroutine(m_LivingIdleCoroutine);
                m_LivingIdleCoroutine = null;
            }

            if (m_MotionCoroutine != null)
            {
                StopCoroutine(m_MotionCoroutine);
                m_MotionCoroutine = null;
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 1.0f;
            }

            if (m_HasCapturedRest && m_CardVisual != null)
            {
                m_CardVisual.anchoredPosition = m_RestPosition;
                m_CardVisual.localScale = m_RestScale;
            }
        }
    }
}
