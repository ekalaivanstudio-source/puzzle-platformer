using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.CinematicEffects.BeatSync;
using MainGame.UI.Unified;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Master director for the Main Menu living cinematic background.
    /// Orchestrates all sub-systems:
    /// - Background shader layers (BG Red, Red & Yellow transition)
    /// - Living Villain character animation (Dr. Glitch)
    /// - Physically suspended Title with dual-hand spring dynamics & energy links
    /// - Living Robot life and energy cycles (Byte)
    /// - Atmospheric sparks and ambient pixel fragments
    /// - Subtle layered UI parallax depth
    /// - Music-reactive accents synchronized with MusicBeatManager
    /// - Contextual menu button focus reactions
    /// - Clean, zero-drift lifecycle management (Play, Stop, Reset, SetIdle).
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuBackgroundDirector : MonoBehaviour
    {
        private static MainMenuBackgroundDirector s_Instance;
        public static MainMenuBackgroundDirector Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindAnyObjectByType<MainMenuBackgroundDirector>();
                }
                return s_Instance;
            }
        }

        [Header("Layer Controllers")]
        [SerializeField] private UIBackgroundLayerController m_BgRedController;
        [SerializeField] private UIBackgroundLayerController m_RedYellowController;

        [Header("Characters & Title Systems")]
        [SerializeField] private UIVillainAnimator m_VillainAnimator;
        [SerializeField] private UITitleSuspensionSystem m_TitleSuspension;
        [SerializeField] private UIRobotLifeAnimator m_RobotAnimator;

        [Header("Atmosphere & Parallax")]
        [SerializeField] private UISparkAtmosphereSystem m_SparkAtmosphere;
        [SerializeField] private UIParallaxController m_ParallaxController;

        [Header("Music Beat Synchronization")]
        [Tooltip("Enable subtle rhythmic accents on strong beats and bars.")]
        [SerializeField] private bool m_EnableMusicSync = true;

        [Tooltip("Interval in bars for machinery energy breathing (default: every 2 bars).")]
        [SerializeField] private int m_BarPulseInterval = 2;

        [Header("Button Focus Reactions")]
        [Tooltip("Enable subtle background response when menu buttons gain focus.")]
        [SerializeField] private bool m_EnableButtonReactions = true;

        [Header("Cinematic Idle Director")]
        [SerializeField] private bool m_EnableIdleDirector = true;
        [SerializeField] private float m_MinIdleEventInterval = 4.0f;
        [SerializeField] private float m_MaxIdleEventInterval = 8.5f;

        private Coroutine m_IdleDirectorRoutine;
        private bool m_IsActive = true;
        private int m_LastFiredBar = -1;

        public UIBackgroundLayerController BgRedController { get => m_BgRedController; set => m_BgRedController = value; }
        public UIBackgroundLayerController RedYellowController { get => m_RedYellowController; set => m_RedYellowController = value; }
        public UIVillainAnimator VillainAnimator { get => m_VillainAnimator; set => m_VillainAnimator = value; }
        public UITitleSuspensionSystem TitleSuspension { get => m_TitleSuspension; set => m_TitleSuspension = value; }
        public UIRobotLifeAnimator RobotAnimator { get => m_RobotAnimator; set => m_RobotAnimator = value; }
        public UISparkAtmosphereSystem SparkAtmosphere { get => m_SparkAtmosphere; set => m_SparkAtmosphere = value; }
        public UIParallaxController ParallaxController { get => m_ParallaxController; set => m_ParallaxController = value; }

        private void Awake()
        {
            s_Instance = this;
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            Play();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
            Play();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            Stop();
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
            UnsubscribeEvents();
        }

        public void ResolveReferences()
        {
            Transform root = transform;

            // Search in root, siblings, and parent hierarchy
            Transform bgRoot = root.Find("Backagrond") ?? root.Find("Background") ?? root.parent?.Find("Backagrond") ?? root.parent?.Find("Background");

            if (bgRoot != null)
            {
                if (m_BgRedController == null)
                {
                    Transform bgRed = bgRoot.Find("BG Red") ?? bgRoot.Find("BG_Red");
                    if (bgRed != null)
                    {
                        m_BgRedController = bgRed.GetComponent<UIBackgroundLayerController>() ?? bgRed.gameObject.AddComponent<UIBackgroundLayerController>();
                        m_BgRedController.Profile = BackgroundLayerProfile.BackgroundRed;
                    }
                }

                if (m_RedYellowController == null)
                {
                    Transform ryLayer = bgRoot.Find("Red and yellow layer") ?? bgRoot.Find("RedAndYellowLayer");
                    if (ryLayer != null)
                    {
                        m_RedYellowController = ryLayer.GetComponent<UIBackgroundLayerController>() ?? ryLayer.gameObject.AddComponent<UIBackgroundLayerController>();
                        m_RedYellowController.Profile = BackgroundLayerProfile.RedYellowTransition;
                    }
                }

                if (m_VillainAnimator == null)
                {
                    Transform villain = bgRoot.Find("DR") ?? bgRoot.Find("Villan") ?? bgRoot.Find("Villain") ?? bgRoot.Find("CharacterArtwork");
                    if (villain != null)
                    {
                        m_VillainAnimator = villain.GetComponent<UIVillainAnimator>() ?? villain.gameObject.AddComponent<UIVillainAnimator>();
                    }
                }

                if (m_RobotAnimator == null)
                {
                    Transform robot = bgRoot.Find("Hero") ?? bgRoot.Find("Robot") ?? bgRoot.Find("Byte");
                    if (robot != null)
                    {
                        m_RobotAnimator = robot.GetComponent<UIRobotLifeAnimator>() ?? robot.gameObject.AddComponent<UIRobotLifeAnimator>();
                    }
                }

                if (m_SparkAtmosphere == null)
                {
                    Transform spark = bgRoot.Find("Spark") ?? bgRoot.Find("FX");
                    if (spark != null)
                    {
                        m_SparkAtmosphere = spark.GetComponent<UISparkAtmosphereSystem>() ?? spark.gameObject.AddComponent<UISparkAtmosphereSystem>();
                    }
                }
            }

            // Title suspension
            if (m_TitleSuspension == null)
            {
                Transform panel = root.Find("HomeScreenPanel  New") ?? root.Find("HomeScreenPanel_New") ?? root.parent?.Find("HomeScreenPanel  New");
                Transform titleRoot = (panel != null) ? (panel.Find("Holder Tittle") ?? panel.Find("Title/Holder Tittle")) : null;
                if (titleRoot == null)
                {
                    titleRoot = root.Find("Holder Tittle") ?? root.parent?.Find("Holder Tittle");
                }

                if (titleRoot != null)
                {
                    m_TitleSuspension = titleRoot.GetComponent<UITitleSuspensionSystem>() ?? titleRoot.gameObject.AddComponent<UITitleSuspensionSystem>();
                    if (m_VillainAnimator != null)
                    {
                        m_TitleSuspension.VillainAnimator = m_VillainAnimator;
                    }
                    m_TitleSuspension.CreateEnergyLinksIfMissing();
                }
            }

            // Parallax
            if (m_ParallaxController == null)
            {
                m_ParallaxController = GetComponent<UIParallaxController>() ?? GetComponentInChildren<UIParallaxController>(true);
            }
        }

        private void SubscribeEvents()
        {
            if (m_EnableMusicSync)
            {
                MusicBeatManager.OnBeat += HandleBeat;
                MusicBeatManager.OnStrongBeat += HandleStrongBeat;
                MusicBeatManager.OnBar += HandleBar;
            }

            if (m_EnableButtonReactions)
            {
                MainMenuButtonEnergyAnimator.OnAnyButtonFocused += HandleButtonFocused;
            }
        }

        private void UnsubscribeEvents()
        {
            MusicBeatManager.OnBeat -= HandleBeat;
            MusicBeatManager.OnStrongBeat -= HandleStrongBeat;
            MusicBeatManager.OnBar -= HandleBar;
            MainMenuButtonEnergyAnimator.OnAnyButtonFocused -= HandleButtonFocused;
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // MUSIC BEAT SYNCHRONIZATION
        // ═════════════════════════════════════════════════════════════════════════════

        private void HandleBeat(BeatEvent evt)
        {
            if (!m_IsActive || !m_EnableMusicSync) return;

            // Very subtle environmental brightness ping on downbeat
            if (evt.IsDownbeat)
            {
                if (m_RedYellowController != null)
                {
                    m_RedYellowController.TriggerPulse(0.04f, 0.22f);
                }
            }
        }

        private void HandleStrongBeat(BeatEvent evt)
        {
            if (!m_IsActive || !m_EnableMusicSync) return;

            // Small energy pulse on Robot and Title attachment points
            if (m_RobotAnimator != null)
            {
                m_RobotAnimator.TriggerSubtleEnergyPulse(0.12f);
            }
            if (m_TitleSuspension != null && UnityEngine.Random.value < 0.4f)
            {
                m_TitleSuspension.TriggerAttachmentSparks();
            }
        }

        private void HandleBar(BeatEvent evt)
        {
            if (!m_IsActive || !m_EnableMusicSync) return;

            if (evt.BarIndex == m_LastFiredBar) return;
            m_LastFiredBar = evt.BarIndex;

            // Every Nth bar, trigger machinery breathing pulse
            if (evt.BarIndex % Mathf.Max(1, m_BarPulseInterval) == 0)
            {
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.08f, 0.40f);
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // BUTTON FOCUS RELATIONSHIP
        // ═════════════════════════════════════════════════════════════════════════════

        private void HandleButtonFocused(MainMenuButtonEnergyAnimator button, bool focused)
        {
            if (!m_IsActive || !m_EnableButtonReactions || !focused || button == null) return;

            string btnName = button.gameObject.name.ToLowerInvariant();

            if (btnName.Contains("continue"))
            {
                // CONTINUE: subtle cyan/blue energy response
                if (m_RedYellowController != null)
                {
                    m_RedYellowController.TriggerPulse(0.10f, 0.25f, new Color(0.35f, 0.85f, 1f, 1f));
                }
            }
            else if (btnName.Contains("new") || btnName.Contains("game"))
            {
                // NEW GAME: small title/robot synchronized pulse
                if (m_TitleSuspension != null)
                {
                    m_TitleSuspension.Impulse(new Vector2(0f, 2.5f), 0.4f);
                }
                if (m_RobotAnimator != null)
                {
                    m_RobotAnimator.TriggerSubtleEnergyPulse(0.14f);
                }
            }
            else if (btnName.Contains("collect"))
            {
                // COLLECT: robot energy ping
                if (m_RobotAnimator != null)
                {
                    m_RobotAnimator.TriggerSubtleEnergyPulse(0.20f);
                }
            }
            else if (btnName.Contains("option") || btnName.Contains("setting"))
            {
                // OPTIONS: subtle environment pulse
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.09f, 0.30f);
                }
            }
            else if (btnName.Contains("credit"))
            {
                // CREDITS: subtle ambient brightness shift
                if (m_RedYellowController != null)
                {
                    m_RedYellowController.TriggerPulse(0.08f, 0.35f, new Color(1f, 0.85f, 0.5f, 1f));
                }
            }
            else if (btnName.Contains("exit") || btnName.Contains("quit"))
            {
                // EXIT: slightly stronger red warning pulse
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.18f, 0.30f, new Color(1f, 0.2f, 0.15f, 1f));
                }
                if (m_VillainAnimator != null)
                {
                    m_VillainAnimator.TriggerVillainEnergyPulse(0.15f);
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // CINEMATIC IDLE DIRECTOR
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator IdleDirectorLoop()
        {
            while (m_IsActive)
            {
                float wait = UnityEngine.Random.Range(m_MinIdleEventInterval, m_MaxIdleEventInterval);
                yield return new WaitForSecondsRealtime(wait);

                if (!m_IsActive) yield break;

                // Pick an occasional low-frequency ambient event
                int eventType = UnityEngine.Random.Range(0, 5);
                switch (eventType)
                {
                    case 0:
                        // Villain glasses glint
                        if (m_VillainAnimator != null)
                        {
                            m_VillainAnimator.TriggerGlassesGlint();
                        }
                        break;

                    case 1:
                        // Title micro-swing impulse
                        if (m_TitleSuspension != null)
                        {
                            float swingDir = (UnityEngine.Random.value > 0.5f) ? 1.0f : -1.0f;
                            m_TitleSuspension.Impulse(new Vector2(swingDir * 1.5f, 1.2f), swingDir * 0.5f);
                        }
                        break;

                    case 2:
                        // Background machinery pulse
                        if (m_BgRedController != null)
                        {
                            m_BgRedController.TriggerPulse(0.09f, 0.35f);
                        }
                        break;

                    case 3:
                        // Red & Yellow subtle glitch block
                        if (m_RedYellowController != null)
                        {
                            m_RedYellowController.TriggerGlitch(0.12f);
                        }
                        break;

                    case 4:
                        // Robot energy charge
                        if (m_RobotAnimator != null)
                        {
                            m_RobotAnimator.TriggerSubtleEnergyPulse(0.16f);
                        }
                        break;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // LIFECYCLE MANAGEMENT
        // ═════════════════════════════════════════════════════════════════════════════

        public void Play()
        {
            m_IsActive = true;

            if (m_BgRedController != null) m_BgRedController.Play();
            if (m_RedYellowController != null) m_RedYellowController.Play();
            if (m_VillainAnimator != null) m_VillainAnimator.Play();
            if (m_TitleSuspension != null) m_TitleSuspension.Play();
            if (m_RobotAnimator != null) m_RobotAnimator.Play();
            if (m_SparkAtmosphere != null) m_SparkAtmosphere.Play();
            if (m_ParallaxController != null) m_ParallaxController.Play();

            if (m_IdleDirectorRoutine != null)
            {
                StopCoroutine(m_IdleDirectorRoutine);
            }
            if (m_EnableIdleDirector)
            {
                m_IdleDirectorRoutine = StartCoroutine(IdleDirectorLoop());
            }
        }

        public void Stop()
        {
            m_IsActive = false;

            if (m_IdleDirectorRoutine != null)
            {
                StopCoroutine(m_IdleDirectorRoutine);
                m_IdleDirectorRoutine = null;
            }

            if (m_BgRedController != null) m_BgRedController.Stop();
            if (m_RedYellowController != null) m_RedYellowController.Stop();
            if (m_VillainAnimator != null) m_VillainAnimator.Stop();
            if (m_TitleSuspension != null) m_TitleSuspension.Stop();
            if (m_RobotAnimator != null) m_RobotAnimator.Stop();
            if (m_SparkAtmosphere != null) m_SparkAtmosphere.Stop();
            if (m_ParallaxController != null) m_ParallaxController.Stop();
        }

        public void Reset()
        {
            Stop();

            if (m_BgRedController != null) m_BgRedController.Reset();
            if (m_RedYellowController != null) m_RedYellowController.Reset();
            if (m_VillainAnimator != null) m_VillainAnimator.Reset();
            if (m_TitleSuspension != null) m_TitleSuspension.Reset();
            if (m_RobotAnimator != null) m_RobotAnimator.Reset();
            if (m_SparkAtmosphere != null) m_SparkAtmosphere.Reset();
            if (m_ParallaxController != null) m_ParallaxController.Reset();
        }

        public void SetIdle()
        {
            Play();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntimeHook()
        {
            GameObject screenMgr = GameObject.Find("ScreenManager");
            if (screenMgr == null)
            {
                var canvas = GameObject.Find("Canvas  Main Menu") ?? GameObject.Find("Canvas Main Menu");
                if (canvas != null)
                {
                    Transform smTrans = canvas.transform.Find("ScreenManager");
                    if (smTrans != null) screenMgr = smTrans.gameObject;
                }
            }

            if (screenMgr != null)
            {
                MainMenuBackgroundDirector director = screenMgr.GetComponent<MainMenuBackgroundDirector>();
                if (director == null)
                {
                    director = screenMgr.AddComponent<MainMenuBackgroundDirector>();
                }
                director.ResolveReferences();
            }
        }
    }
}
