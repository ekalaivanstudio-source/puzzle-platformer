using System.Collections;
using UnityEngine;

/// <summary>
/// The level's entry door — the mirror of <see cref="LevelExitDoor"/>, and the owner of the
/// level's opening beat. The whole sequence runs from here because the player starts the
/// level DISABLED, waiting inside the doorway: a disabled player runs no Start of its own,
/// so something already in the scene has to bring it in. In order:
///
///   1. the screen fades up on a closed door and no player,
///   2. the door plays its open animation,
///   3. the player is enabled on <see cref="InteractionPosition"/>, spins in and moves to
///      its start cell — <see cref="PlayerController.EnterFromDoorRoutine"/>,
///   4. the door plays the same animation backwards, closing behind them.
///
/// Put this on the doorway object and point it at an empty child placed on the ground in the
/// middle of the doorway. One per level: a level without one leaves the intro to
/// <see cref="PlayerController"/>, which fades up and spins the player in where it stands —
/// so the player must be left ENABLED in any level that has no entry door.
/// </summary>
public class LevelEntryDoor : MonoBehaviour
{
    [Tooltip("Where the player appears and spins in before moving to their start cell. " +
             "Normally an empty child sitting on the ground at the centre of the doorway — " +
             "the player's pivot is at their feet, so place it at floor level. Falls back " +
             "to this object's own position when left empty.")]
    [SerializeField] private Transform m_InteractionPoint;

    [Header("Door Animation")]
    [Tooltip("Doorway sprite renderer whose sprite the open and close animations drive.")]
    [SerializeField] private SpriteRenderer m_DoorRenderer;

    [Tooltip("Door open animation frames. Frame 0 is the closed state the level opens on; " +
             "the last frame is fully open. Closing plays the same frames backwards.")]
    [SerializeField] private Sprite[] m_DoorOpenFrames;

    [Tooltip("Seconds each frame of the door animation is shown.")]
    [SerializeField] private float m_FrameTime = 0.06f;

    [Tooltip("Seconds the door stands fully open before the player starts coming out of it.")]
    [SerializeField] private float m_HoldOpen = 0.1f;

    [Tooltip("The player to bring in. Found in the scene when left empty — including while " +
             "disabled, which is how the level normally starts.")]
    [SerializeField] private PlayerController m_Player;

    /// <summary>World position the player spins in on before moving to their start cell.</summary>
    public Vector2 InteractionPosition =>
        m_InteractionPoint != null ? (Vector2)m_InteractionPoint.position : (Vector2)transform.position;

    // Closed before anything renders, so the fade never reveals a door standing open. The
    // authored sprite is whichever frame the doorway was dressed with in the scene.
    private void Awake() => SetFrame(0);

    private IEnumerator Start()
    {
        PlayerController player = ResolvePlayer();

        if (UIManager.Instance != null)
            yield return UIManager.Instance.FadeRoutine(1f, 0f);

        yield return PlayDoorRoutine(opening: true);

        if (m_HoldOpen > 0f)
            yield return new WaitForSecondsRealtime(m_HoldOpen);

        if (player != null)
        {
            // Enabled and handed its arrival point in the same breath: the routine's first
            // statements shrink the body away and park it in the doorway, and nothing has
            // rendered in between, so a player enabled at full size never flashes on screen.
            player.gameObject.SetActive(true);
            yield return player.EnterFromDoorRoutine(InteractionPosition);
        }

        yield return PlayDoorRoutine(opening: false);
    }

    // Plays m_DoorOpenFrames start to finish, or finish to start when closing, and holds on
    // the frame it ends on. Unscaled, like the rest of the intro: the level's opening plays
    // at the same pace whatever the timescale is doing.
    private IEnumerator PlayDoorRoutine(bool opening)
    {
        if (m_DoorRenderer == null || m_DoorOpenFrames == null || m_DoorOpenFrames.Length == 0)
            yield break;

        int count = m_DoorOpenFrames.Length;

        for (int i = 0; i < count; i++)
        {
            SetFrame(opening ? i : count - 1 - i);

            if (i < count - 1 && m_FrameTime > 0f)
                yield return new WaitForSecondsRealtime(m_FrameTime);
        }
    }

    private void SetFrame(int index)
    {
        if (m_DoorRenderer == null || m_DoorOpenFrames == null || m_DoorOpenFrames.Length == 0)
            return;

        m_DoorRenderer.sprite = m_DoorOpenFrames[Mathf.Clamp(index, 0, m_DoorOpenFrames.Length - 1)];
    }

    // Scene-scoped rather than FindAnyObjectByType: the player this door exists to bring in is
    // disabled until step 3, and an inactive-inclusive Unity search also returns the player
    // PREFAB ASSET — whose Awake has never run, so its cached rigidbody is null and whose
    // transform is the asset on disk. See SceneObjects for the whole trap.
    //
    // PlayerController.Instance is preferred over the search when it is already set: it is the
    // player that has actually run its Awake, so a level holding a second, half-initialised
    // copy can't be the one this door tries to bring in.
    private PlayerController ResolvePlayer()
    {
        if (m_Player != null) return m_Player;
        if (PlayerController.Instance != null) return PlayerController.Instance;

        return SceneObjects.FindInActiveScene<PlayerController>();
    }

    // Draws the arrival point so it can be placed without entering play mode. Magenta rather
    // than the exit's cyan, so the two doorways' points are told apart at a glance.
    private void OnDrawGizmosSelected()
    {
        Vector2 point = InteractionPosition;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(point, 0.2f);
        Gizmos.DrawLine(point + Vector2.left * 0.35f, point + Vector2.right * 0.35f);
    }
}
