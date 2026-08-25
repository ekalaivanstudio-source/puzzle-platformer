using UnityEngine;

/// <summary>The mutually-exclusive animation states the player can be in.</summary>
public enum PlayerAnimState { Idle, Run, Jump, GroundPound, Push, Dead }

/// <summary>
/// Drives the player's frame-by-frame sprite-sheet animations on a
/// <see cref="SpriteRenderer"/>. <see cref="PlayerController"/> decides the state
/// each frame (from grounded state + velocity) and calls <see cref="Play"/>;
/// this component owns the actual frame stepping.
///
/// Clip rules:
///  • Idle — rotates between the four idle sheets. Each idle plays once, then a
///    *different* idle is chosen at random, giving continuous standing variety.
///  • Run / Jump — loop continuously.
///  • Push — plays once, then PlayerController hands the sprite back to Idle. The shove
///    is a single action, not a cycle, and it is over before the brick starts to slide.
///  • GroundPound — plays once and holds on the last frame, so the dive pose stays
///    held for however long the drop lasts.
///  • Dead — plays once and holds on the last frame.
///
/// A state whose sprite array was never filled in falls back to the nearest populated
/// clip (GroundPound → Jump, Push → Run) rather than freezing the sprite.
///
/// Uses unscaled time so animation keeps playing correctly during slow-motion and
/// the (realtime) death pause — matching <see cref="SpriteSheetAnimator"/>.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle (rotates randomly between these while standing still)")]
    [SerializeField] private Sprite[] m_Idle1;
    [SerializeField] private Sprite[] m_Idle2;
    [SerializeField] private Sprite[] m_Idle3;
    [SerializeField] private Sprite[] m_Idle4;
    [SerializeField] private float m_IdleFps = 6f;

    [Header("Run")]
    [SerializeField] private Sprite[] m_RunFrames;
    [SerializeField] private float m_RunFps = 12f;

    [Header("Jump")]
    [SerializeField] private Sprite[] m_JumpFrames;
    [SerializeField] private float m_JumpFps = 10f;

    [Header("Ground Pound (the dive held through a drop the jump arc couldn't finish)")]
    [SerializeField] private Sprite[] m_GroundPoundFrames;
    [SerializeField] private float m_GroundPoundFps = 12f;

    [Header("Push (shoving a movable brick)")]
    [SerializeField] private Sprite[] m_PushFrames;
    [SerializeField] private float m_PushFps = 10f;

    [Header("Dead")]
    [SerializeField] private Sprite[] m_DeadFrames;
    [SerializeField] private float m_DeadFps = 8f;

    private SpriteRenderer m_Renderer;
    private Sprite[][] m_IdleClips;

    private PlayerAnimState m_State = PlayerAnimState.Idle;
    private int m_ActiveIdleIndex = -1;   // which idle sheet is currently playing

    // Currently-playing clip parameters.
    private Sprite[] m_Frames;
    private float m_Fps;
    private bool m_Loops;

    private int m_Frame;
    private float m_Timer;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Renderer = GetComponent<SpriteRenderer>();
        m_IdleClips = new[] { m_Idle1, m_Idle2, m_Idle3, m_Idle4 };
        StartIdle();
    }

    private void Update()
    {
        if (m_Frames == null || m_Frames.Length == 0) return;

        m_Timer += Time.unscaledDeltaTime;

        float frameDuration = 1f / Mathf.Max(m_Fps, 0.01f);
        if (m_Timer < frameDuration) return;

        // Absorb overflow so a hitch doesn't burst several frames at once.
        m_Timer %= frameDuration;
        m_Frame++;

        if (m_Frame >= m_Frames.Length)
        {
            // Idle: chain to a fresh random idle instead of looping the same sheet.
            if (m_State == PlayerAnimState.Idle) { StartIdle(); return; }

            if (m_Loops)
                m_Frame = 0;
            else
            {
                // Non-looping (Dead): hold the last frame.
                m_Frame = m_Frames.Length - 1;
                ApplyFrame();
                return;
            }
        }

        ApplyFrame();
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Switches to <paramref name="state"/>. Idempotent — repeat calls with the
    /// state already playing are ignored so a looping clip keeps running smoothly.
    /// </summary>
    public void Play(PlayerAnimState state)
    {
        // An unassigned clip is substituted here rather than played: SetClip with an
        // empty array leaves ApplyFrame nothing to write, so the sprite would freeze on
        // whatever frame happened to be showing until the next state change.
        state = ResolveAssigned(state);

        if (state == m_State) return;
        m_State = state;

        switch (state)
        {
            case PlayerAnimState.Idle: StartIdle(); break;
            case PlayerAnimState.Run:  SetClip(m_RunFrames, m_RunFps, loops: true); break;
            case PlayerAnimState.Jump: SetClip(m_JumpFrames, m_JumpFps, loops: true); break;
            case PlayerAnimState.Dead: SetClip(m_DeadFrames, m_DeadFps, loops: false); break;

            // One shove, not a cycle. Looping re-played the brace for as long as the push
            // command lasted, which read as Byte shoving the brick several times. The
            // controller drops the state as soon as this has played through once, so the
            // last-frame hold below only ever covers a rounding frame or two.
            case PlayerAnimState.Push:
                SetClip(m_PushFrames, m_PushFps, loops: false); break;

            // Held on its last frame: the drop it covers has no fixed length, and a
            // looping dive would re-play the wind-up all the way down.
            case PlayerAnimState.GroundPound:
                SetClip(m_GroundPoundFrames, m_GroundPoundFps, loops: false); break;
        }
    }

    // Maps a state whose frames were never assigned onto the closest clip that has some,
    // so a half-wired animator degrades to the old behaviour instead of a stuck sprite.
    private PlayerAnimState ResolveAssigned(PlayerAnimState state)
    {
        if (state == PlayerAnimState.GroundPound && IsEmpty(m_GroundPoundFrames))
            return PlayerAnimState.Jump;
        if (state == PlayerAnimState.Push && IsEmpty(m_PushFrames))
            return PlayerAnimState.Run;
        return state;
    }

    private static bool IsEmpty(Sprite[] frames) => frames == null || frames.Length == 0;

    // ─── Idle rotation ───────────────────────────────────────────────────────

    // Picks an idle sheet (a different one from the current when possible) and plays it once.
    private void StartIdle()
    {
        int next = PickIdleIndex();
        m_ActiveIdleIndex = next;
        // Each idle plays once (loops: false); Update chains to a new idle when it ends.
        SetClip(next >= 0 ? m_IdleClips[next] : null, m_IdleFps, loops: false);
    }

    // Returns a random non-empty idle index, preferring one different from the active clip.
    // Returns -1 if no idle sheet has any frames.
    private int PickIdleIndex()
    {
        // Collect populated idle sheets.
        int count = 0;
        int firstOther = -1;
        for (int i = 0; i < m_IdleClips.Length; i++)
        {
            if (m_IdleClips[i] == null || m_IdleClips[i].Length == 0) continue;
            count++;
            if (i != m_ActiveIdleIndex && firstOther < 0) firstOther = i;
        }

        if (count == 0) return -1;

        // Only the active one is populated (or it's the first pick) — reuse it.
        if (firstOther < 0)
        {
            for (int i = 0; i < m_IdleClips.Length; i++)
                if (m_IdleClips[i] != null && m_IdleClips[i].Length > 0) return i;
        }

        // Pick randomly among populated sheets, retrying until we get one that
        // isn't the current sheet (guaranteed possible since firstOther exists).
        int pick;
        do
        {
            pick = Random.Range(0, m_IdleClips.Length);
        }
        while (m_IdleClips[pick] == null || m_IdleClips[pick].Length == 0 || pick == m_ActiveIdleIndex);

        return pick;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetClip(Sprite[] frames, float fps, bool loops)
    {
        m_Frames = frames;
        m_Fps = fps;
        m_Loops = loops;
        m_Frame = 0;
        m_Timer = 0f;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (m_Frames == null || m_Frames.Length == 0) return;
        m_Frame = Mathf.Clamp(m_Frame, 0, m_Frames.Length - 1);
        m_Renderer.sprite = m_Frames[m_Frame];
    }
}
