using UnityEngine;

/// <summary>
/// Keeps the background music track playing across scene loads instead of restarting it.
///
/// The Audio Source Manager is nested inside Managers.prefab, so every level scene brings
/// its own copy of the Music source and the track used to start again from zero on each
/// load. This component makes the first Music source it sees persistent: it detaches to the
/// scene root and survives the load, while every later copy silences itself and is thrown
/// away, so the already-playing track simply keeps running.
///
/// Playback is driven from here rather than by Play On Awake, so a discarded copy never
/// gets the chance to double up on the surviving one.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class BackgroundMusic : MonoBehaviour
{
    public static BackgroundMusic Instance { get; private set; }

    private AudioSource m_Source;

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

        // DontDestroyOnLoad only accepts root objects, and this one is a child of the
        // Audio Source Manager — detach it first, keeping its local transform.
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Play On Awake is off on the prefab so a discarded copy can never double up on the
        // surviving one — the copy that survives starts the track here instead, by which point
        // the source is fully enabled.
        if (Instance != this || m_Source == null) return;
        if (!m_Source.isPlaying) m_Source.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Switches to another track. Asking for the track that is already playing is ignored,
    /// so calling this on every level load leaves the music untouched.
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || m_Source == null) return;
        if (m_Source.clip == clip && m_Source.isPlaying) return;

        m_Source.clip = clip;
        m_Source.Play();
    }

    public void Stop()
    {
        if (m_Source != null) m_Source.Stop();
    }
}
