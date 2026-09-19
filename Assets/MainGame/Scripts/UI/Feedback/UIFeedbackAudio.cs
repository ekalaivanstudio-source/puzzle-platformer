using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MainGame.UI.Feedback
{
    public enum UISfxType
    {
        Navigate,
        Confirm,
        Back,
        Whoosh,
        Impact,
        Deploy,
        Retract,
        NodeActivate,
        BubblePop,
        Pulse,
        PointerSnap,
        RobotBoot,
        RobotScan,
        SliderTick,
        PopupSlam,
        // Bespoke Music-Aligned SFX:
        RetryCoreActivation,
        ContinueImpact,
        NewGameImpact,
        CollectImpact,
        OptionsImpact,
        CreditsImpact,
        ExitImpact,
        MajorTransitionImpact,
        EnergyCharge,
        WarningAlert
    }

    /// <summary>
    /// Centralized UI Audio service that routes all UI sounds strictly through the
    /// project's existing SFX AudioMixerGroup with musical anti-fatigue variation,
    /// focus debouncing, and stepped pitch support.
    /// Self-provisioning singleton that persists across scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFeedbackAudio : MonoBehaviour
    {
        private static UIFeedbackAudio s_Instance;

        public static UIFeedbackAudio Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                s_Instance = FindAnyObjectByType<UIFeedbackAudio>();
                if (s_Instance != null) return s_Instance;

                GameObject go = new GameObject("[UIFeedbackAudio]");
                s_Instance = go.AddComponent<UIFeedbackAudio>();
                DontDestroyOnLoad(go);
                return s_Instance;
            }
        }

        [Header("Mixer Routing")]
        [Tooltip("Existing SFX group in BackGround Music Controller.mixer.")]
        [SerializeField] private AudioMixerGroup m_SfxGroup;

        [Header("Curated UI Sound Clips")]
        [SerializeField] private AudioClip m_NavigateClip;
        [SerializeField] private AudioClip m_ConfirmClip;
        [SerializeField] private AudioClip m_BackClip;
        [SerializeField] private AudioClip m_WhooshClip;
        [SerializeField] private AudioClip m_ImpactClip;
        [SerializeField] private AudioClip m_DeployClip;
        [SerializeField] private AudioClip m_RetractClip;
        [SerializeField] private AudioClip m_NodeActivateClip;
        [SerializeField] private AudioClip m_BubblePopClip;
        [SerializeField] private AudioClip m_PulseClip;
        [SerializeField] private AudioClip m_PointerSnapClip;
        [SerializeField] private AudioClip m_RobotBootClip;
        [SerializeField] private AudioClip m_RobotScanClip;
        [SerializeField] private AudioClip m_SliderTickClip;
        [SerializeField] private AudioClip m_PopupSlamClip;

        [Header("Bespoke Music-Aligned Clips")]
        [SerializeField] private AudioClip m_RetryCoreClip;
        [SerializeField] private AudioClip m_ContinueImpactClip;
        [SerializeField] private AudioClip m_NewGameImpactClip;
        [SerializeField] private AudioClip m_CollectImpactClip;
        [SerializeField] private AudioClip m_OptionsImpactClip;
        [SerializeField] private AudioClip m_CreditsImpactClip;
        [SerializeField] private AudioClip m_ExitImpactClip;
        [SerializeField] private AudioClip m_MajorTransitionClip;

        private AudioSource m_AudioSource;
        private AudioSource m_SecondarySource;
        private readonly Dictionary<UISfxType, AudioClip> m_ClipMap = new Dictionary<UISfxType, AudioClip>();

        // Variation pools
        private AudioClip[] m_FocusClips;
        private AudioClip[] m_ConfirmClips;
        private AudioClip[] m_ImpactClips;

        private int m_FocusIndex = 0;
        private int m_ConfirmIndex = 0;
        private int m_ImpactIndex = 0;
        private float m_LastFocusTime = -1f;
        private const float k_FocusDebounceInterval = 0.040f; // 40ms minimum cooldown for focus ticks

        private Coroutine m_DuckingRoutine;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveMixerGroupIfNull();
            SetupAudioSources();
            RebuildClipMap();
        }

        private void ResolveMixerGroupIfNull()
        {
            if (m_SfxGroup != null) return;

            // Search for existing audio mixer
            AudioMixer[] mixers = Resources.FindObjectsOfTypeAll<AudioMixer>();
            for (int i = 0; i < mixers.Length; i++)
            {
                if (mixers[i] != null && mixers[i].name.Contains("Music"))
                {
                    AudioMixerGroup[] groups = mixers[i].FindMatchingGroups("SFX");
                    if (groups != null && groups.Length > 0)
                    {
                        m_SfxGroup = groups[0];
                        break;
                    }
                }
            }
        }

        private void SetupAudioSources()
        {
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.loop = false;
                m_AudioSource.spatialBlend = 0f; // 2D UI sound
                if (m_SfxGroup != null)
                {
                    m_AudioSource.outputAudioMixerGroup = m_SfxGroup;
                }
            }

            if (m_SecondarySource == null)
            {
                m_SecondarySource = gameObject.AddComponent<AudioSource>();
                m_SecondarySource.playOnAwake = false;
                m_SecondarySource.loop = false;
                m_SecondarySource.spatialBlend = 0f; // 2D UI sound
                if (m_SfxGroup != null)
                {
                    m_SecondarySource.outputAudioMixerGroup = m_SfxGroup;
                }
            }
        }

        public void RebuildClipMap()
        {
            m_ClipMap.Clear();
            RegisterClip(UISfxType.Navigate, ref m_NavigateClip, "ui_focus_a", "ui_navigate");
            RegisterClip(UISfxType.Confirm, ref m_ConfirmClip, "ui_confirm_a", "ui_confirm");
            RegisterClip(UISfxType.Back, ref m_BackClip, "ui_back");
            RegisterClip(UISfxType.Whoosh, ref m_WhooshClip, "ui_whoosh");
            RegisterClip(UISfxType.Impact, ref m_ImpactClip, "ui_impact_a", "ui_impact");
            RegisterClip(UISfxType.Deploy, ref m_DeployClip, "ui_deploy");
            RegisterClip(UISfxType.Retract, ref m_RetractClip, "ui_retract");
            RegisterClip(UISfxType.NodeActivate, ref m_NodeActivateClip, "ui_node_activate");
            RegisterClip(UISfxType.BubblePop, ref m_BubblePopClip, "ui_bubble_pop");
            RegisterClip(UISfxType.Pulse, ref m_PulseClip, "ui_pulse", "ui_energy_charge");
            RegisterClip(UISfxType.PointerSnap, ref m_PointerSnapClip, "ui_pointer_snap");
            RegisterClip(UISfxType.RobotBoot, ref m_RobotBootClip, "ui_robot_boot");
            RegisterClip(UISfxType.RobotScan, ref m_RobotScanClip, "ui_robot_scan");
            RegisterClip(UISfxType.SliderTick, ref m_SliderTickClip, "ui_slider_tick");
            RegisterClip(UISfxType.PopupSlam, ref m_PopupSlamClip, "ui_popup_slam");

            // Bespoke Music-Aligned clips
            RegisterClip(UISfxType.RetryCoreActivation, ref m_RetryCoreClip, "ui_retry_core", "ui_impact");
            RegisterClip(UISfxType.ContinueImpact, ref m_ContinueImpactClip, "ui_impact_continue", "ui_impact");
            RegisterClip(UISfxType.NewGameImpact, ref m_NewGameImpactClip, "ui_impact_newgame", "ui_impact");
            RegisterClip(UISfxType.CollectImpact, ref m_CollectImpactClip, "ui_impact_collect", "ui_impact");
            RegisterClip(UISfxType.OptionsImpact, ref m_OptionsImpactClip, "ui_impact_options", "ui_impact");
            RegisterClip(UISfxType.CreditsImpact, ref m_CreditsImpactClip, "ui_impact_credits", "ui_impact");
            RegisterClip(UISfxType.ExitImpact, ref m_ExitImpactClip, "ui_impact_exit", "ui_popup_slam");
            RegisterClip(UISfxType.MajorTransitionImpact, ref m_MajorTransitionClip, "ui_major_transition", "ui_impact");
            RegisterClip(UISfxType.EnergyCharge, ref m_PulseClip, "ui_energy_charge", "ui_pulse");
            RegisterClip(UISfxType.WarningAlert, ref m_PopupSlamClip, "ui_popup_slam");

            // Build variation pools
            List<AudioClip> focusList = new List<AudioClip>();
            TryAddPoolClip(focusList, "ui_focus_a", "ui_navigate");
            TryAddPoolClip(focusList, "ui_focus_b");
            TryAddPoolClip(focusList, "ui_focus_c");
            m_FocusClips = focusList.ToArray();

            List<AudioClip> confirmList = new List<AudioClip>();
            TryAddPoolClip(confirmList, "ui_confirm_a", "ui_confirm");
            TryAddPoolClip(confirmList, "ui_confirm_b");
            m_ConfirmClips = confirmList.ToArray();

            List<AudioClip> impactList = new List<AudioClip>();
            TryAddPoolClip(impactList, "ui_impact_a", "ui_impact");
            TryAddPoolClip(impactList, "ui_impact_b");
            TryAddPoolClip(impactList, "ui_impact_c");
            m_ImpactClips = impactList.ToArray();
        }

        private void RegisterClip(UISfxType type, ref AudioClip clipField, string primaryResource, string fallbackResource = null)
        {
            if (clipField == null)
            {
                clipField = Resources.Load<AudioClip>("Audio/UI/" + primaryResource);
                if (clipField == null && !string.IsNullOrEmpty(fallbackResource))
                {
                    clipField = Resources.Load<AudioClip>("Audio/UI/" + fallbackResource);
                }
            }
            if (clipField != null)
            {
                m_ClipMap[type] = clipField;
            }
        }

        private void TryAddPoolClip(List<AudioClip> list, string primaryResource, string fallbackResource = null)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/UI/" + primaryResource);
            if (clip == null && !string.IsNullOrEmpty(fallbackResource))
            {
                clip = Resources.Load<AudioClip>("Audio/UI/" + fallbackResource);
            }
            if (clip != null)
            {
                list.Add(clip);
            }
        }

        /// <summary>
        /// Plays a UI sound with musical anti-fatigue variation through the SFX mixer.
        /// </summary>
        public void Play(UISfxType type, float volumeScale = 1f, float pitchVariance = 0.02f)
        {
            // Focus debouncing & variation
            if (type == UISfxType.Navigate)
            {
                float now = Time.unscaledTime;
                if (m_LastFocusTime > 0f && (now - m_LastFocusTime) < k_FocusDebounceInterval)
                {
                    return; // debounced to prevent machine-gun audio stutter during rapid stick navigation
                }
                m_LastFocusTime = now;

                if (m_FocusClips != null && m_FocusClips.Length > 0)
                {
                    AudioClip focusClip = m_FocusClips[m_FocusIndex % m_FocusClips.Length];
                    m_FocusIndex++;
                    PlayClipInternal(type, focusClip, volumeScale, pitchVariance);
                    return;
                }
            }
            else if (type == UISfxType.Confirm && m_ConfirmClips != null && m_ConfirmClips.Length > 0)
            {
                AudioClip confirmClip = m_ConfirmClips[m_ConfirmIndex % m_ConfirmClips.Length];
                m_ConfirmIndex++;
                PlayClipInternal(type, confirmClip, volumeScale, pitchVariance);
                return;
            }
            else if (type == UISfxType.Impact && m_ImpactClips != null && m_ImpactClips.Length > 0)
            {
                AudioClip impactClip = m_ImpactClips[m_ImpactIndex % m_ImpactClips.Length];
                m_ImpactIndex++;
                PlayClipInternal(type, impactClip, volumeScale, pitchVariance);
                return;
            }

            if (m_ClipMap.TryGetValue(type, out AudioClip clip) && clip != null)
            {
                PlayClipInternal(type, clip, volumeScale, pitchVariance);
            }
        }

        private void PlayClipInternal(UISfxType type, AudioClip clip, float volumeScale, float pitchVariance)
        {
            if (clip == null) return;

            // Controlled micro-pitch variation (-2% to +2%)
            float clampedVariance = Mathf.Clamp(pitchVariance, 0f, 0.025f);
            float pitch = 1f;
            if (clampedVariance > 0f)
            {
                pitch = UnityEngine.Random.Range(1f - clampedVariance, 1f + clampedVariance);
            }

            AudioSource src = (m_AudioSource != null && m_AudioSource.isPlaying) ? m_SecondarySource : m_AudioSource;
            if (src != null)
            {
                src.pitch = pitch;
                src.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            }

#if UNITY_EDITOR || DEBUG
            if (MainGame.UI.CinematicEffects.BeatSync.MusicBeatManager.Instance != null)
            {
                var curBeat = MainGame.UI.CinematicEffects.BeatSync.MusicBeatManager.Instance.CurrentBeat;
                Debug.Log($"[MusicSync] Beat: #{curBeat.BeatIndex} (Bar {curBeat.BarIndex}:{curBeat.BeatInBar}) | [UI] {type} | [SFX] {clip.name} (Vol: {volumeScale:F2}, Pitch: {pitch:F2})");
            }
#endif
        }

        /// <summary>
        /// Plays a sound with stepped musical pitch progression (e.g. for sequential level node activations).
        /// </summary>
        public void PlayStepped(UISfxType type, int stepIndex, int totalSteps, float minPitch = 0.94f, float maxPitch = 1.16f, float volumeScale = 1f)
        {
            if (m_ClipMap.TryGetValue(type, out AudioClip clip) && clip != null)
            {
                float t = totalSteps > 1 ? Mathf.Clamp01((float)stepIndex / (totalSteps - 1)) : 0.5f;
                float pitch = Mathf.Lerp(minPitch, maxPitch, t);

                AudioSource src = (m_AudioSource != null && m_AudioSource.isPlaying) ? m_SecondarySource : m_AudioSource;
                if (src != null)
                {
                    src.pitch = pitch;
                    src.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
                }

#if UNITY_EDITOR || DEBUG
                if (MainGame.UI.CinematicEffects.BeatSync.MusicBeatManager.Instance != null)
                {
                    var curBeat = MainGame.UI.CinematicEffects.BeatSync.MusicBeatManager.Instance.CurrentBeat;
                    Debug.Log($"[MusicSync] Stepped Beat: #{curBeat.BeatIndex} (Bar {curBeat.BarIndex}) | Step: {stepIndex}/{totalSteps} | [SFX] {clip.name} (Pitch: {pitch:F2})");
                }
#endif
            }
        }

        /// <summary>
        /// Briefly ducks background music volume during major screen transitions (e.g. Home -> Level Selection).
        /// </summary>
        public void DuckMusicBriefly(float targetScale = 0.88f, float duration = 0.25f)
        {
            AudioSource bgSource = BackgroundMusic.Source;
            if (bgSource == null || !bgSource.isPlaying) return;

            if (m_DuckingRoutine != null)
            {
                StopCoroutine(m_DuckingRoutine);
            }
            m_DuckingRoutine = StartCoroutine(MusicDuckingRoutine(bgSource, targetScale, duration));
        }

        private IEnumerator MusicDuckingRoutine(AudioSource bgSource, float targetScale, float duration)
        {
            float baseVol = bgSource.volume;
            float targetVol = baseVol * targetScale;
            float halfDur = duration * 0.5f;

            // Duck
            float elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDur);
                bgSource.volume = Mathf.Lerp(baseVol, targetVol, t);
                yield return null;
            }

            // Return
            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDur);
                bgSource.volume = Mathf.Lerp(targetVol, baseVol, t);
                yield return null;
            }

            bgSource.volume = baseVol;
            m_DuckingRoutine = null;
        }

        // ─── Static Convenience Helpers ──────────────────────────────────────

        public static void PlaySfx(UISfxType type, float volumeScale = 1f, float pitchVariance = 0.02f)
        {
            Instance.Play(type, volumeScale, pitchVariance);
        }

        public static void PlaySteppedSfx(UISfxType type, int stepIndex, int totalSteps, float minPitch = 0.94f, float maxPitch = 1.16f, float volumeScale = 1f)
        {
            Instance.PlayStepped(type, stepIndex, totalSteps, minPitch, maxPitch, volumeScale);
        }
    }
}
