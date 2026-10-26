using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using LevelSelection;
using MainGame.UI.RoboticEffects;
using MainGame.UI.Feedback;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Level Selection screen entrance, exit, and page transitions for the paginated grid design.
    /// Manages panel expansion, header banners, navigation buttons, and staggered level card pop-in.
    /// Preserves full compatibility with existing scene serialization and script references.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionScreenAnimator : MonoBehaviour
    {
#pragma warning disable 0414
        #region Inspector Fields

        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelSlideDistance = 80f;
        [SerializeField] private float m_PanelDuration = 0.32f;

        [Header("Header Signs")]
        [Tooltip("ARC 1 blue sign entering from above with physical settle.")]
        [SerializeField] private RectTransform m_ArcSign;
        [Tooltip("LEVELS blue sign entering from above with physical settle.")]
        [SerializeField] private RectTransform m_LevelsSign;
        [Tooltip("Header title / logo entering from top.")]
        [SerializeField] private RectTransform m_TitleLogo;

        [Header("Buttons")]
        [SerializeField] private RectTransform m_BackButton;
        [SerializeField] private RectTransform m_NextButton;
        [SerializeField] private RectTransform m_PrevButton;

        [Header("Nodes & Path Container")]
        [Tooltip("Parent transform containing the grid buttons (e.g. 'Grid View').")]
        [SerializeField] private RectTransform m_GridContainer;

        [Header("Pointer Reference (Legacy compatibility)")]
        [SerializeField] private LevelSelectionPointer m_Pointer;

        #endregion
#pragma warning restore 0414

        #region Private Fields

        private Vector2 m_PanelRestPos;
        private Vector3 m_PanelRestScale = Vector3.one;

        private Vector2 m_ArcRestPos;
        private Vector3 m_ArcRestAngles;

        private Vector2 m_LevelsRestPos;
        private Vector3 m_LevelsRestAngles;

        private Vector2 m_LogoRestPos;
        private Vector2 m_BackRestPos;
        private Vector2 m_GridRestPos;
        private Vector3 m_NextRestScale = Vector3.one;
        private Vector3 m_PrevRestScale = Vector3.one;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_ArcBreezeRoutine;
        private bool m_HasCapturedRest = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CaptureRestState();
        }

        #endregion

        #region State Management & Auto-Wiring

        public void AutoDiscoverReferences()
        {
            if (m_PanelBackground == null)
            {
                Transform holder = transform.Find("Holder");
                if (holder != null) m_PanelBackground = holder.GetComponent<RectTransform>();
            }

            if (m_ArcSign == null)
            {
                Transform holder = transform.Find("Holder") ?? transform;
                Transform arcTrans = holder.Find("IMG Arc");
                if (arcTrans != null) m_ArcSign = arcTrans.GetComponent<RectTransform>();
            }

            if (m_BackButton == null)
            {
                Transform holder = transform.Find("Holder") ?? transform;
                Transform backTrans = holder.Find("B Back");
                if (backTrans != null) m_BackButton = backTrans.GetComponent<RectTransform>();
            }

            if (m_NextButton == null)
            {
                Transform holder = transform.Find("Holder") ?? transform;
                Transform nextTrans = holder.Find("B Next");
                if (nextTrans != null) m_NextButton = nextTrans.GetComponent<RectTransform>();
            }

            if (m_PrevButton == null)
            {
                Transform holder = transform.Find("Holder") ?? transform;
                Transform prevTrans = holder.Find("B Perivous") ?? holder.Find("B Previous");
                if (prevTrans != null) m_PrevButton = prevTrans.GetComponent<RectTransform>();
            }

            if (m_GridContainer == null)
            {
                var gridLayout = GetComponentInChildren<GridLayoutGroup>(true);
                if (gridLayout != null) m_GridContainer = gridLayout.GetComponent<RectTransform>();
            }
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            AutoDiscoverReferences();

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
                m_PanelRestScale = m_PanelBackground.localScale == Vector3.zero ? Vector3.one : m_PanelBackground.localScale;
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

            if (m_NextButton != null)
            {
                m_NextRestScale = m_NextButton.localScale == Vector3.zero ? Vector3.one : m_NextButton.localScale;
            }

            if (m_PrevButton != null)
            {
                m_PrevRestScale = m_PrevButton.localScale == Vector3.zero ? Vector3.one : m_PrevButton.localScale;
            }

            if (m_GridContainer != null)
            {
                m_GridRestPos = m_GridContainer.anchoredPosition;
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            if (!m_HasCapturedRest) CaptureRestState();

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
            }

            // Next and Previous navigation arrow buttons
            if (m_NextButton != null)
            {
                m_NextButton.localScale = m_NextRestScale;
            }

            if (m_PrevButton != null)
            {
                m_PrevButton.localScale = m_PrevRestScale;
            }

            if (m_GridContainer != null)
            {
                m_GridContainer.anchoredPosition = m_GridRestPos;
                CanvasGroup gridCg = m_GridContainer.GetComponent<CanvasGroup>();
                if (gridCg != null) gridCg.alpha = 1f;

                for (int i = 0; i < m_GridContainer.childCount; i++)
                {
                    Transform child = m_GridContainer.GetChild(i);
                    var glb = child.GetComponent<LevelSelection.GridLevelButton>();
                    child.localScale = (glb != null && glb.BaseScale != Vector3.zero) ? glb.BaseScale : Vector3.one;
                }
            }
        }

        public void PrepareEntranceState(List<LevelNodeUI> nodes = null, List<UIPathSegment> segments = null)
        {
            CaptureRestState();
            StopActiveAnimation();

            // Keep panel background frame & level selection BG 100% visible immediately
            if (m_PanelBackground != null)
            {
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = m_PanelRestScale;
            }

            if (m_ArcSign != null)
            {
                m_ArcSign.anchoredPosition = new Vector2(m_ArcRestPos.x, m_ArcRestPos.y + 60f);
            }

            if (m_LevelsSign != null)
            {
                m_LevelsSign.anchoredPosition = new Vector2(m_LevelsRestPos.x, m_LevelsRestPos.y + 60f);
            }

            if (m_TitleLogo != null)
            {
                m_TitleLogo.anchoredPosition = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 60f);
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 60f);
            }

            if (m_NextButton != null && m_NextButton.gameObject.activeSelf)
            {
                m_NextButton.localScale = Vector3.zero;
            }

            if (m_PrevButton != null && m_PrevButton.gameObject.activeSelf)
            {
                m_PrevButton.localScale = Vector3.zero;
            }

            // Keep grid container and cards immediately visible with their thumbnails
            if (m_GridContainer != null)
            {
                CanvasGroup gridCg = m_GridContainer.GetComponent<CanvasGroup>();
                if (gridCg != null) gridCg.alpha = 1f;
                m_GridContainer.anchoredPosition = m_GridRestPos;

                for (int i = 0; i < m_GridContainer.childCount; i++)
                {
                    Transform child = m_GridContainer.GetChild(i);
                    var glb = child.GetComponent<LevelSelection.GridLevelButton>();
                    child.localScale = (glb != null && glb.BaseScale != Vector3.zero) ? glb.BaseScale : Vector3.one;
                }
            }
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

            StopAllCoroutines();
        }

        #endregion

        #region Public Transitions

        /// <summary>
        /// Main entrance animation for the Level Selection screen.
        /// </summary>
        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(GridEntranceSequenceRoutine(onComplete));
        }

        /// <summary>
        /// Main exit animation for the Level Selection screen.
        /// </summary>
        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(GridExitSequenceRoutine(onComplete));
        }

        /// <summary>
        /// Backwards-compatible map entrance signature with 4-6 parameters.
        /// </summary>
        public void PlayMapEntrance(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            Sprite arcSprite = null,
            int focusTargetNodeIndex = -1)
        {
            PlayEntrance(onComplete);
        }

        /// <summary>
        /// Backwards-compatible map entrance overload without arcSprite.
        /// </summary>
        public void PlayMapEntrance(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            int focusTargetNodeIndex)
        {
            PlayEntrance(onComplete);
        }

        /// <summary>
        /// Backwards-compatible map exit signature.
        /// </summary>
        public void PlayMapExit(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            Action onComplete)
        {
            PlayExit(onComplete);
        }

        /// <summary>
        /// Smooth page flip transition when advancing or rewinding pages in the grid.
        /// </summary>
        public void PlayArcTransition(
            int arcIndex,
            Sprite arcSprite,
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            int focusTargetNodeIndex = -1)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(PageTransitionRoutine(null, onComplete));
        }

        /// <summary>
        /// Dedicated page flip animation for the Grid Level Generator.
        /// </summary>
        public void PlayPageTransition(Action onPageSwitch, Action onComplete = null)
        {
            PlayPageTransition(onPageSwitch, onComplete, isForward: true);
        }

        /// <summary>
        /// Dedicated directional page flip animation for the Grid Level Generator.
        /// </summary>
        public void PlayPageTransition(Action onPageSwitch, Action onComplete, bool isForward)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(PageTransitionRoutine(onPageSwitch, onComplete, isForward));
        }

        #endregion

        #region Entrance & Exit Coroutines

        private IEnumerator GridEntranceSequenceRoutine(Action onComplete)
        {
            PrepareEntranceState();

            // 1. Animate header signs (slide down with gentle overshoot)
            if (m_ArcSign != null)
            {
                Vector2 startArc = new Vector2(m_ArcRestPos.x, m_ArcRestPos.y + 60f);
                StartCoroutine(AnimateMotion(m_ArcSign, startArc, m_ArcRestPos, 0.28f, 0.02f, EasingType.EaseOutBack, 1.15f));
            }
            if (m_LevelsSign != null)
            {
                Vector2 startLevels = new Vector2(m_LevelsRestPos.x, m_LevelsRestPos.y + 60f);
                StartCoroutine(AnimateMotion(m_LevelsSign, startLevels, m_LevelsRestPos, 0.28f, 0.02f, EasingType.EaseOutBack, 1.15f));
            }
            if (m_TitleLogo != null)
            {
                Vector2 startLogo = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 60f);
                StartCoroutine(AnimateMotion(m_TitleLogo, startLogo, m_LogoRestPos, 0.28f, 0.02f, EasingType.EaseOutBack, 1.15f));
            }

            // 2. Animate back button (slide up from bottom)
            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 60f);
                StartCoroutine(AnimateMotion(m_BackButton, startBack, m_BackRestPos, 0.28f, 0.04f, EasingType.EaseOutCubic));
            }

            // 3. Animate next and previous arrow buttons if active
            if (m_NextButton != null && m_NextButton.gameObject.activeSelf)
            {
                StartCoroutine(PopInButtonRoutine(m_NextButton, 0.20f));
            }
            if (m_PrevButton != null && m_PrevButton.gameObject.activeSelf)
            {
                StartCoroutine(PopInButtonRoutine(m_PrevButton, 0.20f));
            }

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.35f, 0.02f);

            // 4. Quick settle wait to ensure all banner animations are completed
            float settleWait = 0.22f;
            float timer = 0f;
            while (timer < settleWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure exact resting transforms
            ResetToRestState();

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateGridContainerEntrance(CanvasGroup gridCg, Vector2 startPos, Vector2 targetPos, float duration)
        {
            if (m_GridContainer == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration && m_GridContainer != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (gridCg != null) gridCg.alpha = Mathf.Lerp(0f, 1f, t * 1.4f);
                m_GridContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                yield return null;
            }

            if (gridCg != null) gridCg.alpha = 1f;
            if (m_GridContainer != null) m_GridContainer.anchoredPosition = targetPos;
        }

        private IEnumerator AnimatePanelEntranceRoutine()
        {
            if (m_PanelBackground == null) yield break;

            CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>() ?? m_PanelBackground.gameObject.AddComponent<CanvasGroup>();
            float duration = Mathf.Max(0.15f, m_PanelDuration);

            float elapsed = 0f;
            Vector3 startScale = m_PanelRestScale * 0.94f;

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.50f, 0.02f);

            while (elapsed < duration && m_PanelBackground != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                cg.alpha = Mathf.Lerp(0f, 1f, t * 1.5f);
                m_PanelBackground.localScale = Vector3.Lerp(startScale, m_PanelRestScale, ease);
                m_PanelBackground.anchoredPosition = m_PanelRestPos;

                yield return null;
            }

            if (m_PanelBackground != null)
            {
                cg.alpha = 1f;
                m_PanelBackground.localScale = m_PanelRestScale;
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
            }
        }

        private IEnumerator GridExitSequenceRoutine(Action onComplete)
        {
            float duration = 0.20f;
            float elapsed = 0f;

            CanvasGroup panelCg = null;
            if (m_PanelBackground != null)
            {
                panelCg = m_PanelBackground.GetComponent<CanvasGroup>() ?? m_PanelBackground.gameObject.AddComponent<CanvasGroup>();
            }

            CanvasGroup gridCg = null;
            if (m_GridContainer != null)
            {
                gridCg = m_GridContainer.GetComponent<CanvasGroup>() ?? m_GridContainer.gameObject.AddComponent<CanvasGroup>();
            }

            Vector3 startScale = m_PanelRestScale;
            Vector3 targetScale = m_PanelRestScale * 0.94f;

            Vector2 gridStartPos = m_GridContainer != null ? m_GridContainer.anchoredPosition : m_GridRestPos;
            Vector2 gridExitPos = gridStartPos + new Vector2(0f, -35f);

            Vector2 arcStartPos = m_ArcSign != null ? m_ArcSign.anchoredPosition : m_ArcRestPos;
            Vector2 arcExitPos = arcStartPos + new Vector2(0f, 50f);

            Vector2 backStartPos = m_BackButton != null ? m_BackButton.anchoredPosition : m_BackRestPos;
            Vector2 backExitPos = backStartPos + new Vector2(0f, -40f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.40f, 0.02f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInCubic, t);

                if (panelCg != null)
                {
                    panelCg.alpha = Mathf.Lerp(1f, 0f, ease);
                }
                if (m_PanelBackground != null)
                {
                    m_PanelBackground.localScale = Vector3.Lerp(startScale, targetScale, ease);
                }
                if (gridCg != null)
                {
                    gridCg.alpha = Mathf.Lerp(1f, 0f, ease);
                }
                if (m_GridContainer != null)
                {
                    m_GridContainer.anchoredPosition = Vector2.Lerp(gridStartPos, gridExitPos, ease);
                }
                if (m_ArcSign != null)
                {
                    m_ArcSign.anchoredPosition = Vector2.Lerp(arcStartPos, arcExitPos, ease);
                }
                if (m_LevelsSign != null)
                {
                    m_LevelsSign.anchoredPosition = Vector2.Lerp(m_LevelsRestPos, m_LevelsRestPos + new Vector2(0f, 50f), ease);
                }
                if (m_BackButton != null)
                {
                    m_BackButton.anchoredPosition = Vector2.Lerp(backStartPos, backExitPos, ease);
                }

                yield return null;
            }

            ResetToRestState();
            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator PageTransitionRoutine(Action onPageSwitch, Action onComplete, bool isForward = true)
        {
            CanvasGroup gridCg = null;
            if (m_GridContainer != null)
            {
                gridCg = m_GridContainer.GetComponent<CanvasGroup>() ?? m_GridContainer.gameObject.AddComponent<CanvasGroup>();
            }

            float slideDist = 70f;
            float exitOffset = isForward ? -slideDist : slideDist;
            float enterOffset = isForward ? slideDist : -slideDist;

            // 1. Slide and fade out old page smoothly
            float outDuration = 0.12f;
            float elapsed = 0f;
            Vector2 startPos = m_GridContainer != null ? m_GridContainer.anchoredPosition : m_GridRestPos;
            Vector2 outTargetPos = m_GridRestPos + new Vector2(exitOffset, 0f);

            while (elapsed < outDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / outDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (gridCg != null) gridCg.alpha = Mathf.Lerp(1f, 0f, t);
                if (m_GridContainer != null) m_GridContainer.anchoredPosition = Vector2.Lerp(startPos, outTargetPos, ease);

                yield return null;
            }

            // 2. Switch page data
            onPageSwitch?.Invoke();

            // 3. Prepare for slide-in from opposite side
            Vector2 inStartPos = m_GridRestPos + new Vector2(enterOffset, 0f);
            if (m_GridContainer != null)
            {
                m_GridContainer.anchoredPosition = inStartPos;
            }
            if (gridCg != null) gridCg.alpha = 0f;

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.35f, 0.02f);

            // 4. Slide and fade in new page
            float inDuration = 0.18f;
            elapsed = 0f;

            while (elapsed < inDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / inDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (gridCg != null) gridCg.alpha = Mathf.Lerp(0f, 1f, t * 1.3f);
                if (m_GridContainer != null) m_GridContainer.anchoredPosition = Vector2.Lerp(inStartPos, m_GridRestPos, ease);

                yield return null;
            }

            ResetToRestState();
            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator PopInButtonRoutine(Transform buttonTrans, float duration)
        {
            if (buttonTrans == null) yield break;

            Vector3 targetScale = Vector3.one;
            buttonTrans.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < duration && buttonTrans != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f);
                buttonTrans.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                yield return null;
            }

            if (buttonTrans != null)
            {
                buttonTrans.localScale = targetScale;
            }
        }

        private IEnumerator AnimateMotion(
            RectTransform target,
            Vector2 fromPos,
            Vector2 toPos,
            float duration,
            float delay = 0f,
            EasingType easing = EasingType.EaseOutCubic,
            float overshoot = 1.15f)
        {
            if (target == null) yield break;

            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay)
                {
                    d += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            target.anchoredPosition = fromPos;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);
                target.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, ease);
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = toPos;
            }
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
            while (elapsed < duration && element != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.2f);
                element.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                yield return null;
            }

            if (element != null)
            {
                element.localScale = targetScale;
            }
        }

        #endregion
    }
}
