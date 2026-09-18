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
    [System.Serializable]
    public struct OutlineEventEnvelope
    {
        public float peakMultiplier;
        public float attack;
        public float hold;
        public float release;

        public OutlineEventEnvelope(float peak, float a, float h, float r)
        {
            peakMultiplier = peak;
            attack = a;
            hold = h;
            release = r;
        }
    }

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

        [Header("Character & Title Outline Controllers")]
        [SerializeField] private UIBackgroundLayerController m_VillainOutlineController;
        [SerializeField] private UIBackgroundLayerController m_HeroOutlineController;
        [SerializeField] private UIBackgroundLayerController m_TitleOutlineController;

        [Header("Atmosphere & Parallax")]
        [SerializeField] private UISparkAtmosphereSystem m_SparkAtmosphere;
        [SerializeField] private UIBackgroundLayerController m_SparkController;
        [SerializeField] private UIParallaxController m_ParallaxController;

        [Header("Music Beat Synchronization")]
        [Tooltip("Enable subtle rhythmic accents on strong beats and bars.")]
        [SerializeField] private bool m_EnableMusicSync = true;

        [Tooltip("Interval in bars for machinery energy breathing (default: every 2 bars).")]
        [SerializeField] private int m_BarPulseInterval = 2;

        [Header("Event Outline Envelopes (Zero Latency)")]
        [SerializeField] private OutlineEventEnvelope m_ButtonFocusEnvelope = new OutlineEventEnvelope(1.5f, 0.02f, 0.03f, 0.10f);
        [SerializeField] private OutlineEventEnvelope m_StrongBeatEnvelope = new OutlineEventEnvelope(1.7f, 0.03f, 0.04f, 0.14f);
        [SerializeField] private OutlineEventEnvelope m_DownbeatEnvelope = new OutlineEventEnvelope(1.9f, 0.03f, 0.05f, 0.16f);
        [SerializeField] private OutlineEventEnvelope m_BarEnvelope = new OutlineEventEnvelope(2.2f, 0.04f, 0.06f, 0.20f);
        [SerializeField] private OutlineEventEnvelope m_ConfirmEnvelope = new OutlineEventEnvelope(2.5f, 0.02f, 0.08f, 0.22f);
        [SerializeField] private float m_MusicVisualOffset = 0.0f;

        [Header("Button Focus Reactions")]
        [Tooltip("Enable subtle background response when menu buttons gain focus.")]
        [SerializeField] private bool m_EnableButtonReactions = true;

        [Header("Dynamic Theme Cycling System")]
        [Tooltip("Automatically cycles random theme palettes across characters, title, and background.")]
        [SerializeField] private bool m_EnableRandomThemeCycle = true;
        [Tooltip("Randomize all character and background themes on scene start.")]
        [SerializeField] private bool m_RandomizeOnStart = true;
        [Tooltip("Interval in seconds between random theme changes.")]
        [SerializeField] private float m_ThemeCycleInterval = 7.0f;
        [Tooltip("Smooth crossfade transition duration in seconds.")]
        [SerializeField] private float m_ThemeTransitionDuration = 1.2f;
        [Tooltip("Ensure Hero, Villain, and Title receive contrasting, aesthetically balanced theme palettes.")]
        [SerializeField] private bool m_HarmonizedPalettes = true;

        [Header("Cinematic Idle Director")]
        [SerializeField] private bool m_EnableIdleDirector = true;
        [SerializeField] private float m_MinIdleEventInterval = 4.0f;
        [SerializeField] private float m_MaxIdleEventInterval = 8.5f;

        [Header("Development Visual Debug")]
        [SerializeField] private bool m_EnableVisualDebug = false;
        private string m_LastFocusedButtonName = "None";
        private int m_CurrentHierarchyLevel = 1;

        private Coroutine m_IdleDirectorRoutine;
        private Coroutine m_ThemeCycleRoutine;
        private bool m_IsActive = true;
        private int m_LastFiredBar = -1;

        public bool EnableRandomThemeCycle { get => m_EnableRandomThemeCycle; set => m_EnableRandomThemeCycle = value; }
        public bool RandomizeOnStart { get => m_RandomizeOnStart; set => m_RandomizeOnStart = value; }
        public float ThemeCycleInterval { get => m_ThemeCycleInterval; set => m_ThemeCycleInterval = value; }
        public float ThemeTransitionDuration { get => m_ThemeTransitionDuration; set => m_ThemeTransitionDuration = value; }
        public bool HarmonizedPalettes { get => m_HarmonizedPalettes; set => m_HarmonizedPalettes = value; }

        public UIBackgroundLayerController BgRedController { get => m_BgRedController; set => m_BgRedController = value; }
        public UIBackgroundLayerController RedYellowController { get => m_RedYellowController; set => m_RedYellowController = value; }
        public UIVillainAnimator VillainAnimator { get => m_VillainAnimator; set => m_VillainAnimator = value; }
        public UITitleSuspensionSystem TitleSuspension { get => m_TitleSuspension; set => m_TitleSuspension = value; }
        public UIRobotLifeAnimator RobotAnimator { get => m_RobotAnimator; set => m_RobotAnimator = value; }
        public UISparkAtmosphereSystem SparkAtmosphere { get => m_SparkAtmosphere; set => m_SparkAtmosphere = value; }
        public UIBackgroundLayerController SparkController { get => m_SparkController; set => m_SparkController = value; }
        public UIParallaxController ParallaxController { get => m_ParallaxController; set => m_ParallaxController = value; }

        public UIBackgroundLayerController VillainOutlineController { get => m_VillainOutlineController; set => m_VillainOutlineController = value; }
        public UIBackgroundLayerController HeroOutlineController { get => m_HeroOutlineController; set => m_HeroOutlineController = value; }
        public UIBackgroundLayerController TitleOutlineController { get => m_TitleOutlineController; set => m_TitleOutlineController = value; }

        private void Awake()
        {
            s_Instance = this;
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            Play();
            if (m_RandomizeOnStart && Application.isPlaying)
            {
                RandomizeAllThemes(immediate: true);
            }
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

#if UNITY_EDITOR
        [ContextMenu("Setup Controllers in Scene")]
        public void SetupSceneControllersInEditor()
        {
            ResolveReferences(forceCreateInEditor: true);
            UnityEditor.EditorUtility.SetDirty(this);
            if (m_BgRedController != null) UnityEditor.EditorUtility.SetDirty(m_BgRedController);
            if (m_RedYellowController != null) UnityEditor.EditorUtility.SetDirty(m_RedYellowController);
            if (m_VillainOutlineController != null) UnityEditor.EditorUtility.SetDirty(m_VillainOutlineController);
            if (m_HeroOutlineController != null) UnityEditor.EditorUtility.SetDirty(m_HeroOutlineController);
            if (m_TitleOutlineController != null) UnityEditor.EditorUtility.SetDirty(m_TitleOutlineController);
            if (m_SparkController != null) UnityEditor.EditorUtility.SetDirty(m_SparkController);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("<color=#26E0FF><b>[CinematicUI]</b> Successfully configured all UIBackgroundLayerControllers in scene!</color>");
        }
#endif

        public void ResolveReferences(bool forceCreateInEditor = false)
        {
            Transform root = transform;
            bool canAdd = Application.isPlaying || forceCreateInEditor;

            // Search in root, siblings, and parent hierarchy
            Transform bgRoot = root.Find("Backagrond") ?? root.Find("Background") ?? root.parent?.Find("Backagrond") ?? root.parent?.Find("Background");
            if (bgRoot == null)
            {
                var bgObj = GameObject.Find("Backagrond") ?? GameObject.Find("Background");
                if (bgObj != null) bgRoot = bgObj.transform;
            }

            if (bgRoot != null)
            {
                if (m_BgRedController == null)
                {
                    Transform bgRed = bgRoot.Find("BG Red") ?? bgRoot.Find("BG_Red");
                    if (bgRed != null)
                    {
                        m_BgRedController = bgRed.GetComponent<UIBackgroundLayerController>();
                        if (m_BgRedController == null && canAdd)
                        {
                            m_BgRedController = bgRed.gameObject.AddComponent<UIBackgroundLayerController>();
                            m_BgRedController.Profile = BackgroundLayerProfile.BackgroundRed;
                        }
                    }
                }

                if (m_RedYellowController == null)
                {
                    Transform ryLayer = bgRoot.Find("Red and yellow layer") ?? bgRoot.Find("RedAndYellowLayer");
                    if (ryLayer != null)
                    {
                        m_RedYellowController = ryLayer.GetComponent<UIBackgroundLayerController>();
                        if (m_RedYellowController == null && canAdd)
                        {
                            m_RedYellowController = ryLayer.gameObject.AddComponent<UIBackgroundLayerController>();
                            m_RedYellowController.Profile = BackgroundLayerProfile.RedYellowTransition;
                        }
                    }
                }

                if (m_VillainAnimator == null)
                {
                    Transform villain = bgRoot.Find("Villan") ?? bgRoot.Find("DR") ?? bgRoot.Find("Villain") ?? bgRoot.Find("CharacterArtwork");
                    if (villain != null)
                    {
                        m_VillainAnimator = villain.GetComponent<UIVillainAnimator>();
                        if (m_VillainAnimator == null && canAdd)
                        {
                            m_VillainAnimator = villain.gameObject.AddComponent<UIVillainAnimator>();
                        }
                    }
                }

                Transform villainTrans = bgRoot.Find("Villan") ?? bgRoot.Find("DR") ?? bgRoot.Find("Villain") ?? bgRoot.Find("CharacterArtwork");
                if (villainTrans != null && m_VillainOutlineController == null)
                {
                    m_VillainOutlineController = villainTrans.GetComponent<UIBackgroundLayerController>();
                    if (m_VillainOutlineController == null && canAdd)
                    {
                        m_VillainOutlineController = villainTrans.gameObject.AddComponent<UIBackgroundLayerController>();
                        m_VillainOutlineController.Profile = BackgroundLayerProfile.Villain;
                        m_VillainOutlineController.EnsureOutlineLayer();
                    }
                }

                if (m_RobotAnimator == null)
                {
                    Transform robot = bgRoot.Find("Hero") ?? bgRoot.Find("Robot") ?? bgRoot.Find("Byte");
                    if (robot != null)
                    {
                        m_RobotAnimator = robot.GetComponent<UIRobotLifeAnimator>();
                        if (m_RobotAnimator == null && canAdd)
                        {
                            m_RobotAnimator = robot.gameObject.AddComponent<UIRobotLifeAnimator>();
                        }
                    }
                }

                Transform robotTrans = bgRoot.Find("Hero") ?? bgRoot.Find("Robot") ?? bgRoot.Find("Byte");
                if (robotTrans != null && m_HeroOutlineController == null)
                {
                    m_HeroOutlineController = robotTrans.GetComponent<UIBackgroundLayerController>();
                    if (m_HeroOutlineController == null && canAdd)
                    {
                        m_HeroOutlineController = robotTrans.gameObject.AddComponent<UIBackgroundLayerController>();
                        m_HeroOutlineController.Profile = BackgroundLayerProfile.Hero;
                        m_HeroOutlineController.EnsureOutlineLayer();
                    }
                }

                Transform spark = bgRoot.Find("Spark") ?? bgRoot.Find("FX");
                if (spark != null)
                {
                    if (m_SparkAtmosphere == null)
                    {
                        m_SparkAtmosphere = spark.GetComponent<UISparkAtmosphereSystem>();
                        if (m_SparkAtmosphere == null && canAdd)
                        {
                            m_SparkAtmosphere = spark.gameObject.AddComponent<UISparkAtmosphereSystem>();
                        }
                    }
                    if (m_SparkController == null)
                    {
                        m_SparkController = spark.GetComponent<UIBackgroundLayerController>();
                        if (m_SparkController == null && canAdd)
                        {
                            m_SparkController = spark.gameObject.AddComponent<UIBackgroundLayerController>();
                            m_SparkController.Profile = BackgroundLayerProfile.Spark;
                        }
                    }
                }
            }

            // Title suspension
            if (m_TitleSuspension == null)
            {
                Transform panel = root.Find("HomeScreenPanel  New") ?? root.Find("HomeScreenPanel_New") ?? root.parent?.Find("HomeScreenPanel  New");
                if (panel == null)
                {
                    var pObj = GameObject.Find("HomeScreenPanel  New") ?? GameObject.Find("HomeScreenPanel_New");
                    if (pObj != null) panel = pObj.transform;
                }
                Transform titleRoot = (panel != null) ? (panel.Find("Holder Tittle") ?? panel.Find("Title/Holder Tittle")) : null;
                if (titleRoot == null)
                {
                    titleRoot = root.Find("Holder Tittle") ?? root.parent?.Find("Holder Tittle");
                    if (titleRoot == null)
                    {
                        var tObj = GameObject.Find("Holder Tittle");
                        if (tObj != null) titleRoot = tObj.transform;
                    }
                }

                if (titleRoot != null)
                {
                    m_TitleSuspension = titleRoot.GetComponent<UITitleSuspensionSystem>();
                    if (m_TitleSuspension == null && canAdd)
                    {
                        m_TitleSuspension = titleRoot.gameObject.AddComponent<UITitleSuspensionSystem>();
                    }
                    if (m_TitleSuspension != null)
                    {
                        if (m_VillainAnimator != null)
                        {
                            m_TitleSuspension.VillainAnimator = m_VillainAnimator;
                        }
                        m_TitleSuspension.CleanupEnergyLinks();
                    }

                    Transform titleImgTrans = titleRoot.Find("Title") ?? titleRoot;
                    if (titleImgTrans != null && titleImgTrans.GetComponent<Image>() != null && m_TitleOutlineController == null)
                    {
                        m_TitleOutlineController = titleImgTrans.GetComponent<UIBackgroundLayerController>();
                        if (m_TitleOutlineController == null && canAdd)
                        {
                            m_TitleOutlineController = titleImgTrans.gameObject.AddComponent<UIBackgroundLayerController>();
                            m_TitleOutlineController.Profile = BackgroundLayerProfile.Title;
                            m_TitleOutlineController.EnsureOutlineLayer();
                        }
                    }
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
                    m_RedYellowController.TriggerPulse(0.08f, 0.22f);
                }
                if (m_TitleOutlineController != null)
                {
                    m_TitleOutlineController.PulseOutlineImmediate(
                        m_DownbeatEnvelope.peakMultiplier,
                        m_DownbeatEnvelope.attack,
                        m_DownbeatEnvelope.hold,
                        m_DownbeatEnvelope.release,
                        null, "Downbeat");
                }
            }
        }

        private void HandleStrongBeat(BeatEvent evt)
        {
            if (!m_IsActive || !m_EnableMusicSync) return;

            // Immediate punchy energy pulse on Robot and Title
            if (m_RobotAnimator != null)
            {
                m_RobotAnimator.TriggerSubtleEnergyPulse(0.18f);
            }
            if (m_HeroOutlineController != null)
            {
                m_HeroOutlineController.PulseOutlineImmediate(
                    m_StrongBeatEnvelope.peakMultiplier,
                    m_StrongBeatEnvelope.attack,
                    m_StrongBeatEnvelope.hold,
                    m_StrongBeatEnvelope.release,
                    null, "StrongBeat");
            }
            if (m_TitleOutlineController != null)
            {
                m_TitleOutlineController.PulseOutlineImmediate(
                    m_StrongBeatEnvelope.peakMultiplier * 0.9f,
                    m_StrongBeatEnvelope.attack,
                    m_StrongBeatEnvelope.hold,
                    m_StrongBeatEnvelope.release,
                    null, "StrongBeat");
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

            // Every Nth bar, trigger machinery breathing pulse and villain controlled energy presence
            if (evt.BarIndex % Mathf.Max(1, m_BarPulseInterval) == 0)
            {
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.12f, 0.40f);
                }
                if (m_VillainOutlineController != null)
                {
                    m_VillainOutlineController.PulseOutlineImmediate(
                        m_BarEnvelope.peakMultiplier,
                        m_BarEnvelope.attack,
                        m_BarEnvelope.hold,
                        m_BarEnvelope.release,
                        null, "BarBeat");
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // BUTTON FOCUS RELATIONSHIP
        // ═════════════════════════════════════════════════════════════════════════════

        private void HandleButtonFocused(MainMenuButtonEnergyAnimator button, bool focused)
        {
            if (!m_IsActive || !m_EnableButtonReactions || !focused || button == null) return;

            m_LastFocusedButtonName = button.gameObject.name;
            m_CurrentHierarchyLevel = 3;

            // Punctuate button focus with subtle Type A Micro Glitch ONLY on permitted background layers (Red BG & Spark)
            if (m_BgRedController != null)
            {
                m_BgRedController.TriggerControlledGlitch(ControlledGlitchType.TypeA_MicroGlitch, 0.8f);
            }
            if (m_SparkController != null && UnityEngine.Random.value < 0.45f)
            {
                m_SparkController.TriggerControlledGlitch(ControlledGlitchType.TypeA_MicroGlitch, 0.8f);
            }

            string btnName = button.gameObject.name.ToLowerInvariant();

            if (btnName.Contains("continue"))
            {
                // CONTINUE: subtle cyan/blue energy response on Title and Villain
                if (m_RedYellowController != null)
                {
                    m_RedYellowController.TriggerPulse(0.10f, 0.25f, new Color(0.35f, 0.85f, 1f, 1f));
                }
                if (m_TitleOutlineController != null)
                {
                    m_TitleOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, new Color(0.22f, 0.88f, 1f, 1f), "FocusContinue");
                }
                if (m_VillainOutlineController != null)
                {
                    m_VillainOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier * 0.8f, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, new Color(0.35f, 0.85f, 1f, 1f), "FocusContinue");
                }
            }
            else if (btnName.Contains("new") || btnName.Contains("game"))
            {
                if (m_TitleSuspension != null)
                {
                    m_TitleSuspension.Impulse(new Vector2(0f, 2.5f), 0.4f);
                }
                if (m_TitleOutlineController != null)
                {
                    m_TitleOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier * 1.2f, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, null, "FocusNewGame");
                }
                if (m_RobotAnimator != null)
                {
                    m_RobotAnimator.TriggerSubtleEnergyPulse(0.20f);
                }
            }
            else if (btnName.Contains("collect"))
            {
                if (m_RobotAnimator != null)
                {
                    m_RobotAnimator.TriggerSubtleEnergyPulse(0.25f);
                }
                if (m_HeroOutlineController != null)
                {
                    m_HeroOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier * 1.3f, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, null, "FocusCollect");
                }
            }
            else if (btnName.Contains("option") || btnName.Contains("setting"))
            {
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.12f, 0.30f);
                }
            }
            else if (btnName.Contains("credit"))
            {
                if (m_RedYellowController != null)
                {
                    m_RedYellowController.TriggerPulse(0.10f, 0.35f, new Color(1f, 0.85f, 0.5f, 1f));
                }
                if (m_TitleOutlineController != null)
                {
                    m_TitleOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, new Color(1f, 0.88f, 0.35f, 1f), "FocusCredits");
                }
            }
            else if (btnName.Contains("exit") || btnName.Contains("quit"))
            {
                if (m_BgRedController != null)
                {
                    m_BgRedController.TriggerPulse(0.22f, 0.30f, new Color(1f, 0.2f, 0.15f, 1f));
                }
                if (m_VillainAnimator != null)
                {
                    m_VillainAnimator.TriggerVillainEnergyPulse(0.25f);
                }
                if (m_VillainOutlineController != null)
                {
                    m_VillainOutlineController.PulseOutlineImmediate(m_ButtonFocusEnvelope.peakMultiplier * 1.5f, m_ButtonFocusEnvelope.attack, m_ButtonFocusEnvelope.hold, m_ButtonFocusEnvelope.release, new Color(1f, 0.165f, 0.28f, 1f), "FocusExit");
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
                        // Background machinery pulse and digital glitch on Red BG
                        if (m_BgRedController != null)
                        {
                            m_BgRedController.TriggerPulse(0.09f, 0.35f);
                            m_BgRedController.TriggerControlledGlitch(ControlledGlitchType.TypeB_DigitalTear, 1f);
                        }
                        break;

                    case 3:
                        // Spark electrical digital glitch burst (only Spark / Red BG permitted)
                        if (m_SparkController != null)
                        {
                            m_SparkController.TriggerControlledGlitch(ControlledGlitchType.TypeB_DigitalTear, 1f);
                        }
                        else if (m_BgRedController != null)
                        {
                            m_BgRedController.TriggerControlledGlitch(ControlledGlitchType.TypeA_MicroGlitch, 1f);
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

            if (m_ThemeCycleRoutine != null)
            {
                StopCoroutine(m_ThemeCycleRoutine);
                m_ThemeCycleRoutine = null;
            }
            if (m_EnableRandomThemeCycle && Application.isPlaying)
            {
                m_ThemeCycleRoutine = StartCoroutine(RandomThemeCycleRoutine());
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

            if (m_ThemeCycleRoutine != null)
            {
                StopCoroutine(m_ThemeCycleRoutine);
                m_ThemeCycleRoutine = null;
            }

            if (m_BgRedController != null) m_BgRedController.Stop();
            if (m_RedYellowController != null) m_RedYellowController.Stop();
            if (m_VillainAnimator != null) m_VillainAnimator.Stop();
            if (m_TitleSuspension != null) m_TitleSuspension.Stop();
            if (m_RobotAnimator != null) m_RobotAnimator.Stop();
            if (m_SparkAtmosphere != null) m_SparkAtmosphere.Stop();
            if (m_ParallaxController != null) m_ParallaxController.Stop();
        }

        private IEnumerator RandomThemeCycleRoutine()
        {
            yield return new WaitForSecondsRealtime(m_ThemeCycleInterval);

            while (m_IsActive && m_EnableRandomThemeCycle)
            {
                RandomizeAllThemes(immediate: false);
                yield return new WaitForSecondsRealtime(m_ThemeCycleInterval);
            }
            m_ThemeCycleRoutine = null;
        }

        /// <summary>
        /// Randomizes themes across Hero, Villain, Title, and Background layers with smooth crossfading.
        /// Uses all 12 themes dynamically.
        /// </summary>
        public void RandomizeAllThemes(bool immediate = false)
        {
            ResolveReferences();

            var allPresets = new CinematicThemePreset[]
            {
                CinematicThemePreset.CyberCyan,
                CinematicThemePreset.VillainCrimson,
                CinematicThemePreset.ElectricPurple,
                CinematicThemePreset.PlasmaBlue,
                CinematicThemePreset.ToxicGreen,
                CinematicThemePreset.GoldenPower,
                CinematicThemePreset.OrangeEnergy,
                CinematicThemePreset.Magenta,
                CinematicThemePreset.Ice,
                CinematicThemePreset.RetroArcade,
                CinematicThemePreset.Industrial,
                CinematicThemePreset.NightTech
            };

            CinematicThemePreset villainPreset;
            CinematicThemePreset heroPreset;
            CinematicThemePreset titlePreset;
            CinematicThemePreset bgPreset;

            if (m_HarmonizedPalettes)
            {
                CinematicThemePreset[] villainPool = {
                    CinematicThemePreset.VillainCrimson,
                    CinematicThemePreset.ElectricPurple,
                    CinematicThemePreset.ToxicGreen,
                    CinematicThemePreset.Industrial,
                    CinematicThemePreset.Magenta,
                    CinematicThemePreset.OrangeEnergy
                };
                CinematicThemePreset[] heroPool = {
                    CinematicThemePreset.CyberCyan,
                    CinematicThemePreset.PlasmaBlue,
                    CinematicThemePreset.Ice,
                    CinematicThemePreset.GoldenPower,
                    CinematicThemePreset.RetroArcade,
                    CinematicThemePreset.NightTech
                };
                CinematicThemePreset[] titlePool = {
                    CinematicThemePreset.GoldenPower,
                    CinematicThemePreset.RetroArcade,
                    CinematicThemePreset.CyberCyan,
                    CinematicThemePreset.ElectricPurple,
                    CinematicThemePreset.PlasmaBlue,
                    CinematicThemePreset.OrangeEnergy,
                    CinematicThemePreset.Magenta
                };

                villainPreset = villainPool[UnityEngine.Random.Range(0, villainPool.Length)];
                heroPreset = heroPool[UnityEngine.Random.Range(0, heroPool.Length)];
                titlePreset = titlePool[UnityEngine.Random.Range(0, titlePool.Length)];
                bgPreset = allPresets[UnityEngine.Random.Range(0, allPresets.Length)];
            }
            else
            {
                villainPreset = allPresets[UnityEngine.Random.Range(0, allPresets.Length)];
                heroPreset = allPresets[UnityEngine.Random.Range(0, allPresets.Length)];
                titlePreset = allPresets[UnityEngine.Random.Range(0, allPresets.Length)];
                bgPreset = allPresets[UnityEngine.Random.Range(0, allPresets.Length)];
            }

            ApplyThemeToController(m_VillainOutlineController, villainPreset, immediate);
            ApplyThemeToController(m_HeroOutlineController, heroPreset, immediate);
            ApplyThemeToController(m_TitleOutlineController, titlePreset, immediate);
            ApplyThemeToController(m_RedYellowController, titlePreset, immediate);
            ApplyThemeToController(m_BgRedController, bgPreset, immediate);
            ApplyThemeToController(m_SparkController, heroPreset, immediate);
        }

        public void ApplyThemePresetToAll(CinematicThemePreset preset, bool immediate = false)
        {
            ResolveReferences();
            ApplyThemeToController(m_VillainOutlineController, preset, immediate);
            ApplyThemeToController(m_HeroOutlineController, preset, immediate);
            ApplyThemeToController(m_TitleOutlineController, preset, immediate);
            ApplyThemeToController(m_RedYellowController, preset, immediate);
            ApplyThemeToController(m_BgRedController, preset, immediate);
            ApplyThemeToController(m_SparkController, preset, immediate);
        }

        private void ApplyThemeToController(UIBackgroundLayerController ctrl, CinematicThemePreset preset, bool immediate)
        {
            if (ctrl == null) return;
            if (immediate || !Application.isPlaying)
            {
                ctrl.ApplyThemePreset(preset);
            }
            else
            {
                ctrl.TransitionToTheme(preset, m_ThemeTransitionDuration);
            }
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

#if UNITY_EDITOR || DEBUG
        private void OnGUI()
        {
            if (!m_EnableVisualDebug) return;

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(new Rect(10, 10, 260, 120), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(16, 14, 250, 110));
            GUILayout.Label("<color=#26E0FF><b>MAIN MENU CINEMATIC DEBUG</b></color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            GUILayout.Label($"<b>Screen:</b> Main Menu / Home", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            GUILayout.Label($"<b>Focus Button:</b> {m_LastFocusedButtonName}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            GUILayout.Label($"<b>Visual Hierarchy:</b> Level {m_CurrentHierarchyLevel}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            int bar = (MusicBeatManager.Instance != null && MusicBeatManager.Instance.CurrentBeat.BarIndex >= 0) ? MusicBeatManager.Instance.CurrentBeat.BarIndex : 0;
            int beat = (MusicBeatManager.Instance != null && MusicBeatManager.Instance.CurrentBeat.BeatInBar >= 0) ? MusicBeatManager.Instance.CurrentBeat.BeatInBar : 0;
            GUILayout.Label($"<b>Beat Sync:</b> Bar {bar}, Beat {beat}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            GUILayout.EndArea();
        }
#endif
    }
}
