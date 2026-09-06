using System;
using System.Collections;
using UnityEngine;
using MainGame.UI.Animation;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Collection screen entrance and exit:
    /// panel scaling with micro-shake, COLLECTION signboard landing with decaying pendulum,
    /// ambient rope breeze sway, counter reveal, 4-corner physical card entrances with
    /// portrait delay separation, and complete out-of-screen exits.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectionScreenAnimator : MonoBehaviour
    {
        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelStartScale = 0.95f;
        [SerializeField] private float m_PanelDuration = 0.36f;

        [Header("Header Elements")]
        [Tooltip("COLLECTION signboard entering from upper-right with physical pendulum decay.")]
        [SerializeField] private RectTransform m_CollectionSign;
        [Tooltip("0/20 total counter entering with slight delay.")]
        [SerializeField] private RectTransform m_TotalCounter;

        [Header("Character Cards (Echo, Nova, Patch, Pixel)")]
        [SerializeField] private RectTransform m_CardEcho;
        [SerializeField] private RectTransform m_CardNova;
        [SerializeField] private RectTransform m_CardPatch;
        [SerializeField] private RectTransform m_CardPixel;

        [Header("Back Button")]
        [SerializeField] private RectTransform m_BackButton;

        private Vector2 m_PanelRestPos;
        private Vector3 m_PanelRestScale = Vector3.one;

        private Vector2 m_SignRestPos;
        private Vector3 m_SignRestAngles;

        private Vector2 m_CounterRestPos;
        private Vector2 m_BackRestPos;

        private RectTransform[] m_CardRects;
        private Vector2[] m_CardRestPositions;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_SignBreezeRoutine;
        private bool m_HasCapturedRest = false;

        private void Awake()
        {
            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_PanelBackground == null)
            {
                Transform bg = transform.Find("Holder/BG") ?? transform.Find("BG");
                if (bg != null) m_PanelBackground = bg as RectTransform;
            }

            if (m_CollectionSign == null)
            {
                Transform sign = transform.Find("Holder/Setting IMG") ?? transform.Find("Setting IMG");
                if (sign != null) m_CollectionSign = sign as RectTransform;
            }

            if (m_TotalCounter == null)
            {
                Transform total = transform.Find("Holder/RobotCollection/Total") ?? transform.Find("RobotCollection/Total");
                if (total != null) m_TotalCounter = total as RectTransform;
            }

            if (m_CardEcho == null)
            {
                Transform echo = transform.Find("Holder/RobotCollection/Robots/Slot_Echo") ?? transform.Find("RobotCollection/Robots/Slot_Echo");
                if (echo != null) m_CardEcho = echo as RectTransform;
            }

            if (m_CardNova == null)
            {
                Transform nova = transform.Find("Holder/RobotCollection/Robots/Slot_Nova") ?? transform.Find("RobotCollection/Robots/Slot_Nova");
                if (nova != null) m_CardNova = nova as RectTransform;
            }

            if (m_CardPatch == null)
            {
                Transform patch = transform.Find("Holder/RobotCollection/Robots/Slot_Patch") ?? transform.Find("RobotCollection/Robots/Slot_Patch");
                if (patch != null) m_CardPatch = patch as RectTransform;
            }

            if (m_CardPixel == null)
            {
                Transform pixel = transform.Find("Holder/RobotCollection/Robots/Slot_Pixel") ?? transform.Find("RobotCollection/Robots/Slot_Pixel");
                if (pixel != null) m_CardPixel = pixel as RectTransform;
            }

            if (m_BackButton == null)
            {
                Transform back = transform.Find("Holder/Back B") ?? transform.Find("Holder/B Back") ?? transform.Find("Back B") ?? transform.Find("B Back");
                if (back != null) m_BackButton = back as RectTransform;
            }

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
                m_PanelRestScale = m_PanelBackground.localScale;
            }

            if (m_CollectionSign != null)
            {
                m_SignRestPos = m_CollectionSign.anchoredPosition;
                m_SignRestAngles = m_CollectionSign.localEulerAngles;
            }

            if (m_TotalCounter != null)
            {
                m_CounterRestPos = m_TotalCounter.anchoredPosition;
            }

            if (m_BackButton != null)
            {
                m_BackRestPos = m_BackButton.anchoredPosition;
            }

            m_CardRects = new RectTransform[] { m_CardEcho, m_CardNova, m_CardPatch, m_CardPixel };
            m_CardRestPositions = new Vector2[m_CardRects.Length];

            for (int i = 0; i < m_CardRects.Length; i++)
            {
                if (m_CardRects[i] != null)
                {
                    m_CardRestPositions[i] = m_CardRects[i].anchoredPosition;
                }
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_PanelBackground != null)
            {
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = m_PanelRestScale;
            }

            if (m_CollectionSign != null)
            {
                m_CollectionSign.anchoredPosition = m_SignRestPos;
                m_CollectionSign.localEulerAngles = m_SignRestAngles;
            }

            if (m_TotalCounter != null)
            {
                m_TotalCounter.anchoredPosition = m_CounterRestPos;
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = m_BackRestPos;
                m_BackButton.localScale = Vector3.one;
            }

            if (m_CardRects != null)
            {
                for (int i = 0; i < m_CardRects.Length; i++)
                {
                    if (m_CardRects[i] != null)
                    {
                        m_CardRects[i].anchoredPosition = m_CardRestPositions[i];
                        m_CardRects[i].localScale = Vector3.one;
                    }
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(EntranceRoutine(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(ExitRoutine(onComplete));
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }
        }

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Dark panel establishes itself with micro-shake
            if (m_PanelBackground != null)
            {
                Vector3 startScale = m_PanelRestScale * m_PanelStartScale;
                m_PanelBackground.localScale = startScale;
                StartCoroutine(AnimateScale(m_PanelBackground, startScale, m_PanelRestScale, m_PanelDuration, EasingType.EaseOutBack, 1.14f, () =>
                {
                    UIMicroShake.Shake(0.60f, 0.06f);
                }));
            }

            // 2. COLLECTION sign enters from upper-right with decaying pendulum swing
            if (m_CollectionSign != null)
            {
                StartCoroutine(SignEntranceRoutine());
            }

            // 3. Counter with slight delay
            if (m_TotalCounter != null)
            {
                Vector2 startCounter = new Vector2(m_CounterRestPos.x, m_CounterRestPos.y + 60f);
                m_TotalCounter.anchoredPosition = startCounter;
                StartCoroutine(AnimateMotion(
                    m_TotalCounter,
                    startCounter, m_CounterRestPos,
                    0f, 0f,
                    0.35f, 0.10f,
                    EasingType.EaseOutBack, 1.15f
                ));
            }

            // 4. Back button from bottom
            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 120f);
                m_BackButton.anchoredPosition = startBack;
                StartCoroutine(AnimateMotion(
                    m_BackButton,
                    startBack, m_BackRestPos,
                    0f, 0f,
                    0.32f, 0.14f,
                    EasingType.EaseOutBack, 1.1f
                ));
            }

            // 5. Four cards physical entrance from 4 screen corners:
            // ECHO: falls from upper-left (-750, +450)
            // NOVA: falls from top (0, +700)
            // PATCH: falls from upper-right (+750, +450)
            // PIXEL: flies from right (+800, 0)
            Vector2[] startOffsets = new Vector2[]
            {
                new Vector2(-750f, 450f),
                new Vector2(0f, 700f),
                new Vector2(750f, 450f),
                new Vector2(800f, 0f)
            };

            float[] cardDelays = new float[] { 0.12f, 0.18f, 0.24f, 0.30f };
            float[] cardDurations = new float[] { 0.36f, 0.40f, 0.38f, 0.34f };

            int finishedCards = 0;
            int totalCards = 0;

            for (int i = 0; i < m_CardRects.Length; i++)
            {
                if (m_CardRects[i] != null) totalCards++;
            }

            for (int i = 0; i < m_CardRects.Length; i++)
            {
                RectTransform card = m_CardRects[i];
                if (card == null) continue;

                Vector2 restPos = m_CardRestPositions[i];
                Vector2 startPos = restPos + startOffsets[i];
                float delay = cardDelays[i];
                float duration = cardDurations[i];

                StartCoroutine(CardPhysicalEntranceRoutine(card, startPos, restPos, delay, duration, () => finishedCards++));
            }

            while (finishedCards < totalCards)
            {
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator CardPhysicalEntranceRoutine(
            RectTransform card,
            Vector2 startPos, Vector2 targetPos,
            float delay, float travelDuration,
            Action onDone)
        {
            if (delay > 0f)
            {
                float dTimer = 0f;
                while (dTimer < delay)
                {
                    dTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Check if there is an inner portrait/silhouette child to animate with slight trailing delay for 2.5D depth
            Transform portrait = card.Find("Portrait") ?? card.Find("Silhouette");
            RectTransform portraitRt = portrait != null ? portrait as RectTransform : null;

            card.anchoredPosition = startPos;
            card.localScale = new Vector3(0.88f, 0.88f, 1f);

            // Phase 1: High velocity card travel
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f);

                if (card != null)
                {
                    card.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                    card.localScale = Vector3.LerpUnclamped(new Vector3(0.88f, 0.88f, 1f), Vector3.one, ease);
                }
                yield return null;
            }

            if (card != null)
            {
                card.anchoredPosition = targetPos;
                card.localScale = Vector3.one;
            }

            // Phase 2: Trailing portrait punch (0.05s later)
            if (portraitRt != null)
            {
                Vector3 pRestScale = portraitRt.localScale;
                Vector3 pPunchScale = pRestScale * 1.08f;

                elapsed = 0f;
                float pDur = 0.08f;
                while (elapsed < pDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / pDur);
                    if (portraitRt != null) portraitRt.localScale = Vector3.Lerp(pRestScale, pPunchScale, t);
                    yield return null;
                }

                elapsed = 0f;
                float pSet = 0.08f;
                while (elapsed < pSet)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / pSet);
                    if (portraitRt != null) portraitRt.localScale = Vector3.Lerp(pPunchScale, pRestScale, t);
                    yield return null;
                }
                if (portraitRt != null) portraitRt.localScale = pRestScale;
            }

            onDone?.Invoke();
        }

        private IEnumerator SignEntranceRoutine()
        {
            Vector2 startSign = new Vector2(m_SignRestPos.x + 450f, m_SignRestPos.y + 300f);
            m_CollectionSign.anchoredPosition = startSign;
            m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, 6f);

            float dropDuration = 0.34f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_CollectionSign != null)
                {
                    m_CollectionSign.anchoredPosition = Vector2.Lerp(startSign, m_SignRestPos, ease);
                }
                yield return null;
            }

            if (m_CollectionSign != null) m_CollectionSign.anchoredPosition = m_SignRestPos;

            // Physical damped pendulum decay (swings -9.5° -> +5.5° -> -3° -> +1° -> 0°)
            elapsed = 0f;
            float swingDuration = 0.65f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 4f);
                float angle = -decay * Mathf.Sin(elapsed * 13f) * 9.5f;

                if (m_CollectionSign != null)
                {
                    m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_CollectionSign != null) m_CollectionSign.localEulerAngles = m_SignRestAngles;

            // Ambient rope breeze sway
            m_SignBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine());
        }

        private IEnumerator SignAmbientSwayRoutine()
        {
            while (m_CollectionSign != null)
            {
                float angle = Mathf.Sin(Time.unscaledTime * 1.5f) * 0.65f;
                m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                yield return null;
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.26f;

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }

            // Sign shoots upper-right out of screen (+800px X, +400px Y)
            if (m_CollectionSign != null)
            {
                Vector2 targetSign = new Vector2(m_SignRestPos.x + 800f, m_SignRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_CollectionSign, m_CollectionSign.anchoredPosition, targetSign, 0f, 6f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // Counter shoots top (+400px Y)
            if (m_TotalCounter != null)
            {
                Vector2 targetCounter = new Vector2(m_CounterRestPos.x, m_CounterRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_TotalCounter, m_TotalCounter.anchoredPosition, targetCounter, 0f, 0f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // Cards shoot downward off-screen (-650px Y)
            if (m_CardRects != null)
            {
                for (int i = 0; i < m_CardRects.Length; i++)
                {
                    RectTransform card = m_CardRects[i];
                    if (card == null) continue;

                    Vector2 targetCard = new Vector2(m_CardRestPositions[i].x, m_CardRestPositions[i].y - 650f);
                    StartCoroutine(AnimateMotion(card, card.anchoredPosition, targetCard, 0f, 0f, duration, i * 0.02f, EasingType.EaseInCubic, 1f));
                }
            }

            // Back button drops bottom (-400px Y)
            if (m_BackButton != null)
            {
                Vector2 targetBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 400f);
                StartCoroutine(AnimateMotion(m_BackButton, m_BackButton.anchoredPosition, targetBack, 0f, 0f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // Panel shrinks/slides off right (+800px X)
            if (m_PanelBackground != null)
            {
                StartCoroutine(AnimateScale(m_PanelBackground, m_PanelBackground.localScale, m_PanelRestScale * 0.90f, duration, EasingType.EaseInCubic, 1f));
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateMotion(
            RectTransform target,
            Vector2 startPos, Vector2 targetPos,
            float startRotZ, float targetRotZ,
            float duration, float delay,
            EasingType easing, float overshoot,
            Action onDone = null)
        {
            if (delay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                    if (Mathf.Abs(startRotZ - targetRotZ) > 0.01f)
                    {
                        target.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(startRotZ, targetRotZ, ease));
                    }
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = targetPos;
                target.localEulerAngles = new Vector3(0f, 0f, targetRotZ);
            }

            onDone?.Invoke();
        }

        private IEnumerator AnimateScale(
            RectTransform target,
            Vector3 startScale, Vector3 targetScale,
            float duration, EasingType easing, float overshoot = 1f, Action onDone = null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
                }
                yield return null;
            }

            if (target != null)
            {
                target.localScale = targetScale;
            }

            onDone?.Invoke();
        }
    }
}
