using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UI.CinematicEffects.BeatSync
{
    /// <summary>
    /// Central audio rhythm coordinator for UI animation, shader modulation, particles, and SFX.
    /// Tracks the persistent BackgroundMusic AudioSource in real-time, calculates musical beats/bars,
    /// and dispatches synchronized lifecycle events to subscribers.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicBeatManager : MonoBehaviour
    {
        private static MusicBeatManager s_Instance;

        public static MusicBeatManager Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                s_Instance = FindAnyObjectByType<MusicBeatManager>();
                if (s_Instance != null) return s_Instance;

                GameObject go = new GameObject("[MusicBeatManager]");
                s_Instance = go.AddComponent<MusicBeatManager>();
                DontDestroyOnLoad(go);
                return s_Instance;
            }
        }

        [Header("Rhythm Configuration Asset")]
        [SerializeField] private MusicBeatData m_BeatData;

        [Header("Latency & Offset Calibration")]
        [Tooltip("Manual latency offset in milliseconds (-200ms to +200ms). Positive = events fire earlier.")]
        [Range(-200f, 200f)]
        [SerializeField] private float m_BeatOffsetMs = 0f;

        [Tooltip("Visual anticipation lead time in seconds before an impact beat.")]
        [SerializeField] private float m_VisualLeadTime = 0.15f;

        [Tooltip("SFX playback lead time in seconds to account for audio engine buffering.")]
        [SerializeField] private float m_SfxLeadTime = 0.02f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Development Debug")]
        [Tooltip("Display real-time beat timeline and telemetry overlay in Editor/Dev builds.")]
        [SerializeField] private bool m_ShowDebugTimeline = false;
#endif

        // Public lifecycle events
        public static event Action<BeatEvent> OnBeat;
        public static event Action<BeatEvent> OnDownbeat;
        public static event Action<BeatEvent> OnBar;
        public static event Action<BeatEvent> OnStrongBeat;

        // Runtime state
        private float m_LastTrackTime = -1f;
        private int m_LastFiredBeatIndex = -1;
        private AudioSource m_TrackedSource;
        private BeatMarker m_CurrentBeat;
        private BeatMarker m_NextBeat;

        public MusicBeatData BeatData => m_BeatData;
        public float BeatOffsetMs { get => m_BeatOffsetMs; set => m_BeatOffsetMs = value; }
        public float VisualLeadTime => m_VisualLeadTime;
        public float SfxLeadTime => m_SfxLeadTime;
        public BeatMarker CurrentBeat => m_CurrentBeat;
        public BeatMarker NextBeat => m_NextBeat;

        public float Bpm => m_BeatData != null ? m_BeatData.Bpm : 113.304f;
        public float BeatDuration => m_BeatData != null ? m_BeatData.BeatDuration : (60f / 113.304f);
        public float BarDuration => m_BeatData != null ? m_BeatData.BarDuration : (60f / 113.304f * 4f);

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeBeatData();
        }

        private void Start()
        {
            InitializeBeatData();
        }

        private void InitializeBeatData()
        {
            if (m_BeatData == null)
            {
                m_BeatData = Resources.Load<MusicBeatData>("MusicBeatData");
                if (m_BeatData == null)
                {
                    m_BeatData = ScriptableObject.CreateInstance<MusicBeatData>();
                    m_BeatData.Bpm = 113.304f;
                    m_BeatData.FirstBeatOffset = 0.136f;
                    m_BeatData.BeatsPerBar = 4;
                    m_BeatData.TrackLength = 59.736f;
                    m_BeatData.GenerateMarkers();
                }
            }
            else
            {
                m_BeatData.EnsureMarkers();
            }
        }

        public AudioSource GetAudioSource()
        {
            if (m_TrackedSource != null) return m_TrackedSource;
            m_TrackedSource = BackgroundMusic.Source;
            return m_TrackedSource;
        }

        /// <summary>
        /// Current playback time of background music with calibration offset applied.
        /// </summary>
        public float CurrentTrackTime
        {
            get
            {
                AudioSource src = GetAudioSource();
                if (src == null || !src.isPlaying) return 0f;

                float rawTime = src.time;
                float offsetSec = m_BeatOffsetMs / 1000f;
                float calibrated = rawTime + offsetSec;

                if (m_BeatData != null && m_BeatData.TrackLength > 0f)
                {
                    calibrated = Mathf.Repeat(calibrated, m_BeatData.TrackLength);
                }
                return calibrated;
            }
        }

        /// <summary>
        /// Normalized progress (0.0 to 1.0) through the current beat.
        /// </summary>
        public float BeatProgress
        {
            get
            {
                float time = CurrentTrackTime;
                float dur = BeatDuration;
                if (dur <= 0.001f) return 0f;

                float offset = m_BeatData != null ? m_BeatData.FirstBeatOffset : 0.136f;
                float rel = Mathf.Max(0f, time - offset);
                return (rel % dur) / dur;
            }
        }

        /// <summary>
        /// Normalized progress (0.0 to 1.0) through the current bar.
        /// </summary>
        public float BarProgress
        {
            get
            {
                float time = CurrentTrackTime;
                float dur = BarDuration;
                if (dur <= 0.001f) return 0f;

                float offset = m_BeatData != null ? m_BeatData.FirstBeatOffset : 0.136f;
                float rel = Mathf.Max(0f, time - offset);
                return (rel % dur) / dur;
            }
        }

        /// <summary>
        /// Seconds remaining until the next beat marker.
        /// </summary>
        public float TimeToNextBeat
        {
            get
            {
                float cur = CurrentTrackTime;
                BeatMarker next = m_BeatData != null ? m_BeatData.GetNextBeat(cur) : default;
                if (next.Time >= cur) return next.Time - cur;
                float trackLen = m_BeatData != null ? m_BeatData.TrackLength : 59.736f;
                return (trackLen - cur) + next.Time;
            }
        }

        /// <summary>
        /// Seconds remaining until the next bar downbeat.
        /// </summary>
        public float TimeToNextDownbeat
        {
            get
            {
                float time = CurrentTrackTime;
                float barDur = BarDuration;
                if (barDur <= 0.001f) return 0f;

                float offset = m_BeatData != null ? m_BeatData.FirstBeatOffset : 0.136f;
                float rel = Mathf.Max(0f, time - offset);
                float progress = rel % barDur;
                return barDur - progress;
            }
        }

        public void ResetTracking()
        {
            m_LastTrackTime = -1f;
            m_LastFiredBeatIndex = -1;
        }

        private void Update()
        {
            AudioSource src = GetAudioSource();
            if (src == null || !src.isPlaying) return;

            if (m_BeatData == null) InitializeBeatData();
            if (m_BeatData == null || m_BeatData.Markers == null || m_BeatData.Markers.Length == 0) return;

            float curTime = CurrentTrackTime;

            // First frame initialization
            if (m_LastTrackTime < 0f)
            {
                m_LastTrackTime = curTime;
                m_CurrentBeat = m_BeatData.GetClosestBeat(curTime);
                m_NextBeat = m_BeatData.GetNextBeat(curTime);
                m_LastFiredBeatIndex = m_CurrentBeat.BeatIndex;
                return;
            }

            // Detect beat crossings
            float prevTime = m_LastTrackTime;
            float trackLength = m_BeatData.TrackLength;

            if (curTime >= prevTime)
            {
                ProcessRange(prevTime, curTime);
            }
            else
            {
                // Track wrapped around loop boundary
                ProcessRange(prevTime, trackLength);
                ProcessRange(0f, curTime);
            }

            m_LastTrackTime = curTime;
            m_CurrentBeat = m_BeatData.GetClosestBeat(curTime);
            m_NextBeat = m_BeatData.GetNextBeat(curTime);
        }

        private void ProcessRange(float t0, float t1)
        {
            BeatMarker[] markers = m_BeatData.Markers;
            int count = markers.Length;

            for (int i = 0; i < count; i++)
            {
                float mTime = markers[i].Time;
                if (mTime > t0 && mTime <= t1)
                {
                    if (markers[i].BeatIndex != m_LastFiredBeatIndex)
                    {
                        m_LastFiredBeatIndex = markers[i].BeatIndex;
                        DispatchBeat(markers[i]);
                    }
                }
            }
        }

        private void DispatchBeat(BeatMarker marker)
        {
            BeatEvent evt = new BeatEvent(marker);

            try
            {
                OnBeat?.Invoke(evt);

                if (evt.IsStrongBeat)
                {
                    OnStrongBeat?.Invoke(evt);
                }

                if (evt.IsDownbeat)
                {
                    OnDownbeat?.Invoke(evt);
                    OnBar?.Invoke(evt);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // Compiled out of release players entirely. Merely declaring OnGUI opts this behaviour
        // into Unity's IMGUI event loop, which then runs Layout and Repaint passes against it
        // every frame -- allocating an Event each time -- even though the body returns on the
        // first line. This is a calibration HUD, so it has no business costing anything in a
        // shipped build.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!m_ShowDebugTimeline) return;

            // Sleek developer HUD for beat synchronization calibration
            GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, 180), GUI.skin.box);
            GUILayout.Label($"<b><color=#00e5ff>RETRY MUSIC BEAT SYNC</color></b>", GetRichTextStyle(13));
            GUILayout.Label($"Tempo: {Bpm:F1} BPM | Meter: {m_BeatData?.BeatsPerBar ?? 4}/4");
            GUILayout.Label($"Time: {CurrentTrackTime:F2}s / {m_BeatData?.TrackLength ?? 0f:F1}s");
            GUILayout.Label($"Current Beat: #{m_CurrentBeat.BeatIndex} (Bar {m_CurrentBeat.BarIndex}:{m_CurrentBeat.BeatInBar})");
            GUILayout.Label($"Next Beat In: {TimeToNextBeat:F3}s | Downbeat In: {TimeToNextDownbeat:F3}s");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Offset: {m_BeatOffsetMs:F0}ms", GUILayout.Width(100));
            m_BeatOffsetMs = GUILayout.HorizontalSlider(m_BeatOffsetMs, -200f, 200f);
            GUILayout.EndHorizontal();

            // Visual rhythm indicator
            float prog = BeatProgress;
            Rect r = GUILayoutUtility.GetRect(290, 16);
            GUI.Box(r, "");
            Rect fill = new Rect(r.x, r.y, r.width * prog, r.height);
            Color barColor = m_CurrentBeat.IsDownbeat ? new Color(1f, 0.85f, 0.2f, 0.85f) : (m_CurrentBeat.IsStrongBeat ? new Color(0.2f, 0.85f, 1f, 0.8f) : new Color(0.4f, 0.5f, 0.7f, 0.6f));
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, barColor, 0, 0);

            GUILayout.EndArea();
        }

        // Cached per size: OnGUI runs at least twice a frame, and a fresh GUIStyle per label
        // is pure garbage.
        private readonly Dictionary<int, GUIStyle> m_RichTextStyles = new Dictionary<int, GUIStyle>();

        private GUIStyle GetRichTextStyle(int fontSize)
        {
            if (m_RichTextStyles.TryGetValue(fontSize, out GUIStyle cached) && cached != null)
            {
                return cached;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = fontSize };
            m_RichTextStyles[fontSize] = style;
            return style;
        }
#endif

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }
    }
}
