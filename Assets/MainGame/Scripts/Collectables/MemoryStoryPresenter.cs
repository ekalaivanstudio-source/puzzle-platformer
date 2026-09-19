using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Collectables
{
    /// <summary>
    /// Plays the memory-shard story cutscenes that the player has earned but not yet seen.
    ///
    /// Shards are collected mid-level, but a story is never played there — being pulled out of a
    /// puzzle by a cutscene is exactly what the queue exists to avoid. Instead the unlock is
    /// saved the instant the shard is grabbed, and this drains the queue at the end of the level,
    /// after the doctor's reaction and before the fade out (see the win routine in
    /// <c>PlayerController</c>, which is the only caller).
    ///
    /// Because <see cref="MemoryShardService.PendingStories"/> is derived from the shard count
    /// rather than stored, a story unlocked in a session that was quit before the level ended is
    /// still waiting the next time a level is finished. An unlock cannot be lost, only deferred.
    ///
    /// <b>A story with no clip assigned is skipped and left pending</b>, not marked as seen — so
    /// thresholds can be authored and played against long before the video exists, and the
    /// cutscene plays the first time a level ends after the clip is dropped in.
    ///
    /// The VideoPlayer setup here is deliberately identical to
    /// <see cref="MainGame.UI.Unified.IntroCutsceneScreen"/> — direct audio output, no
    /// wait-for-first-frame, and the surface re-bound every frame. Those three are not style
    /// choices; each one is a bug that screen already hit. See the comments on ConfigurePlayer.
    ///
    /// Build it into every level scene with Tools ▸ Memory Shards ▸ Run Full Setup.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class MemoryStoryPresenter : MonoBehaviour
    {
        /// <summary>The instance in the current scene, or null when this level has no presenter built.</summary>
        public static MemoryStoryPresenter Instance { get; private set; }

        [Header("Scene References")]
        [Tooltip("Plays the story clip.")]
        [SerializeField] private VideoPlayer m_Player;

        [Tooltip("Full-screen RawImage the video frames are drawn into.")]
        [SerializeField] private RawImage m_Surface;

        [Tooltip("Optional. Letterboxes the surface to the clip aspect ratio instead of stretching it.")]
        [SerializeField] private AspectRatioFitter m_SurfaceFitter;

        [Tooltip("Optional 'press to skip' hint, shown for the duration of a clip.")]
        [SerializeField] private GameObject m_SkipPrompt;

        [Header("Timing")]
        [Tooltip("Seconds the cutscene takes to fade up over the finished level.")]
        [SerializeField] private float m_FadeInDuration = 0.35f;

        [Tooltip("Seconds the fade out takes, between stories and at the end.")]
        [SerializeField] private float m_FadeOutDuration = 0.35f;

        [Tooltip("Seconds to wait for a clip to buffer before giving up on it.")]
        [SerializeField] private float m_PrepareTimeout = 5f;

        [Header("Playback")]
        [Tooltip("Volume of the clips' own audio tracks.")]
        [Range(0f, 1f)] [SerializeField] private float m_VideoVolume = 1f;

        [Tooltip("Let the player skip a story with Space / Enter / Escape / gamepad South. " +
                 "A skipped story still counts as seen and does not come back.")]
        [SerializeField] private bool m_AllowSkip = true;

        [Tooltip("Stop the background music while a story plays. The next level starts its own.")]
        [SerializeField] private bool m_StopMusicDuringStory = true;

        [Header("Diagnostics")]
        [Tooltip("Logs each story played. Turn off once the cutscenes are behaving.")]
        [SerializeField] private bool m_LogPhases = true;

        private Canvas m_Canvas;
        private CanvasGroup m_CanvasGroup;

        // Whichever player is on screen. Update keeps the RawImage bound to its live texture.
        private VideoPlayer m_ActivePlayer;

        // Set from VideoPlayer callbacks and consumed by the coroutine — never tear the player
        // down from inside its own callback.
        private bool m_ClipFinished;
        private bool m_Aborted;

        private bool m_IsPlaying;

        /// <summary>True while a story is on screen. Gameplay should stay parked until it clears.</summary>
        public static bool IsPlaying => Instance != null && Instance.m_IsPlaying;

        // ─── Entry point ─────────────────────────────────────────────────────────

        /// <summary>
        /// Plays every story the player has earned and not yet seen, one after another, and
        /// returns when the last one is done.
        ///
        /// Safe to yield on unconditionally: it completes in a single frame when this level has
        /// no presenter, when nothing is pending, or when the pending stories have no clips yet.
        /// That is what lets the win routine call it without knowing anything about shards.
        /// </summary>
        public static IEnumerator PlayPendingRoutine()
        {
            if (Instance == null) yield break;
            yield return Instance.PlayPending();
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[MemoryStoryPresenter] Another instance already exists in this scene.", this);
            Instance = this;

            m_Canvas = GetComponent<Canvas>();
            m_CanvasGroup = GetComponent<CanvasGroup>();

            // The object stays active so PlayPendingRoutine can find it, but nothing renders
            // until a story plays.
            Hide();

            // A VideoPlayer on an inactive GameObject never prepares and never decodes, so it is
            // switched on only for as long as a clip is wanted — and off again afterwards so no
            // decoder is alive during play.
            SetPlayerActive(false);

            if (m_SkipPrompt != null) m_SkipPrompt.SetActive(false);
        }

        /// <summary>
        /// A VideoPlayer reallocates its internal texture when playback actually starts, so the
        /// reference taken right after Prepare goes stale the moment Play kicks in — which is
        /// what leaves the surface showing a frozen or blank frame. Re-reading it each frame is
        /// cheap and is the only reliable way to stay bound to the live one.
        /// </summary>
        private void Update()
        {
            if (m_ActivePlayer == null || m_Surface == null) return;

            if (m_Surface.texture != m_ActivePlayer.texture)
                m_Surface.texture = m_ActivePlayer.texture;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            m_ActivePlayer = null;
            Unsubscribe();
        }

        // ─── Playback ────────────────────────────────────────────────────────────

        private IEnumerator PlayPending()
        {
            if (m_IsPlaying) yield break;

            List<MemoryStoryEntry> pending = MemoryShardService.PendingStories();
            if (pending.Count == 0) yield break;

            // A story whose video has not been made yet is left pending rather than consumed, so
            // nothing is lost by authoring the threshold first. If that leaves nothing playable,
            // this frame is the whole cost.
            var playable = new List<MemoryStoryEntry>();
            foreach (var story in pending)
            {
                if (story.HasClip) playable.Add(story);
                else Log($"'{story.SafeId}' is unlocked but has no clip yet — left pending.");
            }

            if (playable.Count == 0) yield break;

            if (m_Player == null || m_Surface == null)
            {
                Debug.LogWarning("[MemoryStoryPresenter] Not fully configured; skipping the story. " +
                                 "Run Tools ▸ Memory Shards ▸ Run Full Setup.", this);
                yield break;
            }

            m_IsPlaying = true;

            if (m_StopMusicDuringStory) AudioManager.Instance?.StopMusic();

            m_Canvas.enabled = true;
            m_CanvasGroup.blocksRaycasts = true;
            m_CanvasGroup.alpha = m_FadeInDuration > 0f ? 0f : 1f;

            bool fadedUp = false;

            for (int i = 0; i < playable.Count; i++)
            {
                MemoryStoryEntry story = playable[i];

                // The flag is only meaningful once something actually reached the screen — a
                // story that failed to buffer never faded anything up, so the next one still has
                // to.
                bool presented = false;
                yield return PlayOne(story, fadeUp: !fadedUp, result => presented = result);
                fadedUp |= presented;

                // Marked only when it reached the player: finished, skipped, or ran to its
                // backstop. A clip that failed to buffer or errored is left pending and retried
                // at the end of the next level, so a transient failure cannot silently eat story
                // content — the error is logged loudly instead.
                if (presented) MemoryShardService.MarkStoryShown(story);
            }

            yield return FadeTo(0f, m_FadeOutDuration);
            Hide();

            m_IsPlaying = false;
        }

        /// <summary>
        /// Plays one story. <paramref name="onPresented"/> reports whether it actually reached
        /// the screen — false when the clip never buffered or the player errored, which is what
        /// keeps a failed story in the queue instead of consuming it.
        /// </summary>
        private IEnumerator PlayOne(MemoryStoryEntry story, bool fadeUp, System.Action<bool> onPresented)
        {
            m_ClipFinished = false;
            m_Aborted = false;

            VideoClip clip = story.clip;
            Log($"playing '{story.SafeId}' ({story.title}) at {story.requiredShards} shards");

            SetPlayerActive(true);
            ConfigurePlayer(clip);
            m_Player.loopPointReached += HandleClipFinished;

            m_Player.Prepare();
            yield return WaitForPrepare();

            if (m_Aborted)
            {
                Debug.LogWarning($"[MemoryStoryPresenter] '{story.SafeId}' could not be played and stays " +
                                 "queued for the end of the next level.", this);
                StopPlayer();
                onPresented(false);
                yield break;
            }

            ShowSurfaceFor(clip);
            ApplyAudioSettings(clip);
            m_Player.Play();

            if (fadeUp) yield return FadeTo(1f, m_FadeInDuration);

            if (m_SkipPrompt != null) m_SkipPrompt.SetActive(m_AllowSkip);

            // The end is not taken on trust. loopPointReached is the normal signal, but a
            // non-looping player that stalls or ends without raising it parks on its last frame
            // forever, so the clip's own duration is used as a backstop. Whatever happens, this
            // hands control back to the level rather than leaving the player stuck on a frame.
            float deadline = Time.unscaledTime + (float)clip.length + m_PrepareTimeout;
            bool skipped = false;

            while (!m_ClipFinished && !m_Aborted && Time.unscaledTime < deadline)
            {
                if (m_AllowSkip && WasSkipPressed()) { skipped = true; break; }
                yield return null;
            }

            if (m_SkipPrompt != null) m_SkipPrompt.SetActive(false);

            Log(m_ClipFinished ? "clip finished"
                : skipped ? "clip skipped by the player"
                : m_Aborted ? "clip aborted"
                : $"clip hit its {clip.length:0.0}s backstop without reporting an end " +
                  $"(playing={m_Player.isPlaying} frame={m_Player.frame} of {m_Player.frameCount})");

            StopPlayer();

            // It was on screen either way — an error raised mid-playback still showed the player
            // most of the story, and replaying it from the top at the next level would be worse.
            onPresented(true);
        }

        /// <summary>
        /// Stops and releases the decoder between stories. Clearing <see cref="m_ActivePlayer"/>
        /// first stops <see cref="Update"/> rebinding the surface to a texture being torn down.
        /// </summary>
        private void StopPlayer()
        {
            m_ActivePlayer = null;
            Unsubscribe();

            // Switched off rather than left holding a released texture. A RawImage with a null
            // texture draws a solid white quad, so between two back-to-back stories this would
            // flash white — disabling it lets the black backdrop show through instead.
            if (m_Surface != null) m_Surface.enabled = false;

            if (m_Player != null) m_Player.Stop();
            SetPlayerActive(false);
        }

        private IEnumerator WaitForPrepare()
        {
            float deadline = Time.unscaledTime + m_PrepareTimeout;

            while (!m_Player.isPrepared && !m_Aborted && Time.unscaledTime < deadline) yield return null;

            if (!m_Player.isPrepared && !m_Aborted)
            {
                Debug.LogWarning($"[MemoryStoryPresenter] The clip did not buffer within {m_PrepareTimeout}s.", this);
                m_Aborted = true;
            }
        }

        private void ConfigurePlayer(VideoClip clip)
        {
            m_Player.playOnAwake = false;
            m_Player.source = VideoSource.VideoClip;
            m_Player.clip = clip;
            m_Player.isLooping = false;
            m_Player.renderMode = VideoRenderMode.APIOnly;

            // Never wait for the first frame. With it on, Play() holds the clock until the decoder
            // hands over frame 0, which a 1080p clip can take seconds to do — and until then the
            // player reports isPlaying = true while frame stays put and loopPointReached never
            // fires, so the clip sits on its opening frame for its whole backstop. With it off the
            // clock starts immediately and frames arrive as they decode, which the fade-in hides.
            m_Player.waitForFirstFrame = false;

            m_Player.errorReceived += HandleVideoError;

            // Direct output, not AudioSource output. With AudioSource output the video clock is
            // slaved to that source, so if it never produces samples the player sits at frame 0
            // reporting isPlaying = true forever. Direct output decodes on its own clock.
            m_Player.audioOutputMode = VideoAudioOutputMode.Direct;

            if (clip == null) return;

            for (ushort track = 0; track < clip.audioTrackCount; track++)
                m_Player.EnableAudioTrack(track, true);
        }

        /// <summary>
        /// Direct audio bypasses the AudioListener, so the game's own mute has to be forwarded by
        /// hand rather than being applied for us.
        /// </summary>
        private void ApplyAudioSettings(VideoClip clip)
        {
            if (clip == null) return;

            bool muted = AudioManager.Instance != null && AudioManager.Instance.Muted;

            for (ushort track = 0; track < clip.audioTrackCount; track++)
            {
                m_Player.SetDirectAudioMute(track, muted);
                m_Player.SetDirectAudioVolume(track, m_VideoVolume);
            }
        }

        private void ShowSurfaceFor(VideoClip clip)
        {
            m_ActivePlayer = m_Player;
            m_Surface.enabled = true;
            m_Surface.texture = m_Player.texture;

            if (m_SurfaceFitter != null && clip != null && clip.height > 0)
                m_SurfaceFitter.aspectRatio = (float)clip.width / clip.height;
        }

        // ─── Presentation helpers ────────────────────────────────────────────────

        private IEnumerator FadeTo(float target, float duration)
        {
            if (m_CanvasGroup == null) yield break;

            float start = m_CanvasGroup.alpha;

            if (duration <= 0f)
            {
                m_CanvasGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Unscaled: the level is finishing and may well have parked the timescale.
                elapsed += Time.unscaledDeltaTime;
                m_CanvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            m_CanvasGroup.alpha = target;
        }

        private void Hide()
        {
            // Off until a clip is actually on it — see StopPlayer for why a null-textured
            // RawImage must never be left drawing.
            if (m_Surface != null) m_Surface.enabled = false;

            if (m_Canvas != null) m_Canvas.enabled = false;
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// A VideoPlayer on an inactive GameObject never prepares and never decodes, so nothing
        /// may be configured, prepared or played until this has been called with true — and
        /// turning it off again means no decoder is alive outside a story.
        /// </summary>
        private void SetPlayerActive(bool active)
        {
            if (m_Player == null || m_Player.gameObject.activeSelf == active) return;
            m_Player.gameObject.SetActive(active);
        }

        /// <summary>Anything a player would reach for to get past a cutscene.</summary>
        private static bool WasSkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.escapeKey.wasPressedThisFrame)) return true;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.wasPressedThisFrame ||
                 gamepad.startButton.wasPressedThisFrame)) return true;

            Touchscreen touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
        }

        private void HandleClipFinished(VideoPlayer source) => m_ClipFinished = true;

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[MemoryStoryPresenter] '{source.name}' failed: {message}", this);
            m_Aborted = true;
        }

        private void Unsubscribe()
        {
            if (m_Player == null) return;
            m_Player.errorReceived -= HandleVideoError;
            m_Player.loopPointReached -= HandleClipFinished;
        }

        private void Log(string message)
        {
            if (m_LogPhases) Debug.Log($"[MemoryStoryPresenter] {message}", this);
        }
    }
}
