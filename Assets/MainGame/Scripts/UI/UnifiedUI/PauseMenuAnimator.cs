using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.RoboticEffects;
using MainGame.UI.Feedback;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the in-game Pause Menu with immediate, high-velocity physical impact:
    /// - 0.18s fast slam from top with landing overshoot, rebound, and micro-shake
    /// - 0.08s instant background darkening
    /// - Rapid 0.03s button stagger sequence
    /// - 0.14s fast upward ceiling fling exit
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuAnimator : MonoBehaviour
    {
        [Header("Dark Overlay")]
        [SerializeField] private CanvasGroup m_DarkOverlay;
        [SerializeField] private float m_OverlayTargetAlpha = 0.85f;
        [SerializeField] private float m_OverlayDuration = 0.08f;

        [Header("Pause Panel")]
        [Tooltip("Container holding the pause menu elements (Title, buttons).")]
        [SerializeField] private RectTransform m_PausePanel;
        [SerializeField] private float m_SlamDuration = 0.18f;
        [SerializeField] private float m_SlamDistance = 650f;

        [Header("Dialog / Title")]
        [SerializeField] private RectTransform m_TitleDialog;

        [Header("Buttons (Reset, Level, Exit)")]
        [SerializeField] private RectTransform[] m_Buttons;

        private Vector2 m_PanelRestPos;
        private Vector2 m_TitleRestPos;
        private Vector2[] m_ButtonRestPositions;
        private MainMenuButtonEnergyAnimator[] m_ButtonEnergyAnimators;

        private Coroutine m_ActiveRoutine;
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

            if (m_DarkOverlay == null)
            {
                m_DarkOverlay = GetComponent<CanvasGroup>();
                if (m_DarkOverlay == null) m_DarkOverlay = GetComponentInParent<CanvasGroup>();
            }

            if (m_PausePanel == null)
            {
                Transform panel = transform.Find("Pause panel") ?? transform.Find("Holder") ?? transform;
                if (panel != null) m_PausePanel = panel as RectTransform;
            }

            if (m_PausePanel != null)
            {
                m_PanelRestPos = m_PausePanel.anchoredPosition;

                if (m_TitleDialog == null)
                {
                    Transform dialog = m_PausePanel.Find("Dialog") ?? m_PausePanel.Find("Title");
                    if (dialog != null) m_TitleDialog = dialog as RectTransform;
                }

                if (m_Buttons == null || m_Buttons.Length == 0)
                {
                    Transform resetBtn = m_PausePanel.Find("Reset");
                    Transform levelBtn = m_PausePanel.Find("Level");
                    Transform exitBtn = m_PausePanel.Find("Exit");

                    var list = new System.Collections.Generic.List<RectTransform>();
                    if (resetBtn != null) list.Add(resetBtn as RectTransform);
                    if (levelBtn != null) list.Add(levelBtn as RectTransform);
                    if (exitBtn != null) list.Add(exitBtn as RectTransform);

                    if (list.Count > 0) m_Buttons = list.ToArray();
                }
            }

            if (m_TitleDialog != null)
            {
                m_TitleRestPos = m_TitleDialog.anchoredPosition;
            }

            if (m_Buttons != null && m_Buttons.Length > 0)
            {
                m_ButtonRestPositions = new Vector2[m_Buttons.Length];
                m_ButtonEnergyAnimators = new MainMenuButtonEnergyAnimator[m_Buttons.Length];
                for (int i = 0; i < m_Buttons.Length; i++)
                {
                    if (m_Buttons[i] != null)
                    {
                        m_ButtonRestPositions[i] = m_Buttons[i].anchoredPosition;
                        m_ButtonEnergyAnimators[i] = m_Buttons[i].GetComponent<MainMenuButtonEnergyAnimator>();
                    }
                }
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = 0f;
            }

            if (m_PausePanel != null)
            {
                m_PausePanel.anchoredPosition = m_PanelRestPos;
                m_PausePanel.localScale = Vector3.one;
            }

            if (m_TitleDialog != null)
            {
                m_TitleDialog.anchoredPosition = m_TitleRestPos;
                m_TitleDialog.localScale = Vector3.one;
            }

            if (m_Buttons != null)
            {
                for (int i = 0; i < m_Buttons.Length; i++)
                {
                    if (m_Buttons[i] != null)
                    {
                        if (m_ButtonEnergyAnimators != null && i < m_ButtonEnergyAnimators.Length && m_ButtonEnergyAnimators[i] != null)
                        {
                            m_ButtonEnergyAnimators[i].ResetToRestState();
                        }
                        else if (m_ButtonRestPositions != null && i < m_ButtonRestPositions.Length)
                        {
                            m_Buttons[i].anchoredPosition = m_ButtonRestPositions[i];
                            m_Buttons[i].localScale = Vector3.one;
                        }
                    }
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();

            // Prime all buttons in dormant state before entrance begins
            if (m_Buttons != null)
            {
                for (int i = 0; i < m_Buttons.Length; i++)
                {
                    if (m_Buttons[i] != null)
                    {
                        if (m_ButtonEnergyAnimators != null && i < m_ButtonEnergyAnimators.Length && m_ButtonEnergyAnimators[i] != null)
                        {
                            m_ButtonEnergyAnimators[i].PrepareDormantState();
                        }
                        else if (m_ButtonRestPositions != null && i < m_ButtonRestPositions.Length)
                        {
                            m_Buttons[i].anchoredPosition = new Vector2(m_ButtonRestPositions[i].x, m_ButtonRestPositions[i].y - 45f);
                            m_Buttons[i].localScale = new Vector3(0.90f, 0.90f, 1f);
                        }
                    }
                }
            }

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
        }

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Background darkens instantly (0.06s)
            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = 0f;
                StartCoroutine(AnimateOverlayAlpha(0f, m_OverlayTargetAlpha, 0.06f));
            }

            // 2. Fast panel drop slam from top (0.14s total) with overshoot and micro-shake
            if (m_PausePanel != null)
            {
                Vector2 startPos = new Vector2(m_PanelRestPos.x, m_PanelRestPos.y + m_SlamDistance);
                Vector2 overshootPos = new Vector2(m_PanelRestPos.x, m_PanelRestPos.y - 12f);

                m_PausePanel.anchoredPosition = startPos;

                // Phase A: Rapid drop (0.10s)
                float dropDur = 0.10f;
                float elapsed = 0f;
                while (elapsed < dropDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / dropDur);
                    float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);
                    m_PausePanel.anchoredPosition = Vector2.Lerp(startPos, overshootPos, ease);
                    yield return null;
                }

                m_PausePanel.anchoredPosition = overshootPos;
                UIMicroShake.Shake(0.55f, 0.05f);
                UIFeedbackAudio.PlaySfx(UISfxType.PopupSlam, 1.0f, 0.02f);

                RoboticUIPanelEffect panelFX = m_PausePanel.GetComponent<RoboticUIPanelEffect>();
                if (panelFX == null)
                {
                    Image panelImg = m_PausePanel.GetComponent<Image>();
                    if (panelImg != null)
                    {
                        panelFX = RoboticUIManager.GetOrAddPanelEffect(panelImg, new Color(0.35f, 0.78f, 1.0f, 1.0f), cornerBrackets: true, scanShimmer: false);
                    }
                }
                if (panelFX != null)
                {
                    panelFX.PlayPowerUp(0.14f);
                }

                if (RoboticUIManager.Instance != null)
                {
                    RoboticUIManager.Instance.SpawnSparkBurst(Vector2.zero, m_PausePanel, new Color(0.35f, 0.85f, 1.0f), 6, 16f);
                }

                // Phase B: Settle rebound to rest position (0.04s)
                float settleDur = 0.04f;
                elapsed = 0f;
                while (elapsed < settleDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / settleDur);
                    m_PausePanel.anchoredPosition = Vector2.Lerp(overshootPos, m_PanelRestPos, t);
                    yield return null;
                }

                m_PausePanel.anchoredPosition = m_PanelRestPos;
            }

            // 3. Fast sequential button snap (staggered 0.04s apart)
            if (m_Buttons != null && m_ButtonRestPositions != null)
            {
                for (int i = 0; i < m_Buttons.Length; i++)
                {
                    RectTransform btn = m_Buttons[i];
                    if (btn == null) continue;

                    float staggerDelay = 0.02f + (i * 0.04f);

                    if (m_ButtonEnergyAnimators != null && i < m_ButtonEnergyAnimators.Length && m_ButtonEnergyAnimators[i] != null)
                    {
                        m_ButtonEnergyAnimators[i].PlaySnapEntrance(staggerDelay);
                    }
                    else
                    {
                        Vector2 restPos = m_ButtonRestPositions[i];
                        Vector2 startPos = new Vector2(restPos.x, restPos.y - 45f);
                        StartCoroutine(AnimateButtonEntrance(btn, startPos, restPos, 0.14f, staggerDelay));
                    }
                }
            }

            // Wait for sequential snaps to complete:
            // Last button stagger (0.10s) + snap duration (0.14s) = 0.24s
            float totalWait = 0.24f;
            float timer = 0f;
            while (timer < totalWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateButtonEntrance(RectTransform target, Vector2 startPos, Vector2 restPos, float duration, float delay)
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

            if (target == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, restPos, ease);
                    target.localScale = Vector3.LerpUnclamped(new Vector3(0.92f, 0.92f, 1f), Vector3.one, ease);
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = restPos;
                target.localScale = Vector3.one;
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.14f;

            // Background fades out in 0.08s
            if (m_DarkOverlay != null)
            {
                StartCoroutine(AnimateOverlayAlpha(m_DarkOverlay.alpha, 0f, 0.08f));
            }

            // Buttons retract rapidly
            if (m_ButtonEnergyAnimators != null)
            {
                for (int i = 0; i < m_ButtonEnergyAnimators.Length; i++)
                {
                    if (m_ButtonEnergyAnimators[i] != null)
                    {
                        m_ButtonEnergyAnimators[i].PlayRetract();
                    }
                }
            }

            // Panel flings up into ceiling (+700px)
            if (m_PausePanel != null)
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.85f, 0.02f);
                RoboticUIPanelEffect panelFX = m_PausePanel.GetComponent<RoboticUIPanelEffect>();
                if (panelFX != null)
                {
                    panelFX.PlayPowerDown(duration);
                }
                Vector2 exitPos = new Vector2(m_PanelRestPos.x, m_PanelRestPos.y + 700f);
                StartCoroutine(AnimateMotion(m_PausePanel, m_PausePanel.anchoredPosition, exitPos, duration, EasingType.EaseInQuad));
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

        private IEnumerator AnimateOverlayAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (m_DarkOverlay != null)
                {
                    m_DarkOverlay.alpha = Mathf.Lerp(from, to, t);
                }
                yield return null;
            }

            if (m_DarkOverlay != null)
            {
                m_DarkOverlay.alpha = to;
            }
        }

        private IEnumerator AnimateMotion(RectTransform target, Vector2 startPos, Vector2 targetPos, float duration, EasingType easing)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = targetPos;
            }
        }
    }
}
