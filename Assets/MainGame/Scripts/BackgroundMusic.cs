using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the background music playing across scene loads and picks the right track for
/// every scene: the menu track on the Launcher / home screen, and a per-level-range track
/// inside levels (e.g. 1–10, 11–20). Switching tracks fades the old one smoothly down to 0
/// before the new one fades in; moving between two levels that share a track (or reloading
/// a level) leaves the music running untouched.
///
/// The Audio Source Manager is nested inside Managers.prefab, so every level scene brings
/// its own copy of the Music source and the track used to start again from zero on each
/// load. This component makes the first Music source it sees persistent: it detaches to the
/// scene root and survives the load, while every later copy silences itself and is thrown
/// away, so the already-playing track simply keeps running.
///
/// Playback is driven from here rather than by Play On Awake, so a discarded copy never
/// gets the chance to double up on the surviving one.
///
/// Track choice is keyed off the build index, which equals the level number (Launcher is 0,
/// Level1..LevelN are 1..N — see <see cref="GameManager.HomeSceneName"/>). That makes it
/// work no matter how a level was reached: Continue, level select, next level or restart.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class BackgroundMusic : MonoBehaviour
{
    [Serializable]
    public struct LevelRangeTrack
    {
        [Tooltip("First level number (inclusive) that uses this track.")]
        public int FirstLevel;
        [Tooltip("Last level number (inclusive) that uses this track.")]
        public int LastLevel;
        public AudioClip Clip;
    }

    [Header("Tracks")]
    [Tooltip("Played on the Launcher and the home screen.")]
    [SerializeField] private AudioClip m_MenuTrack;
    [Tooltip("Track per level range. Levels outside every range keep whatever is already playing.")]
    [SerializeField] private LevelRangeTrack[] m_LevelTracks = new LevelRangeTrack[0];

    [Header("Fades (seconds, unaffected by pause / timeScale)")]
    [Min(0f)] [SerializeField] private float m_FadeOutTime = 1.5f;
    [Min(0f)] [SerializeField] private float m_FadeInTime = 1.5f;

    public static BackgroundMusic Instance { get; private set; }

    private AudioSource m_Source;

    // Full volume the source was authored with — fades run between 0 and this.
    private float m_BaseVolume = 1f;

    // The clip we are playing or fading towards. Compared against instead of m_Source.clip so
    // a request for the incoming track during a fade does not restart the fade.
    private AudioClip m_TargetClip;
    private Coroutine m_Transition;

    /// <summary>The surviving music source, or null before any scene has loaded one.</summary>
    public static AudioSource Source => Instance != null ? Instance.m_Source : null;

    private void Awake()
    {
        m_Source = GetComponent<AudioSource>();

        if (Instance != null && Instance != this)
        {
            // A later scene brought its own copy. Silence it before it is torn down so the
            // running track is never doubled, then drop it.
            m_Source.Stop();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        m_BaseVolume = m_Source.volume;
        m_Source.loop = true;

        // DontDestroyOnLoad only accepts root objects, and this one is a child of the
        // Audio Source Manager — detach it first, keeping its local transform.
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // Play On Awake is off on the prefab so a discarded copy can never double up on the
        // surviving one — the copy that survives starts the track here instead, by which point
        // the source is fully enabled.
        if (Instance != this || m_Source == null) return;

        ApplyTrackFor(SceneManager.GetActiveScene());

        // A scene with no mapped track (e.g. a test scene outside the build) still gets the
        // source's own authored clip, as before.
        if (m_TargetClip == null && !m_Source.isPlaying) m_Source.Play();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ApplyTrackFor(scene);
    }

    private void ApplyTrackFor(Scene scene)
    {
        AudioClip clip = ResolveTrack(scene);
        if (clip != null) Play(clip);
    }

    private AudioClip ResolveTrack(Scene scene)
    {
        if (scene.buildIndex == 0 || scene.name == GameManager.HomeSceneName) return m_MenuTrack;

        int level = scene.buildIndex;
        if (level < 1) return null;

        foreach (LevelRangeTrack range in m_LevelTracks)
        {
            if (level >= range.FirstLevel && level <= range.LastLevel) return range.Clip;
        }
        return null;
    }

    /// <summary>
    /// Switches to another track, fading the current one out and the new one in. Asking for
    /// the track that is already playing (or being faded to) is ignored, so calling this on
    /// every level load leaves the music untouched.
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || m_Source == null) return;
        if (m_TargetClip == clip && m_Source.isPlaying) return;

        m_TargetClip = clip;
        if (m_Transition != null) StopCoroutine(m_Transition);
        m_Transition = StartCoroutine(TransitionTo(clip));
    }

    public void Stop()
    {
        if (m_Transition != null) StopCoroutine(m_Transition);
        m_Transition = null;
        m_TargetClip = null;
        if (m_Source != null) m_Source.Stop();
    }

    private IEnumerator TransitionTo(AudioClip clip)
    {
        // Already on this clip (e.g. it was fading out and got requested again) — just
        // bring it back up instead of restarting it from the top.
        bool sameClip = m_Source.clip == clip && m_Source.isPlaying;

        if (!sameClip && m_Source.isPlaying)
        {
            // Scale by the current level so an interrupted fade does not jump.
            float outTime = m_BaseVolume > 0f ? m_FadeOutTime * (m_Source.volume / m_BaseVolume) : 0f;
            yield return FadeVolume(0f, outTime);
            m_Source.Stop();
        }

        if (!sameClip)
        {
            m_Source.volume = 0f;
            m_Source.clip = clip;
            m_Source.Play();
        }

        yield return FadeVolume(m_BaseVolume, m_FadeInTime);
        m_Transition = null;
    }

    private IEnumerator FadeVolume(float target, float duration)
    {
        float start = m_Source.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            m_Source.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        m_Source.volume = target;
    }
}
