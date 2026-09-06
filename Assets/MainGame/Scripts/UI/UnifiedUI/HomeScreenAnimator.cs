using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Drives the high-velocity, authored physical motion design for the Home Screen:
    /// - Distinct HERO animation for Title/Logo: dynamic RectMask2D lettering wipe reveal + vertical drop plunge + impact stretch/squash + settle.
    /// - Authored Physical Signboard identities for buttons: rotational fall (-12 deg -> +2 deg -> 0 deg), fast ballistic descent, impact squash (1.06x / 0.92y), rebound, and secondary text / accent reveal (0.04s later).
    /// - Start Order != Landing Order: Collect (lands 0.44s) overtakes New Game (lands 0.72s), Exit (lands 0.78s) overtakes Credits (lands 0.96s).
    /// - Directionally distinct exits: Title flings up, Continue/Collect fly right, New Game flings up, Options/Exit drop down, Credits flings diagonally down-right.
    /// - Replays FULL choreography on every entry and re-entry (Home -> LevelSelection -> Home), never skipping or compressing timings.
    /// - Zero GC allocations during runtime animation, driven by unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public class HomeScreenAnimator : MonoBehaviour
    {
        [Header("Background & Scene Establishment")]
        [Tooltip("Background container or layer RectTransform.")]
        [SerializeField] private RectTransform m_Background;
        [SerializeField] private RectTransform m_BackgroundLayer2;
        [SerializeField] private float m_BgStartScale = 1.04f;
        [SerializeField] private float m_BgZoomDuration = 0.90f;

        [Header("Main Character Artwork (Optional Left Hero/Villain Layer)")]
        [Tooltip("Character artwork on left if separated from background.")]
        [SerializeField] private RectTransform m_CharacterArtwork;
        [SerializeField] private float m_CharacterEntryDistanceX = -750f;
        [SerializeField] private float m_CharacterDuration = 0.70f;

        [Header("RETRY Logo (Hero Animation)")]
        [Tooltip("Game logo container transform with heavy ceiling plunge and impact settling.")]
        [SerializeField] private RectTransform m_Logo;
        [Tooltip("RectMask2D used for the top-to-bottom lettering wipe reveal.")]
        [SerializeField] private RectMask2D m_LogoMask;
        [SerializeField] private float m_MaskWipeDuration = 0.24f;
        [SerializeField] private UIElementMotionConfig m_LogoMotion = new UIElementMotionConfig(
            "Logo", new Vector2(0f, 1f), 700f, 0.04f, 0.38f, -4f, new Vector2(0.94f, 1.06f), 22f
        );

        [Header("Buttons (In Layout Order: Continue, NewGame, Collect, Options, Credits, Exit)")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_NewGameButton;
        [SerializeField] private Button m_CollectButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_CreditsButton;
        [SerializeField] private Button m_ExitButton;

        [Header("Authored Physical Button Motion Profiles")]
        [Tooltip("Exposed physical motion profiles for each button. Distinct velocities, directions, delays, and impacts.")]
        [SerializeField] private UIElementMotionConfig[] m_ButtonMotions;

        private Vector2 m_LogoRestPos;
        private Vector3 m_LogoRestScale = Vector3.one;
        private Vector3 m_LogoRestAngles = Vector3.zero;

        private Vector2 m_CharacterRestPos;
        private Vector2 m_Bg1RestPos;
        private Vector2 m_Bg2RestPos;
        private Vector3 m_BgRestScale = Vector3.one;
        private Vector3 m_Bg2RestScale = Vector3.one;

        private RectTransform[] m_ButtonRects;
        private Vector2[] m_ButtonRestPositions;
        private RectTransform[] m_ButtonAccents;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_LogoIdleRoutine;
        private Coroutine m_ParallaxRoutine;
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

            if (m_CharacterArtwork == null)
            {
                Transform charArt = transform.Find("Character") ?? transform.Find("Hero") ?? transform.Find("Villain");
                if (charArt != null) m_CharacterArtwork = charArt as RectTransform;
            }

            if (m_Logo == null)
            {
                Transform logo = transform.Find("Holder Tittle") ?? transform.Find("Title/Holder Tittle");
                if (logo != null) m_Logo = logo as RectTransform;
            }

            EnsureLogoMask();

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
                m_CharacterRestPos = m_CharacterArtwork.anchoredPosition;
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
                    }
                }
            }

            EnsureMotionConfigs();
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
                    // Create dedicated mask container between m_Logo and titleChild
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

        private void EnsureMotionConfigs()
        {
            if (m_ButtonMotions == null || m_ButtonMotions.Length != 6)
            {
                m_ButtonMotions = new UIElementMotionConfig[]
                {
                    // 0. CONTINUE: Upper-right, fast rotational descent, lands at 0.44s
                    new UIElementMotionConfig("Continue", new Vector2(0.707f, 0.707f), 750f, 0.10f, 0.34f, -12f, new Vector2(1.06f, 0.94f), 18f),

                    // 1. NEW GAME: High vertical drop from top, lands at 0.72s (AFTER Collect!)
                    new UIElementMotionConfig("NewGame", new Vector2(0f, 1f), 850f, 0.24f, 0.48f, 6f, new Vector2(1.08f, 0.92f), 22f),

                    // 2. COLLECT: Horizontal right slide, VERY FAST ballistic flight, lands at 0.44s (BEFORE New Game!)
                    new UIElementMotionConfig("Collect", new Vector2(1f, 0f), 800f, 0.18f, 0.26f, -6f, new Vector2(1.09f, 0.91f), 20f),

                    // 3. OPTIONS: Diagonal upper-right drop, lands at 0.74s
                    new UIElementMotionConfig("Options", new Vector2(0.8f, 0.6f), 750f, 0.32f, 0.42f, -8f, new Vector2(1.05f, 0.95f), 16f),

                    // 4. CREDITS: High diagonal drop, heavier/slower wooden sign, lands at 0.96s (Lands LAST!)
                    new UIElementMotionConfig("Credits", new Vector2(0.6f, 0.8f), 850f, 0.40f, 0.56f, 8f, new Vector2(1.04f, 0.96f), 12f),

                    // 5. EXIT: Rising from bottom-right, snappy, lands at 0.78s (BEFORE Credits!)
                    new UIElementMotionConfig("Exit", new Vector2(0.85f, -0.55f), 800f, 0.48f, 0.30f, -10f, new Vector2(1.08f, 0.92f), 22f)
                };
            }

            if (m_LogoMotion == null)
            {
                m_LogoMotion = new UIElementMotionConfig("Logo", new Vector2(0f, 1f), 700f, 0.04f, 0.38f, -4f, new Vector2(0.94f, 1.06f), 22f);
            }
        }

        /// <summary>
        /// Instantly places all elements into their starting off-screen/masked poses.
        /// Must be called before making the screen active to prevent visible rest pops on re-entry.
        /// </summary>
        public void PrepareEntranceState()
        {
            StopActiveAnimation();
            CaptureRestState();

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

            if (m_CharacterArtwork != null)
            {
                m_CharacterArtwork.anchoredPosition = new Vector2(m_CharacterRestPos.x + m_CharacterEntryDistanceX, m_CharacterRestPos.y);
                m_CharacterArtwork.localScale = Vector3.one;
            }

            if (m_Logo != null)
            {
                float dropDist = m_LogoMotion != null ? m_LogoMotion.EntryDistance : 700f;
                m_Logo.anchoredPosition = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + dropDist);
                m_Logo.localScale = new Vector3(0.92f, 0.92f, 1f);
                m_Logo.localEulerAngles = new Vector3(0f, 0f, -4f);
            }

            if (m_LogoMask != null)
            {
                // Top lettering masked out completely (320px from top)
                m_LogoMask.padding = new Vector4(0f, 0f, 0f, 320f);
            }

            if (m_ButtonRects != null && m_ButtonRestPositions != null)
            {
                for (int i = 0; i < m_ButtonRects.Length; i++)
                {
                    RectTransform btnRect = m_ButtonRects[i];
                    if (btnRect == null) continue;

                    UIElementMotionConfig cfg = (m_ButtonMotions != null && i < m_ButtonMotions.Length)
                        ? m_ButtonMotions[i]
                        : new UIElementMotionConfig();

                    btnRect.anchoredPosition = m_ButtonRestPositions[i] + cfg.CalculateStartOffset();
                    btnRect.localScale = new Vector3(1.02f, 1.02f, 1f);
                    btnRect.localEulerAngles = new Vector3(0f, 0f, cfg.RotationStart);
                }
            }
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_Background != null)
            {
                m_Background.anchoredPosition = m_Bg1RestPos;
                m_Background.localScale = m_BgRestScale;
            }

            if (m_BackgroundLayer2 != null)
            {
                m_BackgroundLayer2.anchoredPosition = m_Bg2RestPos;
                m_BackgroundLayer2.localScale = m_Bg2RestScale;
            }

            if (m_CharacterArtwork != null)
            {
                m_CharacterArtwork.anchoredPosition = m_CharacterRestPos;
                m_CharacterArtwork.localScale = Vector3.one;
            }

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

            if (m_ButtonRects != null && m_ButtonRestPositions != null)
            {
                for (int i = 0; i < m_ButtonRects.Length; i++)
                {
                    if (m_ButtonRects[i] != null && i < m_ButtonRestPositions.Length)
                    {
                        m_ButtonRects[i].anchoredPosition = m_ButtonRestPositions[i];
                        m_ButtonRects[i].localScale = Vector3.one;
                        m_ButtonRects[i].localEulerAngles = Vector3.zero;
                    }
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            PrepareEntranceState();
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

            if (m_LogoIdleRoutine != null)
            {
                StopCoroutine(m_LogoIdleRoutine);
                m_LogoIdleRoutine = null;
            }

            if (m_ParallaxRoutine != null)
            {
                StopCoroutine(m_ParallaxRoutine);
                m_ParallaxRoutine = null;
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

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Background establishment (slow establishing camera punch/zoom)
            if (m_Background != null)
            {
                TrackChildCoroutine(AnimateScale(m_Background, m_BgRestScale * m_BgStartScale, m_BgRestScale, m_BgZoomDuration, EasingType.EaseOutQuad));
            }

            // 2. Character artwork slide-in
            if (m_CharacterArtwork != null)
            {
                Vector2 startChar = new Vector2(m_CharacterRestPos.x + m_CharacterEntryDistanceX, m_CharacterRestPos.y);
                TrackChildCoroutine(AnimateMotion(m_CharacterArtwork, startChar, m_CharacterRestPos, 0f, 0f, m_CharacterDuration, 0.05f, EasingType.EaseOutBack, 1.15f));
            }

            // 3. Hero RETRY Logo: Mask reveal + vertical plunge + squash & stretch
            bool logoDone = (m_Logo == null);
            if (m_Logo != null)
            {
                TrackChildCoroutine(LogoHeroPlungeRoutine(() => logoDone = true));
            }

            // 4. Choreographed Physical Button Entrances with Start Order != Landing Order
            int totalActive = 0;
            for (int i = 0; i < m_ButtonRects.Length; i++)
            {
                RectTransform btnRect = m_ButtonRects[i];
                if (btnRect != null && btnRect.gameObject.activeInHierarchy) totalActive++;
            }

            int buttonsFinished = 0;
            for (int i = 0; i < m_ButtonRects.Length; i++)
            {
                RectTransform btnRect = m_ButtonRects[i];
                if (btnRect == null || !btnRect.gameObject.activeInHierarchy) continue;

                UIElementMotionConfig cfg = (m_ButtonMotions != null && i < m_ButtonMotions.Length)
                    ? m_ButtonMotions[i]
                    : new UIElementMotionConfig();

                Vector2 restPos = m_ButtonRestPositions[i];
                Vector2 startPos = restPos + cfg.CalculateStartOffset();
                RectTransform accent = (m_ButtonAccents != null && i < m_ButtonAccents.Length) ? m_ButtonAccents[i] : null;

                TrackChildCoroutine(ButtonBallisticEntranceRoutine(
                    btnRect, accent,
                    startPos, restPos,
                    cfg,
                    cfg.StartDelay,
                    () => buttonsFinished++
                ));
            }

            // Wait for both Logo Hero sequence AND all buttons to land and settle
            while (!logoDone || buttonsFinished < totalActive)
            {
                yield return null;
            }

            // Start living background parallax & subtle logo breathing bob ONLY after landing is complete
            m_ParallaxRoutine = StartCoroutine(BackgroundParallaxRoutine());
            m_LogoIdleRoutine = StartCoroutine(LogoIdleRoutine());

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator LogoHeroPlungeRoutine(Action onDone)
        {
            float dropDist = m_LogoMotion != null ? m_LogoMotion.EntryDistance : 700f;
            Vector2 startLogoPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + dropDist);
            Vector2 overshootPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y - 22f);
            Vector2 reboundPos = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 6f);

            m_Logo.anchoredPosition = startLogoPos;
            m_Logo.localScale = new Vector3(0.92f, 0.92f, 1f);
            m_Logo.localEulerAngles = new Vector3(0f, 0f, -4f);

            // Phase 1: Lettering wipe reveal (0.24s) via RectMask2D
            if (m_LogoMask != null)
            {
                m_LogoMask.padding = new Vector4(0f, 0f, 0f, 320f);
            }

            float duration = m_LogoMotion != null ? m_LogoMotion.Duration : 0.38f;
            AnimationCurve curve = UIMotionProfile.FastDrop;
            float elapsed = 0f;

            // Phase 2: High velocity ballistic drop and brake
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float progress = curve.Evaluate(t);

                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.LerpUnclamped(startLogoPos, overshootPos, progress);
                    m_Logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-4f, 2f, progress));
                }

                // Progressive mask wipe reveal of lettering
                if (m_LogoMask != null && elapsed < m_MaskWipeDuration)
                {
                    float maskT = Mathf.Clamp01(elapsed / m_MaskWipeDuration);
                    float easeMask = UIEasing.Evaluate(EasingType.EaseOutQuad, maskT);
                    m_LogoMask.padding = new Vector4(0f, 0f, 0f, Mathf.Lerp(320f, 0f, easeMask));
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
            if (m_LogoMask != null)
            {
                m_LogoMask.padding = Vector4.zero;
            }

            // Heavy Hero micro-shake impact!
            UIMicroShake.Shake(0.85f, 0.08f);

            // Phase 3: Impact Squash & Stretch (Scale X compresses, Scale Y stretches on downward hit)
            Vector3 squishScale = new Vector3(m_LogoRestScale.x * 0.94f, m_LogoRestScale.y * 1.06f, 1f);
            elapsed = 0f;
            float squishDur = 0.05f;
            while (elapsed < squishDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / squishDur);
                if (m_Logo != null) m_Logo.localScale = Vector3.Lerp(m_LogoRestScale, squishScale, t);
                yield return null;
            }

            // Phase 4: Rebound up to +6px
            Vector3 stretchScale = new Vector3(m_LogoRestScale.x * 1.02f, m_LogoRestScale.y * 0.97f, 1f);
            elapsed = 0f;
            float reboundDur = 0.05f;
            while (elapsed < reboundDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / reboundDur);
                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                    m_Logo.localScale = Vector3.Lerp(squishScale, stretchScale, t);
                    m_Logo.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(2f, 0f, t));
                }
                yield return null;
            }

            // Phase 5: Final settle to rest position and scale
            elapsed = 0f;
            float settleDur = 0.05f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                if (m_Logo != null)
                {
                    m_Logo.anchoredPosition = Vector2.Lerp(reboundPos, m_LogoRestPos, t);
                    m_Logo.localScale = Vector3.Lerp(stretchScale, m_LogoRestScale, t);
                }
                yield return null;
            }

            if (m_Logo != null)
            {
                m_Logo.anchoredPosition = m_LogoRestPos;
                m_Logo.localScale = m_LogoRestScale;
                m_Logo.localEulerAngles = m_LogoRestAngles;
            }

            onDone?.Invoke();
        }

        private IEnumerator ButtonBallisticEntranceRoutine(
            RectTransform button,
            RectTransform accent,
            Vector2 startPos, Vector2 targetPos,
            UIElementMotionConfig cfg,
            float delay,
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

            if (button == null)
            {
                onDone?.Invoke();
                yield break;
            }

            // Calculate overshoot and rebound targets
            Vector2 travelDir = (targetPos - startPos).normalized;
            Vector2 overshootPos = targetPos + (travelDir * cfg.OvershootDistance);
            Vector2 reboundPos = targetPos - (travelDir * (cfg.OvershootDistance * cfg.ReboundAmount));

            AnimationCurve curve = cfg.MotionCurve != null ? cfg.MotionCurve : UIMotionProfile.FastDrop;
            float travelDuration = cfg.Duration;
            float elapsed = 0f;

            // Phase 1: High Velocity Flight -> Hard Deceleration -> Overshoot
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float progress = curve.Evaluate(t);

                if (button != null)
                {
                    button.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, progress);
                    // Rotation straightens: cfg.RotationStart -> +2 deg
                    button.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(cfg.RotationStart, 2f, progress));
                }
                yield return null;
            }

            if (button != null)
            {
                button.anchoredPosition = overshootPos;
                button.localEulerAngles = new Vector3(0f, 0f, 2f);
            }

            // Audio & Micro-Shake Hook
            if (cfg.MicroShakeMagnitude > 0.01f)
            {
                UIMicroShake.Shake(cfg.MicroShakeMagnitude, 0.05f);
            }
            if (cfg.AudioClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(cfg.AudioClip);
            }

            // Phase 2: Impact Compression (Signboard squashes on landing)
            if (cfg.ImpactEnabled)
            {
                elapsed = 0f;
                float compressDur = cfg.ImpactDuration;
                Vector3 squishScale = new Vector3(cfg.ImpactScale.x, cfg.ImpactScale.y, 1f);

                while (elapsed < compressDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / compressDur);
                    if (button != null)
                    {
                        button.localScale = Vector3.Lerp(Vector3.one, squishScale, t);
                    }
                    yield return null;
                }

                // Phase 3: Rebound
                elapsed = 0f;
                float reboundDur = cfg.SettleDuration * 0.5f;
                Vector3 reboundScale = new Vector3(0.98f, 1.02f, 1f);

                while (elapsed < reboundDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / reboundDur);
                    if (button != null)
                    {
                        button.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                        button.localScale = Vector3.Lerp(squishScale, reboundScale, t);
                        button.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(2f, 0f, t));
                    }
                    yield return null;
                }

                // Phase 4: Final Settle
                elapsed = 0f;
                float settleDur = cfg.SettleDuration * 0.5f;

                while (elapsed < settleDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / settleDur);
                    if (button != null)
                    {
                        button.anchoredPosition = Vector2.Lerp(reboundPos, targetPos, t);
                        button.localScale = Vector3.Lerp(reboundScale, Vector3.one, t);
                    }
                    yield return null;
                }
            }

            if (button != null)
            {
                button.anchoredPosition = targetPos;
                button.localScale = Vector3.one;
                button.localEulerAngles = Vector3.zero;
            }

            // Phase 5: Secondary Accent / Text Reveal (0.04s after signboard lands)
            TrackChildCoroutine(ButtonSecondaryAccentRevealRoutine(button, accent));

            onDone?.Invoke();
        }

        private IEnumerator ButtonSecondaryAccentRevealRoutine(RectTransform button, RectTransform accent)
        {
            // Delay 0.04s after physical signboard impact
            float delay = 0.04f;
            float timer = 0f;
            while (timer < delay)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (button == null) yield break;

            // Punch pulse on button image / visual
            float pulseDuration = 0.08f;
            float elapsed = 0f;
            Vector3 peakScale = new Vector3(1.05f, 1.05f, 1f);

            while (elapsed < pulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);
                float sinPop = Mathf.Sin(t * Mathf.PI);
                if (button != null)
                {
                    button.localScale = Vector3.Lerp(Vector3.one, peakScale, sinPop);
                }
                yield return null;
            }

            if (button != null) button.localScale = Vector3.one;

            // If this button has an active selection accent (e.g. pointer diamond)
            if (accent != null && accent.gameObject.activeInHierarchy)
            {
                elapsed = 0f;
                float accentDur = 0.10f;
                while (elapsed < accentDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / accentDur);
                    float pop = Mathf.Sin(t * Mathf.PI);
                    if (accent != null)
                    {
                        accent.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.20f, 1.20f, 1f), pop);
                    }
                    yield return null;
                }
                if (accent != null) accent.localScale = Vector3.one;
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.22f; // Fast, punchy exit

            if (m_LogoIdleRoutine != null)
            {
                StopCoroutine(m_LogoIdleRoutine);
                m_LogoIdleRoutine = null;
            }

            if (m_ParallaxRoutine != null)
            {
                StopCoroutine(m_ParallaxRoutine);
                m_ParallaxRoutine = null;
            }

            // Directionally distinct button exits
            if (m_ButtonRects != null)
            {
                for (int i = 0; i < m_ButtonRects.Length; i++)
                {
                    RectTransform btnRect = m_ButtonRects[i];
                    if (btnRect == null || !btnRect.gameObject.activeInHierarchy) continue;

                    Vector2 exitTarget;
                    float rotZ;

                    switch (i)
                    {
                        case 0: // Continue: Flies RIGHT
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x + 900f, m_ButtonRestPositions[i].y);
                            rotZ = 6f;
                            break;
                        case 1: // New Game: Flies UP
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x, m_ButtonRestPositions[i].y + 800f);
                            rotZ = 4f;
                            break;
                        case 2: // Collect: Flies RIGHT
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x + 950f, m_ButtonRestPositions[i].y + 30f);
                            rotZ = -4f;
                            break;
                        case 3: // Options: Drops DOWN
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x, m_ButtonRestPositions[i].y - 800f);
                            rotZ = -5f;
                            break;
                        case 4: // Credits: Flies DOWN-RIGHT
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x + 750f, m_ButtonRestPositions[i].y - 500f);
                            rotZ = -6f;
                            break;
                        case 5: // Exit: Drops DOWN
                        default:
                            exitTarget = new Vector2(m_ButtonRestPositions[i].x + 200f, m_ButtonRestPositions[i].y - 850f);
                            rotZ = 6f;
                            break;
                    }

                    float stagger = i * 0.02f;
                    TrackChildCoroutine(AnimateMotion(
                        btnRect,
                        btnRect.anchoredPosition, exitTarget,
                        0f, rotZ,
                        duration, stagger,
                        EasingType.EaseInBack, 1.2f
                    ));
                }
            }

            // Hero Logo flings UP (+750px)
            if (m_Logo != null)
            {
                Vector2 exitLogo = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + 750f);
                TrackChildCoroutine(AnimateMotion(
                    m_Logo,
                    m_Logo.anchoredPosition, exitLogo,
                    0f, -4f,
                    duration, 0.02f,
                    EasingType.EaseInBack, 1.15f
                ));
            }

            // Character artwork slides LEFT (-750px)
            if (m_CharacterArtwork != null)
            {
                Vector2 exitChar = new Vector2(m_CharacterRestPos.x - 750f, m_CharacterRestPos.y);
                TrackChildCoroutine(AnimateMotion(
                    m_CharacterArtwork,
                    m_CharacterArtwork.anchoredPosition, exitChar,
                    0f, 0f,
                    duration, 0f,
                    EasingType.EaseInCubic, 1f
                ));
            }

            float timer = 0f;
            while (timer < duration + 0.06f)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator LogoIdleRoutine()
        {
            while (m_Logo != null)
            {
                float bobY = Mathf.Sin(Time.unscaledTime * 1.6f) * 2.5f;
                float tilt = Mathf.Sin(Time.unscaledTime * 0.8f) * 0.4f;
                m_Logo.anchoredPosition = new Vector2(m_LogoRestPos.x, m_LogoRestPos.y + bobY);
                m_Logo.localEulerAngles = new Vector3(0f, 0f, m_LogoRestAngles.z + tilt);
                yield return null;
            }
        }

        private IEnumerator BackgroundParallaxRoutine()
        {
            while (m_Background != null)
            {
                float driftX = Mathf.Sin(Time.unscaledTime * 0.4f) * 4f;
                float driftY = Mathf.Cos(Time.unscaledTime * 0.3f) * 3f;
                m_Background.anchoredPosition = new Vector2(m_Bg1RestPos.x + driftX, m_Bg1RestPos.y + driftY);
                yield return null;
            }
        }

        private void TrackChildCoroutine(IEnumerator routine)
        {
            Coroutine c = StartCoroutine(routine);
            m_ChildCoroutines.Add(c);
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

        private IEnumerator AnimateScale(RectTransform target, Vector3 startScale, Vector3 targetScale, float duration, EasingType easing)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t);

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
        }
    }
}
