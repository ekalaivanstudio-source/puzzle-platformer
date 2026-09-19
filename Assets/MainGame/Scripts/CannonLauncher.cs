using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cannon that catches the player as they pass and fires them across the level on an arc.
///
/// The flight is AUTHORED, not simulated. The designer drags the landing handle onto the
/// cell they want the player to arrive on, and the cannon guarantees that arrival: nothing
/// about the speed the player walked in at, the beat the catch happened on, or the physics
/// step rate can move where they come down. That is what lets a cannon sit in the middle of
/// a solution — the commands queued after it are planned against a known landing cell, and
/// <see cref="PlayerController"/> resumes them there the moment the flight ends.
///
/// The ride costs exactly ONE beat. The command the player was part-way through when the
/// cannon caught them is spent on the flight, and execution picks up at the next one — the
/// same contract <see cref="PlayerController.StartWaypointTransport"/> already uses to hand
/// a turn back after a scripted route.
///
/// Placement is done entirely in the Scene view. The arc, the muzzle, the landing cell and
/// the catch radius all draw as gizmos — dim when the cannon is not selected so a whole
/// level's shots can be read at a glance, bright with drag handles when it is. The barrel
/// re-aims along the launch tangent as the landing point moves, so the cannon always
/// visibly points where it will actually throw the player.
///
/// Nothing here depends on what the sprites look like, only on three numbers agreeing
/// with them: <see cref="m_BarrelPivotOffset"/> (where the barrel is mounted),
/// <see cref="m_BarrelLength"/> (pivot to mouth) and <see cref="m_BarrelArtAngle"/> (the
/// angle the barrel is drawn at). Re-measure those three when the art is redrawn and
/// everything else — aim, muzzle, recoil, gizmos — follows.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CannonLauncher : MonoBehaviour
{
    // ─── Flight ──────────────────────────────────────────────────────────────────

    [Header("Flight")]
    [Tooltip("Where the player comes down, as a world-space offset from this cannon. Drag " +
             "the yellow handle in the Scene view rather than typing it in. Snapped to a " +
             "whole cell, because a body that comes to rest off a cell centre puts every " +
             "command queued after the flight half a cell out of step with the grid.")]
    [SerializeField] private Vector2 m_LandingOffset = new Vector2(10f, 0f);

    [Tooltip("How far the arc bulges above the straight line from the muzzle to the landing " +
             "point, measured half way along. This is the knob that turns a flat throw into " +
             "a lob: it is measured off the CHORD, not off the ground, so a shot that lands " +
             "higher or lower than it launched keeps the same visible bulge.")]
    [Min(0f)] [SerializeField] private float m_ArcLift = 5f;

    [Tooltip("How fast the player travels along the arc, in units per second. A cannon is a " +
             "LAUNCH, so this wants to be several times walking pace — the player walks a " +
             "cell in well under a tenth of a second, and a shot that crawls next to that " +
             "reads as being carried rather than fired.\n\n" +
             "The flight time is derived from this and the arc's real length rather than " +
             "authored, so dragging the landing point further keeps the shot exactly as " +
             "fast instead of quietly turning it into a long slow float.")]
    [Min(1f)] [SerializeField] private float m_LaunchSpeed = 25f;

    [Tooltip("Floor on the flight time, in seconds. Stops a very short shot from being over " +
             "in a frame or two, which reads as a teleport rather than a launch.")]
    [Min(0.05f)] [SerializeField] private float m_MinFlightDuration = 0.2f;

    // ─── Loading ─────────────────────────────────────────────────────────────────

    [Header("Loading")]
    [Tooltip("Radius around this cannon's catch point that swallows the player. Only ever " +
             "tested while a turn is executing, so a player standing here between turns — " +
             "or parked here by a reset — is left alone.")]
    [Min(0.1f)] [SerializeField] private float m_CatchRadius = 1.2f;

    [Tooltip("Offset of the catch point from the cannon's origin. Lets the mouth of the " +
             "cannon sit above or beside the line the player actually walks along.")]
    [SerializeField] private Vector2 m_CatchOffset = Vector2.zero;

    [Tooltip("Seconds the player takes to slide from wherever they were caught into the " +
             "barrel. Short — it reads as being sucked in, not as a walk.")]
    [Min(0f)] [SerializeField] private float m_LoadDuration = 0.12f;

    [Tooltip("Seconds the player sits inside the cannon, out of sight, before it fires. " +
             "The pause is what makes the launch land as an event rather than a teleport.")]
    [Min(0f)] [SerializeField] private float m_WindUpDuration = 0.2f;

    // ─── Parts ───────────────────────────────────────────────────────────────────

    [Header("Parts")]
    [Tooltip("The barrel sprite. Rotated to the launch angle every frame, in the editor as " +
             "well as in play mode, so what is on screen is always where the shot goes. " +
             "Its sprite pivot must sit on the CENTRE OF THE BREECH — the flat back face " +
             "of the barrel — because that is the point this component swings it around.")]
    [SerializeField] private Transform m_Barrel;

    [Tooltip("The angle the barrel is DRAWN at in its own sprite, in degrees anticlockwise " +
             "from pointing right. Art that is drawn already tilted — like a barrel resting " +
             "on a wedge — carries that tilt into every rotation, so it is subtracted back " +
             "out before the aim is applied. Leave at 0 for art drawn flat along +X; the " +
             "current barrel is drawn at 40.19 degrees, measured off its own long edges.")]
    [Range(-180f, 180f)] [SerializeField] private float m_BarrelArtAngle = 40.19f;

    [Tooltip("The base the barrel is mounted on. Optional, and only read to flip it: the " +
             "wedge is not symmetrical — its long slope carries the barrel's overhang — so " +
             "a cannon firing left has to turn its base around with the barrel or it props " +
             "the shot up from the wrong side. Leave empty for a symmetrical base.")]
    [SerializeField] private Transform m_Base;

    [Tooltip("Where the barrel pivots, as an offset from the cannon's origin — which sits " +
             "on the floor, so this is measured up from the walk line. Wants to land just " +
             "inside the top of the base so the breech stays buried in it at every aim; too " +
             "low and the barrel's back corner cuts through the floor on a flat shot.")]
    [SerializeField] private Vector2 m_BarrelPivotOffset = new Vector2(-0.06f, 0.44f);

    [Tooltip("Distance from the barrel's pivot to its mouth, in units. This is where the " +
             "player is fired from — set it to the distance from the breech to the mouth " +
             "in the barrel art, or the shot will leave from inside or beyond the sprite.")]
    [Min(0f)] [SerializeField] private float m_BarrelLength = 1.109f;

    [Tooltip("How far the barrel kicks back along its own axis when it fires, in units. " +
             "0 disables the recoil.")]
    [Min(0f)] [SerializeField] private float m_RecoilDistance = 0.22f;

    [Tooltip("Seconds the barrel takes to slide back out to rest after a shot.")]
    [Min(0f)] [SerializeField] private float m_RecoilRecovery = 0.25f;

    [Tooltip("Frames of the shot, played once in order the moment the cannon fires. " +
             "Unscaled, so the hit-stop in the fire feel slows the game without also " +
             "slowing the animation the player's launch is timed against. Leave empty to " +
             "keep the barrel on its resting sprite and fire the player immediately.")]
    [SerializeField] private Sprite[] m_FireFrames;

    [Tooltip("Frames per second for the shot animation. With the launch frame below, this " +
             "is what sets how long the player waits inside the barrel before leaving.")]
    [Min(1f)] [SerializeField] private float m_FireFrameRate = 24f;

    [Tooltip("Which frame of the shot the player actually leaves on, counting from 0. The " +
             "muzzle effect is spawned on the same frame, so the two can never drift apart. " +
             "Defaults to the middle of a four-frame shot: the blast reads first, then the " +
             "body comes out of it. Clamped to the last frame.")]
    [Min(0)] [SerializeField] private int m_LaunchFrame = 2;

    [Tooltip("Optional one-shot effect spawned at the muzzle, on the launch frame above — " +
             "NOT on the frame the shot animation starts, so the puff and the player leave " +
             "the barrel together.")]
    [SerializeField] private GameObject m_MuzzleFlash;

    [Tooltip("The angle the muzzle effect is DRAWN blowing at, in degrees anticlockwise " +
             "from pointing right. Subtracted from the aim before the effect is spawned, so " +
             "a puff authored drifting one way still blows out along the barrel whichever " +
             "way the cannon is pointing. Same idea as the barrel's own art angle.")]
    [Range(-180f, 180f)] [SerializeField] private float m_MuzzleFlashArtAngle = 57f;

    // ─── Feel ────────────────────────────────────────────────────────────────────

    [Header("Feel")]
    [Tooltip("Played on the frame the cannon fires. The shot is the loudest thing that " +
             "happens to the player all turn, so it gets a real kick.")]
    [SerializeField] private FeelPreset m_FireFeel = new FeelPreset
    {
        Haptic = HapticPattern.HeavyImpact,
        FreezeDuration = 0.05f,
    };

    // ─── Debug ───────────────────────────────────────────────────────────────────

    [Header("Debug")]
    [Tooltip("Logs each phase of a shot — caught, loaded, fired, landed — with the cell it " +
             "happened on. The cheapest way to tell a mis-placed landing point from a " +
             "cannon that never caught the player at all.")]
    [SerializeField] private bool m_LogPhases;

    [Tooltip("Draws the arc even when this cannon is not selected, so a level's shots can " +
             "be read without clicking each one.")]
    [SerializeField] private bool m_AlwaysDrawArc = true;

    // How many line segments the arc gizmo is drawn with. Purely cosmetic — the flight
    // itself samples the same curve at whatever the physics rate gives it.
    private const int k_ArcGizmoSegments = 32;

    // Frame the shot animation is showing, and the time spent on it. -1 means the barrel
    // is at rest and Update has no animation to step. Unscaled, like every other sprite
    // animation in the project.
    private int m_FireFrame = -1;
    private float m_FireFrameTime;

    // The barrel's resting sprite, taken the first time a shot plays and put back when it
    // ends. Restored rather than left on the animation's last frame so CannonBarrel.png
    // stays the one place the rest pose is authored, even if the two are drawn identically
    // today.
    private Sprite m_RestingSprite;
    private SpriteRenderer m_BarrelRenderer;

    // Latched on the shot that fires and cleared by the turn reset, so one cannon fires
    // once per turn. Without it the commands resumed after a flight could walk the player
    // back past the cannon and set it off a second time.
    private bool m_HasFiredThisTurn;

    // How far the barrel is currently kicked back from its rest position. The recoil is
    // applied on top of the aim every frame rather than animated into the Transform, so
    // the two never fight over the same channel.
    private float m_RecoilOffset;

    // Reused across frames so the per-frame catch test allocates nothing.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_NoFilter;

    // ─── Public API — read by PlayerController while it flies the player ──────────

    /// <summary>Where the player is fired from: the mouth of the barrel, at its current aim.</summary>
    public Vector2 MuzzlePoint => PivotPoint + AimDirection * m_BarrelLength;

    /// <summary>
    /// The cell the player comes down on. Snapped, so the landing always agrees with
    /// <see cref="GridWorld"/> no matter where the handle was released.
    /// </summary>
    public Vector2 LandingPoint =>
        GridWorld.SnapToCell((Vector2)transform.position + m_LandingOffset);

    /// <summary>
    /// Seconds the flight itself takes: the arc's length at <see cref="m_LaunchSpeed"/>,
    /// never less than the floor. Derived rather than authored, so every cannon in a level
    /// throws at the same speed however far it throws.
    /// </summary>
    public float FlightDuration => Mathf.Max(m_MinFlightDuration, ArcLength() / m_LaunchSpeed);

    // Length of the flight path, measured off the curve the player actually follows rather
    // than the straight line between its ends — a high lob is a good deal longer than its
    // chord, and timing it by the chord would make the loftiest shots the slowest ones.
    private float ArcLength()
    {
        const int samples = 16;

        float length = 0f;
        Vector2 previous = ArcPointAt(0f);

        for (int i = 1; i <= samples; i++)
        {
            Vector2 next = ArcPointAt((float)i / samples);
            length += Vector2.Distance(previous, next);
            previous = next;
        }

        return length;
    }

    /// <summary>
    /// Seconds between the shot going off and the player leaving the barrel — the run-up
    /// of the shot animation, in REAL time.
    ///
    /// Real time on purpose: the fire feel opens with a hit stop, and the animation this
    /// measures runs unscaled. Waiting this out on the scaled clock would hold the player
    /// in the barrel long after the frame they were supposed to leave on had gone past.
    /// </summary>
    public float LaunchDelay =>
        m_FireFrames == null || m_FireFrames.Length == 0
            ? 0f
            : Mathf.Min(m_LaunchFrame, m_FireFrames.Length - 1) / Mathf.Max(1f, m_FireFrameRate);

    /// <summary>Seconds the player takes to be drawn into the barrel once caught.</summary>
    public float LoadDuration => m_LoadDuration;

    /// <summary>Seconds the player waits inside the barrel before the shot.</summary>
    public float WindUpDuration => m_WindUpDuration;

    /// <summary>
    /// Point on the flight path at normalised time <paramref name="u"/> (0 at the muzzle,
    /// 1 at the landing point).
    ///
    /// The single source of truth for the shape of a shot: the flight samples it, the
    /// gizmo draws it, and the barrel takes its aim from its tangent. Anything that wants
    /// to know where a cannon throws the player asks here rather than re-deriving a
    /// parabola of its own, so what the designer sees in the Scene view is exactly the
    /// path the player takes.
    /// </summary>
    public Vector2 ArcPointAt(float u) => ArcBetween(MuzzlePoint, LandingPoint, u);

    // ─── Geometry ────────────────────────────────────────────────────────────────

    private Vector2 PivotPoint => (Vector2)transform.position + m_BarrelPivotOffset;

    private Vector2 CatchPoint => (Vector2)transform.position + m_CatchOffset;

    // A parabola laid over the straight line from `from` to `to`: the chord gives the shot
    // its direction and its rise or fall, and 4*lift*u*(1-u) adds a symmetric bulge that
    // peaks at exactly m_ArcLift half way along and is zero at both ends. Written this way
    // rather than as a launch velocity under a gravity because BOTH endpoints are authored
    // — a designer places the landing cell and expects to hit it, and a ballistic solution
    // would instead have to be re-solved (and could fail to exist at all) every time the
    // arc height or the drop between the two ends changed.
    private Vector2 ArcBetween(Vector2 from, Vector2 to, float u)
    {
        Vector2 point = Vector2.Lerp(from, to, u);
        point.y += 4f * m_ArcLift * u * (1f - u);
        return point;
    }

    // Direction the barrel points, and the direction the shot leaves along.
    //
    // Taken from the tangent of the arc measured at the PIVOT rather than at the muzzle,
    // which is what stops this from being circular: the muzzle sits along this direction,
    // so it cannot also be an input to it. Over a barrel a unit or two long the two
    // tangents differ by a fraction of a degree, which is why the cheap answer is the
    // right one here.
    private Vector2 AimDirection
    {
        get
        {
            Vector2 chord = LandingPoint - PivotPoint;

            // Derivative of ArcBetween at u = 0, with du folded out — only the direction is
            // wanted, and the chord's length scales both components equally.
            Vector2 tangent = new Vector2(chord.x, chord.y + 4f * m_ArcLift);

            // A cannon whose landing point sits exactly on its own pivot has no direction
            // to point in. Aim straight up rather than returning a zero vector, which would
            // collapse the muzzle onto the pivot and NaN the normalisation.
            return tangent.sqrMagnitude < 1e-6f ? Vector2.up : tangent.normalized;
        }
    }

    /// <summary>
    /// Which way the player is thrown, as the ±1 the movement code writes into
    /// <c>localScale.x</c>. A shot fired left to right faces the player right; one fired
    /// right to left faces them left, so they always look the way they are travelling.
    /// A dead-vertical shot keeps whichever way the barrel leans.
    /// </summary>
    public float LaunchFacingSign
    {
        get
        {
            float dx = LandingPoint.x - MuzzlePoint.x;
            if (!Mathf.Approximately(dx, 0f)) return Mathf.Sign(dx);

            float aimX = AimDirection.x;
            return Mathf.Approximately(aimX, 0f) ? 1f : Mathf.Sign(aimX);
        }
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_NoFilter = ContactFilter2D.noFilter;
        AimBarrel();
    }

    private void OnEnable()
    {
        m_HasFiredThisTurn = false;
        m_RecoilOffset = 0f;
        EndFireAnimation();

        if (Application.isPlaying) GameManager.OnTurnReset += Rearm;
    }

    private void OnDisable()
    {
        if (Application.isPlaying) GameManager.OnTurnReset -= Rearm;
    }

    private void OnValidate()
    {
        // Keeps the authored offset on a cell, so the handle and the inspector agree with
        // where the player will actually be put down.
        m_LandingOffset = LandingPoint - (Vector2)transform.position;
        AimBarrel();
    }

    // Runs in edit mode too (see ExecuteAlways): the barrel has to track the landing handle
    // while a designer drags it, or the cannon would only look right after a domain reload.
    private void Update()
    {
        AimBarrel();

        if (!Application.isPlaying) return;

        StepFireAnimation();
        RecoverRecoil();
        PollForPlayer();
    }

    // ─── Catching the player ─────────────────────────────────────────────────────

    // Re-armed by the turn reset rather than by the flight ending, so a cannon fires at
    // most once per attempt however the turn it fired on finished.
    private void Rearm()
    {
        m_HasFiredThisTurn = false;

        // A reset part way through a shot must not leave the barrel lit up on a blast frame
        // for the player's next attempt.
        EndFireAnimation();
    }

    // Overlap test rather than a trigger callback, matching InvisibleLockPoint: it needs no
    // collider on the cannon, it reads the same whether the player is walking, jumping or
    // being carried, and it cannot be missed by a body that is moved with MovePosition.
    //
    // It also cannot catch a player who is already riding something — a rider's collider is
    // switched off for the length of the ride, so there is nothing here to overlap.
    private void PollForPlayer()
    {
        if (m_HasFiredThisTurn) return;

        PlayerController player = PlayerController.Instance;

        // Only ever fires mid-turn. A cannon placed near a level's start cell would
        // otherwise swallow the player the moment they were reset onto it, before the
        // player had entered a single command.
        if (player == null || !player.IsExecuting || player.IsRidingCannon) return;

        int count = Physics2D.OverlapCircle(CatchPoint, m_CatchRadius, m_NoFilter, m_OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit == null || !hit.CompareTag("Player")) continue;
            if (hit.GetComponentInParent<PlayerController>() != player) continue;

            m_HasFiredThisTurn = true;

            if (m_LogPhases)
                Debug.Log($"[Cannon] '{name}' caught the player at {(Vector2)player.transform.position} — " +
                          $"firing to {LandingPoint}.", this);

            player.StartCannonLaunch(this);
            return;
        }
    }

    // ─── Presentation hooks, called by PlayerController's flight ──────────────────

    /// <summary>
    /// The player is inside the barrel and the wind-up has started. Called by
    /// <see cref="PlayerController"/> once it has slid the body onto the muzzle and hidden it.
    /// </summary>
    public void OnPlayerLoaded()
    {
        if (m_LogPhases) Debug.Log($"[Cannon] '{name}' loaded.", this);
    }

    /// <summary>
    /// The shot has gone off. Kicks the barrel back, spawns the muzzle flash and plays the
    /// fire feel; the player is already on their way by the time this returns.
    /// </summary>
    public void OnPlayerFired()
    {
        m_RecoilOffset = m_RecoilDistance;

        StartFireAnimation();

        m_FireFeel.Play(MuzzlePoint, m_Barrel, AimDirection);

        if (m_LogPhases)
            Debug.Log($"[Cannon] '{name}' fired from {MuzzlePoint} at " +
                      $"{Vector2.SignedAngle(Vector2.right, AimDirection):0.#} degrees.", this);
    }

    /// <summary>
    /// The player is leaving the barrel, <see cref="LaunchDelay"/> after the shot went off.
    /// Called by <see cref="PlayerController"/> on the frame it starts the arc, so the
    /// muzzle effect and the body come out of the cannon together rather than each being
    /// timed against a clock of its own.
    /// </summary>
    public void OnPlayerLaunched()
    {
        if (m_MuzzleFlash == null) return;

        // Turned by the aim with the effect's own drawn direction taken back out, so a puff
        // authored blowing one way still leaves along the barrel at any aim.
        ParticleEffectSpawner.Spawn(
            m_MuzzleFlash, MuzzlePoint, 1f,
            Vector2.SignedAngle(Vector2.right, AimDirection) - m_MuzzleFlashArtAngle);
    }

    /// <summary>The player has come down. Logged so a mis-placed landing reads as a cell, not a guess.</summary>
    public void OnPlayerLanded(Vector2 restingCell)
    {
        if (m_LogPhases)
            Debug.Log($"[Cannon] '{name}' landed the player on {restingCell} " +
                      $"(aimed at {LandingPoint}).", this);
    }

    // ─── Barrel ──────────────────────────────────────────────────────────────────

    // Points the barrel along the launch tangent and holds it there.
    //
    // Writes are guarded on an actual change so this can run every frame in edit mode
    // without marking the scene dirty on its own — the barrel only moves when the aim it
    // is derived from moved, which is a real edit worth saving.
    private void AimBarrel()
    {
        if (m_Barrel == null) return;

        Vector2 aim = AimDirection;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

        // Rotating the art past vertical to fire left would leave it standing on its head,
        // so a left-facing shot is mirrored on Y instead — the same trick the player's own
        // sprite uses on X.
        float mirror = aim.x < 0f ? -1f : 1f;
        Vector3 scale = new Vector3(1f, mirror, 1f);

        // The barrel is not drawn along +X, so the tilt baked into the sprite has to come
        // back out before the aim goes in — otherwise the barrel sits m_BarrelArtAngle
        // degrees off the arc it is supposed to be firing along, and the muzzle gizmo
        // parts company with the painted mouth.
        //
        // The sign rides on `mirror` because flipping the sprite on Y flips that baked
        // tilt with it: the art reads as -m_BarrelArtAngle once mirrored, so the
        // correction has to reverse as well or a left-facing shot lands twice as wrong as
        // an uncorrected right-facing one.
        angle -= mirror * m_BarrelArtAngle;

        // Kept at the barrel's own depth so a recoil never drags it in front of, or behind,
        // the base it is mounted on.
        Vector3 position = (Vector3)(PivotPoint - aim * m_RecoilOffset);
        position.z = m_Barrel.position.z;

        if (Quaternion.Angle(m_Barrel.rotation, Quaternion.Euler(0f, 0f, angle)) > 0.001f)
            m_Barrel.rotation = Quaternion.Euler(0f, 0f, angle);

        if (m_Barrel.localScale != scale) m_Barrel.localScale = scale;

        if ((m_Barrel.position - position).sqrMagnitude > 1e-8f) m_Barrel.position = position;

        // The base turns around with the barrel rather than with the shot's own facing,
        // so the two can never disagree. Flipped on X — it stands on the floor, and Y is
        // the one axis a mount can't be mirrored on without standing on its roof.
        //
        // Safe to read the same AimDirection the barrel just used: nothing here feeds back
        // into it. The aim is taken at m_BarrelPivotOffset, which this does not touch.
        if (m_Base == null) return;

        Vector3 baseScale = m_Base.localScale;
        float wanted = Mathf.Abs(baseScale.x) * mirror;

        if (!Mathf.Approximately(baseScale.x, wanted))
        {
            baseScale.x = wanted;
            m_Base.localScale = baseScale;
        }
    }

    // The barrel's own renderer, found lazily rather than in Awake: m_Barrel is authored in
    // the inspector, and re-pointing it at another object does not re-enable this component.
    private SpriteRenderer BarrelRenderer
    {
        get
        {
            if (m_BarrelRenderer == null && m_Barrel != null)
                m_BarrelRenderer = m_Barrel.GetComponent<SpriteRenderer>();

            return m_BarrelRenderer;
        }
    }

    // Puts the barrel on the first frame of the shot.
    //
    // The resting sprite is remembered on the way in rather than assumed to be the
    // animation's last frame. They are drawn identically today, but keeping the round trip
    // means CannonBarrel.png stays the single place the rest pose is authored — redraw it
    // without touching the shot frames and the barrel still settles back onto it.
    private void StartFireAnimation()
    {
        if (m_FireFrames == null || m_FireFrames.Length == 0) return;

        SpriteRenderer renderer = BarrelRenderer;
        if (renderer == null) return;

        // Only when not already mid-shot, or a re-fire would remember a blast frame as the
        // pose to settle back onto.
        if (m_FireFrame < 0) m_RestingSprite = renderer.sprite;

        m_FireFrame = 0;
        m_FireFrameTime = 0f;

        if (m_FireFrames[0] != null) renderer.sprite = m_FireFrames[0];
    }

    // Steps the shot a frame at a time.
    //
    // Unscaled, matching OneShotEffect and SpriteSheetAnimator — and, more to the point,
    // matching LaunchDelay, which is what PlayerController waits out before starting the
    // arc. Those two have to be read off the SAME clock: the fire feel opens with a hit
    // stop, so a scaled animation and a real-time wait would put the body on the way out
    // several frames from the one the launch was authored on.
    private void StepFireAnimation()
    {
        if (m_FireFrame < 0) return;

        SpriteRenderer renderer = BarrelRenderer;
        if (renderer == null)
        {
            m_FireFrame = -1;
            return;
        }

        m_FireFrameTime += Time.unscaledDeltaTime;

        float frameDuration = 1f / Mathf.Max(1f, m_FireFrameRate);

        // A loop rather than a single step: one long frame — a hitch, an asset load — must
        // not leave the barrel part way through a shot that is already over in real time.
        while (m_FireFrameTime >= frameDuration)
        {
            m_FireFrameTime -= frameDuration;
            m_FireFrame++;

            if (m_FireFrame >= m_FireFrames.Length)
            {
                EndFireAnimation();
                return;
            }
        }

        Sprite frame = m_FireFrames[m_FireFrame];
        if (frame != null && renderer.sprite != frame) renderer.sprite = frame;
    }

    // Back to rest. Safe at any point in a shot, which is what lets the turn reset call it.
    private void EndFireAnimation()
    {
        if (m_FireFrame < 0) return;

        m_FireFrame = -1;
        m_FireFrameTime = 0f;

        SpriteRenderer renderer = BarrelRenderer;
        if (renderer != null && m_RestingSprite != null) renderer.sprite = m_RestingSprite;
    }

    // Eases the barrel back out to rest after a shot. Unscaled so the recoil still plays
    // through the hit-stop the fire feel puts on the game clock.
    private void RecoverRecoil()
    {
        if (m_RecoilOffset <= 0f) return;

        m_RecoilOffset = m_RecoilRecovery > 0f
            ? Mathf.Max(0f, m_RecoilOffset - m_RecoilDistance * Time.unscaledDeltaTime / m_RecoilRecovery)
            : 0f;
    }

    // ─── Gizmos ──────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!m_AlwaysDrawArc) return;
        DrawArcGizmo(new Color(1f, 0.75f, 0.2f, 0.35f));
    }

    private void OnDrawGizmosSelected()
    {
        DrawArcGizmo(new Color(1f, 0.8f, 0.25f, 1f));

        // The mouth the player leaves from.
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        Gizmos.DrawWireSphere(MuzzlePoint, 0.15f);
        Gizmos.DrawLine(PivotPoint, MuzzlePoint);

        // The cell the player is put down on, drawn as the cell itself rather than a point
        // so it can be lined up against the level's tiles by eye.
        Gizmos.color = new Color(0.35f, 1f, 0.45f, 1f);
        Gizmos.DrawWireCube(LandingPoint, new Vector3(GridWorld.CellSize, GridWorld.CellSize, 0f));

        // How close the player has to get to be swallowed.
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(CatchPoint, m_CatchRadius);
    }

    private void DrawArcGizmo(Color color)
    {
        Gizmos.color = color;

        Vector2 previous = ArcPointAt(0f);
        for (int i = 1; i <= k_ArcGizmoSegments; i++)
        {
            Vector2 next = ArcPointAt((float)i / k_ArcGizmoSegments);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        // An arrowhead on the last segment, so which way a shot runs is readable without
        // selecting the cannon — the difference between a left-to-right and a right-to-left
        // cannon is the whole reason the player ends up facing either way.
        Vector2 tip = ArcPointAt(1f);
        Vector2 back = ArcPointAt(1f - 1f / k_ArcGizmoSegments);
        Vector2 dir = (tip - back).sqrMagnitude > 1e-6f ? (tip - back).normalized : Vector2.right;
        Vector2 side = new Vector2(-dir.y, dir.x);

        Gizmos.DrawLine(tip, tip - dir * 0.45f + side * 0.22f);
        Gizmos.DrawLine(tip, tip - dir * 0.45f - side * 0.22f);
    }
}
