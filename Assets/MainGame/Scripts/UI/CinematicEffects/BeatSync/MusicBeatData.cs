using System;
using UnityEngine;

namespace MainGame.UI.CinematicEffects.BeatSync
{
    [Serializable]
    public struct BeatMarker
    {
        public float Time;
        public int BeatIndex;
        public int BarIndex;
        public int BeatInBar; // 0 = Downbeat, 1, 2 = Strong beat, 3
        public float Strength; // 0.0 to 1.0
        public bool IsDownbeat;
        public bool IsStrongBeat;

        public BeatMarker(float time, int beatIndex, int barIndex, int beatInBar, float strength, bool isDownbeat, bool isStrongBeat)
        {
            Time = time;
            BeatIndex = beatIndex;
            BarIndex = barIndex;
            BeatInBar = beatInBar;
            Strength = strength;
            IsDownbeat = isDownbeat;
            IsStrongBeat = isStrongBeat;
        }
    }

    [Serializable]
    public struct BeatEvent
    {
        public float Time;
        public int BeatIndex;
        public int BarIndex;
        public int BeatInBar;
        public float Strength;
        public bool IsDownbeat;
        public bool IsStrongBeat;

        public BeatEvent(BeatMarker marker)
        {
            Time = marker.Time;
            BeatIndex = marker.BeatIndex;
            BarIndex = marker.BarIndex;
            BeatInBar = marker.BeatInBar;
            Strength = marker.Strength;
            IsDownbeat = marker.IsDownbeat;
            IsStrongBeat = marker.IsStrongBeat;
        }

        public BeatEvent(float time, int beatIndex, int barIndex, int beatInBar, float strength, bool isDownbeat, bool isStrongBeat)
        {
            Time = time;
            BeatIndex = beatIndex;
            BarIndex = barIndex;
            BeatInBar = beatInBar;
            Strength = strength;
            IsDownbeat = isDownbeat;
            IsStrongBeat = isStrongBeat;
        }
    }

    /// <summary>
    /// ScriptableObject storing pre-computed rhythmic parameters and beat markers
    /// for background music synchronization.
    /// </summary>
    [CreateAssetMenu(fileName = "MusicBeatData", menuName = "RETRY/Audio/Music Beat Data")]
    public class MusicBeatData : ScriptableObject
    {
        [Header("Tempo & Meter")]
        [Tooltip("Beats per minute of the track.")]
        [SerializeField] private float m_Bpm = 113.304f;

        [Tooltip("Seconds from start of track to the very first downbeat/beat.")]
        [SerializeField] private float m_FirstBeatOffset = 0.136f;

        [Tooltip("Time signature numerator (typically 4 for 4/4 meter).")]
        [SerializeField] private int m_BeatsPerBar = 4;

        [Tooltip("Total duration of audio loop in seconds.")]
        [SerializeField] private float m_TrackLength = 59.736f;

        [Header("Precomputed Markers")]
        [SerializeField] private BeatMarker[] m_Markers;

        public float Bpm
        {
            get => m_Bpm;
            set => m_Bpm = value;
        }

        public float FirstBeatOffset
        {
            get => m_FirstBeatOffset;
            set => m_FirstBeatOffset = value;
        }

        public int BeatsPerBar
        {
            get => m_BeatsPerBar;
            set => m_BeatsPerBar = value;
        }

        public float TrackLength
        {
            get => m_TrackLength;
            set => m_TrackLength = value;
        }

        public BeatMarker[] Markers => m_Markers;

        public float BeatDuration => 60f / Mathf.Max(1f, m_Bpm);
        public float BarDuration => BeatDuration * Mathf.Max(1, m_BeatsPerBar);

        /// <summary>
        /// Populates the markers array deterministically based on tempo, offset, and track length.
        /// Can be called in Editor or as runtime fallback if markers are not pre-serialized.
        /// </summary>
        public void GenerateMarkers()
        {
            float beatDur = BeatDuration;
            if (beatDur <= 0.001f || m_TrackLength <= 0f) return;

            int totalBeats = Mathf.CeilToInt((m_TrackLength - m_FirstBeatOffset) / beatDur);
            if (totalBeats <= 0) totalBeats = 1;

            m_Markers = new BeatMarker[totalBeats];
            for (int i = 0; i < totalBeats; i++)
            {
                float t = m_FirstBeatOffset + (i * beatDur);
                int beatInBar = i % m_BeatsPerBar;
                int barIndex = i / m_BeatsPerBar;
                bool isDownbeat = (beatInBar == 0);
                bool isStrongBeat = (beatInBar == 0 || beatInBar == 2);
                float strength = isDownbeat ? 1.0f : (isStrongBeat ? 0.75f : 0.50f);

                m_Markers[i] = new BeatMarker(t, i, barIndex, beatInBar, strength, isDownbeat, isStrongBeat);
            }
        }

        /// <summary>
        /// Finds the closest beat marker to the provided playback time.
        /// </summary>
        public BeatMarker GetClosestBeat(float time)
        {
            EnsureMarkers();
            if (m_Markers == null || m_Markers.Length == 0)
            {
                return CreateSyntheticMarker(time);
            }

            // Binary search for closest timestamp
            int low = 0;
            int high = m_Markers.Length - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (m_Markers[mid].Time < time)
                {
                    low = mid + 1;
                }
                else if (m_Markers[mid].Time > time)
                {
                    high = mid - 1;
                }
                else
                {
                    return m_Markers[mid];
                }
            }

            // Compare low and high
            int bestIdx = 0;
            float minDiff = float.MaxValue;

            for (int i = Mathf.Max(0, high - 1); i <= Mathf.Min(m_Markers.Length - 1, low + 1); i++)
            {
                float diff = Mathf.Abs(m_Markers[i].Time - time);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestIdx = i;
                }
            }

            return m_Markers[bestIdx];
        }

        /// <summary>
        /// Gets the upcoming beat marker strictly >= the provided time.
        /// </summary>
        public BeatMarker GetNextBeat(float time)
        {
            EnsureMarkers();
            if (m_Markers == null || m_Markers.Length == 0)
            {
                return CreateSyntheticMarker(time + BeatDuration);
            }

            for (int i = 0; i < m_Markers.Length; i++)
            {
                if (m_Markers[i].Time >= time)
                {
                    return m_Markers[i];
                }
            }

            // Past last marker, wrap to first marker of next loop
            return m_Markers[0];
        }

        public void EnsureMarkers()
        {
            if (m_Markers == null || m_Markers.Length == 0)
            {
                GenerateMarkers();
            }
        }

        private BeatMarker CreateSyntheticMarker(float time)
        {
            float beatDur = BeatDuration;
            float relativeTime = Mathf.Max(0f, time - m_FirstBeatOffset);
            int beatIndex = Mathf.FloorToInt(relativeTime / beatDur);
            int beatInBar = beatIndex % m_BeatsPerBar;
            int barIndex = beatIndex / m_BeatsPerBar;
            bool isDownbeat = (beatInBar == 0);
            bool isStrongBeat = (beatInBar == 0 || beatInBar == 2);
            float strength = isDownbeat ? 1.0f : (isStrongBeat ? 0.75f : 0.50f);

            return new BeatMarker(time, beatIndex, barIndex, beatInBar, strength, isDownbeat, isStrongBeat);
        }
    }
}
