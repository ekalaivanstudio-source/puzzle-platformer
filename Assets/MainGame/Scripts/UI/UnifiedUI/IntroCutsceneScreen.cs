using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Full-screen intro cutscene played when the player starts a new game.
    ///
    /// Flow:
    ///   1. <see cref="m_LoopClip"/> (Video1) loops while a prompt invites the player to press right.
    ///   2. Pressing right cuts to <see cref="m_StoryClip"/> (Video2), which plays through once.
    ///   3. When it ends, the first playable level is loaded.
    ///
    /// Like <see cref="CompanySplashScreen"/> this lives in the HomeScreen scene rather than a scene
    /// of its own: every level is addressed by its build index and the list is ordered Launcher (0),
    /// levels (1..N), HomeScreen (last) so that level number == build index (see
    /// LevelManager.LoadLevel and GameManager.GoToMainMenu). A scene of its own inserted among the
    /// levels would shift every one of them.
    ///
    /// Each clip gets its own VideoPlayer — swapping clips on a single player blacks the screen out
    /// for the length of a prepare. Both objects sit disabled in the scene and are switched on only
    /// while their clip is wanted, so exactly one decoder is ever alive (see SetPlayerActive).
    ///
    /// Both players use direct audio output. Routing a clip's audio through an AudioSource slaves the
    /// video clock to that source, and a source that never produces samples leaves the player
    /// reporting isPlaying = true while sitting on frame 0 forever.
    ///
    /// Neither player waits for its first frame. waitForFirstFrame holds the clock until the decoder
    /// produces frame 0, and these 1080p clips take seconds to do that — long enough that the clip
    /// appeared frozen on its opening frame and, for Video2, never started at all. See ConfigurePlayer.
    ///
    /// Build the object in the open HomeScreen scene with Tools/Intro/Build Intro Cutscene.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class IntroCutsceneScreen : MonoBehaviour
    {
        /// <summary>
        /// The instance living in the current scene, or null when the home screen has no intro built.
        /// </summary>
        public static IntroCutsceneScreen Instance { get; private set; }

        [Header("Clips")]
        [Tooltip("Looped until the player presses right (Assets/MainGame/Video/Video1.mp4).")]
        [SerializeField] private VideoClip m_LoopClip;

        [Tooltip("Played through once after the player presses right (Assets/MainGame/Video/Video2.mp4).")]
        [SerializeField] private VideoClip m_StoryClip;

        [Header("Scene References")]
        [Tooltip("Plays the looping clip.")]
        [SerializeField] private VideoPlayer m_LoopPlayer;

        [Tooltip("Plays the story clip. Left disabled until the player presses right.")]
        [SerializeField] private VideoPlayer m_StoryPlayer;

        [Tooltip("Full-screen RawImage the video frames are drawn into.")]
        [SerializeField] private RawImage m_Surface;

        [Tooltip("Optional. Letterboxes the surface to the clip aspect ratio instead of stretching it.")]
        [SerializeField] private AspectRatioFitter m_SurfaceFitter;

        [Tooltip("Press-right hint. Shown only while the first clip loops.")]
        [SerializeField] private GameObject m_ContinuePrompt;

        [Header("Timing")]
        [Tooltip("Seconds the cutscene takes to fade up over the home screen. 0 for an instant cut.")]
        [SerializeField] private float m_FadeInDuration = 0.25f;

        [Tooltip("Seconds the fade to the level takes.")]
        [SerializeField] private float m_FadeOutDuration = 0.35f;

        [Tooltip("Seconds to wait for a clip to buffer before giving up and loading the level anyway.")]
        [SerializeField] private float m_PrepareTimeout = 5f;

        [Tooltip("Volume of the clips' own audio tracks.")]
        [Range(0f, 1f)] [SerializeField] private float m_VideoVolume = 1f;

        [Header("Diagnostics")]
        [Tooltip("Logs each phase of the cutscene. Turn off once the intro is behaving.")]
        [SerializeField] private bool m_LogPhases = true;

        private enum Phase { Idle, Preparing, Looping, Story, Finishing }

        private Canvas m_Canvas;
        private CanvasGroup m_CanvasGroup;
        private Phase m_Phase = Phase.Idle;
        private int m_TargetBuildIndex = -1;

        // Whichever player is on screen. Update keeps the RawImage bound to its live texture.
        private VideoPlayer m_ActivePlayer;

        // Set from VideoPlayer callbacks, consumed by PlayRoutine — never load the scene from inside
        // a callback, or the coroutine would keep running against a torn-down player.
        private bool m_StoryFinished;
        private bool m_Aborted;

        // The EventSystem is switched off for the duration so arrow keys drive the cutscene instead
        // of moving the menu selection underneath it. Always restored before the scene changes.
        private EventSystem m_SuspendedEventSystem;

        // The home screen canvases switched off behind the cutscene, remembered so the ones we turned
        // off are the only ones turned back on. Only ever restored when the cutscene returns to the
        // menu; on the normal path the level load takes the whole scene with it.
        private readonly List<Canvas> m_HiddenCanvases = new();

        // ─── Entry point ─────────────────────────────────────────────────────────

        /// <summary>
        /// Plays the intro over the home screen and loads <paramref name="targetBuildIndex"/> when it
        /// finishes. Returns false when there is no usable cutscene in the scene, so callers can fall
        /// back to loading the level directly.
        /// </summary>
        public static bool TryPlay(int targetBuildIndex)
        {
            return Instance != null && Instance.Play(targetBuildIndex);
        }

        private bool Play(int targetBuildIndex)
        {
            if (m_Phase != Phase.Idle) return false;

            if (m_LoopPlayer == null || m_StoryPlayer == null || m_Surface == null ||
                m_LoopClip == null || m_StoryClip == null)
            {
                Debug.LogWarning("[IntroCutsceneScreen] Not fully configured, skipping the intro.", this);
                return false;
            }

            m_TargetBuildIndex = targetBuildIndex;
            StartCoroutine(PlayRoutine());
            return true;
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            m_Canvas = GetComponent<Canvas>();
            m_CanvasGroup = GetComponent<CanvasGroup>();

            // The object stays active so TryPlay can find it, but nothing renders until it plays.
            Hide();

            // Deterministic starting point whichever way the scene was authored — PlayRoutine and
            // PlayStoryClip each switch their own player on when they want it.
            SetPlayerActive(m_LoopPlayer, false);
            SetPlayerActive(m_StoryPlayer, false);

            if (m_ContinuePrompt != null) m_ContinuePrompt.SetActive(false);
        }

        /// <summary>
        /// A VideoPlayer reallocates its internal texture when playback actually starts, so the
        /// reference taken right after Prepare goes stale the moment Play kicks in — which is what
        /// leaves the surface showing a frozen or blank frame. Re-reading it each frame is cheap and
        /// is the only reliable way to stay bound to the live one.
        /// </summary>
        private void Update()
        {
            if (m_ActivePlayer == null || m_Surface == null) return;

            if (m_Surface.texture != m_ActivePlayer.texture)
            {
                m_Surface.texture = m_ActivePlayer.texture;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            UnsubscribeFrom(m_LoopPlayer);
            UnsubscribeFrom(m_StoryPlayer);

            // Guards against leaving the game unclickable, or the menu invisible, if this is torn
            // down mid-cutscene.
            RestoreMenuInput();
            RestoreHomeScreen();
        }

        // ─── Playback ────────────────────────────────────────────────────────────

        private IEnumerator PlayRoutine()
        {
            m_Phase = Phase.Preparing;
            m_StoryFinished = false;
            m_Aborted = false;

            SuspendMenuInput();
            AudioManager.Instance?.StopMusic();

            m_Canvas.enabled = true;
            m_CanvasGroup.alpha = m_FadeInDuration > 0f ? 0f : 1f;
            m_CanvasGroup.blocksRaycasts = true;

            // The story player stays off until PlayStoryClip wants it — see SetPlayerActive.
            SetPlayerActive(m_StoryPlayer, false);
            SetPlayerActive(m_LoopPlayer, true);

            ConfigurePlayer(m_LoopPlayer, m_LoopClip, isLooping: true);

            m_LoopPlayer.Prepare();
            yield return WaitForPrepare(m_LoopPlayer);
            if (m_Aborted) { LoadTarget(); yield break; }

            ShowSurfaceFor(m_LoopPlayer, m_LoopClip);
            ApplyAudioSettings(m_LoopPlayer, m_LoopClip);
            m_LoopPlayer.Play();

            yield return FadeTo(1f, m_FadeInDuration);

            // Only once the cutscene fully covers the screen — pulling the menu before that would
            // show a hole through the fade-in.
            HideHomeScreen();

            m_Phase = Phase.Looping;
            if (m_ContinuePrompt != null) m_ContinuePrompt.SetActive(true);
            Log("looping the first clip, waiting for right");

            while (!m_Aborted && !WasRightPressed()) yield return null;
            if (m_Aborted) { LoadTarget(); yield break; }

            m_Phase = Phase.Story;
            if (m_ContinuePrompt != null) m_ContinuePrompt.SetActive(false);
            Log("right pressed, cutting to the story clip");

            yield return PlayStoryClip();
            if (m_Aborted) { LoadTarget(); yield break; }

            m_Phase = Phase.Finishing;
            yield return FadeTo(0f, m_FadeOutDuration);

            LoadTarget();
        }

        /// <summary>
        /// Cuts from the loop to the story clip and waits for it to finish.
        ///
        /// The clip is prepared here rather than up front alongside the loop. A player left prepared
        /// for however long the player watches the loop can go stale, and Prepare on an
        /// already-prepared player is a no-op, so there is no way to refresh it later. Preparing on
        /// demand is invisible anyway: the loop stays on screen until the buffer is ready.
        ///
        /// The end is not taken on trust either. loopPointReached is the normal signal, but a
        /// non-looping player that stalls or ends without raising it parks on its last frame forever,
        /// so the clip's own duration is used as a backstop. Whatever happens, this hands over to the
        /// level rather than leaving the player stuck watching a frozen frame.
        /// </summary>
        private IEnumerator PlayStoryClip()
        {
            // Switched on only now. Configuring and preparing it earlier would do nothing while the
            // object was still inactive, and preparing it here costs nothing visible anyway.
            SetPlayerActive(m_StoryPlayer, true);
            ConfigurePlayer(m_StoryPlayer, m_StoryClip, isLooping: false);
            m_StoryPlayer.loopPointReached += HandleStoryFinished;

            m_StoryPlayer.Prepare();
            yield return WaitForPrepare(m_StoryPlayer);
            if (m_Aborted) yield break;

            // The loop is finished with, so free its decoder before the story clip asks for one.
            // Everything here happens in a single frame, so the surface never shows a gap.
            m_LoopPlayer.Stop();
            SetPlayerActive(m_LoopPlayer, false);

            ShowSurfaceFor(m_StoryPlayer, m_StoryClip);
            ApplyAudioSettings(m_StoryPlayer, m_StoryClip);
            m_StoryPlayer.Play();

            float deadline = Time.unscaledTime + (float)m_StoryClip.length + m_PrepareTimeout;
            while (!m_StoryFinished && !m_Aborted && Time.unscaledTime < deadline) yield return null;

            Log(m_StoryFinished
                ? "story clip finished"
                : $"story clip hit its {m_StoryClip.length:0.0}s backstop without reporting an end " +
                  $"(playing={m_StoryPlayer.isPlaying} frame={m_StoryPlayer.frame} of {m_StoryPlayer.frameCount})");
        }

        private void Log(string message)
        {
            if (m_LogPhases) Debug.Log($"[IntroCutsceneScreen] {message}", this);
        }

        private IEnumerator WaitForPrepare(VideoPlayer player)
        {
            float deadline = Time.unscaledTime + m_PrepareTimeout;

            while (!player.isPrepared && !m_Aborted && Time.unscaledTime < deadline) yield return null;

            if (!player.isPrepared && !m_Aborted)
            {
                Debug.LogWarning($"[IntroCutsceneScreen] '{player.name}' did not buffer within {m_PrepareTimeout}s.", this);
                m_Aborted = true;
            }
        }

        /// <summary>
        /// Both player objects sit disabled in the scene and are switched on only for as long as
        /// their clip is wanted. A VideoPlayer on an inactive GameObject never prepares and never
        /// decodes, so nothing may be configured, prepared or played until this has been called —
        /// and turning each one off again means only one 1080p decoder is ever alive.
        /// </summary>
        private static void SetPlayerActive(VideoPlayer player, bool active)
        {
            if (player == null || player.gameObject.activeSelf == active) return;

            player.gameObject.SetActive(active);
        }

        private void ConfigurePlayer(VideoPlayer player, VideoClip clip, bool isLooping)
        {
            player.playOnAwake = false;
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.isLooping = isLooping;
            player.renderMode = VideoRenderMode.APIOnly;

            // Never wait for the first frame. With it on, Play() holds the clock until the decoder
            // hands over frame 0 — and these 1080p clips take seconds to produce it, measured at
            // ~4.5s for Video1. Until then the player reports isPlaying = true while frame stays put,
            // nothing advances, and loopPointReached never fires, so Video2 sat on its opening frame
            // for its whole 20s backstop. With it off the clock starts immediately and frames arrive
            // as they decode, which is the behaviour a fade-in over black already hides.
            player.waitForFirstFrame = false;

            player.errorReceived += HandleVideoError;

            // Direct output, not AudioSource output. With AudioSource output the video clock is
            // slaved to that source, so if it never produces samples the player sits at frame 0
            // reporting isPlaying = true forever — which is exactly what Video2 did here. Direct
            // output decodes on its own clock and cannot stall that way.
            player.audioOutputMode = VideoAudioOutputMode.Direct;

            if (clip == null) return;

            for (ushort track = 0; track < clip.audioTrackCount; track++)
            {
                player.EnableAudioTrack(track, true);
            }
        }

        /// <summary>
        /// Direct audio bypasses the AudioListener, so the game's own mute has to be forwarded by
        /// hand rather than being applied for us.
        /// </summary>
        private void ApplyAudioSettings(VideoPlayer player, VideoClip clip)
        {
            if (clip == null) return;

            bool muted = AudioManager.Instance != null && AudioManager.Instance.Muted;

            for (ushort track = 0; track < clip.audioTrackCount; track++)
            {
                player.SetDirectAudioMute(track, muted);
                player.SetDirectAudioVolume(track, m_VideoVolume);
            }
        }

        private void ShowSurfaceFor(VideoPlayer player, VideoClip clip)
        {
            m_ActivePlayer = player;
            m_Surface.texture = player.texture;

            if (m_SurfaceFitter != null && clip != null && clip.height > 0)
            {
                m_SurfaceFitter.aspectRatio = (float)clip.width / clip.height;
            }
        }

        /// <summary>
        /// Matches the game's own Right binding (see the Player map in PlayerInputAction), plus the
        /// left stick so a controller works without reaching for the D-pad.
        /// </summary>
        private static bool WasRightPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) return true;

            Gamepad gamepad = Gamepad.current;
            return gamepad != null &&
                   (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame);
        }

        private void HandleStoryFinished(VideoPlayer source) => m_StoryFinished = true;

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[IntroCutsceneScreen] '{source.name}' failed: {message}", this);
            m_Aborted = true;
        }

        private void UnsubscribeFrom(VideoPlayer player)
        {
            if (player == null) return;
            player.errorReceived -= HandleVideoError;
            player.loopPointReached -= HandleStoryFinished;
        }

        // ─── Finishing ───────────────────────────────────────────────────────────

        private void LoadTarget()
        {
            m_Phase = Phase.Finishing;

            // Cleared before the objects go off, so Update cannot rebind the surface to a texture
            // that is being torn down.
            m_ActivePlayer = null;
            UnsubscribeFrom(m_LoopPlayer);
            UnsubscribeFrom(m_StoryPlayer);
            SetPlayerActive(m_LoopPlayer, false);
            SetPlayerActive(m_StoryPlayer, false);
            RestoreMenuInput();
            Log($"loading build index {m_TargetBuildIndex}");

            if (m_TargetBuildIndex < 0 || m_TargetBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[IntroCutsceneScreen] Build index {m_TargetBuildIndex} is not in the build " +
                               "settings; returning to the home screen.", this);

                // The only path that stays on the menu, so the only one that puts it back.
                RestoreHomeScreen();
                Hide();
                m_Phase = Phase.Idle;
                return;
            }

            SceneManager.LoadScene(m_TargetBuildIndex);
        }

        private void Hide()
        {
            if (m_Canvas != null) m_Canvas.enabled = false;
            if (m_CanvasGroup == null) return;

            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Switches off every home screen canvas so nothing of the menu is left behind the cutscene.
        /// The fade-out at the end drops the cutscene to transparent before the level has loaded, and
        /// without this the home screen buttons show through for about a second.
        ///
        /// Canvases from other scenes are deliberately left alone: the dev build stamp lives on a
        /// DontDestroyOnLoad object, and disabling that here would keep it hidden for the rest of the
        /// session rather than just for the cutscene.
        /// </summary>
        private void HideHomeScreen()
        {
            m_HiddenCanvases.Clear();

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.transform.IsChildOf(transform)) continue;
                if (canvas.gameObject.scene != gameObject.scene) continue;
                if (!canvas.enabled) continue;

                canvas.enabled = false;
                m_HiddenCanvases.Add(canvas);
            }

            Log($"hid {m_HiddenCanvases.Count} home screen canvas(es)");
        }

        private void RestoreHomeScreen()
        {
            foreach (Canvas canvas in m_HiddenCanvases)
            {
                if (canvas != null) canvas.enabled = true;
            }

            m_HiddenCanvases.Clear();
        }

        private void SuspendMenuInput()
        {
            m_SuspendedEventSystem = EventSystem.current;
            if (m_SuspendedEventSystem != null) m_SuspendedEventSystem.enabled = false;
        }

        private void RestoreMenuInput()
        {
            if (m_SuspendedEventSystem == null) return;

            m_SuspendedEventSystem.enabled = true;
            m_SuspendedEventSystem = null;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (duration <= 0f)
            {
                m_CanvasGroup.alpha = targetAlpha;
                yield break;
            }

            float startAlpha = m_CanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                m_CanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            m_CanvasGroup.alpha = targetAlpha;
        }
    }
}
