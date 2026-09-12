using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.RoboticEffects;
using MainGame.UI.CinematicEffects;
using MainGame.UI.CinematicEffects.BeatSync;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Master cinematic animator for the Main Menu screen.
    /// Drives the "Transforming Machine Interface" where UI components physically assemble and reconfigure:
    /// - 1. MACHINE WAKE: Background establishes breathing machine foundation.
    /// - 2. RETRY LOGO ASSEMBLY: Dual plates travel along horizontal guide rails, rotate to seam, interlock (CLACK!), and lock.
    /// - 3. LIVING LAB BOOT: Byte robot elevator platform rises with cyan pulse; Dr. Glitch settles.
    /// - 4. MOUNTING ARMS UNLOCK: Internal latches release.
    /// - 5. SIGNBOARDS DEPLOY:
    ///      * CONTINUE: Rotating arm swinging around corner mount, sliding outward, locking.
    ///      * NEW GAME: Linear sliding panel extending on chassis rails, hard brake snap.
    ///      * COLLECT: Fold-out panel rotating around hinge, expanding, spring latch lock.
    ///      * OPTIONS: Corner-pivot swing dropping down like an access hatch, snap lock.
    ///      * CREDITS: Vertical pneumatic extension on dual guide rods, notch lock.
    ///      * EXIT: Heaviest mass ceiling plunge on reinforced rails, massive kinetic stop, squash, micro-shake, orange sparks.
    /// - 6. FINAL SYSTEM LOCK: All components settle into a rigid machine. Input unlocked.
    /// - 7. RECONFIGURATION EXIT: Selected button locks -> Other signboards unlatch -> Arms retract -> Signboards fold -> Logo splits -> Chassis collapses.
    /// </summary>
    [DisallowMultipleComponent]
    public class HomeScreenAnimator : MonoBehaviour
    {
        [Header("Background Elements")]
        [SerializeField] private RectTransform m_Background;
        [SerializeField] private RectTransform m_BackgroundLayer2;
        [SerializeField] private float m_BgStartScale = 1.03f;
        [SerializeField] private float m_BgZoomDuration = 0.90f;

        [Header("Character & Robot (Living Lab Machine Core)")]
        [Tooltip("Dr. Glitch character artwork transform.")]
        [SerializeField] private RectTransform m_CharacterArtwork;
        [Tooltip("Byte robot artwork transform.")]
        [SerializeField] private RectTransform m_RobotArtwork;

        [Header("RETRY Logo Assembly")]
        [Tooltip("Container holding the logo visual (Holder Tittle).")]
        [SerializeField] private RectTransform m_Logo;
        [SerializeField] private MechanicalLogoAssembly m_LogoAssembly;

        [Header("Buttons (In Layout Order)")]
        [SerializeField] private Button m_ContinueButton;
        [SerializeField] private Button m_NewGameButton;
        [SerializeField] private Button m_CollectButton;
        [SerializeField] private Button m_OptionsButton;
        [SerializeField] private Button m_CreditsButton;
        [SerializeField] private Button m_ExitButton;

        private MainMenuButtonEnergyAnimator[] m_ButtonAnimators;

        // Baseline Rest Transforms
        private Vector2 m_CharRestPos;
        private Vector2 m_RobotRestPos;
        private Vector2 m_Bg1RestPos;
        private Vector2 m_Bg2RestPos;
        private Vector3 m_BgRestScale = Vector3.one;
        private Vector3 m_Bg2RestScale = Vector3.one;
        private RectTransform m_ScreenPanelRoot;
        private Vector3 m_PanelRootRestScale = Vector3.one;

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

            // Resolve background
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

            // Resolve character & robot
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

            // Resolve logo & assembly
            if (m_Logo == null)
            {
                Transform logo = transform.Find("Holder Tittle") ?? transform.Find("Title/Holder Tittle");
                if (logo != null) m_Logo = logo as RectTransform;
            }
            if (m_LogoAssembly == null && m_Logo != null)
            {
                m_LogoAssembly = m_Logo.GetComponent<MechanicalLogoAssembly>();
                if (m_LogoAssembly == null)
                {
                    m_LogoAssembly = m_Logo.gameObject.AddComponent<MechanicalLogoAssembly>();
                }
            }

            // Resolve buttons
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

            Button[] buttons = new Button[]
            {
                m_ContinueButton,
                m_NewGameButton,
                m_CollectButton,
                m_OptionsButton,
                m_CreditsButton,
                m_ExitButton
            };

            UISfxType[] impactSfxTypes = new UISfxType[]
            {
                UISfxType.ContinueImpact,
                UISfxType.NewGameImpact,
                UISfxType.CollectImpact,
                UISfxType.OptionsImpact,
                UISfxType.CreditsImpact,
                UISfxType.ExitImpact
            };

            m_ButtonAnimators = new MainMenuButtonEnergyAnimator[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    MainMenuButtonEnergyAnimator anim = buttons[i].GetComponent<MainMenuButtonEnergyAnimator>();
                    if (anim == null)
                    {
                        anim = buttons[i].gameObject.AddComponent<MainMenuButtonEnergyAnimator>();
                    }
                    anim.EntranceImpactSfx = impactSfxTypes[i];
                    anim.IsExitButton = (i == 5); // Exit is the destructive action
                    anim.ConfirmSfx = (i == 5) ? UISfxType.ExitImpact : UISfxType.Confirm;

                    // Neutralize conflicting legacy scripts on these buttons
                    UIAnimatedButton legacyAnim = buttons[i].GetComponent<UIAnimatedButton>();
                    if (legacyAnim != null) legacyAnim.enabled = false;

                    UIButtonEffect legacyEffect = buttons[i].GetComponent<UIButtonEffect>();
                    if (legacyEffect != null) legacyEffect.enabled = false;

                    MechanicalTransformElement legacyMech = buttons[i].GetComponent<MechanicalTransformElement>();
                    if (legacyMech != null) legacyMech.enabled = false;

                    anim.CaptureRestState();
                    m_ButtonAnimators[i] = anim;
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

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            CaptureRestState();

            if (CinematicUIFXManager.Instance != null)
            {
                CinematicUIFXManager.Instance.ResetFX();
            }

            if (m_ScreenPanelRoot != null)
            {
                m_ScreenPanelRoot.localScale = m_PanelRootRestScale;
            }

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
                m_CharacterArtwork.anchoredPosition = m_CharRestPos;
                m_CharacterArtwork.localScale = Vector3.one;
            }
            if (m_RobotArtwork != null)
            {
                m_RobotArtwork.anchoredPosition = m_RobotRestPos;
                m_RobotArtwork.localScale = Vector3.one;
                CanvasGroup rCg = m_RobotArtwork.GetComponent<CanvasGroup>();
                if (rCg != null) rCg.alpha = 1.0f;
            }

            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.ResetToRestState();
            }

            if (m_ButtonAnimators != null)
            {
                for (int i = 0; i < m_ButtonAnimators.Length; i++)
                {
                    if (m_ButtonAnimators[i] != null)
                    {
                        m_ButtonAnimators[i].ResetToRestState();
                    }
                }
            }
        }

        /// <summary>
        /// Instantly places all machine components into their pre-transformation retracted states.
        /// Call synchronously at frame 0 before the screen is visible.
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
                m_RobotArtwork.anchoredPosition = m_RobotRestPos + new Vector2(0f, -8f);
                m_RobotArtwork.localScale = new Vector3(0.95f, 0.95f, 1f);
                CanvasGroup rCg = m_RobotArtwork.GetComponent<CanvasGroup>();
                if (rCg != null) rCg.alpha = 0.65f;
            }

            // 3. RETRY Logo dual-plate separated rail state
            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.PrepareRetractedState();
            }

            // 4. Buttons in their fast snap dormant states
            if (m_ButtonAnimators != null)
            {
                for (int i = 0; i < m_ButtonAnimators.Length; i++)
                {
                    if (m_ButtonAnimators[i] != null)
                    {
                        m_ButtonAnimators[i].PrepareDormantState();
                    }
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(MasterMachineAssemblySequence(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            PlayExitWithSelectedButton(-1, onComplete);
        }

        /// <summary>
        /// Screen Reconfiguration Exit:
        /// Selected button locks -> Other signboards unlatch -> Mechanical arms retract ->
        /// Signboards fold into housings -> RETRY logo splits and retracts -> Main chassis collapses.
        /// </summary>
        public void PlayExitWithSelectedButton(int selectedButtonIndex, Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(MasterMachineReconfigurationSequence(selectedButtonIndex, onComplete));
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

            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.KillMotion();
            }

            if (m_ButtonAnimators != null)
            {
                for (int i = 0; i < m_ButtonAnimators.Length; i++)
                {
                    if (m_ButtonAnimators[i] != null)
                    {
                        m_ButtonAnimators[i].KillMotion();
                    }
                }
            }
        }

        private void TrackCoroutine(Coroutine c)
        {
            if (c != null) m_ChildCoroutines.Add(c);
        }

        private IEnumerator WaitForTrackTime(float targetTime, float maxWait = 1.0f)
        {
            if (MusicBeatManager.Instance != null && MusicBeatManager.Instance.GetAudioSource() != null && MusicBeatManager.Instance.GetAudioSource().isPlaying)
            {
                float startTime = Time.unscaledTime;
                while (Time.unscaledTime - startTime < maxWait)
                {
                    float cur = MusicBeatManager.Instance.CurrentTrackTime;
                    if (cur >= targetTime) yield break;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(Mathf.Min(maxWait, 0.25f));
            }
        }

        private IEnumerator WaitForNextMusicalBeat(float leadTime = 0f)
        {
            float wait = 0.52955f;
            if (MusicBeatManager.Instance != null && MusicBeatManager.Instance.GetAudioSource() != null && MusicBeatManager.Instance.GetAudioSource().isPlaying)
            {
                wait = MusicBeatManager.Instance.TimeToNextBeat - leadTime;
                if (wait < 0.05f) wait += MusicBeatManager.Instance.BeatDuration;
            }
            yield return new WaitForSecondsRealtime(Mathf.Clamp(wait, 0.05f, 0.60f));
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MASTER MACHINE ASSEMBLY SEQUENCE (ENTRANCE — MUSIC BEAT SYNCHRONIZED)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator MasterMachineAssemblySequence(Action onComplete)
        {
            // ─── PHASE 1: DORMANT DARK STATE ───────────────────────────────────
            // Background, logo, and signboards prepared in dormant coordinates
            float startTrackTime = MusicBeatManager.Instance != null ? MusicBeatManager.Instance.CurrentTrackTime : 0f;
            bool isFromIntro = startTrackTime < 2.0f;

            // ─── PHASE 2: TINY ELECTRICAL ACTIVITY (Bar 0, Beat 1 / Pickup - 0.136s) ─────
            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.70f, 0.02f);
            if (CinematicUIParticleSystem.Instance != null && m_ScreenPanelRoot != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ScreenPanelRoot, new Color(0.35f, 0.85f, 1f, 0.9f), 4, 28f);
            }

            // ─── PHASE 3: CORE RADIAL ENERGY PULSE FROM RETRY CORE (Bar 0, Beat 3 - 1.195s) ───
            if (isFromIntro)
            {
                yield return WaitForTrackTime(1.195f, 1.20f);
            }
            else
            {
                yield return WaitForNextMusicalBeat();
            }

            if (m_Background != null)
            {
                TrackCoroutine(StartCoroutine(AnimateScale(m_Background, m_BgRestScale * m_BgStartScale, m_BgRestScale, m_BgZoomDuration, EasingType.EaseOutQuad)));
                CinematicUIEffect bgEffect = m_Background.GetComponent<CinematicUIEffect>() ?? m_Background.gameObject.AddComponent<CinematicUIEffect>();
                if (bgEffect != null)
                {
                    // Home Screen Signature: Core radial pulse expanding outward from RETRY logo center
                    bgEffect.TriggerRadialPulse(0.48f, new Color(0.35f, 0.85f, 1.0f, 0.85f), new Vector2(0.5f, 0.72f), 1.8f, 2.8f, 0.04f);
                    bgEffect.TriggerBorderPulse(0.35f, new Color(0.35f, 0.85f, 1.0f, 0.9f), 1.8f, UIBorderDirection.PerimeterClockwise);
                }
            }

            // ─── PHASE 4: RETRY RECEIVES ENERGY & IMPACT SLAM (Bar 1, Beat 1 - 2.254s Downbeat) ───
            // DeploySequence takes ~0.18s of anticipation sweep before impact flash & slam
            if (isFromIntro)
            {
                yield return WaitForTrackTime(2.074f, 0.90f); // 2.254s - 0.18s = 2.074s
            }
            else
            {
                yield return WaitForNextMusicalBeat(0.18f);
            }

            bool logoDone = false;
            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.DeploySequence(
                    onComplete: () => logoDone = true,
                    onImpact: () =>
                    {
                        if (CinematicUIParticleSystem.Instance != null && m_Logo != null)
                        {
                            CinematicUIParticleSystem.Instance.SpawnSparkBurst(m_Logo.position, new Color(1.0f, 0.92f, 0.35f, 1f), 10, 32f);
                        }
                        if (m_Background != null)
                        {
                            CinematicUIEffect bgEffect = m_Background.GetComponent<CinematicUIEffect>();
                            if (bgEffect != null)
                            {
                                bgEffect.TriggerRadialPulse(0.35f, new Color(1.0f, 0.92f, 0.35f, 0.6f), new Vector2(0.5f, 0.72f), 1.2f, 2.0f, 0.03f);
                            }
                        }
                    });
            }
            else
            {
                logoDone = true;
            }

            // ─── PHASE 5: BUTTON POWER CASCADE (FAST SNAP + IMPACT + SETTLE) ──────
            // Rapid stagger of 0.05s between buttons so the entire menu activates in ~0.35s
            float staggerDelay = 0.05f;
            int buttonsPending = 0;

            if (m_ButtonAnimators != null)
            {
                for (int i = 0; i < m_ButtonAnimators.Length; i++)
                {
                    if (GetButtonValid(i))
                    {
                        buttonsPending++;
                        int idx = i;
                        m_ButtonAnimators[idx].PlaySnapEntrance(idx * staggerDelay, () => buttonsPending--);
                    }
                }
            }

            // Also boot character & robot living lab core
            if (m_CharacterArtwork != null)
            {
                TrackCoroutine(StartCoroutine(CharacterLifeEntranceRoutine()));
            }
            if (m_RobotArtwork != null)
            {
                TrackCoroutine(StartCoroutine(RobotSystemBootRoutine()));
            }

            // Wait for logo and all buttons to complete their snap & impact settle
            while (!logoDone || buttonsPending > 0)
            {
                yield return null;
            }

            ResetToRestState();

            // Begin subtle ambient living breathing loop
            m_AmbientRoutine = StartCoroutine(AmbientLivingLoop());

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MASTER MACHINE RECONFIGURATION SEQUENCE (EXIT TO NEXT SCREEN)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator MasterMachineReconfigurationSequence(int selectedIndex, Action onComplete)
        {
            // ─── STEP 1: SELECTED BUTTON CONFIRM PUNCH ──────────────────────────
            if (selectedIndex >= 0 && m_ButtonAnimators != null && selectedIndex < m_ButtonAnimators.Length && m_ButtonAnimators[selectedIndex] != null)
            {
                bool lockDone = false;
                m_ButtonAnimators[selectedIndex].PlayConfirmPunch(() => lockDone = true);
                while (!lockDone)
                {
                    yield return null;
                }
            }

            // ─── STEP 2: OTHER BUTTONS POWER DOWN / RETRACT ─────────────────────
            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.85f, 0.02f);

            int pendingRetracts = 0;
            if (m_ButtonAnimators != null)
            {
                for (int i = 0; i < m_ButtonAnimators.Length; i++)
                {
                    if (i != selectedIndex && m_ButtonAnimators[i] != null && m_ButtonAnimators[i].gameObject.activeInHierarchy)
                    {
                        pendingRetracts++;
                        int idx = i;
                        m_ButtonAnimators[idx].PlayRetract(() => pendingRetracts--);
                    }
                }
            }

            yield return new WaitForSecondsRealtime(0.04f);

            // ─── STEP 3: SELECTED BUTTON RETRACTS ───────────────────────────────
            if (selectedIndex >= 0 && m_ButtonAnimators != null && selectedIndex < m_ButtonAnimators.Length && m_ButtonAnimators[selectedIndex] != null && m_ButtonAnimators[selectedIndex].gameObject.activeInHierarchy)
            {
                pendingRetracts++;
                m_ButtonAnimators[selectedIndex].PlayRetract(() => pendingRetracts--);
            }

            // ─── STEP 4: RETRY LOGO SHADER PIXEL DISSOLVE & SCATTERING SHARDS ───
            bool logoBreakdownDone = false;
            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.BreakdownSequence(() => logoBreakdownDone = true);
            }
            else
            {
                logoBreakdownDone = true;
            }

            // ─── STEP 5: SCREEN EDGE ENERGY WAVE & DIGITAL GLITCH DISTORTION ────
            UIFeedbackAudio.PlaySfx(UISfxType.MajorTransitionImpact, 0.85f, 0.02f);
            if (UIFeedbackAudio.Instance != null)
            {
                UIFeedbackAudio.Instance.DuckMusicBriefly(0.88f, 0.25f);
            }

            if (CinematicUIParticleSystem.Instance != null && m_ScreenPanelRoot != null)
            {
                CinematicUIParticleSystem.Instance.SpawnScreenEdgeWave(m_ScreenPanelRoot, new Color(0.35f, 0.85f, 1f, 1f), 0.30f);
            }

            if (m_ScreenPanelRoot != null)
            {
                CinematicUIEffect rootEffect = m_ScreenPanelRoot.GetComponent<CinematicUIEffect>();
                if (rootEffect != null)
                {
                    rootEffect.TriggerDigitalGlitch(0.12f, 1.0f);
                }
                TrackCoroutine(StartCoroutine(AnimateScale(m_ScreenPanelRoot, m_PanelRootRestScale, new Vector3(0.96f, 0.96f, 1f), 0.20f, EasingType.EaseInQuad)));
            }

            if (m_Background != null)
            {
                CinematicUIEffect bgEffect = m_Background.GetComponent<CinematicUIEffect>();
                if (bgEffect != null)
                {
                    bgEffect.TriggerRadialPulse(0.24f, new Color(0.35f, 0.85f, 1.0f, 0.6f), new Vector2(0.5f, 0.72f), 0.3f, 2.0f);
                }
            }

            while (pendingRetracts > 0 || !logoBreakdownDone)
            {
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private bool GetButtonValid(int index)
        {
            return m_ButtonAnimators != null && index < m_ButtonAnimators.Length &&
                   m_ButtonAnimators[index] != null && m_ButtonAnimators[index].gameObject.activeInHierarchy;
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // LIVING LAB MACHINE CORE (ROBOT & CHARACTER)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator RobotSystemBootRoutine()
        {
            if (m_RobotArtwork == null) yield break;

            Vector2 startPos = m_RobotRestPos + new Vector2(0f, -8f);
            m_RobotArtwork.anchoredPosition = startPos;
            m_RobotArtwork.localScale = new Vector3(0.95f, 0.95f, 1f);

            CanvasGroup cg = m_RobotArtwork.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0.65f;

            float liftDur = 0.26f;
            float elapsed = 0f;
            while (elapsed < liftDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / liftDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.3f);

                if (m_RobotArtwork != null)
                {
                    m_RobotArtwork.anchoredPosition = Vector2.Lerp(startPos, m_RobotRestPos, ease);
                    m_RobotArtwork.localScale = Vector3.Lerp(new Vector3(0.95f, 0.95f, 1f), Vector3.one, t);
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

            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.75f, 0.02f);
            RoboticPixelFXPool pool = FindAnyObjectByType<RoboticPixelFXPool>();
            if (pool != null && m_RobotArtwork != null)
            {
                pool.SpawnSparkBurst(new Vector2(0f, 40f), m_RobotArtwork, new Color(0.35f, 0.95f, 0.70f, 1f), 4, 14f);
            }
        }

        private IEnumerator CharacterLifeEntranceRoutine()
        {
            if (m_CharacterArtwork == null) yield break;
            Vector2 basePos = m_CharRestPos;

            float dur = 0.36f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float offset = Mathf.Sin(t * Mathf.PI) * 1.5f;
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
            int lastPulseBar = -1;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                timer += dt;

                // Sync character, robot, and background subtle breathing to music BPM if available
                float tempoSpeed = 1.88f; // ~113.3 BPM in rad/sec (113.3/60 * PI)
                if (MusicBeatManager.Instance != null)
                {
                    tempoSpeed = (MusicBeatManager.Instance.Bpm / 60f) * Mathf.PI;
                }

                if (m_CharacterArtwork != null)
                {
                    float charBob = Mathf.Sin(timer * (tempoSpeed * 0.5f)) * 0.8f;
                    m_CharacterArtwork.anchoredPosition = m_CharRestPos + new Vector2(0f, charBob);
                }

                if (m_RobotArtwork != null)
                {
                    float botBob = Mathf.Sin(timer * (tempoSpeed * 0.5f) + 0.8f) * 0.5f;
                    m_RobotArtwork.anchoredPosition = m_RobotRestPos + new Vector2(0f, botBob);
                }

                if (m_Background != null)
                {
                    float bgParallax = Mathf.Sin(timer * (tempoSpeed * 0.25f)) * 1.2f;
                    m_Background.anchoredPosition = m_Bg1RestPos + new Vector2(bgParallax, 0f);
                }

                // Downbeat reaction: every 2 or 4 bars, trigger a very subtle rhythmic micro-pulse
                int currentBar = -1;
                if (MusicBeatManager.Instance != null)
                {
                    currentBar = MusicBeatManager.Instance.CurrentBeat.BarIndex;
                }

                if (currentBar >= 0 && currentBar != lastPulseBar && (currentBar % 2 == 0))
                {
                    lastPulseBar = currentBar;

                    // Byte robot subtle core pulse on alternate bars
                    if (m_RobotArtwork != null && (currentBar % 4 == 0))
                    {
                        CinematicUIEffect botFx = m_RobotArtwork.GetComponent<CinematicUIEffect>();
                        if (botFx != null)
                        {
                            botFx.TriggerBorderPulse(0.18f, new Color(0.2f, 0.85f, 1f, 0.6f), 0.8f);
                        }
                    }

                    int target = UnityEngine.Random.Range(0, 7); // 0-5 = buttons, 6 = logo
                    if (target < 6 && m_ButtonAnimators != null && target < m_ButtonAnimators.Length && m_ButtonAnimators[target] != null && m_ButtonAnimators[target].gameObject.activeInHierarchy)
                    {
                        m_ButtonAnimators[target].CinematicUI?.TriggerBorderPulse(0.20f, new Color(0.35f, 0.85f, 1f, 0.50f), 0.8f);
                        if (CinematicUIParticleSystem.Instance != null && UnityEngine.Random.value > 0.7f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ButtonAnimators[target].RectComponent, new Color(0.35f, 0.85f, 1f, 0.65f), 2, 10f);
                        }
                    }
                    else if (m_LogoAssembly != null && m_LogoAssembly.gameObject.activeInHierarchy)
                    {
                        m_LogoAssembly.CinematicUI?.TriggerBorderPulse(0.22f, new Color(1.0f, 0.92f, 0.35f, 0.50f), 0.8f);
                    }
                }

                yield return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // UTILITIES
        // ═════════════════════════════════════════════════════════════════════════════

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
