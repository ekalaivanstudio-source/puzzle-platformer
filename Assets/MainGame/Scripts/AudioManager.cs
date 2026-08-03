using UnityEngine;

/// <summary>
/// Central singleton that owns and plays every sound in the game. No other script
/// holds an AudioSource or AudioClip — they call <c>AudioManager.Instance.PlayXxx()</c>.
///
/// It manages four auto-created AudioSources:
///   • Music   — looping background track.
///   • SFX     — one-shot effects via PlayOneShot (jump, pickup, brick, UI, …).
///   • Walk    — looping footstep sound, toggled while the player moves.
///   • Laser   — looping laser hum, reference-counted across all active LaserShooters.
///
/// Assign the clips in the Inspector. Any clip left empty is simply skipped, so the
/// game runs fine before audio is added.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ─── Volumes ──────────────────────────────────────────────────────────────

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float m_MasterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float m_MusicVolume = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float m_SfxVolume = 1f;
    [Tooltip("Volume of UI sounds (button clicks, queue, submit…), separate from gameplay SFX.")]
    [Range(0f, 1f)] [SerializeField] private float m_UiVolume = 1f;
    [Tooltip("When true, all audio is silenced regardless of the individual volumes.")]
    [SerializeField] private bool m_Muted = false;

    // ─── Clips ────────────────────────────────────────────────────────────────

    [Header("Music")]
    [SerializeField] private AudioClip m_MusicTrack;
    [SerializeField] private bool m_PlayMusicOnStart = true;

    [Header("Player")]
    [SerializeField] private AudioClip m_JumpClip;
    [Tooltip("Looping footstep clip played while the player walks.")]
    [SerializeField] private AudioClip m_WalkLoopClip;
    [SerializeField] private AudioClip m_DeathClip;
    [SerializeField] private AudioClip m_WinClip;

    [Header("Items / Keys")]
    [Tooltip("Played when the player picks up / collects a key.")]
    [SerializeField] private AudioClip m_PickupClip;
    [Tooltip("Played when a carried key is dropped into a slot.")]
    [SerializeField] private AudioClip m_KeyPlacedClip;
    [SerializeField] private AudioClip m_DoorOpenClip;

    [Header("Bricks")]
    [SerializeField] private AudioClip m_BrickPushClip;
    [SerializeField] private AudioClip m_BrickDestroyClip;

    [Header("Enemy")]
    [SerializeField] private AudioClip m_EnemyDeathClip;

    [Header("Laser")]
    [Tooltip("Continuous hum played while any laser beam is active.")]
    [SerializeField] private AudioClip m_LaserLoopClip;
    [Tooltip("Played when the player moves a laser redirector one step.")]
    [SerializeField] private AudioClip m_LaserMoveClip;
    [Tooltip("Played when the player rotates a laser redirector.")]
    [SerializeField] private AudioClip m_LaserRotateClip;

    [Header("UI / Input")]
    [Tooltip("Played when an action is queued (Left/Right/Jump/Interact).")]
    [SerializeField] private AudioClip m_QueueClip;
    [SerializeField] private AudioClip m_SubmitClip;
    [SerializeField] private AudioClip m_UndoClip;
    [SerializeField] private AudioClip m_ClearClip;
    [Tooltip("Generic UI button click.")]
    [SerializeField] private AudioClip m_ButtonClip;
    [Tooltip("Played when entering a laser redirector control mode.")]
    [SerializeField] private AudioClip m_ControlEnterClip;
    [SerializeField] private AudioClip m_ControlExitClip;
    [SerializeField] private AudioClip m_LevelCompleteClip;

    // ─── Runtime ──────────────────────────────────────────────────────────────

    private AudioSource m_MusicSource;
    public AudioSource m_SfxSource;
    public AudioSource m_VoiceSource;
    private AudioSource m_WalkSource;
    private AudioSource m_LaserSource;
    private AudioSource m_UiSource;

    // Number of laser beams currently asking for the hum. The loop plays while > 0
    // so several LaserShooters share a single looping source without cutting it off.
    private int m_LaserActiveCount;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        m_MusicSource = CreateSource(true);
       // m_SfxSource   = CreateSource(false);
        m_WalkSource  = CreateSource(true);
        m_LaserSource = CreateSource(true);
        m_UiSource    = CreateSource(false);
      //  ApplyVolumes();
    }

    private void Start()
    {
        if (m_PlayMusicOnStart) PlayMusic(m_MusicTrack);
    }

    private void OnValidate()
    {
        // Keep live sources in sync while tweaking volumes in the Inspector at play time.
        if (Application.isPlaying && m_MusicSource != null) ApplyVolumes();
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        return src;
    }

    private void ApplyVolumes()
    {
        // A single mute gate multiplies through, so unmuting restores the prior levels.
        float master = m_Muted ? 0f : m_MasterVolume;

        if (m_MusicSource != null) m_MusicSource.volume = master * m_MusicVolume;
        float sfx = master * m_SfxVolume;
        if (m_SfxSource != null)   m_SfxSource.volume = sfx;
        if (m_WalkSource != null)  m_WalkSource.volume = sfx;
        if (m_LaserSource != null) m_LaserSource.volume = sfx;
        if (m_UiSource != null)    m_UiSource.volume = master * m_UiVolume;
    }

    // ─── Volume control (used by the settings system) ───────────────────────────

    /// <summary>Master volume, 0..1. Applied immediately to every channel.</summary>
    public float MasterVolume { get => m_MasterVolume; set { m_MasterVolume = Mathf.Clamp01(value); ApplyVolumes(); } }
    public float MusicVolume  { get => m_MusicVolume;  set { m_MusicVolume  = Mathf.Clamp01(value); ApplyVolumes(); } }
    public float SfxVolume    { get => m_SfxVolume;    set { m_SfxVolume    = Mathf.Clamp01(value); ApplyVolumes(); } }
    public float UiVolume     { get => m_UiVolume;     set { m_UiVolume     = Mathf.Clamp01(value); ApplyVolumes(); } }

    /// <summary>Silences/unsilences all audio without losing the individual volume levels.</summary>
    public bool Muted { get => m_Muted; set { m_Muted = value; ApplyVolumes(); } }

    // ─── Generic API ──────────────────────────────────────────────────────────

    /// <summary>Plays a one-shot sound effect. Null clips are ignored.</summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || m_SfxSource == null) return;
        m_SfxSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || m_MusicSource == null) return;
        m_MusicSource.clip = clip;
        m_MusicSource.Play();
    }
    public void PlayVoice(AudioClip clip)
    {
        if (clip == null || m_VoiceSource == null) return;
        m_VoiceSource.clip = clip;
        m_VoiceSource.Play();
    }

    public void StopMusic()
    {
        if (m_MusicSource != null) m_MusicSource.Stop();
    }

    // ─── Player ───────────────────────────────────────────────────────────────

    public void PlayJump()  => PlaySfx(m_JumpClip);
    public void PlayDeath() => PlaySfx(m_DeathClip);
    public void PlayWin()   => PlaySfx(m_WinClip);

    /// <summary>Starts or stops the looping footstep sound. Safe to call every frame.</summary>
    public void SetWalking(bool walking)
    {
        if (m_WalkSource == null || m_WalkLoopClip == null) return;

        if (walking)
        {
            if (!m_WalkSource.isPlaying)
            {
                m_WalkSource.clip = m_WalkLoopClip;
                m_WalkSource.Play();
            }
        }
        else if (m_WalkSource.isPlaying)
        {
            m_WalkSource.Stop();
        }
    }

    // ─── Items / Keys ───────────────────────────────────────────────────────────

    public void PlayPickup()    => PlaySfx(m_PickupClip);
    public void PlayKeyPlaced() => PlaySfx(m_KeyPlacedClip);
    public void PlayDoorOpen()  => PlaySfx(m_DoorOpenClip);

    // ─── Bricks ─────────────────────────────────────────────────────────────────

    public void PlayBrickPush()    => PlaySfx(m_BrickPushClip);
    public void PlayBrickDestroy() => PlaySfx(m_BrickDestroyClip);

    // ─── Enemy ──────────────────────────────────────────────────────────────────

    public void PlayEnemyDeath() => PlaySfx(m_EnemyDeathClip);

    // ─── Laser ──────────────────────────────────────────────────────────────────

    public void PlayLaserMove()   => PlaySfx(m_LaserMoveClip);
    public void PlayLaserRotate() => PlaySfx(m_LaserRotateClip);

    /// <summary>
    /// Reference-counted control of the looping laser hum. Each active beam calls this
    /// with <c>true</c>; the hum plays while at least one beam is active and stops when
    /// the last one is removed.
    /// </summary>
    public void NotifyLaserActive(bool active)
    {
        m_LaserActiveCount = Mathf.Max(0, m_LaserActiveCount + (active ? 1 : -1));

        if (m_LaserSource == null || m_LaserLoopClip == null) return;

        bool shouldPlay = m_LaserActiveCount > 0;
        if (shouldPlay && !m_LaserSource.isPlaying)
        {
            m_LaserSource.clip = m_LaserLoopClip;
            m_LaserSource.Play();
        }
        else if (!shouldPlay && m_LaserSource.isPlaying)
        {
            m_LaserSource.Stop();
        }
    }

    // ─── UI / Input ─────────────────────────────────────────────────────────────

    /// <summary>Plays a one-shot UI sound on the dedicated UI channel (its own volume slider).</summary>
    public void PlayUi(AudioClip clip)
    {
        if (clip == null || m_UiSource == null) return;
        m_UiSource.PlayOneShot(clip);
    }

    public void PlayQueue()         => PlayUi(m_QueueClip);
    public void PlaySubmit()        => PlayUi(m_SubmitClip);
    public void PlayUndo()          => PlayUi(m_UndoClip);
    public void PlayClear()         => PlayUi(m_ClearClip);
    public void PlayButton()        => PlayUi(m_ButtonClip);
    public void PlayControlEnter()  => PlayUi(m_ControlEnterClip);
    public void PlayControlExit()   => PlayUi(m_ControlExitClip);
    public void PlayLevelComplete() => PlaySfx(m_LevelCompleteClip);
}
