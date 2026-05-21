using UnityEngine;

/// <summary>
/// Singleton audio manager with four dedicated <see cref="AudioSource"/> channels:
/// Background, Player, UI, and Effect. Persists across scene loads via DontDestroyOnLoad.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager m_Instance;

    /// <summary>Global singleton access to the AudioManager.</summary>
    public static AudioManager Instance => m_Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource m_BackgroundSource;
    [SerializeField] private AudioSource m_PlayerSource;
    [SerializeField] private AudioSource m_UISource;
    [SerializeField] private AudioSource m_EffectSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip m_WalkClip;
    [SerializeField] private AudioClip m_JumpClip;
    [SerializeField] private AudioClip m_ButtonClickClip;

    // Tracks whether walk audio is currently active to avoid redundant Play/Stop calls
    private bool m_IsWalking;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject); // Persist music and channels across scene loads

        ValidateSources();
    }

    // Warns in editor if any required AudioSource is missing from the Inspector
    private void ValidateSources()
    {
        if (m_BackgroundSource == null) Debug.LogWarning("[AudioManager] BackgroundSource is not assigned.", this);
        if (m_PlayerSource == null) Debug.LogWarning("[AudioManager] PlayerSource is not assigned.", this);
        if (m_UISource == null) Debug.LogWarning("[AudioManager] UISource is not assigned.", this);
        if (m_EffectSource == null) Debug.LogWarning("[AudioManager] EffectSource is not assigned.", this);
    }

    /// <summary>
    /// Starts or stops the looping walk sound on the player channel.
    /// Uses a state flag to prevent redundant Play/Stop calls every frame.
    /// </summary>
    /// <param name="isActive">True to start walking audio, false to stop it.</param>
    public void PlayPlayerWalk(bool isActive)
    {
        if (m_PlayerSource == null || m_IsWalking == isActive) return;

        m_IsWalking = isActive;

        if (isActive)
        {
            m_PlayerSource.clip = m_WalkClip;
            m_PlayerSource.loop = true;
            m_PlayerSource.Play();
        }
        else
        {
            m_PlayerSource.Stop();
        }
    }

    /// <summary>Plays a one-shot jump sound on the player channel.</summary>
    public void PlayPlayerJump()
    {
        if (m_PlayerSource == null || m_JumpClip == null) return;
        m_PlayerSource.PlayOneShot(m_JumpClip);
    }

    /// <summary>Plays a one-shot death sound on the player channel.</summary>
    public void PlayPlayerDeath(AudioClip clip)
    {
        if (m_PlayerSource == null || clip == null) return;
        m_PlayerSource.Stop();
        m_PlayerSource.PlayOneShot(clip);
    }

    /// <summary>Plays a one-shot UI button click sound.</summary>
    public void PlayButtonClick()
    {
        if (m_UISource == null || m_ButtonClickClip == null) return;
        m_UISource.PlayOneShot(m_ButtonClickClip);
    }

    /// <summary>
    /// Plays a beat tune on the UI channel at the specified pitch.
    /// Stops any currently playing clip on the channel before starting the new one.
    /// </summary>
    /// <param name="clip">The beat audio clip to play.</param>
    /// <param name="pitch">Pitch multiplier (randomized per row).</param>
    public void PlayBeatTune(AudioClip clip, float pitch)
    {
        if (m_UISource == null || clip == null) return;

        m_UISource.Stop();
        m_UISource.clip = clip;
        m_UISource.pitch = pitch;
        m_UISource.Play();
    }
}
