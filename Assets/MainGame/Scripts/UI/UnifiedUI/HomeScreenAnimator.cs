using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.RoboticEffects;
using MainGame.UI.CinematicEffects;

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

        private MechanicalTransformElement[] m_ButtonElements;

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

            MechanicalMechanismType[] mechanismTypes = new MechanicalMechanismType[]
            {
                MechanicalMechanismType.RotatingArm,          // CONTINUE
                MechanicalMechanismType.SlidingChassisPanel,   // NEW GAME
                MechanicalMechanismType.HingedFoldOut,        // COLLECT
                MechanicalMechanismType.CornerPivotSwing,     // OPTIONS
                MechanicalMechanismType.PneumaticExtension,   // CREDITS
                MechanicalMechanismType.HeavyDualRailPlunge   // EXIT
            };

            MechanicalWeight[] weights = new MechanicalWeight[]
            {
                MechanicalWeight.Medium,
                MechanicalWeight.Medium,
                MechanicalWeight.Medium,
                MechanicalWeight.Medium,
                MechanicalWeight.Light,
                MechanicalWeight.VeryHeavy
            };

            m_ButtonElements = new MechanicalTransformElement[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    MechanicalTransformElement elem = buttons[i].GetComponent<MechanicalTransformElement>();
                    if (elem == null)
                    {
                        elem = buttons[i].gameObject.AddComponent<MechanicalTransformElement>();
                    }
                    elem.Mechanism = mechanismTypes[i];
                    elem.Weight = weights[i];
                    elem.CaptureRestState();
                    m_ButtonElements[i] = elem;
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

            if (m_ButtonElements != null)
            {
                for (int i = 0; i < m_ButtonElements.Length; i++)
                {
                    if (m_ButtonElements[i] != null)
                    {
                        m_ButtonElements[i].ResetToRestState();
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

            // 4. Buttons in their individual mechanical retracted states
            if (m_ButtonElements != null)
            {
                for (int i = 0; i < m_ButtonElements.Length; i++)
                {
                    if (m_ButtonElements[i] != null)
                    {
                        m_ButtonElements[i].PrepareRetractedState();
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

            if (m_ButtonElements != null)
            {
                for (int i = 0; i < m_ButtonElements.Length; i++)
                {
                    if (m_ButtonElements[i] != null)
                    {
                        m_ButtonElements[i].KillMotion();
                    }
                }
            }
        }

        private void TrackCoroutine(Coroutine c)
        {
            if (c != null) m_ChildCoroutines.Add(c);
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MASTER MACHINE ASSEMBLY SEQUENCE (ENTRANCE)
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator MasterMachineAssemblySequence(Action onComplete)
        {
            // ─── PHASE 1: DORMANT DARK STATE ───────────────────────────────────
            // Background, logo, and signboards prepared in dormant coordinates

            // ─── PHASE 2: TINY ELECTRICAL ACTIVITY ─────────────────────────────
            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.70f, 0.02f);
            if (CinematicUIParticleSystem.Instance != null && m_ScreenPanelRoot != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ScreenPanelRoot, new Color(0.35f, 0.85f, 1f, 0.9f), 4, 28f);
            }

            // ─── PHASE 3: CORE RADIAL ENERGY PULSE FROM RETRY CORE ──────────────
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

            yield return new WaitForSecondsRealtime(0.08f);

            // ─── PHASE 4, 5, 6: RETRY RECEIVES ENERGY, SHADER ACTIVATES & SPARKS
            bool logoDone = false;
            if (m_LogoAssembly != null)
            {
                m_LogoAssembly.DeploySequence(() =>
                {
                    logoDone = true;
                    if (CinematicUIParticleSystem.Instance != null && m_Logo != null)
                    {
                        CinematicUIParticleSystem.Instance.SpawnSparkBurst(m_Logo.position, new Color(1.0f, 0.92f, 0.35f, 1f), 10, 32f);
                    }
                });
            }
            else
            {
                logoDone = true;
            }

            // Let logo power pulse settle before buttons deploy
            yield return new WaitForSecondsRealtime(0.18f);

            // ─── PHASE 7-10: BUTTONS ACTIVATE SEQUENTIALLY WITH PRE-ENERGY, SPEED TRAILS & LOCK
            bool[] buttonDone = new bool[6];

            // 5.1: CONTINUE
            if (GetButtonValid(0))
            {
                m_ButtonElements[0].Deploy(() => buttonDone[0] = true);
            }
            else buttonDone[0] = true;

            yield return new WaitForSecondsRealtime(0.10f);

            // 5.2: NEW GAME
            if (GetButtonValid(1))
            {
                m_ButtonElements[1].Deploy(() => buttonDone[1] = true);
            }
            else buttonDone[1] = true;

            yield return new WaitForSecondsRealtime(0.10f);

            // 5.3: COLLECT
            if (GetButtonValid(2))
            {
                m_ButtonElements[2].Deploy(() => buttonDone[2] = true);
            }
            else buttonDone[2] = true;

            yield return new WaitForSecondsRealtime(0.08f);

            // 5.4: OPTIONS
            if (GetButtonValid(3))
            {
                m_ButtonElements[3].Deploy(() => buttonDone[3] = true);
            }
            else buttonDone[3] = true;

            yield return new WaitForSecondsRealtime(0.08f);

            // 5.5: CREDITS
            if (GetButtonValid(4))
            {
                m_ButtonElements[4].Deploy(() => buttonDone[4] = true);
            }
            else buttonDone[4] = true;

            yield return new WaitForSecondsRealtime(0.08f);

            // 5.6: EXIT
            if (GetButtonValid(5))
            {
                m_ButtonElements[5].Deploy(() => buttonDone[5] = true);
            }
            else buttonDone[5] = true;

            // ─── PHASE 11: LIVING LAB CORE ACTIVATION ────────────────────────────
            if (m_CharacterArtwork != null)
            {
                TrackCoroutine(StartCoroutine(CharacterLifeEntranceRoutine()));
            }
            if (m_RobotArtwork != null)
            {
                TrackCoroutine(StartCoroutine(RobotSystemBootRoutine()));
            }

            // Wait for all mechanisms to complete their locks
            while (!logoDone || !buttonDone[0] || !buttonDone[1] || !buttonDone[2] || !buttonDone[3] || !buttonDone[4] || !buttonDone[5])
            {
                yield return null;
            }

            // ─── PHASE 12: FINAL SYSTEM LOCK & SETTLE ────────────────────────────
            ResetToRestState();
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.65f, 0.02f);
            UIMicroShake.Shake(0.30f, 0.05f);

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
            if (selectedIndex >= 0 && selectedIndex < m_ButtonElements.Length && m_ButtonElements[selectedIndex] != null)
            {
                bool lockDone = false;
                m_ButtonElements[selectedIndex].PlaySelectionLockPunch(() => lockDone = true);
                while (!lockDone)
                {
                    yield return null;
                }
            }

            // ─── STEP 2: OTHER SIGNBOARDS RETRACT WITH SPEED TRAILS ─────────────
            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.85f, 0.02f);

            int pendingRetracts = 0;
            for (int i = 0; i < m_ButtonElements.Length; i++)
            {
                if (i != selectedIndex && m_ButtonElements[i] != null && m_ButtonElements[i].gameObject.activeInHierarchy)
                {
                    pendingRetracts++;
                    int idx = i;
                    m_ButtonElements[idx].Retract(() => pendingRetracts--);
                }
            }

            yield return new WaitForSecondsRealtime(0.06f);

            // ─── STEP 3: SELECTED BUTTON RETRACTS ───────────────────────────────
            if (selectedIndex >= 0 && selectedIndex < m_ButtonElements.Length && m_ButtonElements[selectedIndex] != null && m_ButtonElements[selectedIndex].gameObject.activeInHierarchy)
            {
                pendingRetracts++;
                m_ButtonElements[selectedIndex].Retract(() => pendingRetracts--);
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
            return m_ButtonElements != null && index < m_ButtonElements.Length &&
                   m_ButtonElements[index] != null && m_ButtonElements[index].gameObject.activeInHierarchy;
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
            float pulseInterval = UnityEngine.Random.Range(3.5f, 5.0f);
            float pulseTimer = 0f;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                timer += dt;
                pulseTimer += dt;

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
                    float bgParallax = Mathf.Sin(timer * 0.4f) * 1.2f;
                    m_Background.anchoredPosition = m_Bg1RestPos + new Vector2(bgParallax, 0f);
                }

                // Idle Living Machine Atmosphere: subtle low-frequency traveling micro-pulses
                if (pulseTimer >= pulseInterval)
                {
                    pulseTimer = 0f;
                    pulseInterval = UnityEngine.Random.Range(4.0f, 6.0f);

                    int target = UnityEngine.Random.Range(0, 7); // 0-5 = buttons, 6 = logo
                    if (target < 6 && m_ButtonElements != null && target < m_ButtonElements.Length && m_ButtonElements[target] != null && m_ButtonElements[target].gameObject.activeInHierarchy)
                    {
                        if (CinematicUIFXManager.Instance != null)
                        {
                            CinematicUIFXManager.Instance.TriggerEnergyDischarge(m_ButtonElements[target].RectTransformComponent, Vector2.zero, new Color(0.35f, 0.85f, 1f, 0.75f));
                        }
                        else
                        {
                            m_ButtonElements[target].CinematicUI?.TriggerBorderPulse(0.28f, new Color(0.35f, 0.85f, 1f, 0.65f), 1.2f);
                            if (CinematicUIParticleSystem.Instance != null && UnityEngine.Random.value > 0.6f)
                            {
                                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_ButtonElements[target].RectTransformComponent, new Color(0.35f, 0.85f, 1f, 0.75f), 2, 12f);
                            }
                        }
                    }
                    else if (m_LogoAssembly != null && m_LogoAssembly.gameObject.activeInHierarchy)
                    {
                        if (CinematicUIFXManager.Instance != null)
                        {
                            CinematicUIFXManager.Instance.TriggerEnergyStart(m_LogoAssembly, new Color(0.35f, 0.85f, 1f, 0.65f));
                        }
                        m_LogoAssembly.CinematicUI?.TriggerBorderPulse(0.32f, new Color(0.35f, 0.85f, 1f, 0.65f), 1.2f);
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
