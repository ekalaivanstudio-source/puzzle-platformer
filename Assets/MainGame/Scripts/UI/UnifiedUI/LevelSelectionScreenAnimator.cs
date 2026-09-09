using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using LevelSelection;
using MainGame.UI.RoboticEffects;
using MainGame.UI.Feedback;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Level Selection screen entrance and exit as a procedural Map Network Activation:
    /// 1. Map panel arrives via unfold / perspective deployment (+80px, scale X 0.92 -> 1.02 -> 1.00).
    /// 2. Stylized pixel-art bubble and particle activation effect across map and nodes.
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

        [Header("Pointer Reference")]
        [SerializeField] private LevelSelectionPointer m_Pointer;

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

        #region Pixel Bubble Activation System

        private class PixelBubbleItem
        {
            public GameObject root;
            public RectTransform rectTransform;
            public Image mainImage;
            public CanvasGroup canvasGroup;
            public RectTransform[] shards;
            public Image[] shardImages;
        }

        private enum BubbleStyle
        {
            TinyDriftLeft,
            TinyDriftRight,
            SmallOutward,
            MediumAroundNode,
            PixelFragment
        }

        private RectTransform m_BubbleContainer;
        private List<PixelBubbleItem> m_BubblePool;
        private const int BUBBLE_POOL_SIZE = 22;

        private static Sprite s_PixelTinyBubbleSprite;
        private static Sprite s_PixelSmallBubbleSprite;
        private static Sprite s_PixelMediumBubbleSprite;
        private static Sprite s_PixelFragmentSprite;
        private static Sprite s_PixelShardSprite;

        #endregion

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

            if (m_Pointer == null)
            {
                LevelSelectionManager mgr = GetComponent<LevelSelectionManager>() ?? GetComponentInParent<LevelSelectionManager>() ?? FindAnyObjectByType<LevelSelectionManager>();
                if (mgr != null) m_Pointer = mgr.Pointer;
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

            if (m_Pointer == null)
            {
                LevelSelectionManager mgr = GetComponent<LevelSelectionManager>() ?? GetComponentInParent<LevelSelectionManager>() ?? FindAnyObjectByType<LevelSelectionManager>();
                if (mgr != null) m_Pointer = mgr.Pointer;
            }
            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

            ResetBubblePool();

            if (m_PanelBackground != null)
            {
                Transform oldScan = m_PanelBackground.Find("MapScanline");
                if (oldScan != null) Destroy(oldScan.gameObject);
            }

            // Ensure the main shared scene background is always visible behind the level selection panel
            Transform screenManager = transform.parent;
            if (screenManager != null)
            {
                Transform bgLayer1 = screenManager.Find("Backagrond/Layer 01") ?? screenManager.Find("Background/Layer 01");
                if (bgLayer1 != null)
                {
                    CanvasGroup bgCg = bgLayer1.GetComponent<CanvasGroup>();
                    if (bgCg != null) bgCg.alpha = 1f;
                    bgLayer1.localScale = Vector3.one;
                }
                Transform bgLayer2 = screenManager.Find("Backagrond/Layer 02") ?? screenManager.Find("Background/Layer 02");
                if (bgLayer2 != null)
                {
                    CanvasGroup bgCg2 = bgLayer2.GetComponent<CanvasGroup>();
                    if (bgCg2 != null) bgCg2.alpha = 1f;
                    bgLayer2.localScale = Vector3.one;
                }
            }

            if (m_PanelBackground != null)
            {
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg == null) cg = m_PanelBackground.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = m_PanelRestScale;
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
        /// <summary>
        /// Plays the complete coordinated Map Network Activation cinematic with dynamically generated nodes and segments.
        /// </summary>
        public void PlayMapEntrance(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            Sprite arcSprite = null,
            int focusTargetNodeIndex = -1)
        {
            StopActiveAnimation();
            CaptureRestState();

            if (arcSprite != null && m_ArcSign != null)
            {
                Image arcImg = m_ArcSign.GetComponent<Image>() ?? m_ArcSign.GetComponentInChildren<Image>(true);
                if (arcImg != null) arcImg.sprite = arcSprite;
            }

            m_ActiveRoutine = StartCoroutine(MapEntranceSequenceRoutine(nodes, segments, highestUnlockedLevel, onComplete, focusTargetNodeIndex));
        }

        /// <summary>
        /// Plays a clean, synchronized map network activation transition when switching between arcs.
        /// Does not re-unfold the outer panel; updates the header sign and builds the new arc route.
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

            // Ensure outer panel and buttons stay at resting transforms
            if (m_PanelBackground != null)
            {
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = m_PanelRestScale;
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
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

            // Update Arc Sign header immediately with the new arc's sprite
            if (m_ArcSign != null)
            {
                m_ArcSign.anchoredPosition = m_ArcRestPos;
                m_ArcSign.localEulerAngles = m_ArcRestAngles;
                if (arcSprite != null)
                {
                    Image arcImg = m_ArcSign.GetComponent<Image>() ?? m_ArcSign.GetComponentInChildren<Image>(true);
                    if (arcImg != null) arcImg.sprite = arcSprite;
                }
                m_ArcBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine(m_ArcSign, m_ArcRestAngles.z, 0f));
            }

            if (m_LevelsSign != null)
            {
                m_LevelsSign.anchoredPosition = m_LevelsRestPos;
                m_LevelsSign.localEulerAngles = m_LevelsRestAngles;
                m_LevelsBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine(m_LevelsSign, m_LevelsRestAngles.z, 0.6f));
            }

            m_ActiveRoutine = StartCoroutine(ArcSwitchSequenceRoutine(nodes, segments, highestUnlockedLevel, onComplete, focusTargetNodeIndex));
        }

        private IEnumerator ArcSwitchSequenceRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            int focusTargetNodeIndex = -1)
        {
            if (m_Pointer == null)
            {
                LevelSelectionManager mgr = GetComponent<LevelSelectionManager>() ?? GetComponentInParent<LevelSelectionManager>() ?? FindAnyObjectByType<LevelSelectionManager>();
                if (mgr != null) m_Pointer = mgr.Pointer;
            }
            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

            // 1. Prepare node and segment initial states
            if (segments != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null) segments[i].SetProgressiveScale(0f);
                }
            }

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null) nodes[i].SetInitialBootState();
                }
            }

            // 2. Map Network Activation (Path reveal + Node boot-up + Camera scanline sweep + Pointer lock)
            yield return StartCoroutine(MapNetworkActivationRoutine(nodes, segments, highestUnlockedLevel, onComplete, focusTargetNodeIndex));
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

            StopAllCoroutines();

            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

            ResetBubblePool();
        }

        #endregion

        #region Entrance Coroutines

        private IEnumerator MapEntranceSequenceRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            int highestUnlockedLevel,
            Action onComplete,
            int focusTargetNodeIndex = -1)
        {
            // 1. Prepare entrance states
            PrepareEntranceState(nodes, segments);

            // 2. Animate Map Panel rapid expansion toward player (Beat 4)
            if (m_PanelBackground != null)
            {
                StartCoroutine(PanelUnfoldRoutine(m_PanelDuration));
            }

            // 3. Header elements entrance
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
            yield return StartCoroutine(MapNetworkActivationRoutine(nodes, segments, highestUnlockedLevel, onComplete, focusTargetNodeIndex));
        }

        private IEnumerator PanelUnfoldRoutine(float duration)
        {
            if (m_PanelBackground == null) yield break;

            CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
            if (cg == null) cg = m_PanelBackground.gameObject.AddComponent<CanvasGroup>();

            Vector3 startScale = new Vector3(0.70f, 0.70f, 1f);
            Vector3 overshootScale = new Vector3(1.025f, 1.025f, 1f);

            m_PanelBackground.anchoredPosition = m_PanelRestPos;
            m_PanelBackground.localScale = startScale;
            cg.alpha = 1f;

            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.85f, 0.02f);

            RoboticUIPanelEffect panelFX = m_PanelBackground.GetComponent<RoboticUIPanelEffect>();
            if (panelFX == null)
            {
                Image bgImg = m_PanelBackground.GetComponent<Image>();
                if (bgImg != null)
                {
                    panelFX = RoboticUIManager.GetOrAddPanelEffect(bgImg, new Color(0.35f, 0.78f, 1.0f, 1.0f), cornerBrackets: true, scanShimmer: false);
                }
            }
            if (panelFX != null)
            {
                panelFX.PlayPowerUp(duration);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                cg.alpha = 1f;
                m_PanelBackground.anchoredPosition = m_PanelRestPos;

                // Rapid forward expansion toward player with punchy settle
                if (t < 0.72f)
                {
                    float sT = t / 0.72f;
                    m_PanelBackground.localScale = Vector3.Lerp(startScale, overshootScale, UIEasing.Evaluate(EasingType.EaseOutQuad, sT));
                }
                else
                {
                    float sT = (t - 0.72f) / 0.28f;
                    m_PanelBackground.localScale = Vector3.Lerp(overshootScale, m_PanelRestScale, UIEasing.Evaluate(EasingType.EaseInOutQuad, sT));
                }

                yield return null;
            }

            m_PanelBackground.anchoredPosition = m_PanelRestPos;
            m_PanelBackground.localScale = m_PanelRestScale;
            cg.alpha = 1f;

            UIMicroShake.Shake(1.6f, 0.08f);
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.75f, 0.03f);
        }

        #region Pixel-Art Bubble Map Activation System

        private static Sprite GetTinyBubbleSprite()
        {
            if (s_PixelTinyBubbleSprite != null) return s_PixelTinyBubbleSprite;
            Texture2D tex = new Texture2D(6, 6, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PixelTinyBubble"
            };
            Color[] cols = new Color[36];
            Color cClear = Color.clear;
            Color cRim = new Color(0.25f, 0.75f, 0.95f, 0.90f);
            Color cBody = new Color(0.40f, 0.90f, 1.0f, 0.80f);
            Color cGlint = new Color(1f, 1f, 1f, 0.98f);

            string[] pattern = new string[]
            {
                "..##..",
                ".####.",
                "######",
                "##G###",
                ".####.",
                "..##.."
            };
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    char ch = pattern[5 - y][x];
                    cols[y * 6 + x] = (ch == '.') ? cClear : (ch == 'G') ? cGlint : (ch == '#') ? cBody : cRim;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            s_PixelTinyBubbleSprite = Sprite.Create(tex, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 1f);
            return s_PixelTinyBubbleSprite;
        }

        private static Sprite GetSmallBubbleSprite()
        {
            if (s_PixelSmallBubbleSprite != null) return s_PixelSmallBubbleSprite;
            Texture2D tex = new Texture2D(10, 10, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PixelSmallBubble"
            };
            Color[] cols = new Color[100];
            Color cClear = Color.clear;
            Color cRim = new Color(0.20f, 0.65f, 0.92f, 0.95f);
            Color cCore = new Color(0.35f, 0.85f, 1.0f, 0.35f);
            Color cGlint = new Color(1f, 1f, 1f, 0.98f);

            string[] pattern = new string[]
            {
                "...####...",
                "..######..",
                ".##GG..##.",
                ".##G....##.",
                "##......##",
                "##......##",
                ".##....##.",
                ".##....##.",
                "..######..",
                "...####..."
            };
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    char ch = pattern[9 - y][x];
                    cols[y * 10 + x] = (ch == '.') ? (x > 1 && x < 8 && y > 1 && y < 8 ? cCore : cClear) : (ch == 'G') ? cGlint : cRim;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            s_PixelSmallBubbleSprite = Sprite.Create(tex, new Rect(0, 0, 10, 10), new Vector2(0.5f, 0.5f), 1f);
            return s_PixelSmallBubbleSprite;
        }

        private static Sprite GetMediumBubbleSprite()
        {
            if (s_PixelMediumBubbleSprite != null) return s_PixelMediumBubbleSprite;
            Texture2D tex = new Texture2D(14, 14, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PixelMediumBubble"
            };
            Color[] cols = new Color[196];
            Color cClear = Color.clear;
            Color cRim = new Color(0.18f, 0.58f, 0.88f, 0.95f);
            Color cCore = new Color(0.35f, 0.85f, 1.0f, 0.28f);
            Color cGlint = new Color(1f, 1f, 1f, 0.98f);

            string[] pattern = new string[]
            {
                "....######....",
                "..##########..",
                ".##........##.",
                ".##GG......##.",
                "##GGG.......##",
                "##.G........##",
                "##..........##",
                "##..........##",
                "##..........##",
                "##..........##",
                ".##........##.",
                ".##........##.",
                "..##########..",
                "....######...."
            };
            for (int y = 0; y < 14; y++)
            {
                for (int x = 0; x < 14; x++)
                {
                    char ch = pattern[13 - y][x];
                    cols[y * 14 + x] = (ch == '.') ? (x > 1 && x < 12 && y > 1 && y < 12 ? cCore : cClear) : (ch == 'G') ? cGlint : cRim;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            s_PixelMediumBubbleSprite = Sprite.Create(tex, new Rect(0, 0, 14, 14), new Vector2(0.5f, 0.5f), 1f);
            return s_PixelMediumBubbleSprite;
        }

        private static Sprite GetPixelFragmentSprite()
        {
            if (s_PixelFragmentSprite != null) return s_PixelFragmentSprite;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PixelFragment"
            };
            Color[] cols = new Color[16];
            Color cClear = Color.clear;
            Color cBody = new Color(0.35f, 0.88f, 1.0f, 0.95f);
            Color cGlint = new Color(1f, 1f, 1f, 1.0f);

            string[] pattern = new string[]
            {
                ".##.",
                "#G##",
                "####",
                ".##."
            };
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    char ch = pattern[3 - y][x];
                    cols[y * 4 + x] = (ch == '.') ? cClear : (ch == 'G') ? cGlint : cBody;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            s_PixelFragmentSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
            return s_PixelFragmentSprite;
        }

        private static Sprite GetPixelShardSprite()
        {
            if (s_PixelShardSprite != null) return s_PixelShardSprite;
            Texture2D tex = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PixelShard"
            };
            Color[] cols = new Color[9];
            Color cClear = Color.clear;
            Color cColor = new Color(0.40f, 0.90f, 1.0f, 0.95f);
            Color cGlint = new Color(1f, 1f, 1f, 1.0f);

            string[] pattern = new string[]
            {
                ".#.",
                "#G#",
                ".#."
            };
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    char ch = pattern[2 - y][x];
                    cols[y * 3 + x] = (ch == '.') ? cClear : (ch == 'G') ? cGlint : cColor;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            s_PixelShardSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 1f);
            return s_PixelShardSprite;
        }

        private void EnsureBubblePool()
        {
            if (m_BubblePool != null && m_BubbleContainer != null) return;

            Transform targetParent = m_GridContainer != null ? m_GridContainer.transform : (m_PanelBackground != null ? m_PanelBackground.transform : transform);

            // Clean up any legacy scanline GameObject if present
            Transform oldScan = targetParent.Find("MapScanline");
            if (oldScan != null) Destroy(oldScan.gameObject);
            if (m_PanelBackground != null)
            {
                Transform pScan = m_PanelBackground.Find("MapScanline");
                if (pScan != null) Destroy(pScan.gameObject);
            }

            if (m_BubbleContainer == null)
            {
                Transform existing = targetParent.Find("BubbleFXContainer");
                if (existing != null)
                {
                    m_BubbleContainer = existing as RectTransform;
                }
                else
                {
                    GameObject containerObj = new GameObject("BubbleFXContainer", typeof(RectTransform));
                    containerObj.transform.SetParent(targetParent, false);
                    m_BubbleContainer = containerObj.GetComponent<RectTransform>();
                    m_BubbleContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    m_BubbleContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    m_BubbleContainer.pivot = new Vector2(0.5f, 0.5f);
                    m_BubbleContainer.sizeDelta = targetParent is RectTransform rt ? rt.sizeDelta : new Vector2(1400f, 900f);
                    m_BubbleContainer.anchoredPosition = Vector2.zero;
                }
            }

            // Ensure bubble container renders behind node buttons so level numbers are never obscured
            m_BubbleContainer.SetSiblingIndex(0);

            if (m_BubblePool == null)
            {
                m_BubblePool = new List<PixelBubbleItem>(BUBBLE_POOL_SIZE);
                for (int i = 0; i < BUBBLE_POOL_SIZE; i++)
                {
                    GameObject bubbleObj = new GameObject($"PixelBubble_{i}", typeof(RectTransform), typeof(CanvasGroup));
                    bubbleObj.transform.SetParent(m_BubbleContainer, false);

                    RectTransform rt = bubbleObj.GetComponent<RectTransform>();
                    CanvasGroup cg = bubbleObj.GetComponent<CanvasGroup>();

                    GameObject mainImgObj = new GameObject("MainSprite", typeof(RectTransform), typeof(Image));
                    mainImgObj.transform.SetParent(bubbleObj.transform, false);
                    RectTransform mainRt = mainImgObj.GetComponent<RectTransform>();
                    mainRt.anchorMin = new Vector2(0.5f, 0.5f);
                    mainRt.anchorMax = new Vector2(0.5f, 0.5f);
                    mainRt.pivot = new Vector2(0.5f, 0.5f);
                    mainRt.anchoredPosition = Vector2.zero;
                    mainRt.sizeDelta = new Vector2(16f, 16f);

                    Image img = mainImgObj.GetComponent<Image>();
                    img.raycastTarget = false;

                    RectTransform[] shards = new RectTransform[4];
                    Image[] shardImages = new Image[4];
                    Sprite shardSprite = GetPixelShardSprite();

                    for (int s = 0; s < 4; s++)
                    {
                        GameObject shardObj = new GameObject($"Shard_{s}", typeof(RectTransform), typeof(Image));
                        shardObj.transform.SetParent(bubbleObj.transform, false);
                        RectTransform srt = shardObj.GetComponent<RectTransform>();
                        srt.anchorMin = new Vector2(0.5f, 0.5f);
                        srt.anchorMax = new Vector2(0.5f, 0.5f);
                        srt.pivot = new Vector2(0.5f, 0.5f);
                        srt.sizeDelta = new Vector2(6f, 6f);
                        srt.anchoredPosition = Vector2.zero;

                        Image sImg = shardObj.GetComponent<Image>();
                        sImg.sprite = shardSprite;
                        sImg.raycastTarget = false;
                        shardObj.SetActive(false);

                        shards[s] = srt;
                        shardImages[s] = sImg;
                    }

                    bubbleObj.SetActive(false);

                    m_BubblePool.Add(new PixelBubbleItem
                    {
                        root = bubbleObj,
                        rectTransform = rt,
                        mainImage = img,
                        canvasGroup = cg,
                        shards = shards,
                        shardImages = shardImages
                    });
                }
            }

            ResetBubblePool();
        }

        private void ResetBubblePool()
        {
            if (m_BubblePool != null)
            {
                for (int i = 0; i < m_BubblePool.Count; i++)
                {
                    PixelBubbleItem item = m_BubblePool[i];
                    if (item == null || item.root == null) continue;

                    item.root.SetActive(false);
                    if (item.mainImage != null) item.mainImage.gameObject.SetActive(true);
                    if (item.rectTransform != null)
                    {
                        item.rectTransform.localScale = Vector3.zero;
                        item.rectTransform.localEulerAngles = Vector3.zero;
                    }
                    if (item.canvasGroup != null) item.canvasGroup.alpha = 0f;
                    if (item.shards != null)
                    {
                        for (int s = 0; s < item.shards.Length; s++)
                        {
                            if (item.shards[s] != null) item.shards[s].gameObject.SetActive(false);
                        }
                    }
                }
            }

            if (m_BubbleContainer != null)
            {
                m_BubbleContainer.gameObject.SetActive(false);
            }
        }

        private IEnumerator PixelBubbleActivationRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments)
        {
            EnsureBubblePool();
            if (m_BubblePool == null || m_BubblePool.Count == 0 || m_BubbleContainer == null) yield break;

            m_BubbleContainer.gameObject.SetActive(true);

            // Collect anchor points around runtime-generated nodes
            List<Vector2> anchorPositions = new List<Vector2>();

            if (nodes != null && nodes.Count > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null)
                    {
                        Vector3 worldPos = nodes[i].transform.position;
                        Vector2 localPos = m_BubbleContainer.InverseTransformPoint(worldPos);
                        anchorPositions.Add(localPos);
                    }
                }
            }

            if (anchorPositions.Count == 0)
            {
                anchorPositions.Add(new Vector2(-300f, -100f));
                anchorPositions.Add(new Vector2(0f, 0f));
                anchorPositions.Add(new Vector2(300f, 100f));
            }

            float totalDuration = 0.48f;
            int bubbleCount = Mathf.Min(BUBBLE_POOL_SIZE, 20);

            for (int i = 0; i < bubbleCount; i++)
            {
                PixelBubbleItem item = m_BubblePool[i];
                if (item == null) continue;

                Vector2 basePos = anchorPositions[i % anchorPositions.Count];

                BubbleStyle style;
                if (i % 5 == 0) style = BubbleStyle.MediumAroundNode;
                else if (i % 5 == 1) style = BubbleStyle.TinyDriftLeft;
                else if (i % 5 == 2) style = BubbleStyle.TinyDriftRight;
                else if (i % 5 == 3) style = BubbleStyle.SmallOutward;
                else style = BubbleStyle.PixelFragment;

                // Position offset: 42px to 80px away from node center so level numbers stay crisp & clear
                float angle = (i * 49f + 25f) * Mathf.Deg2Rad;
                float radius = UnityEngine.Random.Range(42f, 80f);
                Vector2 spawnPos = basePos + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

                float delay = UnityEngine.Random.Range(0f, 0.12f);
                float life = UnityEngine.Random.Range(0.30f, 0.38f);

                StartCoroutine(AnimateSingleBubble(item, spawnPos, style, delay, life));
            }

            float timer = 0f;
            while (timer < totalDuration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            ResetBubblePool();
        }

        private IEnumerator AnimateSingleBubble(
            PixelBubbleItem item,
            Vector2 spawnPos,
            BubbleStyle style,
            float delay,
            float lifetime)
        {
            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay)
                {
                    d += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            Sprite sprite;
            Vector2 baseSize;
            Vector2 moveDelta;
            float rotSpeed = 0f;

            switch (style)
            {
                case BubbleStyle.TinyDriftLeft:
                    sprite = GetTinyBubbleSprite();
                    baseSize = new Vector2(12f, 12f);
                    moveDelta = new Vector2(UnityEngine.Random.Range(-25f, -10f), UnityEngine.Random.Range(32f, 52f));
                    break;
                case BubbleStyle.TinyDriftRight:
                    sprite = GetTinyBubbleSprite();
                    baseSize = new Vector2(12f, 12f);
                    moveDelta = new Vector2(UnityEngine.Random.Range(10f, 25f), UnityEngine.Random.Range(32f, 52f));
                    break;
                case BubbleStyle.SmallOutward:
                    sprite = GetSmallBubbleSprite();
                    baseSize = new Vector2(18f, 18f);
                    moveDelta = new Vector2(UnityEngine.Random.Range(-28f, 28f), UnityEngine.Random.Range(15f, 38f));
                    break;
                case BubbleStyle.MediumAroundNode:
                    sprite = GetMediumBubbleSprite();
                    baseSize = new Vector2(24f, 24f);
                    moveDelta = new Vector2(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(18f, 35f));
                    break;
                case BubbleStyle.PixelFragment:
                default:
                    sprite = GetPixelFragmentSprite();
                    baseSize = new Vector2(10f, 10f);
                    moveDelta = new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(18f, 42f));
                    rotSpeed = UnityEngine.Random.Range(-120f, 120f);
                    break;
            }

            item.mainImage.sprite = sprite;
            item.mainImage.gameObject.SetActive(true);
            item.rectTransform.sizeDelta = baseSize;
            item.rectTransform.anchoredPosition = spawnPos;
            item.rectTransform.localScale = Vector3.zero;
            item.rectTransform.localEulerAngles = Vector3.zero;
            item.canvasGroup.alpha = 1f;

            for (int s = 0; s < item.shards.Length; s++)
            {
                item.shards[s].gameObject.SetActive(false);
            }

            item.root.SetActive(true);

            // Phase 1: Float, drift, scale expansion
            float elapsed = 0f;
            float popPhaseDuration = 0.08f;
            float floatDuration = Mathf.Max(0.10f, lifetime - popPhaseDuration);
            float startRot = UnityEngine.Random.Range(0f, 360f);

            while (elapsed < floatDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / floatDuration);

                // Quick scale-in and subtle float breathe
                float scale;
                if (t < 0.25f)
                {
                    scale = Mathf.Lerp(0f, 1.12f, t / 0.25f);
                }
                else
                {
                    scale = Mathf.Lerp(1.12f, 1.0f, (t - 0.25f) / 0.75f);
                }
                item.rectTransform.localScale = new Vector3(scale, scale, 1f);

                // Varied movement with subtle sine wobble
                float wobble = Mathf.Sin(t * Mathf.PI * 2.5f) * 6f;
                Vector2 currentPos = Vector2.Lerp(spawnPos, spawnPos + moveDelta, t) + new Vector2(wobble, 0f);
                item.rectTransform.anchoredPosition = currentPos;

                if (rotSpeed != 0f)
                {
                    item.rectTransform.localEulerAngles = new Vector3(0f, 0f, startRot + rotSpeed * t);
                }

                yield return null;
            }

            // Phase 2: Bubble Pop with 4-shard pixel burst
            UIFeedbackAudio.PlaySfx(UISfxType.BubblePop, 0.45f, 0.08f);
            item.mainImage.gameObject.SetActive(false);

            Vector2[] shardDirs = new Vector2[]
            {
                new Vector2(-0.707f, 0.707f),
                new Vector2(0.707f, 0.707f),
                new Vector2(-0.707f, -0.707f),
                new Vector2(0.707f, -0.707f)
            };

            for (int s = 0; s < item.shards.Length; s++)
            {
                item.shards[s].sizeDelta = new Vector2(6f, 6f);
                item.shards[s].anchoredPosition = Vector2.zero;
                item.shards[s].localScale = Vector3.one;
                item.shardImages[s].color = new Color(0.40f, 0.90f, 1.0f, 1.0f);
                item.shards[s].gameObject.SetActive(true);
            }

            float burstElapsed = 0f;
            float burstDistance = UnityEngine.Random.Range(10f, 16f);

            while (burstElapsed < popPhaseDuration)
            {
                burstElapsed += Time.unscaledDeltaTime;
                float bt = Mathf.Clamp01(burstElapsed / popPhaseDuration);

                float shardAlpha = 1f - bt;
                for (int s = 0; s < item.shards.Length; s++)
                {
                    item.shards[s].anchoredPosition = shardDirs[s] * (burstDistance * bt);
                    item.shards[s].localScale = Vector3.one * (1f - bt * 0.4f);
                    item.shardImages[s].color = new Color(0.40f, 0.90f, 1.0f, shardAlpha);
                }

                yield return null;
            }

            item.root.SetActive(false);
        }

        #endregion

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
            Action onComplete,
            int focusTargetNodeIndex = -1)
        {
            // Step 1: Sequential Route Drawing & Node Boot-Up
            if (nodes != null && nodes.Count > 0)
            {
                // First node boots up immediately
                UIFeedbackAudio.PlaySteppedSfx(UISfxType.NodeActivate, 0, nodes.Count);
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
                            UIFeedbackAudio.PlaySteppedSfx(UISfxType.NodeActivate, i + 1, nodes.Count);
                            StartCoroutine(targetNode.PlayBootUpRoutine(targetNode.IsUnlocked, 0f));
                        }
                    }
                }
            }

            // Step 2: Ensure all nodes have completed boot-up
            float nodeSettleWait = 0.10f;
            float timer = 0f;
            while (timer < nodeSettleWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Step 2.5: Pixel-Art Bubble & Particle Map Activation Effect
            yield return StartCoroutine(PixelBubbleActivationRoutine(nodes, segments));

            // Step 2.6: Small Node Ripples across unlocked nodes
            if (nodes != null && nodes.Count > 0)
            {
                float rippleDuration = 0.08f;
                for (int i = 0; i < nodes.Count; i++)
                {
                    LevelNodeUI node = nodes[i];
                    if (node != null && node.IsUnlocked)
                    {
                        StartCoroutine(NodeRippleRoutine(node.transform, rippleDuration));
                    }
                }
                float rTimer = 0f;
                while (rTimer < rippleDuration)
                {
                    rTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Step 3 (Beat 8): Route Energy Circuit Pulse from Start to Current Level (only on initial entrance when focusing current level)
            LevelNodeUI currentLevelNode = null;
            int currentNodeIndex = -1;
            bool currentLevelInThisArc = false;

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null && nodes[i].levelNumber == highestUnlockedLevel)
                    {
                        currentLevelNode = nodes[i];
                        currentNodeIndex = i;
                        currentLevelInThisArc = true;
                        break;
                    }
                }
            }

            if (segments != null && focusTargetNodeIndex < 0 && currentLevelInThisArc && currentNodeIndex > 0)
            {
                UIFeedbackAudio.PlaySfx(UISfxType.Pulse, 0.70f, 0.03f);
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

            // Step 4: Drop Pointer from above onto authoritative destination node
            LevelNodeUI dropTargetNode = null;
            if (nodes != null && nodes.Count > 0)
            {
                if (focusTargetNodeIndex >= 0)
                {
                    int targetIdx = Mathf.Clamp(focusTargetNodeIndex, 0, nodes.Count - 1);
                    dropTargetNode = nodes[targetIdx];
                }
                else if (currentLevelInThisArc && currentLevelNode != null)
                {
                    dropTargetNode = currentLevelNode;
                }
                else
                {
                    dropTargetNode = nodes[0];
                }
            }
            if (dropTargetNode != null)
            {
                if (m_Pointer == null)
                {
                    LevelSelectionManager mgr = GetComponent<LevelSelectionManager>() ?? GetComponentInParent<LevelSelectionManager>() ?? FindAnyObjectByType<LevelSelectionManager>();
                    if (mgr != null) m_Pointer = mgr.Pointer;
                }

                UIFeedbackAudio.PlaySfx(UISfxType.PointerSnap, 0.85f, 0.02f);

                if (m_Pointer != null)
                {
                    bool dropDone = false;
                    m_Pointer.PlayEntranceDrop(dropTargetNode, () => dropDone = true);
                    while (!dropDone)
                    {
                        yield return null;
                    }
                }
                else
                {
                    yield return StartCoroutine(dropTargetNode.PlayMarkerDropRoutine());
                }
            }

            // Step 5: Finished! Navigation is now ready to be unlocked
            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator NodeRippleRoutine(Transform target, float duration)
        {
            if (target == null) yield break;
            Vector3 baseScale = target.localScale;
            Vector3 rippleScale = baseScale * 1.08f;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Sin(t * Mathf.PI);
                target.localScale = Vector3.Lerp(baseScale, rippleScale, s);
                yield return null;
            }
            if (target != null) target.localScale = baseScale;
        }

        #endregion

        #region Exit Coroutines

        private IEnumerator PlayMapExitRoutine(
            List<LevelNodeUI> nodes,
            List<UIPathSegment> segments,
            Action onComplete)
        {
            float duration = 0.24f;

            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

            ResetBubblePool();

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
                RoboticUIPanelEffect panelFX = m_PanelBackground.GetComponent<RoboticUIPanelEffect>();
                if (panelFX != null)
                {
                    panelFX.PlayPowerDown(duration);
                }
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
