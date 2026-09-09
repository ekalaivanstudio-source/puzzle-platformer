using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.RoboticEffects;
using MainGame.UI.Feedback;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Master cinematic animator for the Main Menu screen.
    /// Drives the "Machine Wakes Up" assembly sequence where every element has distinct physical weight:
    /// - 0.00s: Background establishes with subtle parallax/breathing
    /// - 0.05s: RETRY Logo Hero plunge from ceiling with top-to-bottom mask wipe reveal, hard impact, squash, micro-shake, sparks, and pendulum settle
    /// - 0.10s: Character (Dr. Glitch) subtle breathing & Robot (Byte) system boot-up (mechanical lift, cyan pulse, digital sparks, boot chime)
    /// - 0.15s: CONTINUE: Fast diagonal arrival from upper-right with tilt, overshoot, pendulum wobble, and secondary child counter-motion
    /// - 0.22s: NEW GAME: Heavy horizontal slide from far right with rigid 0 deg tilt, hard reverse brake snap, and secondary counter-motion
    /// - 0.30s: COLLECT: Rising curved arc from lower-right with rotation, overshoot, landing snap, and particle burst
    /// - 0.38s: OPTIONS: Short mechanical drop from upper-right with quick squash and rapid snap
    /// - 0.46s: CREDITS: Smooth vertical rise from below with soft elastic settle
    /// - 0.54s: EXIT: Heaviest signboard! Fast ceiling drop plunge, hard impact, horizontal squash, popup slam SFX, micro-shake, and warning sparks
    /// - 0.85s+: All elements settle into clean, stable rest state (no endless wobbling). Navigation unlocked.
    /// </summary>
    [DisallowMultipleComponent]
    public class HomeScreenAnimator : MonoBehaviour
    {
        [Header("Background Elements")]
        [SerializeField] private RectTransform m_Background;
        [SerializeField] private RectTransform m_BackgroundLayer2;
        [SerializeField] private float m_BgStartScale = 1.03f;
        [SerializeField] private float m_BgZoomDuration = 0.90f;

        [Header("Character & Robot (Living Lab Presence)")]
        [Tooltip("Dr. Glitch character artwork transform for subtle breathing motion.")]
        [SerializeField] private RectTransform m_CharacterArtwork;
        [Tooltip("Byte robot artwork transform for system boot-up and mechanical lift.")]
        [SerializeField] private RectTransform m_RobotArtwork;

        [Header("RETRY Logo (Hero Animation)")]
        [Tooltip("Container holding the logo visual.")]
        [SerializeField] private RectTransform m_Logo;
        [Tooltip("RectMask2D for the top-to-bottom lettering wipe reveal.")]
        [SerializeField] private RectMask2D m_LogoMask;

        [Header("Buttons (In Layout Order)")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_NewGameButton;
        [SerializeField] private Button m_CollectButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_CreditsButton;
        [SerializeField] private Button m_ExitButton;

        // Baseline Rest Transforms (Permanently captured once to eliminate drift)
        private Vector2 m_LogoRestPos;
        private Vector3 m_LogoRestScale = Vector3.one;
        private Vector3 m_LogoRestAngles = Vector3.zero;

        private Vector2 m_CharRestPos;
        private Vector2 m_RobotRestPos;
        private Vector2 m_Bg1RestPos;
        private Vector2 m_Bg2RestPos;
        private Vector3 m_BgRestScale = Vector3.one;
        private Vector3 m_Bg2RestScale = Vector3.one;
        private RectTransform m_ScreenPanelRoot;
        private Vector3 m_PanelRootRestScale = Vector3.one;

        private RectTransform[] m_ButtonRects;
        private Vector2[] m_ButtonRestPositions;
        private RectTransform[] m_ButtonAccents;
        private Vector2[] m_AccentRestPositions;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_AmbientRoutine;
        private readonly List<Coroutine> m_ChildCoroutines = new List<Coroutine>(16);
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

            if (m_ScreenPanelRoot == null)
            {
                m_ScreenPanelRoot = GetComponent<RectTransform>();
                if (m_ScreenPanelRoot != null)
                {
                    m_PanelRootRestScale = m_ScreenPanelRoot.localScale;
                }
            }

            // Auto-resolve background
            if (m_Background == null)
            {
                Transform bg = transform.parent != null ? (transform.parent.Find("Backagrond/Layer 01") ?? transform.parent.Find("Background/Layer 01")) : null;
                if (bg != null) m_Background = bg as RectTransform;
            }
            if (m_BackgroundLayer2 == null)
            {
                Transform bg2 = transform.parent != null ? (transform.parent.Find("Backagrond/Layer 02") ?? transform.parent.Find("Background/Layer 02")) : null;
                if (bg2 != null) m_BackgroundLayer2 = bg2 as RectTransform;
            }

            // Auto-resolve character & robot
            if (m_CharacterArtwork == null)
            {
                Transform cArt = transform.parent != null ? transform.parent.Find("Backagrond/CharacterArtwork") : null;
                if (cArt == null) cArt = transform.Find("CharacterArtwork") ?? transform.Find("Character") ?? transform.Find("Hero");
                if (cArt != null) m_CharacterArtwork = cArt as RectTransform;
            }
            if (m_RobotArtwork == null)
            {
                Transform rArt = transform.parent != null ? transform.parent.Find("Backagrond/Robot") : null;
                if (rArt == null) rArt = transform.Find("Robot") ?? transform.Find("ByteRobot") ?? transform.Find("Bot");
                if (rArt != null) m_RobotArtwork = rArt as RectTransform;
            }

            // Auto-resolve logo
            if (m_Logo == null)
            {
                Transform logo = transform.Find("Holder Tittle") ?? transform.Find("Title/Holder Tittle");
                if (logo != null) m_Logo = logo as RectTransform;
            }

            EnsureLogoMask();

            // Auto-resolve buttons
            if (m_ContinueButton == null || m_NewGameButton == null)
            {
                MainMenuScreen menu = GetComponent<MainMenuScreen>() ?? GetComponentInParent<MainMenuScreen>();
                if (menu != null)
                {
                    if (m_ContinueButton == null) m_ContinueButton = menu.ContinueButton;
                    if (m_NewGameButton == null) m_NewGameButton = menu.NewGameButton;
                    if (m_CollectButton == null) m_CollectButton = menu.CollectButton;
                    if (m_OptionsButton == null) m_OptionsButton = menu.OptionsButton;
                    if (m_CreditsButton == null) m_CreditsButton = menu.CreditsButton;
                    if (m_ExitButton == null) m_ExitButton = menu.ExitButton;
                }
            }

            if (m_Background != null)
            {
                m_Bg1RestPos = m_Background.anchoredPosition;
                m_BgRestScale = m_Background.localScale;
            }
            if (m_BackgroundLayer2 != null)
            {
                m_Bg2RestPos = m_BackgroundLayer2.anchoredPosition;
                m_Bg2RestScale = m_BackgroundLayer2.localScale;
            }

            if (m_CharacterArtwork != null)
            {
                m_CharRestPos = m_CharacterArtwork.anchoredPosition;
            }
            if (m_RobotArtwork != null)
            {
                m_RobotRestPos = m_RobotArtwork.anchoredPosition;
            }

            if (m_Logo != null)
            {
                m_LogoRestPos = m_Logo.anchoredPosition;
                m_LogoRestScale = m_Logo.localScale;
                m_LogoRestAngles = m_Logo.localEulerAngles;
            }

            Button[] buttons = new Button[]
            {
                m_ContinueButton,
                m_NewGameButton,
                m_CollectButton,
                m_OptionsButton,
                m_CreditsButton,
                m_ExitButton
            };

            m_ButtonRects = new RectTransform[buttons.Length];
            m_ButtonRestPositions = new Vector2[buttons.Length];
            m_ButtonAccents = new RectTransform[buttons.Length];
            m_AccentRestPositions = new Vector2[buttons.Length];

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    m_ButtonRects[i] = buttons[i].GetComponent<RectTransform>();
                    if (m_ButtonRects[i] != null)
                    {
                        m_ButtonRestPositions[i] = m_ButtonRects[i].anchoredPosition;
                    }

                    Transform selectIcon = buttons[i].transform.Find("Select Icon") ?? buttons[i].transform.Find("pointer");
                    if (selectIcon != null)
                    {
                        m_ButtonAccents[i] = selectIcon as RectTransform;
                        m_AccentRestPositions[i] = m_ButtonAccents[i].anchoredPosition;
                    }
                }
            }

            m_HasCapturedRest = true;
        }

        private void EnsureLogoMask()
        {
            if (m_Logo == null) return;

            Transform titleChild = m_Logo.Find("Title") ?? m_Logo.Find("Title Image") ?? m_Logo.Find("LogoMask/Title");
            if (titleChild != null)
            {
                if (titleChild.parent != null && titleChild.parent.name == "LogoMask")
                {
                    m_LogoMask = titleChild.parent.GetComponent<RectMask2D>();
                }
                else
                {
                    GameObject maskGo = new GameObject("LogoMask", typeof(RectTransform), typeof(RectMask2D));
                    RectTransform maskRt = maskGo.GetComponent<RectTransform>();
                    RectTransform titleRt = titleChild.GetComponent<RectTransform>();

                    maskRt.SetParent(m_Logo, false);
                    maskRt.anchorMin = titleRt.anchorMin;
                    maskRt.anchorMax = titleRt.anchorMax;
                    maskRt.pivot = titleRt.pivot;
                    maskRt.anchoredPosition = titleRt.anchoredPosition;
                    maskRt.sizeDelta = titleRt.sizeDelta;

                    titleRt.SetParent(maskRt, false);
                    titleRt.anchoredPosition = Vector2.zero;
                    titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                    titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                    titleRt.pivot = new Vector2(0.5f, 0.5f);

                    m_LogoMask = maskGo.GetComponent<RectMask2D>();
                }
            }

            if (m_LogoMask == null)
            {
                m_LogoMask = m_Logo.GetComponent<RectMask2D>();
                if (m_LogoMask == null)
                {
                    m_LogoMask = m_Logo.gameObject.AddComponent<RectMask2D>();
                }
            }
        }

        /// <summary>
        /// Instantly prepares off-screen hidden poses before the screen is visible, eliminating any pop.
        /// </summary>
        public void PrepareEntranceState()
        {
            StopActiveAnimation();
            CaptureRestState();

            if (m_ScreenPanelRoot != null)
            {
                m_ScreenPanelRoot.localScale = m_PanelRootRestScale;
            }

            // 1. Background
            if (m_Background != null)
            {
                m_Background.anchoredPosition = m_Bg1RestPos;
                m_Background.localScale = m_BgRestScale * m_BgStartScale;
            }
            if (m_BackgroundLayer2 != null)
            {
                m_BackgroundLayer2.anchoredPosition = m_Bg2RestPos;
                m_BackgroundLayer2.localScale = m_Bg2RestScale;
            }

            // 2. Character & Robot
            if (m_CharacterArtwork != null)
            {
                m_CharacterArtwork.anchoredPosition = m_CharRestPos;
                m_CharacterArtwork.localScale = Vector3.one;
            }
            if (m_RobotArtwork != null)
            {
                m_RobotArtwork.anchoredPosition = m_RobotRestPos + new Vector2(0f, -6f);
                m_RobotArtwork.localScale = new Vector3(0.96f, 0.96f, 1f);
                CanvasGroup rCg = m_RobotArtwork.GetComponent<CanvasGroup>();
                if (rCg != null) rCg.alpha = 0.65f;
            }

            // 3. Logo (above ceiling, tilted -4 deg, slightly vertically compressed)
            if (m_Logo != null)
            {
                m_Logo.anchoredPosition = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 750f);
                m_Logo.localScale = new Vector3(m_LogoRestScale.x * 1.02f, m_LogoRestScale.y * 0.92f, 1f);
                m_Logo.localEulerAngles = new Vector3(0f, 0f, -4f);
            }
            if (m_LogoMask != null)
            {
                m_LogoMask.padding = new Vector4(0f, 0f, 0f, 320f);
            }

            // 4. Buttons (off-screen on distinct vectors)
            Vector2[] startOffsets = new Vector2[]
            {
                new Vector2(380f, 450f),   // CONTINUE: Upper-right diagonal
                new Vector2(750f, 0f),     // NEW GAME: Far right horizontal
                new Vector2(320f, -520f),  // COLLECT: Lower-right
                new Vector2(140f, 280f),   // OPTIONS: Upper-right short
                new Vector2(0f, -650f),    // CREDITS: Below screen
                new Vector2(0f, 900f)      // EXIT: Ceiling plunge
            };

            float[] startAngles = new float[] { -5f, 0f, 4f, -2f, 0f, -2f };

            for (int i = 0; i < m_ButtonRects.Length; i++)
            {
                if (m_ButtonRects[i] != null)
                {
                    m_ButtonRects[i].anchoredPosition = m_ButtonRestPositions[i] + startOffsets[i];
                    m_ButtonRects[i].localScale = Vector3.one;
                    m_ButtonRects[i].localEulerAngles = new Vector3(0f, 0f, startAngles[i]);
                }

                if (m_ButtonAccents != null && i < m_ButtonAccents.Length && m_ButtonAccents[i] != null)
                {
                    m_ButtonAccents[i].anchoredPosition = m_AccentRestPositions[i];
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(MasterEntranceSequence(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(MasterExitSequence(onComplete));
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_AmbientRoutine != null)
            {
                StopCoroutine(m_AmbientRoutine);
                m_AmbientRoutine = null;
            }

            for (int i = 0; i < m_ChildCoroutines.Count; i++)
            {
                if (m_ChildCoroutines[i] != null)
                {
                    StopCoroutine(m_ChildCoroutines[i]);
                }
            }
            m_ChildCoroutines.Clear();
        }

        private void TrackCoroutine(Coroutine c)
        {
            if (c != null) m_ChildCoroutines.Add(c);
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MASTER ENTRANCE CHOREOGRAPHY (0.00s -> ~1.15s)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator MasterEntranceSequence(Action onComplete)
        {
            // 0.00s: Background establishes
            if (m_Background != null)
            {
                TrackCoroutine(StartCoroutine(AnimateScale(m_Background, m_BgRestScale * m_BgStartScale, m_BgRestScale, m_BgZoomDuration, EasingType.EaseOutQuad)));
            }

            // 0.05s: RETRY Logo Hero Drop begins
            yield return new WaitForSecondsRealtime(0.05f);
            bool logoDone = false;
            if (m_Logo != null)
            {
                TrackCoroutine(StartCoroutine(LogoHeroPlungeRoutine(() => logoDone = true)));
            }
            else
            {
                logoDone = true;
            }

            // 0.10s: Character & Robot life begins
            yield return new WaitForSecondsRealtime(0.05f);
            if (m_CharacterArtwork != null)
            {
                TrackCoroutine(StartCoroutine(CharacterLifeEntranceRoutine()));
            }
            if (m_RobotArtwork != null)
            {
                TrackCoroutine(StartCoroutine(RobotSystemBootRoutine()));
            }

            // 0.15s: CONTINUE (Fast Diagonal Arrival)
            yield return new WaitForSecondsRealtime(0.05f);
            bool btn0Done = false;
            if (GetButtonValid(0))
            {
                TrackCoroutine(StartCoroutine(ContinueButtonRoutine(() => btn0Done = true)));
            }
            else btn0Done = true;

            // 0.22s: NEW GAME (Heavy Horizontal Slide)
            yield return new WaitForSecondsRealtime(0.07f);
            bool btn1Done = false;
            if (GetButtonValid(1))
            {
                TrackCoroutine(StartCoroutine(NewGameButtonRoutine(() => btn1Done = true)));
            }
            else btn1Done = true;

            // 0.30s: COLLECT (Rising Arc)
            yield return new WaitForSecondsRealtime(0.08f);
            bool btn2Done = false;
            if (GetButtonValid(2))
            {
                TrackCoroutine(StartCoroutine(CollectButtonRoutine(() => btn2Done = true)));
            }
            else btn2Done = true;

            // 0.38s: OPTIONS (Short Mechanical Drop)
            yield return new WaitForSecondsRealtime(0.08f);
            bool btn3Done = false;
            if (GetButtonValid(3))
            {
                TrackCoroutine(StartCoroutine(OptionsButtonRoutine(() => btn3Done = true)));
            }
            else btn3Done = true;

            // 0.46s: CREDITS (Vertical Rise)
            yield return new WaitForSecondsRealtime(0.08f);
            bool btn4Done = false;
            if (GetButtonValid(4))
            {
                TrackCoroutine(StartCoroutine(CreditsButtonRoutine(() => btn4Done = true)));
            }
            else btn4Done = true;

            // 0.54s: EXIT (Heaviest Signboard Ceiling Slam)
            yield return new WaitForSecondsRealtime(0.08f);
            bool btn5Done = false;
            if (GetButtonValid(5))
            {
                TrackCoroutine(StartCoroutine(ExitButtonRoutine(() => btn5Done = true)));
            }
            else btn5Done = true;

            // Wait for all elements to finish landing and settle
            while (!logoDone || !btn0Done || !btn1Done || !btn2Done || !btn3Done || !btn4Done || !btn5Done)
            {
                yield return null;
            }

            // Settle all buttons into their exact rest state (NO wobbling/looping)
            ForceAllElementsToRest();

            // Start subtle, non-intrusive ambient life for character and background
            m_AmbientRoutine = StartCoroutine(AmbientLivingLoop());

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private bool GetButtonValid(int index)
        {
            return m_ButtonRects != null && index < m_ButtonRects.Length &&
                   m_ButtonRects[index] != null && m_ButtonRects[index].gameObject.activeInHierarchy;
        }

        private void ForceAllElementsToRest()
        {
            if (m_Logo != null)
            {
                m_Logo.anchoredPosition = m_LogoRestPos;
                m_Logo.localScale = m_LogoRestScale;
                m_Logo.localEulerAngles = m_LogoRestAngles;
            }
            if (m_LogoMask != null)
            {
                m_LogoMask.padding = Vector4.zero;
            }

            for (int i = 0; i < m_ButtonRects.Length; i++)
            {
                if (m_ButtonRects[i] != null)
                {
                    m_ButtonRects[i].anchoredPosition = m_ButtonRestPositions[i];
                    m_ButtonRects[i].localScale = Vector3.one;
                    m_ButtonRects[i].localEulerAngles = Vector3.zero;
                }

                if (m_ButtonAccents != null && i < m_ButtonAccents.Length && m_ButtonAccents[i] != null)
                {
                    m_ButtonAccents[i].anchoredPosition = m_AccentRestPositions[i];
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // ELEMENT KINEMATIC ROUTINES
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// RETRY Logo Hero Drop: Plunge from +750px down, top-to-bottom mask wipe reveal,
        /// hard landing impact (-20px overshoot), squash, micro-shake, sparks, and pendulum settle.
        /// </summary>
        private IEnumerator LogoHeroPlungeRoutine(Action onDone)
        {
            Vector2 startLogoPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 750f);
            Vector2 overshootPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y - 20f);
            Vector2 reboundPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 8f);

            m_Logo.anchoredPosition = startLogoPos;
            m_Logo.localScale = new Vector3(m_LogoRestScale.x * 0.94f, m_LogoRestScale.y * 1.06f, 1f);
            m_Logo.localEulerAngles = new Vector3(0f, 0f, -4f);

            float dropDur = 0.36f;
            float wipeDur = 0.22f;
            float elapsed = 0f;

            // Phase 1: Ballistic Drop + Mask Wipe
            while (elapsed < dropDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.LerpUnclamped(startLogoPos, overshootPos, ease);
                    m_Logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-4f, 2f, ease));
                    float stretch = Mathf.Lerp(1.06f, 0.96f, ease);
                    m_Logo.localScale = new Vector3(m_LogoRestScale.x / stretch, m_LogoRestScale.y * stretch, 1f);
                }

                if (m_LogoMask != null && elapsed < wipeDur)
                {
                    float wt = Mathf.Clamp01(elapsed / wipeDur);
                    m_LogoMask.padding = new Vector4(0f, 0f, 0f, Mathf.Lerp(320f, 0f, wt));
                }
                else if (m_LogoMask != null)
                {
                    m_LogoMask.padding = Vector4.zero;
                }

                yield return null;
            }

            if (m_Logo != null)
            {
                m_Logo.anchoredPosition = overshootPos;
                m_Logo.localEulerAngles = new Vector3(0f, 0f, 2f);
            }
            if (m_LogoMask != null) m_LogoMask.padding = Vector4.zero;

            // IMPACT FRAME EVENTS
            UIMicroShake.Shake(0.85f, 0.08f);
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.95f, 0.02f);
            SpawnLogoSparks();

            // Phase 2: Impact Squash (0.05s)
            Vector3 squishScale = new Vector3(m_LogoRestScale.x * 1.08f, m_LogoRestScale.y * 0.92f, 1f);
            elapsed = 0f;
            float squishDur = 0.05f;
            while (elapsed < squishDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / squishDur);
                if (m_Logo != null) m_Logo.localScale = Vector3.Lerp(m_LogoRestScale, squishScale, t);
                yield return null;
            }

            // Phase 3: Rebound (+8px Y, -1 deg angle)
            elapsed = 0f;
            float reboundDur = 0.06f;
            while (elapsed < reboundDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDur);
                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                    m_Logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(2f, -1f, t));
                    m_Logo.localScale = Vector3.Lerp(squishScale, m_LogoRestScale, t);
                }
                yield return null;
            }

            // Phase 4: Final Settle to exact rest (0.06s)
            elapsed = 0f;
            float settleDur = 0.06f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.Lerp(reboundPos, m_LogoRestPos, t);
                    m_Logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-1f, 0f, t));
                }
                yield return null;
            }

            if (m_Logo != null)
            {
                m_Logo.anchoredPosition = m_LogoRestPos;
                m_Logo.localScale = m_LogoRestScale;
                m_Logo.localEulerAngles = m_LogoRestAngles;
                StartCoroutine(LogoStabilizationGlitchRoutine(m_Logo));
            }

            onDone?.Invoke();
        }

        private void SpawnLogoSparks()
        {
            RoboticPixelFXPool pool = FindAnyObjectByType<RoboticPixelFXPool>();
            if (pool != null && m_Logo != null)
            {
                pool.SpawnSparkBurst(Vector2.zero, m_Logo, new Color(1.0f, 0.85f, 0.35f, 1f), 4, 18f);
            }
        }

        private IEnumerator LogoStabilizationGlitchRoutine(RectTransform logo)
        {
            if (logo == null) yield break;
            Vector2 basePos = logo.anchoredPosition;
            logo.anchoredPosition = basePos + new Vector2(-3f, 0f);
            yield return null;
            logo.anchoredPosition = basePos + new Vector2(2f, 0f);
            yield return null;
            logo.anchoredPosition = basePos;
        }

        /// <summary>
        /// 1. CONTINUE: Fast diagonal arrival from upper-right, -5 -> +2 -> 0 deg rotation,
        /// overshoot, hard landing, pendulum wobble, and secondary child counter-motion.
        /// </summary>
        private IEnumerator ContinueButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[0];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 0 ? m_ButtonAccents[0] : null;
            Vector2 targetPos = m_ButtonRestPositions[0];
            Vector2 startPos = targetPos + new Vector2(380f, 450f);
            Vector2 overshootPos = targetPos + new Vector2(-10f, -14f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = new Vector3(0f, 0f, -5f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.35f, 0.05f);

            float travelDur = 0.30f;
            float elapsed = 0f;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-5f, 2f, ease));
                }
                yield return null;
            }

            // LANDING IMPACT
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.70f, 0.04f);
            SpawnButtonSparks(btn, 3, new Color(0.35f, 0.80f, 1.0f));

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(-2f, -2f), 0.16f));
            }

            Vector3 squishScale = new Vector3(1.05f, 0.95f, 1f);
            elapsed = 0f;
            float squishDur = 0.04f;
            while (elapsed < squishDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / squishDur);
                if (btn != null) btn.localScale = Vector3.Lerp(Vector3.one, squishScale, t);
                yield return null;
            }

            elapsed = 0f;
            float wobbleDur = 0.16f;
            while (elapsed < wobbleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / wobbleDur);
                float rot = Mathf.Sin(t * Mathf.PI * 2.5f) * (3f * (1f - t));

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(overshootPos, targetPos, t);
                    btn.localScale = Vector3.Lerp(squishScale, Vector3.one, t);
                    btn.localEulerAngles = new Vector3(0f, 0f, rot);
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        /// <summary>
        /// 2. NEW GAME: Heavy horizontal slide from far right. Rigid 0 deg tilt (no rotation!),
        /// passes slightly beyond target (-18px overshoot), hard reverse brake snap, and settles.
        /// </summary>
        private IEnumerator NewGameButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[1];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 1 ? m_ButtonAccents[1] : null;
            Vector2 targetPos = m_ButtonRestPositions[1];
            Vector2 startPos = targetPos + new Vector2(750f, 0f);
            Vector2 overshootPos = targetPos + new Vector2(-18f, 0f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = Vector3.zero;

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.40f, 0.03f);

            float travelDur = 0.32f;
            float elapsed = 0f;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInCubic, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    btn.localEulerAngles = Vector3.zero;
                }
                yield return null;
            }

            // HARD REVERSE BRAKE SNAP
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.75f, 0.02f);
            SpawnButtonSparks(btn, 3, new Color(0.35f, 0.80f, 1.0f));

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(2.5f, 0f), 0.14f));
            }

            Vector3 squishScale = new Vector3(0.93f, 1.05f, 1f);
            elapsed = 0f;
            float brakeDur = 0.05f;
            while (elapsed < brakeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / brakeDur);
                if (btn != null) btn.localScale = Vector3.Lerp(Vector3.one, squishScale, t);
                yield return null;
            }

            elapsed = 0f;
            float snapDur = 0.10f;
            while (elapsed < snapDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / snapDur);
                float easeSnap = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.4f);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(overshootPos, targetPos, easeSnap);
                    btn.localScale = Vector3.Lerp(squishScale, Vector3.one, t);
                    btn.localEulerAngles = Vector3.zero;
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        /// <summary>
        /// 3. COLLECT: Rising curved arc from lower-right with rotation, overshoot, and landing snap.
        /// </summary>
        private IEnumerator CollectButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[2];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 2 ? m_ButtonAccents[2] : null;
            Vector2 targetPos = m_ButtonRestPositions[2];
            Vector2 startPos = targetPos + new Vector2(320f, -520f);
            Vector2 overshootPos = targetPos + new Vector2(-8f, 12f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = new Vector3(0f, 0f, 4f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.35f, 0.05f);

            float travelDur = 0.32f;
            float elapsed = 0f;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float easeX = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                float easeY = UIEasing.Evaluate(EasingType.EaseInQuad, t);
                float arcBulge = Mathf.Sin(t * Mathf.PI) * 28f;

                if (btn != null)
                {
                    float curX = Mathf.Lerp(startPos.x, overshootPos.x, easeX) + arcBulge;
                    float curY = Mathf.Lerp(startPos.y, overshootPos.y, easeY);
                    btn.anchoredPosition = new Vector2(curX, curY);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(4f, -1.5f, t));
                }
                yield return null;
            }

            // LANDING SNAP
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.65f, 0.03f);
            SpawnButtonSparks(btn, 3, new Color(0.35f, 0.85f, 1.0f));

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(0f, -2f), 0.14f));
            }

            elapsed = 0f;
            float settleDur = 0.12f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(overshootPos, targetPos, ease);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-1.5f, 0f, ease));
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        /// <summary>
        /// 4. OPTIONS: Short snappy mechanical drop from upper-right with quick squash.
        /// </summary>
        private IEnumerator OptionsButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[3];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 3 ? m_ButtonAccents[3] : null;
            Vector2 targetPos = m_ButtonRestPositions[3];
            Vector2 startPos = targetPos + new Vector2(140f, 280f);
            Vector2 overshootPos = targetPos + new Vector2(-4f, -10f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = new Vector3(0f, 0f, -2f);

            float dropDur = 0.20f;
            float elapsed = 0f;

            while (elapsed < dropDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-2f, 1f, ease));
                }
                yield return null;
            }

            // MECHANICAL DROP SNAP
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.60f, 0.05f);
            SpawnButtonSparks(btn, 2, new Color(0.35f, 0.80f, 1.0f));

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(0f, 1.5f), 0.10f));
            }

            Vector3 squishScale = new Vector3(1.05f, 0.95f, 1f);
            elapsed = 0f;
            float squishDur = 0.03f;
            while (elapsed < squishDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / squishDur);
                if (btn != null) btn.localScale = Vector3.Lerp(Vector3.one, squishScale, t);
                yield return null;
            }

            elapsed = 0f;
            float settleDur = 0.08f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(overshootPos, targetPos, t);
                    btn.localScale = Vector3.Lerp(squishScale, Vector3.one, t);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(1f, 0f, t));
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        /// <summary>
        /// 5. CREDITS: Smooth vertical rise from below with soft elastic settle.
        /// </summary>
        private IEnumerator CreditsButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[4];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 4 ? m_ButtonAccents[4] : null;
            Vector2 targetPos = m_ButtonRestPositions[4];
            Vector2 startPos = targetPos + new Vector2(0f, -650f);
            Vector2 overshootPos = targetPos + new Vector2(0f, 14f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = Vector3.zero;

            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.40f, 0.02f);

            float riseDur = 0.34f;
            float elapsed = 0f;

            while (elapsed < riseDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / riseDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    btn.localEulerAngles = Vector3.zero;
                }
                yield return null;
            }

            // SOFT SETTLING TICK
            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.50f, 0.03f);

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(0f, -1.5f), 0.14f));
            }

            elapsed = 0f;
            float settleDur = 0.12f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(overshootPos, targetPos, ease);
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        /// <summary>
        /// 6. EXIT: Heaviest Signboard! Ceiling drop plunge with gravity acceleration,
        /// hard impact (-28px overshoot), horizontal squash, popup slam SFX, micro-shake, and warning sparks.
        /// </summary>
        private IEnumerator ExitButtonRoutine(Action onDone)
        {
            RectTransform btn = m_ButtonRects[5];
            RectTransform accent = m_ButtonAccents != null && m_ButtonAccents.Length > 5 ? m_ButtonAccents[5] : null;
            Vector2 targetPos = m_ButtonRestPositions[5];
            Vector2 startPos = targetPos + new Vector2(0f, 900f);
            Vector2 overshootPos = targetPos + new Vector2(0f, -28f);
            Vector2 reboundPos = targetPos + new Vector2(0f, 6f);

            btn.anchoredPosition = startPos;
            btn.localEulerAngles = new Vector3(0f, 0f, -2f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.45f, 0.02f);

            float plungeDur = 0.28f;
            float elapsed = 0f;

            while (elapsed < plungeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / plungeDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-2f, 1f, ease));
                    btn.localScale = new Vector3(0.92f, 1.08f, 1f);
                }
                yield return null;
            }

            // HEAVY CEILING SLAM IMPACT
            UIFeedbackAudio.PlaySfx(UISfxType.PopupSlam, 0.95f, 0.02f);
            UIMicroShake.Shake(0.40f, 0.05f);
            SpawnButtonSparks(btn, 5, new Color(1.0f, 0.45f, 0.20f, 1f));

            if (accent != null)
            {
                StartCoroutine(AnimateSecondaryReaction(accent, new Vector2(0f, 3f), 0.16f));
            }

            Vector3 squishScale = new Vector3(1.12f, 0.88f, 1f);
            elapsed = 0f;
            float squishDur = 0.05f;
            while (elapsed < squishDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / squishDur);
                if (btn != null) btn.localScale = Vector3.Lerp(new Vector3(0.92f, 1.08f, 1f), squishScale, t);
                yield return null;
            }

            elapsed = 0f;
            float reboundDur = 0.06f;
            while (elapsed < reboundDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDur);
                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                    btn.localScale = Vector3.Lerp(squishScale, Vector3.one, t);
                    btn.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(1f, 0f, t));
                }
                yield return null;
            }

            elapsed = 0f;
            float settleDur = 0.06f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                if (btn != null)
                {
                    btn.anchoredPosition = Vector2.Lerp(reboundPos, targetPos, t);
                }
                yield return null;
            }

            if (btn != null)
            {
                btn.anchoredPosition = targetPos;
                btn.localScale = Vector3.one;
                btn.localEulerAngles = Vector3.zero;
            }
            onDone?.Invoke();
        }

        private void SpawnButtonSparks(RectTransform target, int count, Color color)
        {
            RoboticPixelFXPool pool = FindAnyObjectByType<RoboticPixelFXPool>();
            if (pool != null && target != null)
            {
                pool.SpawnSparkBurst(Vector2.zero, target, color, count, 14f);
            }
        }

        private IEnumerator AnimateSecondaryReaction(RectTransform child, Vector2 counterShift, float duration)
        {
            if (child == null) yield break;
            Vector2 basePos = child.anchoredPosition;
            child.anchoredPosition = basePos + counterShift;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (child != null)
                {
                    child.anchoredPosition = Vector2.Lerp(basePos + counterShift, basePos, ease);
                }
                yield return null;
            }

            if (child != null) child.anchoredPosition = basePos;
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // CHARACTER & ROBOT LIFE
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator RobotSystemBootRoutine()
        {
            if (m_RobotArtwork == null) yield break;

            Vector2 startPos = m_RobotRestPos + new Vector2(0f, -6f);
            m_RobotArtwork.anchoredPosition = startPos;
            m_RobotArtwork.localScale = new Vector3(0.96f, 0.96f, 1f);

            CanvasGroup cg = m_RobotArtwork.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0.65f;

            float liftDur = 0.28f;
            float elapsed = 0f;
            while (elapsed < liftDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / liftDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.2f);

                if (m_RobotArtwork != null)
                {
                    m_RobotArtwork.anchoredPosition = Vector2.Lerp(startPos, m_RobotRestPos, ease);
                    m_RobotArtwork.localScale = Vector3.Lerp(new Vector3(0.96f, 0.96f, 1f), Vector3.one, t);
                    if (cg != null) cg.alpha = Mathf.Lerp(0.65f, 1.0f, t);
                }
                yield return null;
            }

            if (m_RobotArtwork != null)
            {
                m_RobotArtwork.anchoredPosition = m_RobotRestPos;
                m_RobotArtwork.localScale = Vector3.one;
                if (cg != null) cg.alpha = 1.0f;
            }

            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.70f, 0.02f);
            RoboticPixelFXPool pool = FindAnyObjectByType<RoboticPixelFXPool>();
            if (pool != null && m_RobotArtwork != null)
            {
                pool.SpawnSparkBurst(new Vector2(0f, 40f), m_RobotArtwork, new Color(0.35f, 0.95f, 0.70f, 1f), 3, 12f);
            }
        }

        private IEnumerator CharacterLifeEntranceRoutine()
        {
            if (m_CharacterArtwork == null) yield break;
            Vector2 basePos = m_CharRestPos;

            float dur = 0.40f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float offset = Mathf.Sin(t * Mathf.PI) * 1.0f;
                if (m_CharacterArtwork != null)
                {
                    m_CharacterArtwork.anchoredPosition = basePos + new Vector2(0f, offset);
                }
                yield return null;
            }

            if (m_CharacterArtwork != null) m_CharacterArtwork.anchoredPosition = basePos;
        }

        private IEnumerator AmbientLivingLoop()
        {
            float timer = 0f;
            while (true)
            {
                timer += Time.unscaledDeltaTime;

                if (m_CharacterArtwork != null)
                {
                    float charBob = Mathf.Sin(timer * 1.2f) * 0.8f;
                    m_CharacterArtwork.anchoredPosition = m_CharRestPos + new Vector2(0f, charBob);
                }

                if (m_RobotArtwork != null)
                {
                    float botBob = Mathf.Sin(timer * 1.5f + 0.8f) * 0.5f;
                    m_RobotArtwork.anchoredPosition = m_RobotRestPos + new Vector2(0f, botBob);
                }

                if (m_Background != null)
                {
                    float bgParallax = Mathf.Sin(timer * 0.4f) * 1.5f;
                    m_Background.anchoredPosition = m_Bg1RestPos + new Vector2(bgParallax, 0f);
                }

                yield return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MASTER EXIT CHOREOGRAPHY (~0.35s)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator MasterExitSequence(Action onComplete)
        {
            float duration = 0.32f;
            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.85f, 0.02f);

            if (m_Logo != null)
            {
                Vector2 exitLogo = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 600f);
                StartCoroutine(AnimateMotion(m_Logo, m_Logo.anchoredPosition, exitLogo, duration * 0.8f, EasingType.EaseInQuad));
            }

            Vector2[] exitOffsets = new Vector2[]
            {
                new Vector2(400f, 400f),
                new Vector2(700f, 0f),
                new Vector2(350f, -400f),
                new Vector2(200f, 300f),
                new Vector2(0f, -500f),
                new Vector2(0f, 700f)
            };

            for (int i = 0; i < m_ButtonRects.Length; i++)
            {
                if (m_ButtonRects[i] != null && m_ButtonRects[i].gameObject.activeInHierarchy)
                {
                    Vector2 exitTarget = m_ButtonRestPositions[i] + exitOffsets[i];
                    StartCoroutine(AnimateMotion(m_ButtonRects[i], m_ButtonRects[i].anchoredPosition, exitTarget, duration, EasingType.EaseInQuad));
                }
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

        // ═════════════════════════════════════════════════════════════════════════════
        // UTILITY EASING COROUTINES
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator AnimateMotion(RectTransform target, Vector2 from, Vector2 to, float duration, EasingType easeType)
        {
            if (target == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easeType, t);
                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
                }
                yield return null;
            }
            if (target != null) target.anchoredPosition = to;
        }

        private IEnumerator AnimateScale(RectTransform target, Vector3 from, Vector3 to, float duration, EasingType easeType)
        {
            if (target == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easeType, t);
                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(from, to, ease);
                }
                yield return null;
            }
            if (target != null) target.localScale = to;
        }
    }
}
