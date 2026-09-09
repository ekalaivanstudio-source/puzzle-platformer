using System;
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
        PopupSlam
    }

    /// <summary>
    /// Centralized UI Audio service that routes all UI sounds strictly through the
    /// project's existing SFX AudioMixerGroup with pitch variation and stepped pitch support.
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

        private AudioSource m_AudioSource;
        private AudioSource m_SecondarySource;
        private readonly Dictionary<UISfxType, AudioClip> m_ClipMap = new Dictionary<UISfxType, AudioClip>();

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
            RegisterClip(UISfxType.Navigate, ref m_NavigateClip, "ui_navigate");
            RegisterClip(UISfxType.Confirm, ref m_ConfirmClip, "ui_confirm");
            RegisterClip(UISfxType.Back, ref m_BackClip, "ui_back");
            RegisterClip(UISfxType.Whoosh, ref m_WhooshClip, "ui_whoosh");
            RegisterClip(UISfxType.Impact, ref m_ImpactClip, "ui_impact");
            RegisterClip(UISfxType.Deploy, ref m_DeployClip, "ui_deploy");
            RegisterClip(UISfxType.Retract, ref m_RetractClip, "ui_retract");
            RegisterClip(UISfxType.NodeActivate, ref m_NodeActivateClip, "ui_node_activate");
            RegisterClip(UISfxType.BubblePop, ref m_BubblePopClip, "ui_bubble_pop");
            RegisterClip(UISfxType.Pulse, ref m_PulseClip, "ui_pulse");
            RegisterClip(UISfxType.PointerSnap, ref m_PointerSnapClip, "ui_pointer_snap");
            RegisterClip(UISfxType.RobotBoot, ref m_RobotBootClip, "ui_robot_boot");
            RegisterClip(UISfxType.RobotScan, ref m_RobotScanClip, "ui_robot_scan");
            RegisterClip(UISfxType.SliderTick, ref m_SliderTickClip, "ui_slider_tick");
            RegisterClip(UISfxType.PopupSlam, ref m_PopupSlamClip, "ui_popup_slam");
        }

        private void RegisterClip(UISfxType type, ref AudioClip clipField, string resourceName)
        {
            if (clipField == null)
            {
                clipField = Resources.Load<AudioClip>("Audio/UI/" + resourceName);
            }
            if (clipField != null)
            {
                m_ClipMap[type] = clipField;
            }
        }

        /// <summary>
        /// Plays a UI sound with subtle anti-fatigue pitch variation through the SFX mixer.
        /// </summary>
        public void Play(UISfxType type, float volumeScale = 1f, float pitchVariance = 0.03f)
        {
            if (m_ClipMap.TryGetValue(type, out AudioClip clip) && clip != null)
            {
                float pitch = 1f;
                if (pitchVariance > 0f)
                {
                    pitch = UnityEngine.Random.Range(1f - pitchVariance, 1f + pitchVariance);
                }

                AudioSource src = (m_AudioSource != null && m_AudioSource.isPlaying) ? m_SecondarySource : m_AudioSource;
                if (src != null)
                {
                    src.pitch = pitch;
                    src.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
                }
            }
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
            }
        }

        // ─── Static Convenience Helpers ──────────────────────────────────────

        public static void PlaySfx(UISfxType type, float volumeScale = 1f, float pitchVariance = 0.03f)
        {
            Instance.Play(type, volumeScale, pitchVariance);
        }

        public static void PlaySteppedSfx(UISfxType type, int stepIndex, int totalSteps, float minPitch = 0.94f, float maxPitch = 1.16f, float volumeScale = 1f)
        {
            Instance.PlayStepped(type, stepIndex, totalSteps, minPitch, maxPitch, volumeScale);
        }
    }
}
