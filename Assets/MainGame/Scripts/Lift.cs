using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lift that collects the player and carries them somewhere else.
///
/// Three phases, in order: the player walks into the lift's capture zone and is PULLED to
/// the hold point; the lift TRAVELS its waypoints with the player pinned aboard; the player
/// is PUSHED OUT to the exit point and handed back. If the lift interrupted a run, the rest
/// of the queued commands carry on from wherever it put the player down — so a lift is a
/// piece of the route the player has to plan around, not a dead end.
///
/// The ride owns the player outright (see <see cref="PlayerController.BeginExternalRide"/>):
/// the collider comes off and hazards go quiet for the duration, so a lift may be routed
/// straight through a laser without having to switch the beam off. The player is put down on
/// a cell centre whatever the exit point's exact position, because the grid is what every
/// later ground and blocking check reads.
///
/// Scene setup:
///   • Put this on the lift body. A collider is NOT required — the capture zone is a box
///     drawn by this component, not a trigger. Add one only if you want the lift to be
///     solid, in which case put it on a Ground layer so the player can stand on it.
///   • Hold Point: a CHILD of the lift, marking where the rider sits. It has to be a child
///     or it will not travel with the lift, and the player would be left behind at a fixed
///     point. Left empty, the lift's own origin is used.
///   • Travel Waypoints: world transforms, in the order the lift visits them — NOT children
///     of the lift, or they would move as it chases them. Leave empty for a lift that does
///     not go anywhere and simply moves the player from its zone to its exit point.
///   • Exit Point: where the player is set down at the end of the ride. Put it on a cell
///     centre on solid ground — the Scene gizmo draws the cell the player will actually
///     land on. Left empty, the player is released at the hold point.
///
/// The lift returns to where it started once the rider is off, so the same lift can be
/// ridden again later in the same run. It also goes home whenever the player does — on a
/// restart (<see cref="GameManager.OnFullReset"/>) and on any return to spawn
/// (<see cref="GameManager.OnPlayerRespawn"/>) — but NOT on a plain turn reset, so a
/// checkpoint lever ending a run does not yank a lift out from under the player.
/// </summary>
public class Lift : MonoBehaviour
{
    private enum LiftState
    {
        Idle,       // parked, watching its zone
        Carrying,   // pulling in, travelling, or pushing out
        Returning   // rider is off, heading home
    }

    [Header("Capture Zone")]
    [Tooltip("Centre of the zone that catches the player, as an offset from the lift. The " +
             "zone travels with the lift.")]
    [SerializeField] private Vector2 m_CaptureZoneOffset = Vector2.zero;

    [Tooltip("Size of the capture zone in world units. Roughly one cell wider and taller " +
             "than the lift reads as stepping into it rather than walking past it.")]
    [SerializeField] private Vector2 m_CaptureZoneSize = new Vector2(1.5f, 2f);

    [Tooltip("Tag on the player. Nothing else is ever picked up — bricks and enemies walk " +
             "through the zone untouched.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("Pull In")]
    [Tooltip("Speed (units/sec) the player is drawn from wherever they were standing to the " +
             "hold point.")]
    [SerializeField] private float m_PullSpeed = 6f;

    [Tooltip("Transform the rider is held at while aboard. Must be a child of the lift. " +
             "Left empty, the lift's own position is used.")]
    [SerializeField] private Transform m_HoldPoint;

    [Tooltip("Seconds the lift waits with the player aboard before setting off, so the pull " +
             "and the departure read as two separate beats.")]
    [SerializeField] private float m_BoardPause = 0.2f;

    [Header("Travel")]
    [Tooltip("Where the lift goes with the player aboard, in visiting order. World " +
             "transforms — not children of the lift.")]
    [SerializeField] private Transform[] m_TravelWaypoints;

    [Tooltip("Travel speed (units/sec) with the player aboard.")]
    [SerializeField] private float m_TravelSpeed = 3f;

    [Tooltip("Seconds the lift holds at the far end before letting the player out.")]
    [SerializeField] private float m_ArrivePause = 0.15f;

    [Header("Push Out")]
    [Tooltip("Where the player is set down. Put it on a cell centre on solid ground — the " +
             "gizmo shows the cell they will actually land on. Left empty, they are " +
             "released at the hold point.")]
    [SerializeField] private Transform m_ExitPoint;

    [Tooltip("Speed (units/sec) the player is pushed out of the lift at.")]
    [SerializeField] private float m_PushSpeed = 6f;

    [Header("Return")]
    [Tooltip("Send the lift back to where it started once the rider is off, so it can be " +
             "ridden again. Untick for a one-way lift that stays at the far end.")]
    [SerializeField] private bool m_ReturnHome = true;

    [Tooltip("Seconds the lift waits at the far end before heading back, so it does not " +
             "leave on the same frame the player steps off it.")]
    [SerializeField] private float m_ReturnDelay = 0.3f;

    [Tooltip("Speed (units/sec) of the empty return trip. 0 uses Travel Speed.")]
    [SerializeField] private float m_ReturnSpeed = 0f;

    [Header("Audio")]
    [Tooltip("Optional. Played when the lift takes hold of the player.")]
    [SerializeField] private AudioClip m_PullSfx;

    [Tooltip("Optional. Played when the player is set down at the far end.")]
    [SerializeField] private AudioClip m_ReleaseSfx;

    // ─── State ──────────────────────────────────────────────────────────────────

    private LiftState m_State = LiftState.Idle;
    private Vector3 m_HomePosition;
    private Coroutine m_Routine;
    private PlayerController m_Rider;

    // Set the moment a rider is set down. Blocks the lift from grabbing the same player
    // straight back off its own exit point — an exit inside the capture zone would otherwise
    // loop the ride forever. Cleared on the first frame the zone is empty, so the player has
    // to walk out and back in to ride again.
    private bool m_AwaitingZoneExit;

    // Reused so the per-frame zone check allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_ZoneFilter;

    // Distance at which a leg counts as finished.
    private const float k_Arrival = 0.01f;

    // Wall-clock cap on any one leg of the ride. A hold point that cannot be reached, a
    // waypoint parented to the lift so it runs away from it — without this the player would
    // sit inside a lift that never arrives, with the rest of their turn frozen behind it.
    // Ten seconds is far longer than any authored leg and far shorter than "hung".
    private const float k_LegTimeout = 10f;

    /// <summary>True while the lift has the player and is carrying them.</summary>
    public bool IsCarrying => m_State == LiftState.Carrying;

    // ─── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_HomePosition = transform.position;

        // No layer mask: catch everything in the box and decide by tag below. A lift that
        // silently never fires because the player sits on an unexpected layer is a far
        // worse failure than a couple of extra colliders in the results list.
        m_ZoneFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        if (m_HoldPoint != null && !m_HoldPoint.IsChildOf(transform))
            Debug.LogWarning(
                "[Lift] Hold Point is not a child of the lift, so the rider will not travel " +
                "with it — they will be pulled to a fixed point and left there while the " +
                "lift drives off. Parent it under the lift.", this);
    }

    private void OnValidate()
    {
        if (m_PullSpeed <= 0f)   m_PullSpeed = 6f;
        if (m_TravelSpeed <= 0f) m_TravelSpeed = 3f;
        if (m_PushSpeed <= 0f)   m_PushSpeed = 6f;
        if (m_ReturnSpeed < 0f)  m_ReturnSpeed = 0f;
        if (m_BoardPause < 0f)   m_BoardPause = 0f;
        if (m_ArrivePause < 0f)  m_ArrivePause = 0f;
        if (m_ReturnDelay < 0f)  m_ReturnDelay = 0f;
        if (m_CaptureZoneSize.x <= 0f) m_CaptureZoneSize.x = 1.5f;
        if (m_CaptureZoneSize.y <= 0f) m_CaptureZoneSize.y = 2f;
    }

    private void OnEnable()
    {
        // Home when the player is home: an explicit restart, and every return to spawn.
        // Deliberately NOT OnTurnReset — a checkpoint lever ends the turn while leaving the
        // player standing in the level, and a lift they are riding must not vanish from
        // under them because of it.
        GameManager.OnFullReset += ResetLift;
        GameManager.OnPlayerRespawn += ResetLift;
    }

    private void OnDisable()
    {
        GameManager.OnFullReset -= ResetLift;
        GameManager.OnPlayerRespawn -= ResetLift;

        // Disabled mid-ride, the coroutine dies where it stands and nothing would ever hand
        // the player back — they would be left collider-less and frozen, with the turn
        // waiting on a lift that is no longer running.
        ReleaseRider(resumeSequence: false);
        m_State = LiftState.Idle;
        m_Routine = null;
    }

    private void ResetLift()
    {
        if (m_Routine != null)
        {
            StopCoroutine(m_Routine);
            m_Routine = null;
        }

        // The reset already decides what happens to the turn — resuming a sequence here
        // would run commands over the top of it.
        ReleaseRider(resumeSequence: false);

        transform.position = m_HomePosition;
        m_State = LiftState.Idle;
        m_AwaitingZoneExit = false;
    }

    // ─── Catching the player ────────────────────────────────────────────────────

    private void Update()
    {
        if (m_State != LiftState.Idle) return;

        bool inZone = TryFindPlayer(out PlayerController player);

        // Just set someone down: wait for them to clear the zone before offering a ride
        // again, or a lift whose exit point sits in its own doorway never lets go.
        if (m_AwaitingZoneExit)
        {
            if (!inZone) m_AwaitingZoneExit = false;
            return;
        }

        if (!inZone) return;

        // Refused: the player is dying, winning, spinning through a portal, or already on
        // another ride. Nothing is latched, so the lift simply asks again next frame.
        if (!player.BeginExternalRide()) return;

        m_Rider = player;
        m_State = LiftState.Carrying;
        m_Routine = StartCoroutine(RideRoutine(player));
    }

    // The player is moved with MovePosition on a kinematic body, and the lift itself is
    // moved by writing transform.position — neither reliably produces trigger callbacks
    // against the other, so the zone is polled rather than left to OnTriggerEnter2D. This is
    // the same approach InvisibleLockPoint takes, for the same reason.
    private bool TryFindPlayer(out PlayerController player)
    {
        player = null;

        int count = Physics2D.OverlapBox(
            ZoneCentre, m_CaptureZoneSize, 0f, m_ZoneFilter, m_OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            if (!string.IsNullOrEmpty(m_PlayerTag) && !hit.CompareTag(m_PlayerTag)) continue;

            player = hit.GetComponentInParent<PlayerController>();
            if (player != null) return true;
        }

        return false;
    }

    // ─── The ride ───────────────────────────────────────────────────────────────

    private IEnumerator RideRoutine(PlayerController player)
    {
        AudioManager.Instance?.PlaySfx(m_PullSfx);

        // 1 — draw the player in from wherever the zone caught them.
        player.FaceTowards(HoldPosition.x);
        yield return PullPlayerIn(player);

        // 2 — a beat aboard before the lift sets off.
        yield return PinRider(player, m_BoardPause);

        // 3 — travel, with the player pinned to the hold point every physics step. The hold
        //     point is a child of the lift, so this is what carries them along.
        if (m_TravelWaypoints != null)
        {
            foreach (Transform stop in m_TravelWaypoints)
            {
                if (stop == null) continue;
                yield return MoveLift(stop.position, m_TravelSpeed, player);
            }
        }

        // 4 — a beat at the far end before the doors open.
        yield return PinRider(player, m_ArrivePause);

        // 5 — push the player out onto the exit point.
        Vector2 exit = ExitPosition;
        player.FaceTowards(exit.x);
        yield return PushPlayerOut(player, exit);

        // 6 — hand the body back. From here the turn is the player's again: the commands
        //     queued behind the one this ride interrupted run on from the exit point.
        AudioManager.Instance?.PlaySfx(m_ReleaseSfx);
        ReleaseRider(resumeSequence: true);
        m_AwaitingZoneExit = true;

        // 7 — go home empty, so the lift is available for the rest of the run.
        if (m_ReturnHome)
        {
            m_State = LiftState.Returning;
            yield return ReturnHomeRoutine();
        }

        m_State = LiftState.Idle;
        m_Routine = null;
    }

    // Drawn in one straight line: the zone is right next to the lift, so there is nothing to
    // path around, and an arc here would only read as the player being flung.
    private IEnumerator PullPlayerIn(PlayerController player)
    {
        // Tracked locally rather than read back off the body each step. MovePosition is
        // deferred to the physics step, so reading the transform in the same step returns
        // the position from before the move and the pull would crawl.
        Vector2 position = player.transform.position;
        float elapsed = 0f;

        while (Vector2.Distance(position, HoldPosition) > k_Arrival && elapsed < k_LegTimeout)
        {
            position = Vector2.MoveTowards(position, HoldPosition, m_PullSpeed * Time.fixedDeltaTime);
            player.RideTo(position);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        player.RideTo(HoldPosition);
    }

    private IEnumerator PushPlayerOut(PlayerController player, Vector2 exit)
    {
        Vector2 position = HoldPosition;
        float elapsed = 0f;

        while (Vector2.Distance(position, exit) > k_Arrival && elapsed < k_LegTimeout)
        {
            position = Vector2.MoveTowards(position, exit, m_PushSpeed * Time.fixedDeltaTime);
            player.RideTo(position);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        player.RideTo(exit);
    }

    // Holds the rider on the hold point for a beat. The pin has to keep running through the
    // pause: the player is a kinematic body with its own FixedUpdate, and a pause that
    // simply waited would be a pause in which nobody was writing its position.
    private IEnumerator PinRider(PlayerController player, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            player.RideTo(HoldPosition);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }
    }

    // Moves the lift to a world point, carrying <paramref name="rider"/> if there is one.
    // Physics-stepped so the lift and the body it is moving advance together — driving the
    // lift from Update would leave the rider a frame behind it and visibly trailing.
    private IEnumerator MoveLift(Vector3 target, float speed, PlayerController rider)
    {
        // The lift keeps its own sorting depth: a waypoint authored at a different z would
        // otherwise push the lift, and the player pinned to it, in front of or behind the
        // level.
        target.z = transform.position.z;

        float elapsed = 0f;
        while ((transform.position - target).sqrMagnitude > k_Arrival * k_Arrival && elapsed < k_LegTimeout)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);
            if (rider != null) rider.RideTo(HoldPosition);
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        transform.position = target;
        if (rider != null) rider.RideTo(HoldPosition);
    }

    private IEnumerator ReturnHomeRoutine()
    {
        if (m_ReturnDelay > 0f) yield return new WaitForSeconds(m_ReturnDelay);

        float speed = m_ReturnSpeed > 0f ? m_ReturnSpeed : m_TravelSpeed;

        // Retrace the outbound waypoints backwards rather than driving straight home: a lift
        // that went round a corner has to come back round it, not through the wall. The last
        // waypoint is skipped — the lift is standing on it.
        if (m_TravelWaypoints != null)
        {
            for (int i = m_TravelWaypoints.Length - 2; i >= 0; i--)
            {
                Transform stop = m_TravelWaypoints[i];
                if (stop == null) continue;
                yield return MoveLift(stop.position, speed, null);
            }
        }

        yield return MoveLift(m_HomePosition, speed, null);
    }

    // Hands the body back, if this lift is the one holding it. Safe to call when there is no
    // rider, which is what lets the reset and disable paths call it unconditionally.
    private void ReleaseRider(bool resumeSequence)
    {
        if (m_Rider == null) return;

        m_Rider.EndExternalRide(resumeSequence);
        m_Rider = null;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private Vector2 ZoneCentre => (Vector2)transform.position + m_CaptureZoneOffset;

    private Vector2 HoldPosition =>
        m_HoldPoint != null ? (Vector2)m_HoldPoint.position : (Vector2)transform.position;

    private Vector2 ExitPosition =>
        m_ExitPoint != null ? (Vector2)m_ExitPoint.position : HoldPosition;

    // ─── Gizmos ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 home = Application.isPlaying ? m_HomePosition : transform.position;

        // Capture zone.
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(ZoneCentre, m_CaptureZoneSize);

        // Hold point.
        if (m_HoldPoint != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(m_HoldPoint.position, 0.15f);
        }

        // Travel path, home first.
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Vector3 previous = home;
        if (m_TravelWaypoints != null)
        {
            foreach (Transform stop in m_TravelWaypoints)
            {
                if (stop == null) continue;
                Gizmos.DrawLine(previous, stop.position);
                Gizmos.DrawWireSphere(stop.position, 0.15f);
                previous = stop.position;
            }
        }

        // Exit point, and the cell the player is actually put down on — the release snaps to
        // the grid, so an exit point nudged off a cell centre does NOT let the player out
        // where this marker sits. Line them up.
        if (m_ExitPoint != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.9f);
            Gizmos.DrawLine(previous, m_ExitPoint.position);
            Gizmos.DrawWireSphere(m_ExitPoint.position, 0.15f);

            Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.35f);
            Gizmos.DrawWireCube(GridWorld.SnapToCell(m_ExitPoint.position), Vector3.one * GridWorld.CellSize);
        }
    }
}
