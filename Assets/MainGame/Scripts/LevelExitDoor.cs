using System.Collections;
using UnityEngine;

/// <summary>
/// The level's exit door. Touching it while the key is placed wins the level, but the
/// player does NOT stop on contact — they keep walking to <see cref="InteractionPosition"/>
/// first, so the level-complete sequence always plays with the player standing in the
/// doorway instead of frozen wherever their collider happened to clip the door's edge.
///
/// It also owns how the doorway LOOKS, the mirror of <see cref="LevelEntryDoor"/>: the open
/// animation <see cref="KeySlot"/> asks for when the key is placed, and the close
/// <see cref="PlayerController"/> awaits once the player has spun into it. Both live here
/// rather than on the slot so the frames, the renderer and the doorway effect are configured
/// in exactly one place — on the door they belong to.
///
/// The doorway's own trigger is owned here too, opened and shut alongside the frames, so a
/// shut door cannot be walked through and a door that opens can always be entered — the slot
/// no longer has to enable it separately.
///
/// A level with no battery and no socket ticks <see cref="OpensWithoutKey"/>: the doorway
/// starts open, the battery and socket named in Key Puzzle Objects are switched off, and
/// walking in wins. That is the whole conversion — nothing else in the level changes.
///
/// The doorway EFFECT is held back until the doorway is actually standing in the level —
/// see <see cref="SetStanding"/>. On a level that opens without a key the door is open from
/// its first frame, and the effect would otherwise be left glowing in mid-air while
/// <see cref="LevelBuildDirector"/> still has the doorway that owns it scaled to nothing.
///
/// Put this on the object tagged "Door" (the one carrying the door's trigger collider)
/// and point it at an empty child placed on the ground in the middle of the doorway.
/// </summary>
public class LevelExitDoor : MonoBehaviour
{
    [Tooltip("Where the player walks to after touching the open door. Normally an empty " +
             "child sitting on the ground at the centre of the doorway — the player's " +
             "pivot is at their feet, so place it at floor level. Falls back to this " +
             "object's own position when left empty.")]
    [SerializeField] private Transform m_InteractionPoint;

    [Header("Door Animation")]
    [Tooltip("Doorway sprite renderer whose sprite the open and close animations drive.")]
    [SerializeField] private SpriteRenderer m_DoorRenderer;

    [Tooltip("Door open animation frames. Frame 0 is the closed state; the last frame is " +
             "fully open. Closing plays the same frames backwards.")]
    [SerializeField] private Sprite[] m_DoorOpenFrames;

    [Tooltip("Seconds each frame of the door animation is shown.")]
    [SerializeField] private float m_FrameTime = 0.06f;

    [Header("Door Open Effect")]
    [Tooltip("Looping effect sitting in the doorway, shown once the door has finished " +
             "opening and hidden again when it closes or the slot resets. Optional. A child " +
             "of the door authored in place — it is only toggled here, never spawned, so its " +
             "position, scale and sorting are whatever the scene shows.")]
    [SerializeField] private GameObject m_DoorOpenEffect;

    [Header("Doorway Trigger")]
    [Tooltip("The doorway's own trigger — the collider tagged \"Door\" the player walks " +
             "into. Enabled as the door opens and disabled while it is shut, so a shut door " +
             "cannot be walked through. Falls back to the collider on this object.")]
    [SerializeField] private Collider2D m_DoorCollider;

    [Header("Automatic Opening")]
    [Tooltip("Tick this on a level that has no battery and no socket: the doorway simply " +
             "starts open and walking into it completes the level. The key-puzzle objects " +
             "below are switched off with it, so one tick converts a level.")]
    [SerializeField] private bool m_OpensWithoutKey;

    [Tooltip("The key-puzzle objects this door would otherwise wait on — the battery and " +
             "its socket. Deactivated when Opens Without Key is ticked, and left exactly as " +
             "the scene has them otherwise. Wired once on the prefab, so an instance only " +
             "needs the tick.")]
    [SerializeField] private GameObject[] m_KeyPuzzleObjects;

    private Coroutine m_Animation;

    // Whether the doorway is standing in the level yet, and whether the door WANTS its effect
    // showing. The effect is only ever on when both are true, which is the whole of how a
    // door that is open before it has risen keeps its glow to itself.
    //
    // Standing defaults to true so a level with no build behaves exactly as it always has:
    // the director is what says otherwise, and it says it before this component's Start.
    private bool m_IsStanding = true;
    private bool m_EffectWanted;

    /// <summary>
    /// True on a level with no battery and no socket, where the doorway is open from the
    /// start. Read by <see cref="PlayerController"/>, which otherwise only counts a door
    /// touch as a win once the key has been placed — a check no such level could ever pass.
    /// </summary>
    public bool OpensWithoutKey => m_OpensWithoutKey;

    /// <summary>
    /// Seconds <see cref="Open"/> takes to swing from shut to its last frame. Read by
    /// <see cref="KeySlot"/>, which holds the player's queued run until the doorway has
    /// finished opening. One frame short of the frame count, because the last frame is held
    /// rather than waited out.
    /// </summary>
    public float OpenDuration =>
        HasFrames() ? (m_DoorOpenFrames.Length - 1) * Mathf.Max(0f, m_FrameTime) : 0f;

    // Resolved lazily rather than in Awake: KeySlot calls SetClosed from ITS Awake, and the
    // two Awakes run in an undefined order, so a cached-in-Awake reference would still be
    // null for that first call.
    private Collider2D DoorCollider =>
        m_DoorCollider != null ? m_DoorCollider : (m_DoorCollider = GetComponent<Collider2D>());

    // The battery and socket go off before their own Awake runs, so the socket never gets to
    // shut this door or disable the doorway trigger behind the automatic open below.
    private void Awake()
    {
        // However the scene authored it, the doorway effect starts OFF. It belongs to an open
        // doorway standing in a built level, and at Awake there is neither.
        ApplyEffect();

        if (!m_OpensWithoutKey || m_KeyPuzzleObjects == null) return;

        foreach (GameObject puzzleObject in m_KeyPuzzleObjects)
        {
            if (puzzleObject != null)
                puzzleObject.SetActive(false);
        }
    }

    // In Start, not Awake: it has to land after every KeySlot Awake that might have closed
    // this door, and after PlaceableKey's own set-up.
    private void Start()
    {
        if (m_OpensWithoutKey)
            SetOpen();
    }

    /// <summary>
    /// Told by <see cref="LevelBuildDirector"/>: false while the doorway is hidden or still
    /// growing out of the floor, true once it has finished rising and is standing in the
    /// level. A level with no build never calls this, and the doorway is simply there.
    ///
    /// It gates the doorway EFFECT and nothing else. Which frame the door holds and whether
    /// its trigger is live are not this system's business: a door that opens without a key is
    /// genuinely open while it rises, and the player is waiting in the entry doorway either
    /// way. It is only the glow that has to wait for something to glow in.
    /// </summary>
    public void SetStanding(bool standing)
    {
        if (m_IsStanding == standing) return;

        m_IsStanding = standing;
        ApplyEffect();
    }

    /// <summary>World position the player walks to before the level completes.</summary>
    public Vector2 InteractionPosition =>
        m_InteractionPoint != null ? (Vector2)m_InteractionPoint.position : (Vector2)transform.position;

    /// <summary>
    /// Shut, instantly and silently: any animation in flight is dropped, the doorway goes
    /// back to its closed frame and the doorway effect goes out. The slot's own set-up and
    /// reset use this — it is the state a level starts in.
    /// </summary>
    public void SetClosed()
    {
        StopAnimation();
        SetFrame(0);
        SetDoorwayEnabled(false);
        ShowEffect(false);
    }

    /// <summary>
    /// Open, instantly and silently — the mirror of <see cref="SetClosed"/>: the doorway
    /// holds on its last frame with the trigger live and the doorway effect showing, with no
    /// animation to watch. The state a level whose door <see cref="OpensWithoutKey"/> starts
    /// in; there is no key placement to animate away from.
    /// </summary>
    public void SetOpen()
    {
        StopAnimation();
        SetFrame(m_DoorOpenFrames != null ? m_DoorOpenFrames.Length - 1 : 0);
        SetDoorwayEnabled(true);
        ShowEffect(true);
    }

    /// <summary>
    /// Plays the open animation and holds on the last frame, revealing the doorway effect as
    /// it finishes — the doorway only reads as open at that point, so the effect would
    /// otherwise glow through a still-shut door. Called by <see cref="KeySlot"/> when the key
    /// goes in. A door authored with no frames simply lights up straight away.
    /// </summary>
    public void Open()
    {
        // Live from the first frame of the swing, matching the doorway the player can see
        // opening in front of them.
        SetDoorwayEnabled(true);

        if (!HasFrames())
        {
            ShowEffect(true);
            return;
        }

        StopAnimation();
        m_Animation = StartCoroutine(AnimateRoutine(opening: true));
    }

    /// <summary>
    /// Plays the open animation backwards, shutting the doorway behind the player who has
    /// just spun into it, and awaited by <see cref="PlayerController"/>'s win sequence so the
    /// level's outro only carries on once the door is closed. The doorway effect goes out
    /// first: it belongs to an open doorway, and would otherwise glow through the shut door.
    /// </summary>
    public IEnumerator CloseRoutine()
    {
        ShowEffect(false);

        if (!HasFrames())
        {
            SetFrame(0);
            yield break;
        }

        StopAnimation();
        yield return AnimateRoutine(opening: false);
    }

    // Plays m_DoorOpenFrames start to finish, or finish to start when closing, and holds on
    // the frame it ends on.
    private IEnumerator AnimateRoutine(bool opening)
    {
        int count = m_DoorOpenFrames.Length;

        for (int i = 0; i < count; i++)
        {
            SetFrame(opening ? i : count - 1 - i);

            if (i < count - 1 && m_FrameTime > 0f)
                yield return new WaitForSeconds(m_FrameTime);
        }

        if (opening)
            ShowEffect(true);

        m_Animation = null;
    }

    private void StopAnimation()
    {
        if (m_Animation == null) return;

        StopCoroutine(m_Animation);
        m_Animation = null;
    }

    private bool HasFrames() =>
        m_DoorRenderer != null && m_DoorOpenFrames != null && m_DoorOpenFrames.Length > 0;

    private void SetFrame(int index)
    {
        if (!HasFrames()) return;

        m_DoorRenderer.sprite = m_DoorOpenFrames[Mathf.Clamp(index, 0, m_DoorOpenFrames.Length - 1)];
    }

    private void SetDoorwayEnabled(bool enabled)
    {
        if (DoorCollider != null)
            DoorCollider.enabled = enabled;
    }

    private void ShowEffect(bool visible)
    {
        m_EffectWanted = visible;
        ApplyEffect();
    }

    // What the door asked for is remembered separately from what is applied, so a door that
    // opened early lights up the moment the doorway it lives on is standing.
    private void ApplyEffect()
    {
        if (m_DoorOpenEffect != null)
            m_DoorOpenEffect.SetActive(m_EffectWanted && m_IsStanding);
    }

    // Draws the walk destination so the point can be placed without entering play mode.
    private void OnDrawGizmosSelected()
    {
        Vector2 point = InteractionPosition;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point, 0.2f);
        Gizmos.DrawLine(point + Vector2.left * 0.35f, point + Vector2.right * 0.35f);
    }
}
