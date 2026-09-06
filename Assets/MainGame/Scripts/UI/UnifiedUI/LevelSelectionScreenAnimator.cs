using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using LevelSelection;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Level Selection screen entrance and exit as a procedural Map Network Activation:
    /// 1. Map panel arrives via unfold / perspective deployment (+80px, scale X 0.92 -> 1.02 -> 1.00).
    /// 2. Pixel-art scanline sweeps across the grid.
    /// 3. ARC 1 and LEVELS header signs assemble from left/right with opposing rotation snap.
    /// 4. RETRY logo glitched horizontal strip assembly.
    /// 5. Back button signboard arrives from below.
    /// 6. Progressive route construction (0% -> 100%) along dynamically generated segments.
    /// 7. Dynamic node boot-ups with radial bounce and flash as route touches each node.
    /// 8. Route energy circuit highlight pulse sweeping up to current level.
    /// 9. Yellow triangular indicator arrow drops from above (+45px) onto current level.
    /// 10. Navigation unlocked only after marker settles.
    /// 11. Reverse retraction and fold away on back exit.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionScreenAnimator : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelSlideDistance = 80f;
        [SerializeField] private float m_PanelDuration = 0.36f;

        [Header("Header Signs")]
        [Tooltip("ARC 1 blue sign entering from upper-left with physical pendulum decay.")]
        [SerializeField] private RectTransform m_ArcSign;
        [Tooltip("LEVELS blue sign entering from upper-right with physical pendulum decay.")]
        [SerializeField] private RectTransform m_LevelsSign;
        [Tooltip("RETRY header logo entering from top.")]
        [SerializeField] private RectTransform m_TitleLogo;

        [Header("Buttons")]
        [SerializeField] private RectTransform m_BackButton;
        [SerializeField] private RectTransform m_NextButton;
        [SerializeField] private RectTransform m_PrevButton;

        [Header("Nodes & Path Container")]
        [SerializeField] private RectTransform m_GridContainer;

        #endregion

        #region Private Fields

        private Vector2 m_PanelRestPos;
        private Vector3 m_PanelRestScale = Vector3.one;

        private Vector2 m_ArcRestPos;
        private Vector3 m_ArcRestAngles;

        private Vector2 m_LevelsRestPos;
        private Vector3 m_LevelsRestAngles;

        private Vector2 m_LogoRestPos;
        private Vector2 m_BackRestPos;
        private Vector3 m_NextRestScale = Vector3.one;
        private Vector3 m_PrevRestScale = Vector3.one;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_ArcBreezeRoutine;
        private Coroutine m_LevelsBreezeRoutine;
        private bool m_HasCapturedRest = false;

        private Image m_ScanlineImage;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
        }

        private void OnDisable()
        {
            StopActiveAnimation();
        }

        #endregion

        #region State Capture

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_PanelBackground == null)
            {
                Transform bg = transform.Find("Holder/BG") ?? transform.Find("BG");
                if (bg != null) m_PanelBackground = bg as RectTransform;
            }

            if (m_ArcSign == null)
            {
                Transform arc = transform.Find("Holder/IMG Arc") ?? transform.Find("IMG Arc");
                if (arc != null) m_ArcSign = arc as RectTransform;
            }

            if (m_LevelsSign == null)
            {
                Transform levels = transform.Find("Holder/Setting IMG") ?? transform.Find("Setting IMG");
                if (levels != null) m_LevelsSign = levels as RectTransform;
            }

            if (m_TitleLogo == null)
            {
                Transform title = transform.Find("Holder/Tittle IMG") ?? transform.Find("Tittle IMG");
                if (title != null) m_TitleLogo = title as RectTransform;
            }

            if (m_BackButton == null)
            {
                Transform back = transform.Find("Holder/B Back") ?? transform.Find("Holder/Back B") ?? transform.Find("B Back") ?? transform.Find("Back B");
                if (back != null) m_BackButton = back as RectTransform;
            }

            if (m_NextButton == null)
            {
                Transform next = transform.Find("Holder/B Next") ?? transform.Find("B Next");
                if (next != null) m_NextButton = next as RectTransform;
            }

            if (m_PrevButton == null)
            {
                Transform prev = transform.Find("Holder/B Perivous") ?? transform.Find("Holder/B Previous") ?? transform.Find("B Perivous") ?? transform.Find("B Previous");
                if (prev != null) m_PrevButton = prev as RectTransform;
            }

            if (m_GridContainer == null)
            {
                Transform grid = transform.Find("LevelGrid");
                if (grid != null) m_GridContainer = grid as RectTransform;
            }

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
                m_PanelRestScale = m_PanelBackground.localScale;
            }

            if (m_ArcSign != null)
            {
                m_ArcRestPos = m_ArcSign.anchoredPosition;
                m_ArcRestAngles = m_ArcSign.localEulerAngles;
            }

            if (m_LevelsSign != null)
            {
                m_LevelsRestPos = m_LevelsSign.anchoredPosition;
                m_LevelsRestAngles = m_LevelsSign.localEulerAngles;
            }

            if (m_TitleLogo != null)
            {
                m_LogoRestPos = m_TitleLogo.anchoredPosition;
            }

            if (m_BackButton != null)
            {
                m_BackRestPos = m_BackButton.anchoredPosition;
            }

            if (m_NextButton != null) m_NextRestScale = m_NextButton.localScale;
            if (m_PrevButton != null) m_PrevRestScale = m_PrevButton.localScale;

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
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }

            if (m_ArcSign != null)
            {
                m_ArcSign.anchoredPosition = m_ArcRestPos;
                m_ArcSign.localEulerAngles = m_ArcRestAngles;
            }

            if (m_LevelsSign != null)
            {
                m_LevelsSign.anchoredPosition = m_LevelsRestPos;
                m_LevelsSign.localEulerAngles = m_LevelsRestAngles;
            }

            if (m_TitleLogo != null)
            {
                m_TitleLogo.anchoredPosition = m_LogoRestPos;
                m_TitleLogo.localScale = Vector3.one;
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = m_BackRestPos;
                m_BackButton.localScale = Vector3.one;
            }

            if (m_NextButton != null) m_NextButton.localScale = m_NextRestScale;
            if (m_PrevButton != null) m_PrevButton.localScale = m_PrevRestScale;
        }

        #endregion

        #region Pre-Entrance Preparation

        /// <summary>
        /// Prepares initial hidden transforms synchronously at frame 0 before the generation callback completes.
        /// </summary>
        public void PrepareEntranceState(List<LevelNodeUI> nodes = null, List<UIPathSegment> segments = null)
        {
            CaptureRestState();

            if (m_PanelBackground != null)
            {
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg == null) cg = m_PanelBackground.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                m_PanelBackground.anchoredPosition = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
                m_PanelBackground.localScale = new Vector3(0.92f, 1.05f, 1f);
            }

            if (m_ArcSign != null)
            {
                m_ArcSign.anchoredPosition = new Vector2(m_ArcRestPos.x - 650f, m_ArcRestPos.y + 250f);
                m_ArcSign.localEulerAngles = new Vector3(0f, 0f, -8f);
            }

            if (m_LevelsSign != null)
            {
                m_LevelsSign.anchoredPosition = new Vector2(m_LevelsRestPos.x + 650f, m_LevelsRestPos.y + 250f);
                m_LevelsSign.localEulerAngles = new Vector3(0f, 0f, 8f);
            }

            if (m_TitleLogo != null)
            {
                m_TitleLogo.anchoredPosition = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 180f);
                m_TitleLogo.localScale = new Vector3(1.2f, 0.7f, 1f);
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 140f);
            }

            if (m_NextButton != null) m_NextButton.localScale = Vector3.zero;
            if (m_PrevButton != null) m_PrevButton.localScale = Vector3.zero;

            if (segments != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null) segments[i].SetProgressiveScale(0f);
                }
            }
            else if (m_GridContainer != null)
            {
                var foundSegments = m_GridContainer.GetComponentsInChildren<UIPathSegment>(true);
                for (int i = 0; i < foundSegments.Length; i++)
                {
                    if (foundSegments[i] != null) foundSegments[i].SetProgressiveScale(0f);
                }
            }

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null) nodes[i].SetInitialBootState();
                }
            }
            else if (m_GridContainer != null)
            {
                var foundNodes = m_GridContainer.GetComponentsInChildren<LevelNodeUI>(true);
                for (int i = 0; i < foundNodes.Length; i++)
                {
                    if (foundNodes[i] != null) foundNodes[i].SetInitialBootState();
                }
            }
        }

        #endregion

        #region Public Entrance & Exit API

        /// <summary>
        /// Plays the complete coordinated Map Network Activation cinematic with dynamically generated nodes and segments.
        /// </summary>
        public void PlayMapEntrance(List<LevelNodeUI> nodes, List<UIPathSegment> segments, int highestUnlockedLevel, Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(MapEntranceSequenceRoutine(nodes, segments, highestUnlockedLevel, onComplete));
        }

        /// <summary>
        /// Plays the physical reverse retraction and panel fold-away on exit.
        /// </summary>
        public void PlayMapExit(List<LevelNodeUI> nodes, List<UIPathSegment> segments, Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(PlayMapExitRoutine(nodes, segments, onComplete));
        }

        /// <summary>
        /// Fallback entrance entry if nodes/segments were not pre-passed.
        /// </summary>
        public void PlayEntrance(Action onComplete)
        {
            List<LevelNodeUI> nodes = new List<LevelNodeUI>();
            List<UIPathSegment> segments = new List<UIPathSegment>();

            if (m_GridContainer != null)
            {
                m_GridContainer.GetComponentsInChildren(true, nodes);
                m_GridContainer.GetComponentsInChildren(true, segments);
            }

            int highestLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
            PlayMapEntrance(nodes, segments, highestLevel, onComplete);
        }

        /// <summary>
        /// Fallback exit entry if nodes/segments were not pre-passed.
        /// </summary>
        public void PlayExit(Action onComplete)
        {
            List<LevelNodeUI> nodes = new List<LevelNodeUI>();
            List<UIPathSegment> segments = new List<UIPathSegment>();

            if (m_GridContainer != null)
            {
                m_GridContainer.GetComponentsInChildren(true, nodes);
                m_GridContainer.GetComponentsInChildren(true, segments);
            }

            PlayMapExit(nodes, segments, onComplete);
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_ArcBreezeRoutine != null)
            {
                StopCoroutine(m_ArcBreezeRoutine);
                m_ArcBreezeRoutine = null;
            }

            if (m_LevelsBreezeRoutine != null)
            {
                StopCoroutine(m_LevelsBreezeRoutine);
                m_LevelsBreezeRoutine = null;
            }

            if (m_ScanlineImage != null)
            {
                m_ScanlineImage.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Entrance Coroutines

        private IEnumerator MapEntranceSequenceRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete)
        {
            // 1. Prepare entrance states
            PrepareEntranceState(nodes, segments);

            // 2. Animate Map Panel Unfold (Phase 2)
            if (m_PanelBackground != null)
            {
                StartCoroutine(PanelUnfoldRoutine(m_PanelDuration));
            }

            // 3. Scanline sweep across the map (Phase 3)
            StartCoroutine(ScanlineSweepRoutine(0.28f));

            // 4. Header elements entrance (Phases 4 & 5)
            if (m_ArcSign != null) StartCoroutine(ArcSignEntranceRoutine());
            if (m_LevelsSign != null) StartCoroutine(LevelsSignEntranceRoutine());
            if (m_TitleLogo != null) StartCoroutine(TitleGlitchEntranceRoutine(0.26f));

            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 140f);
                StartCoroutine(AnimateMotion(m_BackButton, startBack, m_BackRestPos, 0f, 0f, 0.28f, 0.08f, EasingType.EaseOutBack, 1.2f));
            }

            if (m_NextButton != null)
            {
                StartCoroutine(ScaleElementRoutine(m_NextButton, m_NextRestScale, 0.25f, 0.12f));
            }
            if (m_PrevButton != null)
            {
                StartCoroutine(ScaleElementRoutine(m_PrevButton, m_PrevRestScale, 0.25f, 0.12f));
            }

            // 5. Allow panel to deploy slightly before progressive line construction begins
            float panelLeadWait = 0.18f;
            float timer = 0f;
            while (timer < panelLeadWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // 6. Map Network Activation (Path reveal + Node boot-up + Route energy + Marker drop)
            yield return StartCoroutine(MapNetworkActivationRoutine(nodes, segments, highestUnlockedLevel, onComplete));
        }

        private IEnumerator PanelUnfoldRoutine(float duration)
        {
            if (m_PanelBackground == null) yield break;

            CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
            if (cg == null) cg = m_PanelBackground.gameObject.AddComponent<CanvasGroup>();

            Vector2 startPos = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
            Vector3 startScale = new Vector3(0.92f, 1.05f, 1f);
            Vector3 overshootScale = new Vector3(1.02f, 0.98f, 1f);

            m_PanelBackground.anchoredPosition = startPos;
            m_PanelBackground.localScale = startScale;
            cg.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                cg.alpha = Mathf.Lerp(0f, 1f, t * 2.5f);
                m_PanelBackground.anchoredPosition = Vector2.Lerp(startPos, m_PanelRestPos, UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.12f));

                // Unfold perspective illusion
                if (t < 0.7f)
                {
                    float sT = t / 0.7f;
                    m_PanelBackground.localScale = Vector3.Lerp(startScale, overshootScale, UIEasing.Evaluate(EasingType.EaseOutQuad, sT));
                }
                else
                {
                    float sT = (t - 0.7f) / 0.3f;
                    m_PanelBackground.localScale = Vector3.Lerp(overshootScale, m_PanelRestScale, UIEasing.Evaluate(EasingType.EaseInOutQuad, sT));
                }

                yield return null;
            }

            m_PanelBackground.anchoredPosition = m_PanelRestPos;
            m_PanelBackground.localScale = m_PanelRestScale;
            cg.alpha = 1f;

            UIMicroShake.Shake(0.5f, 0.05f);
        }

        private void EnsureScanline()
        {
            if (m_ScanlineImage != null) return;

            Transform targetParent = m_PanelBackground != null ? m_PanelBackground.transform : transform;
            GameObject scanObj = new GameObject("MapScanline", typeof(RectTransform), typeof(Image));
            scanObj.transform.SetParent(targetParent, false);

            RectTransform rt = scanObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(24f, 0f);
            rt.anchoredPosition = Vector2.zero;

            m_ScanlineImage = scanObj.GetComponent<Image>();
            m_ScanlineImage.color = new Color(0.35f, 0.75f, 1f, 0.35f);
            m_ScanlineImage.raycastTarget = false;
            scanObj.SetActive(false);
        }

        private IEnumerator ScanlineSweepRoutine(float duration)
        {
            EnsureScanline();
            if (m_ScanlineImage == null) yield break;

            GameObject scanObj = m_ScanlineImage.gameObject;
            scanObj.SetActive(true);
            scanObj.transform.SetAsLastSibling();
            RectTransform rt = m_ScanlineImage.rectTransform;

            float parentWidth = 1400f;
            if (m_PanelBackground != null)
            {
                parentWidth = m_PanelBackground.rect.width > 100f ? m_PanelBackground.rect.width : 1400f;
            }

            float startX = -parentWidth * 0.5f - 30f;
            float endX = parentWidth * 0.5f + 30f;

            rt.anchoredPosition = new Vector2(startX, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInOutQuad, t);

                rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, ease), 0f);

                float alpha = Mathf.Sin(t * Mathf.PI) * 0.35f;
                m_ScanlineImage.color = new Color(0.35f, 0.75f, 1f, alpha);

                yield return null;
            }

            scanObj.SetActive(false);
        }

        private IEnumerator ArcSignEntranceRoutine()
        {
            Vector2 startArc = new Vector2(m_ArcRestPos.x - 650f, m_ArcRestPos.y + 250f);
            m_ArcSign.anchoredPosition = startArc;
            m_ArcSign.localEulerAngles = new Vector3(0f, 0f, -8f);

            float travelDuration = 0.28f;
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_ArcSign != null)
                {
                    m_ArcSign.anchoredPosition = Vector2.Lerp(startArc, m_ArcRestPos, ease);
                }
                yield return null;
            }

            if (m_ArcSign != null) m_ArcSign.anchoredPosition = m_ArcRestPos;

            // Physical pendulum oscillation (-8° -> +2° -> -1° -> 0°)
            elapsed = 0f;
            float swingDuration = 0.55f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 4.5f);
                float angle = decay * Mathf.Sin(elapsed * 14f) * 8f;

                if (m_ArcSign != null)
                {
                    m_ArcSign.localEulerAngles = new Vector3(0f, 0f, m_ArcRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_ArcSign != null) m_ArcSign.localEulerAngles = m_ArcRestAngles;

            m_ArcBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine(m_ArcSign, m_ArcRestAngles.z, 0f));
        }

        private IEnumerator LevelsSignEntranceRoutine()
        {
            Vector2 startLevels = new Vector2(m_LevelsRestPos.x + 650f, m_LevelsRestPos.y + 250f);
            m_LevelsSign.anchoredPosition = startLevels;
            m_LevelsSign.localEulerAngles = new Vector3(0f, 0f, 8f);

            float travelDuration = 0.32f;
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_LevelsSign != null)
                {
                    m_LevelsSign.anchoredPosition = Vector2.Lerp(startLevels, m_LevelsRestPos, ease);
                }
                yield return null;
            }

            if (m_LevelsSign != null) m_LevelsSign.anchoredPosition = m_LevelsRestPos;

            // Opposing pendulum oscillation (+8° -> -3° -> +1° -> 0°)
            elapsed = 0f;
            float swingDuration = 0.55f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 4.5f);
                float angle = -decay * Mathf.Sin(elapsed * 14f) * 8f;

                if (m_LevelsSign != null)
                {
                    m_LevelsSign.localEulerAngles = new Vector3(0f, 0f, m_LevelsRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_LevelsSign != null) m_LevelsSign.localEulerAngles = m_LevelsRestAngles;

            m_LevelsBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine(m_LevelsSign, m_LevelsRestAngles.z, 0.6f));
        }

        private IEnumerator SignAmbientSwayRoutine(RectTransform sign, float baseAngleZ, float timeOffset)
        {
            while (sign != null)
            {
                float angle = Mathf.Sin((Time.unscaledTime + timeOffset) * 1.5f) * 0.65f;
                sign.localEulerAngles = new Vector3(0f, 0f, baseAngleZ + angle);
                yield return null;
            }
        }

        private IEnumerator TitleGlitchEntranceRoutine(float duration)
        {
            if (m_TitleLogo == null) yield break;

            Vector2 basePos = m_LogoRestPos;
            Vector3 baseScale = Vector3.one;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ballistic drop from +180px with overshoot
                float yDrop = Mathf.Lerp(180f, 0f, UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.2f));

                // Horizontal micro-jitter slice assembly decaying over time
                float jitterDecay = (1f - t);
                float xJitter = Mathf.Sin(elapsed * 45f) * 10f * jitterDecay;

                // Scale squashes and settles
                float sx = Mathf.Lerp(1.2f, 1f, UIEasing.Evaluate(EasingType.EaseOutQuad, t));
                float sy = Mathf.Lerp(0.7f, 1f, UIEasing.Evaluate(EasingType.EaseOutQuad, t));

                m_TitleLogo.anchoredPosition = new Vector2(basePos.x + xJitter, basePos.y + yDrop);
                m_TitleLogo.localScale = new Vector3(sx, sy, 1f);

                yield return null;
            }

            m_TitleLogo.anchoredPosition = basePos;
            m_TitleLogo.localScale = baseScale;
        }

        private IEnumerator ScaleElementRoutine(RectTransform element, Vector3 targetScale, float duration, float delay)
        {
            if (element == null) yield break;

            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay)
                {
                    d += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            element.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.15f);
                element.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                yield return null;
            }
            element.localScale = targetScale;
        }

        #endregion

        #region Map Network Activation Coroutine

        private IEnumerator MapNetworkActivationRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete)
        {
            // Step 1: Sequential Route Drawing & Node Boot-Up
            if (nodes != null && nodes.Count > 0)
            {
                // First node boots up immediately
                StartCoroutine(nodes[0].PlayBootUpRoutine(nodes[0].IsUnlocked, 0f));
            }

            if (segments != null && segments.Count > 0)
            {
                // Adaptive speed: total route draw ~0.35s
                float segDrawTime = Mathf.Clamp(0.35f / segments.Count, 0.02f, 0.055f);

                for (int i = 0; i < segments.Count; i++)
                {
                    UIPathSegment seg = segments[i];
                    if (seg == null) continue;

                    float elapsed = 0f;
                    while (elapsed < segDrawTime)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float progress = Mathf.Clamp01(elapsed / segDrawTime);
                        seg.SetProgressiveScale(progress);
                        yield return null;
                    }
                    seg.SetProgressiveScale(1f);

                    // As segment i reaches completion, the target node (i + 1) boots up!
                    if (nodes != null && (i + 1) < nodes.Count)
                    {
                        LevelNodeUI targetNode = nodes[i + 1];
                        if (targetNode != null)
                        {
                            StartCoroutine(targetNode.PlayBootUpRoutine(targetNode.IsUnlocked, 0f));
                        }
                    }
                }
            }

            // Step 2: Ensure all nodes have completed boot-up
            float nodeSettleWait = 0.12f;
            float timer = 0f;
            while (timer < nodeSettleWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Step 3: Route Energy Circuit Pulse from Start to Current Level
            LevelNodeUI currentLevelNode = null;
            int currentNodeIndex = -1;

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null && nodes[i].levelNumber == highestUnlockedLevel)
                    {
                        currentLevelNode = nodes[i];
                        currentNodeIndex = i;
                        break;
                    }
                }
                if (currentLevelNode == null && nodes.Count > 0)
                {
                    currentLevelNode = nodes[0];
                    currentNodeIndex = 0;
                }
            }

            if (segments != null && currentNodeIndex > 0)
            {
                int unlockedSegCount = Mathf.Min(currentNodeIndex, segments.Count);
                float energySpeed = Mathf.Clamp(0.24f / Mathf.Max(1, unlockedSegCount), 0.03f, 0.06f);

                for (int i = 0; i < unlockedSegCount; i++)
                {
                    UIPathSegment seg = segments[i];
                    if (seg != null)
                    {
                        seg.PlayEnergyFlash(energySpeed);
                    }
                    timer = 0f;
                    while (timer < energySpeed)
                    {
                        timer += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            // Step 4: Drop Current Level Marker Arrow from above
            if (currentLevelNode != null)
            {
                yield return StartCoroutine(currentLevelNode.PlayMarkerDropRoutine());
            }

            // Step 5: Finished! Navigation is now ready to be unlocked
            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        #endregion

        #region Exit Coroutines

        private IEnumerator PlayMapExitRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            Action onComplete)
        {
            float duration = 0.24f;

            if (m_ArcBreezeRoutine != null) { StopCoroutine(m_ArcBreezeRoutine); m_ArcBreezeRoutine = null; }
            if (m_LevelsBreezeRoutine != null) { StopCoroutine(m_LevelsBreezeRoutine); m_LevelsBreezeRoutine = null; }

            // 1. Rapid route retraction (100% -> 0%) and node collapse
            if (segments != null && segments.Count > 0)
            {
                StartCoroutine(RetractSegmentsRoutine(segments, 0.16f));
            }
            if (nodes != null && nodes.Count > 0)
            {
                StartCoroutine(CollapseNodesRoutine(nodes, 0.14f));
            }

            // 2. ARC 1 shoots upper-left (-800, +400)
            if (m_ArcSign != null)
            {
                Vector2 targetArc = new Vector2(m_ArcRestPos.x - 800f, m_ArcRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_ArcSign, m_ArcSign.anchoredPosition, targetArc, 0f, -8f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // 3. LEVELS sign shoots upper-right (+800, +400)
            if (m_LevelsSign != null)
            {
                Vector2 targetLevels = new Vector2(m_LevelsRestPos.x + 800f, m_LevelsRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_LevelsSign, m_LevelsSign.anchoredPosition, targetLevels, 0f, 8f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // 4. RETRY Logo shoots top (+500px Y)
            if (m_TitleLogo != null)
            {
                Vector2 targetLogo = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 500f);
                StartCoroutine(AnimateMotion(m_TitleLogo, m_TitleLogo.anchoredPosition, targetLogo, 0f, 0f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // 5. Back button drops downward (-350px Y)
            if (m_BackButton != null)
            {
                Vector2 targetBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 350f);
                StartCoroutine(AnimateMotion(m_BackButton, m_BackButton.anchoredPosition, targetBack, 0f, 0f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            if (m_NextButton != null)
            {
                StartCoroutine(ScaleElementRoutine(m_NextButton, Vector3.zero, 0.15f, 0f));
            }
            if (m_PrevButton != null)
            {
                StartCoroutine(ScaleElementRoutine(m_PrevButton, Vector3.zero, 0.15f, 0f));
            }

            // 6. Panel background folds away (Scale X: 1.0 -> 0.90, Pos +80px, alpha -> 0)
            if (m_PanelBackground != null)
            {
                StartCoroutine(AnimateFoldExit(m_PanelBackground, duration));
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

        private IEnumerator RetractSegmentsRoutine(List<UIPathSegment> segments, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null)
                    {
                        segments[i].SetProgressiveScale(p);
                    }
                }
                yield return null;
            }
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null) segments[i].SetProgressiveScale(0f);
            }
        }

        private IEnumerator CollapseNodesRoutine(List<LevelNodeUI> nodes, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 1f - UIEasing.Evaluate(EasingType.EaseInQuad, t);
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null)
                    {
                        nodes[i].transform.localScale = Vector3.one * s;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null) nodes[i].SetInitialBootState();
            }
        }

        private IEnumerator AnimateFoldExit(RectTransform panel, float duration)
        {
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            Vector2 startPos = panel.anchoredPosition;
            Vector2 targetPos = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
            Vector3 startScale = panel.localScale;
            Vector3 targetScale = new Vector3(0.90f, 0.85f, 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                panel.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                panel.localScale = Vector3.Lerp(startScale, targetScale, ease);
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, ease);

                yield return null;
            }

            panel.anchoredPosition = targetPos;
            panel.localScale = targetScale;
            if (cg != null) cg.alpha = 0f;
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

        #endregion
    }
}
