using ModernLevelSelection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the player character during a timeline execution turn using a
/// deterministic, fixed-distance command system. Each command (Left, Right,
/// Jump, JumpRight, JumpLeft) travels an exact number of units every execution,
/// independent of frame rate or physics timing. The execution loop is
/// coroutine-based — each command completes fully before the next begins.
///
/// Combined commands: a Jump followed immediately by Right or Left in the
/// sequence is interpreted as a directional jump (JumpRight / JumpLeft).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    /// <summary>World-space bounds of the player's collider. Hazards (e.g. lasers)
    /// use this for hit tests so they measure against the whole body, not the
    /// foot pivot at <see cref="Transform.position"/>.</summary>
    public Bounds ColliderBounds =>
        m_Collider != null ? m_Collider.bounds : new Bounds(transform.position, Vector3.one);

    [Header("Command Settings")]
    [Tooltip("Horizontal distance (units) the player travels per Left or Right command.")]
    [SerializeField] private float m_MoveDistancePerCommand = 2f;

    [Tooltip("Duration (seconds) to complete one Left or Right command.")]
    [SerializeField] private float m_MoveDuration = 0.4f;

    [Tooltip("Peak height (units) of a Jump command. Vertical velocity is derived from this and gravity.")]
    [SerializeField] private float m_JumpHeight = 3f;

    [Tooltip("Horizontal distance (units) traveled during a JumpRight or JumpLeft command.")]
    [SerializeField] private float m_JumpForwardDistance = 2f;

    [Tooltip("Duration (seconds) to complete a Jump command.")]
    [SerializeField] private float m_JumpDuration = 0.6f;

    [Tooltip("Pause (seconds) between commands so the player can see each action clearly.")]
    [SerializeField] private float m_BeatGapTime = 0.05f;

    [Tooltip("Seconds the push animation is on screen before the brick starts to slide. " +
             "The shove plays exactly once and Byte then returns to idle for the slide " +
             "itself, so set this to the push clip's own length (five frames at 10 fps = " +
             "0.5s) to have one complete shove land before the brick moves. This lengthens " +
             "every push command by the same amount; 0 skips the shove entirely.")]
    [SerializeField] private float m_PushWindUpTime = 0.5f;

    [Header("Ground Check")]
    [Tooltip("All layers that count as walkable ground / solid walls (Ground, Laser, etc.).")]
    [SerializeField] private LayerMask[] m_GroundLayers;

    // Cached union of m_GroundLayers, computed once in Awake. The previous
    // property recomputed this every ground/wall check by looping the array.
    private int m_WalkableMask;
    private LayerMask WalkableMask => m_WalkableMask;

    [Header("Falling")]
    [Tooltip("Downward acceleration (units/sec²) while falling. A pure script value — the " +
             "body is kinematic and ignores Physics2D.gravity entirely.")]
    [SerializeField] private float m_FallGravity = 40f;

    [Tooltip("Terminal fall speed (units/sec).")]
    [SerializeField] private float m_MaxFallSpeed = 25f;

    [Tooltip("Abandon a fall after this many seconds — the player is over the void.")]
    [SerializeField] private float m_FallTimeout = 6f;

    [Tooltip("How far (units) the player has to drop below the height a descent began at " +
             "before the ground-pound dive clip replaces the jump clip. A jump that lands " +
             "back on its own level never descends past its launch height at all, so this " +
             "keeps the dive for drops the arc could not finish — over a pit, off a ledge, " +
             "or onto a platform well below.")]
    [SerializeField] private float m_GroundPoundDropDistance = 0.5f;

    [Tooltip("Peak height (units) of the small hop the player makes when a step walks off a " +
             "ledge into empty space. Set to 0 to step off flat.")]
    [SerializeField] private float m_LedgeHopHeight = 1.2f;

    [Tooltip("Scales the gravity the ledge hop arcs under, relative to Fall Gravity. 1 keeps " +
             "the hop and the fall on one continuous curve. Below 1 makes the hop floatier " +
             "and slower; above 1 snappier. Duration is derived — it is not a separate knob.")]
    [SerializeField] private float m_LedgeHopGravityScale = 1f;

    // Thickness of the probe used to detect ground directly under the player. Matches
    // the equivalent constant in PushBrick so the brick and the player agree on what
    // "supported" means.
    private const float k_GroundProbeThickness = 0.05f;

    // Footprint probes are shrunk by this factor so a surface the player is merely
    // resting flush against never reads as an obstacle blocking the next cell.
    private const float k_ProbeShrink = 0.9f;

    [Header("Portal Spawn / Exit")]
    [Tooltip("Seconds the spawn spin-in and the doorway spin-out each take.")]
    [SerializeField] private float m_PortalDuration = 1f;

    [Tooltip("Full turns the player spins through during a portal animation.")]
    [SerializeField] private float m_PortalSpins = 3f;

    [Tooltip("Effect burst spawned at the door's interaction point once the player has " +
             "spun out of the level there. Optional. A particle prefab — it is destroyed " +
             "automatically once its systems have finished, so it needs no self-cleanup.")]
    [SerializeField] private GameObject m_DoorEnterEffect;

    [Tooltip("Uniform scale applied to the door effect when it spawns. The stock FX packs are " +
             "authored for a far larger world than this one — Flash_magic_ellow_blue throws " +
             "particles 13 units wide, against a 1-unit grid cell — so a burst meant for a " +
             "doorway has to be shrunk here rather than used at 1. 0.2 puts it at about two " +
             "and a half cells, a little wider than the player.")]
    [SerializeField] private float m_DoorEnterEffectScale = 0.2f;

    [Tooltip("Seconds the player waits, invisible, on the entry door's arrival point before " +
             "spinning in, so they read as coming out of the doorway rather than being " +
             "there as the level opens.")]
    [SerializeField] private float m_SpawnDoorLead = 0.15f;

    [Header("Interaction")]
    [Tooltip("Radius of the overlap circle used to detect interactable objects.")]
    [SerializeField] private float m_InteractRadius = 0.5f;
    [SerializeField] private LayerMask m_InteractLayer;

    [Header("References")]

    [Tooltip("Explosion spawned at the player's position when a hazard kills them. Assign " +
             "ByteDeathExplosion — the CFXR3 Fire Explosion B variant scaled down for this " +
             "one-unit grid, with the pack's own camera shake turned off so it doesn't fight " +
             "the CameraController shake below.")]
    [SerializeField] private GameObject m_DeathExplosion;

    [Tooltip("Debris spawned alongside the explosion. Assign ByteDeathDebris — the particle " +
             "systems that throw Byte's five body pieces out under gravity and bounce them " +
             "off the ground. Optional; the explosion plays on its own without it.")]
    [SerializeField] private GameObject m_DeathDebris;

    [SerializeField] private float m_DeathShakeMagnitude = 0.2f;
    [SerializeField] private float m_DeathShakeDuration = 0.4f;

    [Tooltip("Drives the player's sprite-sheet animations (idle / run / jump / push). Auto-fetched if left empty.")]
    [SerializeField] private PlayerAnimator m_Animator;

    [Tooltip("Byte's own sprite. Switched off for the length of a death so the explosion and " +
             "the flying pieces read as the body coming apart, and back on at respawn. " +
             "Auto-fetched if left empty.")]
    [SerializeField] private SpriteRenderer m_SpriteRenderer;

    [Header("Jump VFX")]
    [Tooltip("Dust effect prefab spawned at the player's feet when a jump takes off. Optional. Should carry a OneShotEffect.")]
    [SerializeField] private GameObject m_JumpStartDust;

    [Tooltip("Dust effect prefab spawned at the player's feet when a jump lands. Optional. Should carry a OneShotEffect.")]
    [SerializeField] private GameObject m_JumpEndDust;

    private Rigidbody2D m_Rigidbody;
    private Collider2D m_Collider;
    private bool m_IsDead;   // true while the death animation/reset is playing

    private int m_MaxTimeIndex;        // snapshotted from source at turn start
    private int m_CurrentCommandIndex; // index of the command currently executing
    private bool m_IsGamePlaying;
    private Coroutine m_ExecutionCoroutine;  // stored so it can be stopped on abort
    private Coroutine m_EndTurnCoroutine;    // stored so checkpoint can cancel a pending reset

    // Whether hazards (laser, spikes) can currently affect the player. The rule is
    // simply "is the player a live body standing in the level" — it is NOT tied to the
    // turn system, so if a beam touches the player it kills them whether a turn is
    // running, the lever has parked them on a moving platform, or they were dragged
    // there in the editor. Starts true and is suspended only while the player is not a
    // normal body: mid-death, mid-win, or riding a scripted waypoint route with their
    // collider switched off (where the hit tests would read disabled-collider bounds).
    private bool m_IsHazardable = true;

    private Vector3 m_StartPosition;
    private Vector3 m_OriginalScale;   // spawn facing — restored on respawn so a left-facing death doesn't persist

    // ─── Motion state ───────────────────────────────────────────────────────────
    // The body is kinematic, so linearVelocity is always zero and can no longer tell
    // the animator or the footstep loop what the player is doing. These flags carry
    // that information instead, set by whichever routine currently owns the body.

    private bool m_IsWalking;    // mid-step of a horizontal command
    private bool m_IsAirborne;   // mid-jump arc, or falling
    private bool m_IsPushing;    // holding position while a PushBrick's routine slides it

    // True once a descent has carried the player m_GroundPoundDropDistance below the height
    // it started at — the jump arc ran out with no platform under it, a step walked off a
    // ledge, or the ground left from under a standing player. Latched for the rest of that
    // descent and cleared on landing, so the dive pose is held all the way down rather than
    // flickering against the jump clip.
    private bool m_IsGroundPounding;

    // How far the current passive (FixedUpdate) drop has fallen. Accumulated rather than
    // measured against the transform because MovePosition is deferred to the physics step —
    // reading position back in the same FixedUpdate returns the value from before the move.
    private float m_PassiveFallDistance;

    /// <summary>
    /// True while the player's feet are off the ground — mid-jump arc, or falling. Read by
    /// effects that want to land with the player rather than go off in mid-air.
    /// </summary>
    public bool IsAirborne => m_IsAirborne;

    /// <summary>True while the player is in the long straight drop that plays the
    /// ground-pound dive — a jump whose arc ended with nothing underneath it, or any
    /// other fall that has run past <c>m_GroundPoundDropDistance</c>.</summary>
    public bool IsGroundPounding => m_IsGroundPounding;

    // True while a command/transport routine is driving the body. Blocks the passive
    // settle in FixedUpdate so the two never issue MovePosition in the same step.
    private bool m_IsScriptedMotion;

    // Set the moment the player touches the open door and never cleared — the level is on
    // its way out. Guards against a second win starting while the portal is still drawing
    // the player into the doorway; the door's collider spans the whole opening, so the
    // travel through it would otherwise re-trigger the win every frame.
    private bool m_IsWinning;

    // Wall-clock cap on the travel into the doorway, so a mis-placed interaction point can
    // never leave the player stepping towards a door that ends the level.
    private const float k_DoorApproachTimeout = 3f;

    // True while the spawn / exit portal animation owns the transform and the sprite.
    // Everything that reads the collider has to stand down for it: the body is spinning
    // and scaling through zero, so ground probes and hazard tests would be measuring a
    // shape the player doesn't really have.
    private bool m_IsPortalAnimating;

    // Set the moment an arrival begins, by whichever of the two openings is running it. Start
    // reads it to know the entry door has already brought this player in — the normal order
    // for a player left disabled in the scene, whose Start only runs once the door enables it.
    private bool m_IsEnteringFromDoor;

    // Carried across FixedUpdate calls by the passive settle.
    private float m_PassiveFallSpeed;

    // Downward speed the ledge hop was travelling at when its arc ended, handed to the
    // fall that follows so the drop continues the curve instead of restarting from rest.
    private float m_HopExitFallSpeed;

    // Reusable buffers + filters for physics queries. Reusing these avoids the
    // per-call array allocation of the Physics2D.*All() overloads, which was
    // generating continuous GC garbage every frame (CheckSpikeOverlap runs in Update).
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_NoFilter;       // all layers, includes triggers (matches *All defaults)
    private ContactFilter2D m_InteractFilter; // m_InteractLayer only

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_Collider = GetComponent<Collider2D>();
        if (m_Animator == null) m_Animator = GetComponent<PlayerAnimator>();
        if (m_SpriteRenderer == null) m_SpriteRenderer = GetComponent<SpriteRenderer>();
        // Snapped, so a spawn the designer nudged off-grid in the scene doesn't seed a
        // fractional offset into every command of every turn.
        m_StartPosition = GridWorld.SnapToCell(transform.position);
        // A zero authored scale is treated as 1: the arrival animation writes fractions of
        // this scale, so capturing a zero would multiply every frame of the spin — and every
        // frame of normal play after it — down to nothing, leaving an invisible player with
        // no way back. Zero is a plausible thing to find here, since hiding the player at the
        // start of a level is exactly what the entry door's opening does for real.
        m_OriginalScale = transform.localScale;
        if (m_OriginalScale.x == 0f || m_OriginalScale.y == 0f)
            m_OriginalScale = Vector3.one;

        // Force the body kinematic. Movement is entirely scripted: every command steps a
        // whole number of cells and every fall ends on a cell top, so the player can only
        // ever come to rest on the grid. As a dynamic body the solver owned the transform
        // between snaps — contact depenetration, friction and gravity each nudged it to a
        // fractional position that nothing corrected until the command ended.
        m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        m_Rigidbody.linearVelocity = Vector2.zero;
        m_Rigidbody.angularVelocity = 0f;

        // REQUIRED. Kinematic bodies ignore static and other kinematic colliders by
        // default, which silences OnTrigger/OnCollision callbacks against them. The door,
        // collectables, key slots and touch triggers are all static — without this the
        // level can't even be won.
        m_Rigidbody.useFullKinematicContacts = true;

        // Cache the walkable layer union once — m_GroundLayers never changes at runtime.
        int combined = 0;
        if (m_GroundLayers != null)
            foreach (LayerMask lm in m_GroundLayers) combined |= lm.value;
        m_WalkableMask = combined;

        m_NoFilter = ContactFilter2D.noFilter;
        m_InteractFilter = new ContactFilter2D { useTriggers = true };
        m_InteractFilter.SetLayerMask(m_InteractLayer);
    }

    // The level's opening belongs to the entry door when the level has one: it fades the
    // screen up, opens itself, and only then enables this player and calls
    // EnterFromDoorRoutine. Nothing to do here in that case beyond standing down — and not
    // even that once the door has already started the arrival, which is the normal order for
    // a player left disabled in the scene, since Start runs a beat after being enabled.
    //
    // A level with no entry door still runs its own intro from here.
    private void Start()
    {
        if (m_IsEnteringFromDoor) return;

        if (SceneObjects.FindInActiveScene<LevelEntryDoor>() != null)
        {
            HideForArrival();
            return;
        }

        StartCoroutine(SpawnPortalRoutine());
    }

    private void OnValidate()
    {
        if (m_MoveDistancePerCommand <= 0f) m_MoveDistancePerCommand = 2f;
        if (m_MoveDuration <= 0f) m_MoveDuration = 0.4f;
        if (m_JumpHeight <= 0f) m_JumpHeight = 3f;
        if (m_JumpForwardDistance <= 0f) m_JumpForwardDistance = 2f;
        if (m_JumpDuration <= 0f) m_JumpDuration = 0.6f;
        if (m_BeatGapTime < 0f) m_BeatGapTime = 0f;
        if (m_FallGravity <= 0f) m_FallGravity = 40f;
        if (m_MaxFallSpeed <= 0f) m_MaxFallSpeed = 25f;
        if (m_FallTimeout <= 0f) m_FallTimeout = 6f;
        if (m_LedgeHopHeight < 0f) m_LedgeHopHeight = 0f;
        if (m_LedgeHopGravityScale <= 0f) m_LedgeHopGravityScale = 1f;
        if (m_InteractRadius <= 0f) m_InteractRadius = 0.5f;
        if (m_PortalDuration <= 0f) m_PortalDuration = 1f;
        if (m_PortalSpins <= 0f) m_PortalSpins = 3f;
        if (m_PushWindUpTime < 0f) m_PushWindUpTime = 0f;
        if (m_DoorEnterEffectScale <= 0f) m_DoorEnterEffectScale = 0.2f;
        if (m_SpawnDoorLead < 0f) m_SpawnDoorLead = 0f;
    }

    // Animation-only update — movement is driven by coroutines, not Update
    private void Update()
    {
        // Mid-death the body is hidden and the debris stands in for it; there is no
        // sprite on screen for the animation state machine to drive.
        if (m_IsDead) return;

        // Same for the portal spin: it holds the player on the idle clip, and every check
        // below reads a collider that is mid-spin and mid-scale.
        if (m_IsPortalAnimating) return;

        // Animation runs even between turns so the player idles while standing.
        UpdateAnimationState();

        // Sweep for spikes whenever the player is a live body, not just mid-turn — a
        // moving spike, or a platform carrying the player onto one, counts the same
        // whether or not a command sequence happens to be running.
        if (m_IsHazardable) CheckSpikeOverlap();

        // m_IsWinning keeps the footstep loop running for the walk into the doorway —
        // that walk happens after the turn was aborted, so m_IsGamePlaying is already
        // false by then and this guard would otherwise mute it.
        if (!m_IsGamePlaying && !m_IsWinning)
        {
            AudioManager.Instance?.SetWalking(false);
            return;
        }
        UpdateWalkAudio();
    }

    // Keeps the player resting on a surface when no command is running. A kinematic
    // body has no gravity of its own, so without this the player would hover in place
    // when the ground leaves from under them — a moving platform sliding away, or a
    // brick pushed out from beneath their feet between turns.
    //
    // Deliberately inert while a routine owns the body (m_IsScriptedMotion): two
    // MovePosition calls in one physics step would fight each other.
    private void FixedUpdate()
    {
        if (m_IsDead || m_IsScriptedMotion || m_IsPortalAnimating) { m_PassiveFallSpeed = 0f; return; }

        if (CheckIsGrounded())
        {
            m_PassiveFallSpeed = 0f;
            m_PassiveFallDistance = 0f;
            m_IsAirborne = false;
            m_IsGroundPounding = false;
            return;
        }

        // First ungrounded step of this drop — start its distance tally from zero.
        if (!m_IsAirborne) m_PassiveFallDistance = 0f;

        m_PassiveFallSpeed = Mathf.Min(
            m_PassiveFallSpeed + m_FallGravity * Time.fixedDeltaTime, m_MaxFallSpeed);

        float step = m_PassiveFallSpeed * Time.fixedDeltaTime;
        float drop = GroundDistanceBelow(step);

        if (drop > 0f)
        {
            m_IsAirborne = true;
            m_Rigidbody.MovePosition(m_Rigidbody.position + Vector2.down * drop);

            m_PassiveFallDistance += drop;
            if (m_PassiveFallDistance > m_GroundPoundDropDistance) m_IsGroundPounding = true;
        }

        // A surface stopped the fall short of the full step → landed. Settle on the cell.
        if (drop < step)
        {
            m_PassiveFallSpeed = 0f;
            m_PassiveFallDistance = 0f;
            m_IsAirborne = false;
            m_IsGroundPounding = false;
            SnapToGrid();
        }
    }

    // Chooses the animation clip each frame. Reads the motion flags rather than
    // linearVelocity, which is permanently zero on a kinematic body driven by
    // MovePosition — velocity checks here used to leave the player idling mid-walk.
    private void UpdateAnimationState()
    {
        if (m_Animator == null) return;

        // Ground pound outranks the jump clip: it is only ever set while airborne, and it
        // means this descent is no longer the back half of an arc that lands where it left.
        if (m_IsGroundPounding)
            m_Animator.Play(PlayerAnimState.GroundPound);
        else if (m_IsAirborne || !CheckIsGrounded())
            m_Animator.Play(PlayerAnimState.Jump);
        // Ahead of the walk clip because a push happens INSTEAD of the step that ran into
        // the brick — the player holds position while the brick's routine slides it, so
        // m_IsWalking is already false and the walk clip would fall through to idle.
        else if (m_IsPushing)
            m_Animator.Play(PlayerAnimState.Push);
        else if (m_IsWalking)
            m_Animator.Play(PlayerAnimState.Run);
        else
            m_Animator.Play(PlayerAnimState.Idle);
    }

    // Drives the looping footstep sound: on only while stepping along the ground, so
    // it stays silent through the airborne portion of a jump or a fall.
    private void UpdateWalkAudio()
    {
        AudioManager.Instance?.SetWalking(m_IsWalking && !m_IsAirborne);
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the execution turn. Called by <see cref="GameManager.OnPlayClicked"/>.
    /// Launches the coroutine-based command loop.
    /// </summary>
    public void OnGamePlayStart()
    {
        if (SequenceManager.Instance == null || !SequenceManager.Instance.CanExecute)
        {
            Debug.LogError("[PlayerController] Cannot start � no sequence source or sequence is empty.", this);
            return;
        }

        m_MaxTimeIndex = SequenceManager.Instance.SequenceLength;
        m_IsGamePlaying = true;
        m_IsHazardable = true;
        m_ExecutionCoroutine = StartCoroutine(ExecutionLoop());
    }

    /// <summary>
    /// Immediately stops execution, teleports the player to <paramref name="checkpointPosition"/>,
    /// updates the start position so future turn-ends reset here, then re-enables input.
    /// Called by <see cref="InputResetter"/> when the player interacts with a checkpoint.
    /// </summary>
    public void ResetAtCheckpoint(Vector3 checkpointPosition)
    {
        AbortExecution();
        // The turn is over, but the player is NOT out of the level — they keep standing
        // exactly where they are. PlatformLever uses this to park them ON a moving
        // platform and then starts it patrolling, so the world goes on moving them
        // around while input is being re-entered. Hazards must stay ARMED here, or that
        // platform carries them straight through a laser untouched.
        m_IsHazardable = true;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        // Do NOT update m_StartPosition � death and turn-end still reset to the
        // original spawn. The checkpoint only repositions the player for this turn.
        // Snapped, because the checkpoint is an authored scene transform and nothing
        // guarantees the designer placed it exactly on a cell. Dropping the player onto a
        // fractional X here would put every later command half a cell out of step.
        m_Rigidbody.position = GridWorld.SnapToCell((Vector2)checkpointPosition);
        m_PassiveFallSpeed = 0f;
        GameManager.Instance?.StopExecution();
    }

    /// <summary>
    /// Aborts the current execution turn and smoothly moves the player through
    /// <paramref name="waypoints"/> in order. Input is locked until the movement
    /// finishes, at which point the normal end-of-turn flow resumes.
    /// </summary>
    public void StartWaypointTransport(Transform[] waypoints, float speed)
    {
        AbortExecution();
        StartCoroutine(WaypointTransportRoutine(waypoints, speed));
    }

    private IEnumerator WaypointTransportRoutine(Transform[] waypoints, float speed)
    {
        m_IsScriptedMotion = true;
        // Hazards go quiet for the scripted ride: the collider is off, so their hit
        // tests would be reading a disabled collider's bounds.
        m_IsHazardable = false;
        if (m_Collider != null) m_Collider.enabled = false;

        foreach (Transform target in waypoints)
        {
            if (target == null) continue;

            float xDir = target.position.x - transform.position.x;
            if (!Mathf.Approximately(xDir, 0f))
                transform.localScale = new Vector3(xDir > 0f ? 1f : -1f, 1f, 1f);


            while (Vector2.Distance(m_Rigidbody.position, target.position) > 0.01f)
            {
                Vector2 next = Vector2.MoveTowards(m_Rigidbody.position, target.position, speed * Time.fixedDeltaTime);
                m_Rigidbody.MovePosition(next);
                yield return new WaitForFixedUpdate();
            }

            m_Rigidbody.MovePosition(target.position);
        }

        if (m_Collider != null) m_Collider.enabled = true;
        // Back in the world under its own collider — a normal hittable body again,
        // whether the turn resumes below or ends.
        m_IsHazardable = true;
        m_IsScriptedMotion = false;

        // Resume remaining commands; if none are left, end the turn normally
        int resumeIndex = m_CurrentCommandIndex + 1;
        if (resumeIndex < m_MaxTimeIndex)
        {
            m_IsGamePlaying = true;
            m_ExecutionCoroutine = StartCoroutine(ExecutionLoop(resumeIndex));
        }
        else
        {
            EndTurn();
        }
    }

    /// <summary>
    /// Moves the player through <paramref name="waypoints"/> with input locked, then ends
    /// the turn — resetting the player to start just like a wrong-input run.
    /// Called by InvisibleLockPoint when the player enters a trap zone.
    /// </summary>
    public void StartWaypointTransportThenEndTurn(Transform[] waypoints, float speed)
    {
        AbortExecution();
        StartCoroutine(WaypointTransportThenEndTurnRoutine(waypoints, speed));
    }

    private IEnumerator WaypointTransportThenEndTurnRoutine(Transform[] waypoints, float speed)
    {
        m_IsScriptedMotion = true;
        // Off for the scripted ride only: the collider is disabled below, so hazard hit
        // tests would be reading a disabled collider's bounds. Re-armed on arrival.
        m_IsHazardable = false;
        if (m_Collider != null) m_Collider.enabled = false;

        foreach (Transform target in waypoints)
        {
            if (target == null) continue;

            float xDir = target.position.x - transform.position.x;
            if (!Mathf.Approximately(xDir, 0f))
                transform.localScale = new Vector3(xDir > 0f ? 1f : -1f, 1f, 1f);

            while (Vector2.Distance(m_Rigidbody.position, target.position) > 0.01f)
            {
                Vector2 next = Vector2.MoveTowards(m_Rigidbody.position, target.position, speed * Time.fixedDeltaTime);
                m_Rigidbody.MovePosition(next);
                yield return new WaitForFixedUpdate();
            }

            m_Rigidbody.MovePosition(target.position);
        }

        if (m_Collider != null) m_Collider.enabled = true;
        // Collider back on, so the player is a normal hittable body again.
        m_IsHazardable = true;
        m_IsScriptedMotion = false;

        // End the turn — player resets to start position, just like wrong inputs.
        EndTurn();
    }

    // ─── Execution Loop ─────────────────────────────────────────────────────────

    // Iterates through each beat slot, executing one command per beat.
    private IEnumerator ExecutionLoop(int startIndex = 0)
    {
        int i = startIndex;
        while (i < m_MaxTimeIndex && m_IsGamePlaying)
        {
            m_CurrentCommandIndex = i;
            ActionTypeEnum? action = SequenceManager.Instance.GetActionAt(i);

            if (action != null)
            {
                switch (action.Value)
                {
                    case ActionTypeEnum.Left: yield return MoveLeftCommand(); break;
                    case ActionTypeEnum.Right: yield return MoveRightCommand(); break;
                    case ActionTypeEnum.Jump: yield return JumpCommand(); break;
                    case ActionTypeEnum.JumpRight: yield return JumpRightCommand(); break;
                    case ActionTypeEnum.JumpLeft: yield return JumpLeftCommand(); break;
                    case ActionTypeEnum.Interact: yield return InteractCommand(); break;
                }
            }

            // A move that ended on a battery socket has set something off: the charge is
            // travelling the pipe to the door, and the door is swinging open at the end of it.
            // The beat waits for that. The player stands and watches where the socket they
            // just filled leads, and the moves they queued after it are then walked towards a
            // doorway that is already open rather than one that is still shut.
            while (KeySlot.IsAnsweringBattery && m_IsGamePlaying)
                yield return null;

            if (m_BeatGapTime > 0f && m_IsGamePlaying)
                yield return new WaitForSeconds(m_BeatGapTime);

            i++;
        }

        if (m_IsGamePlaying)
            EndTurn();

        // Cleared only on a loop that ran to completion. A loop killed by StopCoroutine
        // never gets here, and its caller overwrites the handle anyway. WinRoutine watches
        // this to know the command in flight has finished.
        m_ExecutionCoroutine = null;
    }

    // ─── Public Command Methods ──────────────────────────────────────────────────

    /// <summary>Moves exactly <see cref="m_MoveDistancePerCommand"/> units to the left.</summary>
    public IEnumerator MoveLeftCommand() => MoveHorizontal(-m_MoveDistancePerCommand);

    /// <summary>Moves exactly <see cref="m_MoveDistancePerCommand"/> units to the right.</summary>
    public IEnumerator MoveRightCommand() => MoveHorizontal(m_MoveDistancePerCommand);

    /// <summary>Jumps vertically in place to exactly <see cref="m_JumpHeight"/> units peak height.</summary>
    public IEnumerator JumpCommand() => PerformJump(0f);

    /// <summary>Jumps right, reaching <see cref="m_JumpHeight"/> height and <see cref="m_JumpForwardDistance"/> horizontal distance.</summary>
    public IEnumerator JumpRightCommand() => PerformJump(m_JumpForwardDistance);

    /// <summary>Jumps left, reaching <see cref="m_JumpHeight"/> height and <see cref="m_JumpForwardDistance"/> horizontal distance.</summary>
    public IEnumerator JumpLeftCommand() => PerformJump(-m_JumpForwardDistance);


    // ─── Movement Logic ──────────────────────────────────────────────────────────

    /// <summary>
    /// Duration of ONE cell of a Left/Right command — the walk's actual unit of motion.
    ///
    /// A command is not run as a single continuous slide; <see cref="MoveHorizontal"/>
    /// breaks it into one-cell steps and hands each to <see cref="MoveOverTime"/>. So the
    /// pace the player is SEEN to walk at is a cell per this duration, and anything else
    /// that wants to move "at walking speed" has to step on the same clock rather than
    /// divide the command's distance by its duration — those two are not the same number,
    /// because each step also costs the fixed-step overshoot and the settle frame
    /// MoveOverTime ends on.
    /// </summary>
    private float CommandStepDuration =>
        m_MoveDuration / Mathf.Max(1, Mathf.RoundToInt(m_MoveDistancePerCommand / GridWorld.CellSize));

    // Walks the player a whole number of cells in the direction of `distance`.
    //
    // The command is decomposed into 1-cell steps rather than run as one continuous
    // velocity: every step begins and ends exactly on a cell centre, so no partial cell
    // is ever left on the clock. That removes the whole class of special cases the
    // velocity version needed - the flush wall stop, the ledge walk, and the two
    // near-identical "finish this unit then fall" branches - and replaces them with a
    // check between steps.
    private IEnumerator MoveHorizontal(float distance)
    {
        if (!m_IsGamePlaying) yield break;

        float sign = Mathf.Sign(distance);
        int cells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(distance) / GridWorld.CellSize));
        float stepDuration = CommandStepDuration;

        transform.localScale = new Vector3(sign, 1f, 1f);

        bool wasScripted = m_IsScriptedMotion;
        m_IsScriptedMotion = true;

        for (int i = 0; i < cells; i++)
        {
            if (!m_IsGamePlaying) break;

            // A pushable brick in the destination cell takes priority over the blocked
            // check, so the push fires instead of the player silently stopping short.
            PushBrick brick = CheckPushBrick(sign);
            if (brick != null)
            {
                m_IsWalking = false;
                m_IsPushing = true;

                // The impact lands with the shove, not with the slide: burst and shake
                // here, ahead of the wind-up below, so the feedback arrives on contact
                // rather than a wind-up later when the brick is already on its way.
                brick.PlayPushHit(sign);

                // Byte braces and shoves BEFORE the brick moves, so the animation reads as
                // the cause of the slide rather than something playing alongside it.
                //
                // Realtime rather than WaitForSeconds because PlayerAnimator steps its
                // frames on unscaled time: under the slow-motion the hazards use, a scaled
                // wait would stretch while the clip it is waiting on kept running at full
                // speed, and the brick would start moving several shoves later.
                //
                // Deliberately outside the blocked check below, so shoving a brick that
                // cannot move still plays the shove — that IS the feedback that it is stuck.
                if (m_PushWindUpTime > 0f)
                    yield return new WaitForSecondsRealtime(m_PushWindUpTime);

                // One shove and done — dropped here rather than after the brick's routine so
                // Byte stands idle through the slide. Held for the whole slide instead, the
                // clip's final frame just sat there for however long the brick took, which
                // read as the animation having hung rather than finished.
                m_IsPushing = false;

                // The brick's own routine drives it, and may drop it several cells. The
                // player simply holds position for the whole of it - nothing moves this
                // body, so there is no drift to undo afterwards. The dynamic version had
                // to freeze the X axis here to stop depenetration sliding the player
                // backwards while the brick fell.
                if (m_IsGamePlaying)
                    yield return StartCoroutine(brick.Push(sign));

                break;
            }

            // Something solid fills the destination cell - stop, still on the grid.
            if (IsCellBlocked(sign)) break;

            Vector2 stepOffset = new Vector2(sign * GridWorld.CellSize, 0f);
            Vector2 stepTarget = m_Rigidbody.position + stepOffset;

            // Nothing under the destination cell - this step walks off a ledge. Play it as
            // a small hop across that same one-cell distance rather than a flat slide into
            // thin air, then let the fall below carry the player the rest of the way down.
            bool stepsOffLedge = m_LedgeHopHeight > 0f && !IsGroundedAfterOffset(stepOffset);

            m_IsWalking = true;
            if (stepsOffLedge)
                yield return HopOverTime(stepTarget, m_LedgeHopHeight);
            else
                yield return MoveOverTime(stepTarget, stepDuration);
            m_IsWalking = false;

            // Stepped off a ledge - drop to the surface below before the next step. The hop
            // hands over the speed it was already descending at, so the drop continues the
            // arc instead of restarting it from a standstill.
            if (!CheckIsGrounded())
                yield return FallToGround(m_HopExitFallSpeed);
            m_HopExitFallSpeed = 0f;
        }

        m_IsWalking = false;
        m_IsPushing = false;
        m_IsScriptedMotion = wasScripted;
        SnapToGrid();
    }

    // Drives the body from where it is to `target` over `duration` seconds.
    // MovePosition rather than a direct position write, so the body sweeps to its
    // destination and still generates the trigger callbacks the door, collectables and
    // key slots depend on.
    private IEnumerator MoveOverTime(Vector2 target, float duration)
    {
        Vector2 start = m_Rigidbody.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
            yield return new WaitForFixedUpdate();
        }

        m_Rigidbody.MovePosition(target);
        yield return new WaitForFixedUpdate();
    }

    // Same one-cell step as MoveOverTime, but arced through a hop that peaks at `height`
    // mid-step and comes back down to the target's height. Used for the step that walks off
    // a ledge, so the player pushes off the edge instead of sliding flat off it and only
    // then remembering to fall.
    //
    // The arc is a REAL ballistic curve under the fall's own gravity, not a decorative
    // parabola fitted to a chosen duration. That is what makes the hand-off to FallToGround
    // seamless: launching at v0 = sqrt(2gh) means the body is descending at exactly v0 again
    // when it returns to the step's height, and the fall picks up from that speed under the
    // same g. A fitted-duration arc had its own implied gravity, so it arrived moving at one
    // speed while the fall restarted from zero - the body visibly stalled at the ledge edge
    // and then re-accelerated. The duration is therefore DERIVED (2*v0/g), not a knob.
    //
    // Ends exactly on `target` - the hop is presentation on top of the same cell-to-cell
    // step, so it cannot leave the body somewhere the flat version wouldn't have. The drop
    // itself is still FallToGround's job, resumed at m_HopExitFallSpeed.
    private IEnumerator HopOverTime(Vector2 target, float height)
    {
        Vector2 start = m_Rigidbody.position;
        float dt = Time.fixedDeltaTime;

        // Quantised to a WHOLE number of physics steps, then g and v0 are re-derived to hit
        // `height` in exactly that many. Letting the natural duration run and clamping the
        // final iteration to it left one truncated step at the end - a single frame that
        // advanced half as far in both axes, landing exactly as the body cleared the edge,
        // which read as a stutter at the ledge. Whole steps make every frame of the arc the
        // same length and still land precisely on `target`.
        float steps = Mathf.Max(1f, Mathf.Round(
            2f * Mathf.Sqrt(2f * m_FallGravity * m_LedgeHopGravityScale * height) /
            (m_FallGravity * m_LedgeHopGravityScale) / dt));

        float duration = steps * dt;
        float g = 8f * height / (duration * duration);
        float v0 = 4f * height / duration;

        m_IsAirborne = true;   // feet have left the ground - play the jump clip, mute footsteps
        m_HopExitFallSpeed = 0f;

        float elapsed = 0f;
        for (int i = 1; i <= (int)steps; i++)
        {
            elapsed = i * dt;

            // Ballistic in Y, linear across the cell in X.
            Vector2 next = Vector2.Lerp(start, target, elapsed / duration);
            next.y = start.y + v0 * elapsed - 0.5f * g * elapsed * elapsed;

            // Nothing stops a kinematic body at a ceiling, so a low roof over the ledge has
            // to be respected here rather than left to a solver: give up the hop's lift and
            // cross flat instead. If even that is blocked, stop and let the caller settle.
            if (IsBodyBlockedBetween(m_Rigidbody.position, next))
            {
                next.y = Mathf.Lerp(start.y, target.y, elapsed / duration);
                if (IsBodyBlockedBetween(m_Rigidbody.position, next)) break;
            }

            m_Rigidbody.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        // Descent speed reached by the point the arc actually ended. Negative while still
        // rising - a ceiling cut the hop short - which the fall reads as "start from rest".
        m_HopExitFallSpeed = Mathf.Max(0f, g * elapsed - v0);

        m_IsAirborne = false;
        yield return new WaitForFixedUpdate();
    }

    // Drops the player to the first surface below, accelerating the way gravity would.
    // Always ends with the feet resting on a surface, so a fall cannot leave the body at
    // a fractional height the way a solver-driven landing could.
    // `initialFallSpeed` lets a caller that was ALREADY descending hand over its speed, so
    // the drop continues that motion rather than restarting from rest. The ledge hop uses
    // it; everything else falls from a standstill, which is what the default means.
    private IEnumerator FallToGround(float initialFallSpeed = 0f)
    {
        if (CheckIsGrounded()) yield break;

        bool wasScripted = m_IsScriptedMotion;
        m_IsScriptedMotion = true;
        m_IsAirborne = true;

        float fallSpeed = Mathf.Clamp(initialFallSpeed, 0f, m_MaxFallSpeed);
        float elapsed = 0f;

        // Distance this drop has covered. Deliberately not paired with a reset of the dive
        // flag: a jump arc that already went into the dive hands it over still latched, and
        // this only ever adds to it.
        float fallen = 0f;

        while (!CheckIsGrounded() && elapsed < m_FallTimeout)
        {
            fallSpeed = Mathf.Min(fallSpeed + m_FallGravity * Time.fixedDeltaTime, m_MaxFallSpeed);

            float step = fallSpeed * Time.fixedDeltaTime;

            // Look for the surface INSIDE this step, so a fast fall lands on it instead
            // of tunnelling through when the step is longer than the remaining gap.
            float drop = GroundDistanceBelow(step);

            if (drop > 0f)
            {
                m_Rigidbody.MovePosition(m_Rigidbody.position + Vector2.down * drop);

                fallen += drop;
                if (fallen > m_GroundPoundDropDistance) m_IsGroundPounding = true;

                yield return new WaitForFixedUpdate();
            }

            elapsed += Time.fixedDeltaTime;

            if (drop < step) break;   // a surface stopped the fall short - landed
        }

        m_IsAirborne = false;
        m_IsGroundPounding = false;   // feet are down — the dive is over
        m_IsScriptedMotion = wasScripted;
        SnapToGrid();
    }

    // ─── Jump Logic ──────────────────────────────────────────────────────────────

    // Performs a jump with deterministic height and optional horizontal distance.
    //
    // The arc is EVALUATED, not simulated. The previous version already solved the whole
    // parabola up front - effective gravity, launch velocity, and the exact air time to
    // the landing surface - and then handed those numbers to the physics engine and hoped
    // it reproduced the same curve, temporarily rewriting gravityScale for the duration.
    // Sampling the closed form directly gives that curve exactly, with no solver in the
    // loop to deflect it into a fractional landing.
    private IEnumerator PerformJump(float horizontalDistance)
    {
        if (!m_IsGamePlaying) yield break;

        AudioManager.Instance?.PlayJump();

        // Kick up dust at the take-off spot (player's grid position).
        SpawnGridEffect(m_JumpStartDust);

        // Effective gravity that peaks at m_JumpHeight in exactly half of m_JumpDuration:
        //   g = 2h / tHalf^2        v0y = g * tHalf
        float tHalf = m_JumpDuration * 0.5f;
        float gEff = 2f * m_JumpHeight / (tHalf * tHalf);
        float v0y = gEff * tHalf;

        Vector2 start = m_Rigidbody.position;
        float targetX = start.x + horizontalDistance;

        // The surface waiting under the destination column sets the air time, so the arc
        // lands on an elevated or lowered platform at the right moment rather than
        // overshooting it.
        float landingY = start.y;
        RaycastHit2D surface = Physics2D.Raycast(
            new Vector2(targetX, start.y + m_JumpHeight + 1f),
            Vector2.down,
            m_JumpHeight + 20f,
            WalkableMask);

        // An empty destination column — a jump out over a pit or off the end of a platform.
        // The arc below runs ON past its landing time in that case instead of stopping dead
        // at the height it launched from, so the drop is the same curve carrying on rather
        // than a straight fall starting where the jump gave up.
        bool hasLandingSurface = surface.collider != null;

        if (hasLandingSurface)
            landingY = surface.point.y + FootOffset();

        float dY = landingY - start.y;

        // Descending-arc solution of  dY = v0y*t - 0.5*g*t^2.
        // A negative discriminant means the jump cannot reach that height at all; fall
        // back to the flat-ground duration.
        float disc = v0y * v0y - 2f * gEff * dY;
        float tLand = disc >= 0f ? (v0y + Mathf.Sqrt(disc)) / gEff : m_JumpDuration;
        float vx = tLand > 0f ? horizontalDistance / tLand : 0f;

        // Face direction for lateral jumps
        if (!Mathf.Approximately(horizontalDistance, 0f))
            transform.localScale = new Vector3(horizontalDistance > 0f ? 1f : -1f, 1f, 1f);

        bool wasScripted = m_IsScriptedMotion;
        m_IsScriptedMotion = true;
        m_IsAirborne = true;

        float t = 0f;

        // Latched the moment a wall stops the arc's forward run, and never cleared: the rest
        // of THIS jump is vertical only.
        //
        // Re-testing the block each step instead let the arc resume its forward run as soon
        // as the body rose clear of whatever stopped it, so a JumpRight into a wall three
        // cells tall climbed the face and then carried on right off the top of it — the arc
        // does not have the horizontal reach to get over that wall, and letting the pinned
        // distance be spent later is what made it look like it did. A wall means the jump
        // goes straight up and comes back down on the cell it launched from.
        //
        // Low obstacles are unaffected: the arc leaves the ground far faster than it travels
        // sideways (v0y is several times vx), so a one-cell step is already cleared in Y
        // before the body reaches its face and never latches this at all.
        bool horizontalBlocked = false;

        // Past tLand only when the destination column was empty, and then only while the
        // curve is still accelerating within the game's own fall speed. Once it reaches
        // m_MaxFallSpeed the arc has nothing left to add — every fall in the game is capped
        // there — so FallToGround below takes over at exactly that speed and the handover is
        // invisible. Bounding it this way also keeps the body from sailing off across the
        // level on the way down.
        while (t < tLand ||
               (!hasLandingSurface && gEff * t - v0y < m_MaxFallSpeed))
        {
            float previousT = t;
            t = hasLandingSurface
                ? Mathf.Min(t + Time.fixedDeltaTime, tLand)
                : t + Time.fixedDeltaTime;

            // Y is still the closed form evaluated at t, so the deterministic landing
            // height is untouched. X advances INCREMENTALLY from where the body actually
            // is, instead of being re-evaluated as start.x + vx*t. Re-evaluating let the
            // parabola's x run on while a wall pinned the body, so the first sample whose
            // probe read clear teleported the body across the entire accumulated gap —
            // through the wall's face and into its interior.
            Vector2 current = m_Rigidbody.position;
            float arcY = start.y + v0y * t - 0.5f * gEff * t * t;
            float stepX = horizontalBlocked ? 0f : vx * (t - previousT);
            Vector2 next = new Vector2(current.x + stepX, arcY);

            // Nothing stops a kinematic body at a wall, so the arc has to respect walls
            // itself: drop the horizontal component and slide straight up or down the
            // face, and if even that is blocked (a ceiling, or an inside corner) abandon
            // the arc and let the fall below settle the player.
            if (IsBodyBlockedBetween(current, next))
            {
                Vector2 slide = new Vector2(current.x, arcY);
                if (IsBodyBlockedBetween(current, slide)) break;

                // The vertical-only move is clear, so what blocked the diagonal was the
                // horizontal component. Give up the forward run for the WHOLE remaining arc
                // (see horizontalBlocked) and ride the curve up and back down this face.
                next = slide;
                horizontalBlocked = true;
            }

            m_Rigidbody.MovePosition(next);
            yield return new WaitForFixedUpdate();

            // The arc has fallen past the height it launched from and is still going: the
            // jump reached the end of its movement without a platform under it, so what is
            // left is a straight drop rather than the back half of a jump. Read off the
            // evaluated arcY, not the body, because MovePosition has not been applied yet.
            //
            // A jump that lands back on its own level stops at tLand exactly on start.y and
            // never trips this; one out over a pit, or onto a platform well below, does.
            if (!m_IsGroundPounding && start.y - arcY > m_GroundPoundDropDistance)
                m_IsGroundPounding = true;

            // Landed on something the destination-column ray never saw - a brick pushed
            // into the path, or a platform that moved under the arc. Tested only past the
            // apex so it cannot trip on the ground the jump launched from.
            if (t > tHalf && CheckIsGrounded()) break;
        }

        m_IsAirborne = false;
        m_IsScriptedMotion = wasScripted;

        // Settle if the arc finished above a surface, because a wall cut it short or the
        // destination column turned out to be empty.
        //
        // Handed the speed the arc was ALREADY descending at, the same way the ledge hop
        // hands over its own. Falling from rest here is what made the drop off a platform
        // read as a snap: the body arrived travelling at the jump's full landing speed and
        // then hung there while the fall accelerated it back up from zero. Negative while
        // still rising — a ceiling cut the arc short — which the fall reads as "from rest".
        yield return FallToGround(Mathf.Max(0f, gEff * t - v0y));

        // FallToGround clears the dive when it lands, but returns immediately when the arc
        // already finished on a surface — so the landing is signed off here too.
        m_IsGroundPounding = false;

        SnapToGrid();

        // Puff of dust at the landing spot (player's settled grid position).
        SpawnGridEffect(m_JumpEndDust);
    }

    // Distance from the body origin to the bottom of the collider - how far above a
    // surface the origin sits when the player is standing on it.
    private float FootOffset() =>
        m_Collider != null ? m_Rigidbody.position.y - m_Collider.bounds.min.y : GridWorld.HalfCell;

    // Instantiates a one-shot effect prefab at the centre of the cell the player occupies
    // (the same grid SnapToGrid settles them onto). No-op when the prefab is unassigned.
    // The prefab's OneShotEffect destroys itself when done.
    private void SpawnGridEffect(GameObject prefab)
    {
        if (prefab == null) return;

        Vector2 gp = m_Rigidbody != null ? m_Rigidbody.position : (Vector2)transform.position;
        Vector2 cell = GridWorld.SnapToCell(gp);

        Instantiate(prefab, new Vector3(cell.x, cell.y, transform.position.z), Quaternion.identity);
    }

    // Instantiates a particle effect prefab at `position`, at the player's own depth so it
    // draws with them rather than at z 0, shrunk by `scale`. ParticleEffectSpawner owns the
    // cleanup, and the scale is REQUIRED rather than defaulted: every prefab these come from
    // is authored more than a dozen world units across, so an unscaled one swamps the level.
    private void SpawnParticleEffect(GameObject prefab, Vector2 position, float scale) =>
        ParticleEffectSpawner.Spawn(
            prefab, new Vector3(position.x, position.y, transform.position.z), scale);

    // ─── Portal Spawn / Exit Animation ───────────────────────────────────────────

    /// <summary>
    /// The player's arrival, called by <see cref="LevelEntryDoor"/> once its door has opened:
    /// the body appears on `doorPoint`, spins in out of the doorway — from zero size, spinning
    /// fast, winding down into its normal upright pose — and moves to the level's start cell
    /// as it does, leaving the player standing there at full size.
    ///
    /// The door awaits this before closing itself behind them, and calls it in the same breath
    /// as enabling this object, so everything that hides the body runs before anything renders.
    /// </summary>
    public IEnumerator EnterFromDoorRoutine(Vector2 doorPoint)
    {
        // Deliberately NOT an iterator method: with no yield of its own, the hide below runs
        // the moment the door calls this, rather than being deferred until the door's
        // coroutine first steps the returned enumerator. That is what keeps a player enabled
        // at full size from rendering for a frame in the doorway before it is shrunk away.
        HideForArrival();
        return ArriveRoutine(doorPoint);
    }

    // The level intro for a level with NO entry door: fade up from black on an empty level and
    // spin the player in on their start cell. Levels with an entry door never run this — the
    // door owns their opening, fade included.
    private IEnumerator SpawnPortalRoutine()
    {
        // Shrunk away BEFORE the fade rather than when the spin starts. The fade reveals
        // the level, so a full-size player standing there would be visible through it and
        // then pop out of existence the moment the spin began.
        HideForArrival();

        if (UIManager.Instance != null)
            yield return UIManager.Instance.FadeRoutine(1f, 0f);

        yield return ArriveRoutine(m_StartPosition);
    }

    // Shared by both openings: the body materialises at `from`, spins in, and moves to the
    // level's start cell.
    //
    // The destination is m_StartPosition, not wherever the body currently sits — that is the
    // snapped cell every reset and death returns the player to, so the arrival leaves them
    // exactly where the rest of the game agrees the level starts, even if the scene transform
    // was nudged off-grid.
    //
    // Input is held off for all of it, so a queued command can't start writing the facing
    // scale the animation is driving, and so the level doesn't begin under a player who is
    // still on their way out of the doorway.
    private IEnumerator ArriveRoutine(Vector2 from)
    {
        // Awake caches the body; a null one means Awake never finished on this object — it
        // was destroyed as a duplicate by the singleton guard, or the Rigidbody2D the
        // component requires is missing. Either way the arrival cannot move anything, and
        // driving it would throw one UnassignedReferenceException per level load. Said out
        // loud rather than swallowed: it means the level is misconfigured.
        if (m_Rigidbody == null)
        {
            Debug.LogError(
                $"[PlayerController] '{name}' has no Rigidbody2D to move — its Awake did not " +
                "complete, so the level's arrival is being skipped.", this);
            yield break;
        }

        HideForArrival();

        // Into the doorway before the spin, so the player materialises in the door rather
        // than on the cell they are about to move to. Safe to write directly: the body is at
        // zero scale and m_IsPortalAnimating has the passive settle stood down.
        m_Rigidbody.position = from;

        if (m_SpawnDoorLead > 0f)
            yield return new WaitForSecondsRealtime(m_SpawnDoorLead);

        // Spin and travel TOGETHER — the mirror of the win's spin-and-travel out of the exit
        // door. The player unwinds and grows on the way across, instead of completing the
        // whole spin in the doorway and only then moving to the start cell as a separate step.
        //
        // The spin owns the scale, the rotation and the motion flags — including
        // m_IsPortalAnimating, which it clears on its way out, handing the body back to
        // normal play. The move underneath it only moves the body.
        //
        // Unlike the win's pair, these two ARE fitted to a shared duration: an arrival ends
        // at full size, so a move that outran its spin would arrive half-grown and finish
        // spinning on the spot. Both are awaited even so — the spin runs on unscaled time
        // and the move on the physics clock, so whichever is left simply finishes.
        Coroutine spin = StartCoroutine(PortalRoutine(0f, 1f, spinIn: true));
        yield return MoveFromEntryDoorRoutine(m_StartPosition, m_PortalDuration);
        yield return spin;

        DeviceInputProvider.Instance?.SetEnabled(true);
    }

    // Takes the player off the screen and out of play for the wait before their arrival.
    // Runs synchronously — the door calls into the arrival on the same frame it enables this
    // object, and the body must be shrunk away before that frame renders.
    //
    // m_IsPortalAnimating covers the wait as well as the spin: it is what stands the passive
    // settle in FixedUpdate and the hazard sweep in Update down, and neither has any business
    // running on a player who is currently nothing but a spawn point.
    private void HideForArrival()
    {
        m_IsEnteringFromDoor = true;
        DeviceInputProvider.Instance?.SetEnabled(false);
        m_IsPortalAnimating = true;
        ApplyPortalPose(FacingSign(), 0f, 0f);
    }

    // Carries the player out of the entry doorway onto the level's start position over
    // `duration`, accelerating the whole way so the arrival has some weight to it rather than
    // gliding in at a constant crawl.
    //
    // Position ONLY — the spin running alongside owns the scale, the rotation and the sprite,
    // exactly as the win's travel leaves those to the exit portal. Nor does this manage the
    // motion flags: m_IsPortalAnimating is set for the whole of it, which is what holds the
    // passive settle in FixedUpdate off the body, and the spin has hazards off for its own run.
    //
    // Fitted to `duration` rather than walked at the command pace the win's travel uses. The
    // two doorways are not the same problem: the exit is caught from wherever the player
    // happened to touch the door, so its travel has to keep the pace it was already moving
    // at, while the entry always starts from the same doorway and has to be finished — at
    // full size, upright — by the time the spin is. The easing is quadratic, the shape a fall
    // traces under constant gravity.
    //
    // Deliberately NOT FallToGround, for a door placed above the start cell: that one asks the
    // collider what is underneath and stops at the first surface it finds, which on a doorway
    // tucked under an overhang would strand the player on the roof instead of on their start
    // cell. The target here is known, so the move is driven straight to it and cannot end
    // anywhere else.
    private IEnumerator MoveFromEntryDoorRoutine(Vector2 target, float duration)
    {
        Vector2 from = m_Rigidbody.position;

        if (duration <= 0f || Vector2.Distance(from, target) < 0.01f)
        {
            m_Rigidbody.position = target;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            m_Rigidbody.MovePosition(Vector2.Lerp(from, target, t * t));
            yield return new WaitForFixedUpdate();
        }

        m_Rigidbody.position = target;
        m_PassiveFallSpeed = 0f;

        // Puff of dust at the arrival spot, the same one a jump lands with — the player has
        // just put their feet down on the start cell.
        SpawnGridEffect(m_JumpEndDust);
    }

    /// <summary>
    /// Spins the player out of the level — the mirror of the spawn arrival. Called by
    /// <see cref="WinRoutine"/> once the player is standing on the door's interaction
    /// point. Ends with the player at zero size; nothing restores it, because the scene
    /// is on its way out behind this.
    /// </summary>
    public IEnumerator PlayExitPortalRoutine() => PortalRoutine(1f, 0f, spinIn: false);

    // The shared spin. `spinIn` decides which end of the animation the fast part sits at:
    // an arrival decelerates into the still pose, a departure accelerates out of it, so in
    // both cases the player is whirling while they are small and settled while they are
    // full size. The total turn is a whole number of revolutions, so the pose the
    // animation lands on is upright either way.
    private IEnumerator PortalRoutine(float fromScale, float toScale, bool spinIn)
    {
        m_IsPortalAnimating = true;

        // Restored rather than forced back on: the spawn arrives with hazards armed and
        // wants them armed again, while the win has already disarmed them for good and a
        // blind re-arm here would put the player back in reach of a beam on their way out.
        bool wasHazardable = m_IsHazardable;
        bool wasScripted = m_IsScriptedMotion;
        m_IsHazardable = false;
        m_IsScriptedMotion = true;

        m_IsWalking = false;
        m_IsAirborne = false;
        AudioManager.Instance?.SetWalking(false);
        m_Animator?.Play(PlayerAnimState.Idle);

        // Captured once instead of re-read each frame: the scale the animation writes has
        // no usable sign at the zero end, so the facing has to come from before it started.
        float facing = FacingSign();
        float totalSpin = m_PortalSpins * 360f;
        float elapsed = 0f;

        // Unscaled, so a level that ended while some slow-motion effect was still winding
        // down doesn't play the exit spin in slow motion too.
        while (elapsed < m_PortalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / m_PortalDuration);

            float spinT = spinIn ? 1f - (1f - t) * (1f - t) : t * t;
            float angle = -totalSpin * (spinIn ? 1f - spinT : spinT);

            ApplyPortalPose(facing, Mathf.Lerp(fromScale, toScale, Mathf.SmoothStep(0f, 1f, t)), angle);
            yield return null;
        }

        ApplyPortalPose(facing, toScale, 0f);

        m_IsScriptedMotion = wasScripted;
        m_IsHazardable = wasHazardable;
        m_IsPortalAnimating = false;
    }

    // Writes one frame of the portal pose. `scale` is a 0..1 fraction of the spawn scale,
    // and the facing is applied on top of it, so the animation only ever changes how big
    // the player is — never which way they look.
    private void ApplyPortalPose(float facing, float scale, float angleDegrees)
    {
        transform.localScale = new Vector3(
            facing * Mathf.Abs(m_OriginalScale.x) * scale,
            m_OriginalScale.y * scale,
            m_OriginalScale.z);

        transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
    }

    // Which way the player is currently facing, as the ±1 the movement code writes into
    // localScale.x. Falls back to the spawn facing when the current scale is mid-portal
    // and its sign says nothing.
    private float FacingSign()
    {
        if (!Mathf.Approximately(transform.localScale.x, 0f))
            return Mathf.Sign(transform.localScale.x);

        return m_OriginalScale.x < 0f ? -1f : 1f;
    }

    // ─── Interact Command ────────────────────────────────────────────────────────

    // Triggers interaction and holds for m_MoveDuration so it occupies the same
    // time slot as a movement command, keeping beat rhythm consistent.
    private IEnumerator InteractCommand()
    {
        if (!m_IsGamePlaying) yield break;

        TryInteract();
        yield return new WaitForSeconds(m_MoveDuration);
    }

    private void TryInteract()
    {
        int count = Physics2D.OverlapCircle(transform.position, m_InteractRadius, m_InteractFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            if (m_OverlapResults[i].TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact();
                break;
            }
        }
    }
    // ─── Ground Check & Animation ────────────────────────────────────────────────

    // Continuous spike check — covers moving spikes that slide into the player via
    // transform.position, which don’t reliably fire OnTriggerEnter2D without a Rigidbody2D.

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!m_IsHazardable) return;

        if (IsLethalSpike(other))
        {
            AbortExecution();
            StartCoroutine(DeathRoutine());
        }
    }

    private void CheckSpikeOverlap()
    {
        if (m_Collider == null) return;

        // Broad phase only. The overlap just gathers candidates cheaply; IsLethalSpike
        // decides, and it decides on grid cells rather than on this box.
        Bounds b = m_Collider.bounds;

        int count = Physics2D.OverlapBox(
            b.center, b.size * k_ProbeShrink, 0f, m_NoFilter, m_OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit.gameObject == gameObject) continue;
            if (!IsLethalSpike(hit)) continue;

            AbortExecution();
            StartCoroutine(DeathRoutine());
            return;
        }
    }

    /// <summary>
    /// True when <paramref name="other"/> is a spike the player has actually moved INTO —
    /// meaning the two occupy the same grid cell.
    ///
    /// The test is deliberately not "do the colliders touch". A spike owns one cell, so
    /// entering that cell is what kills; clipping a corner of its art collider while
    /// passing through a neighbouring cell is a near miss and must not. Overlap-based
    /// tests cannot tell those apart — they read a grazing jump exactly the same as
    /// standing on the spike, which killed players who never reached it.
    ///
    /// A cell holds the player once their centre is inside it, so a body more than
    /// half-way into the spike's cell dies and anything shallower survives.
    /// </summary>
    private bool IsLethalSpike(Collider2D other)
    {
        if (other == null) return false;
        if (!other.CompareTag("Spike")) return false;
        if (other.TryGetComponent(out EnemyMovement _)) return false; // enemy, not a spike

        Vector2 playerCell = GridWorld.SnapToCell(m_Rigidbody.position);
        Vector2 spikeCell = GridWorld.SnapToCell((Vector2)other.transform.position);

        return Mathf.Approximately(playerCell.x, spikeCell.x)
            && Mathf.Approximately(playerCell.y, spikeCell.y);
    }

    // True when a walkable surface sits directly under the player's footprint. A thin
    // probe just below the collider's bottom edge, narrowed slightly so a wall the player
    // is pressed flush against is never mistaken for a floor. Deliberately the same shape
    // as PushBrick's support probe, so a brick and the player can never disagree about
    // whether the same cell is supported.
    //
    // This replaces the pair of foot rays plus the CheckLedgeSupport fallback. That
    // fallback existed only to paper over dynamic-body behaviour: the solver kept holding
    // the player up while the two colliders were still within their contact offsets, so
    // the rays reported thin air for a body physics considered supported, and the move
    // code would order a fall the body could not perform. A kinematic body is held up by
    // nothing, so the honest question is simply "is there a surface under my footprint".
    private bool CheckIsGrounded()
    {
        if (m_Collider == null) return false;

        Bounds b = m_Collider.bounds;
        Vector2 size = new Vector2(b.size.x * k_ProbeShrink, k_GroundProbeThickness);
        Vector2 centre = new Vector2(b.center.x, b.min.y - k_GroundProbeThickness * 0.5f);

        return Physics2D.OverlapBox(centre, size, 0f, WalkableMask) != null;
    }

    // CheckIsGrounded asked one step early: would a surface sit under the player's
    // footprint if the body were displaced by `offset`? Same probe shape and same
    // WalkableMask, just moved - so "the next cell has nothing under it" is decided by
    // exactly the rule that will judge the player once they get there.
    private bool IsGroundedAfterOffset(Vector2 offset)
    {
        if (m_Collider == null) return false;

        Bounds b = m_Collider.bounds;
        Vector2 size = new Vector2(b.size.x * k_ProbeShrink, k_GroundProbeThickness);
        Vector2 centre = new Vector2(
            b.center.x + offset.x, b.min.y + offset.y - k_GroundProbeThickness * 0.5f);

        return Physics2D.OverlapBox(centre, size, 0f, WalkableMask) != null;
    }

    // How far the player can drop before landing, capped at maxDistance. Returns
    // maxDistance when nothing is in reach.
    //
    // The probe is a THIN box swept down from the FOOT PLANE - the same shape
    // CheckIsGrounded uses - not the whole body swept down from its centre. A full-height
    // box starts out overlapping anything the body is already touching, and Physics2D
    // reports a cast that begins inside a collider at distance 0. The ceiling was the case
    // that mattered: IsBodyBlockedBetween shrinks its sweep by k_ProbeShrink, so a jump
    // that ends against a roof leaves the head up to 5% of the body's height inside it,
    // and from there this returned 0 for every fall. FallToGround and the passive settle
    // both read that as "landed", so the player hung under the platform and the next Left/
    // Right command walked that hovering body along the underside of it.
    //
    // Starting at the feet means only geometry the player could actually fall onto can
    // answer. Distances are unchanged for a genuine surface below: a downward box cast
    // measures how far the box's leading face travels, and that face is the feet either way.
    private float GroundDistanceBelow(float maxDistance)
    {
        if (m_Collider == null) return maxDistance;

        Bounds b = m_Collider.bounds;
        Vector2 size = new Vector2(b.size.x * k_ProbeShrink, k_GroundProbeThickness);
        Vector2 origin = new Vector2(b.center.x, b.min.y + k_GroundProbeThickness * 0.5f);

        RaycastHit2D hit = Physics2D.BoxCast(
            origin, size, 0f, Vector2.down, maxDistance, WalkableMask);

        return hit.collider != null ? Mathf.Min(hit.distance, maxDistance) : maxDistance;
    }

    // True when the cell one step away in `sign` is filled by something solid.
    //
    // Stepping cell to cell means an obstacle has to be seen BEFORE the step, while it is
    // still a full cell away - so the question is about the destination cell, not about
    // the thin sliver just outside the player's side face the way it was when velocity
    // drove the body into walls before anything noticed.
    //
    // SWEPT, not a static overlap of that cell. The levels build their collision as a
    // CompositeCollider2D with Outlines geometry, which is hollow: it carries edges along
    // the surface of the level and nothing at all in the interior. A box parked inside the
    // destination cell touches none of those edges - it stops 0.05 short of the boundary
    // the wall's outline runs along - so every wall read as empty air and the player
    // walked straight through it. A cast crosses that face, which is where the geometry
    // actually is, and keeps working unchanged if the composite is ever rebuilt as solid
    // polygons instead.
    private bool IsCellBlocked(float sign)
    {
        if (m_Collider == null || Mathf.Approximately(sign, 0f)) return false;

        Bounds b = m_Collider.bounds;

        // Vertically shrunk so the floor the player is standing on - which runs on into
        // the destination cell - is never read as a wall. Horizontally thin because it is
        // the sweep, not the box, that has to reach across the cell boundary.
        Vector2 size = new Vector2(GridWorld.CellSize * 0.1f, b.size.y * 0.8f);

        // Reaches to just inside the FAR edge of the destination cell. Stopping short of
        // that edge matters on outline geometry: it doubles as the near face of the cell
        // beyond, so a sweep that touched it would halt the player a full cell early in
        // front of every wall.
        float distance = GridWorld.CellSize * 1.5f - size.x * 0.5f - 0.05f;

        return Physics2D.BoxCast(
            b.center, size, 0f, new Vector2(Mathf.Sign(sign), 0f),
            distance, WalkableMask).collider != null;
    }

    // True when sweeping the body from `from` to `to` would cross something solid. Used by
    // the jump arc, which has no solver to stop it at a wall.
    //
    // SWEPT, for exactly the reason IsCellBlocked is. The levels build their collision as a
    // CompositeCollider2D with Outlines geometry, which is hollow: it carries edges along
    // the surface of the level and nothing at all in the interior. A box tested statically
    // at `to` touches none of those edges once it sits fully inside a wall, so it reported
    // clear air from inside solid rock — and because the probe is shrunk by k_ProbeShrink,
    // there was a 0.05-wide band just past every wall face where that happened. The body
    // crossed the face in one step and kept going, ending up a full cell deep in the wall.
    // A cast crosses the face, which is where the geometry actually is.
    private bool IsBodyBlockedBetween(Vector2 from, Vector2 to)
    {
        if (m_Collider == null) return false;

        Bounds b = m_Collider.bounds;
        Vector2 colliderOffset = (Vector2)b.center - m_Rigidbody.position;
        Vector2 size = b.size * k_ProbeShrink;
        Vector2 delta = to - from;
        float distance = delta.magnitude;

        // Nothing to sweep (a purely vertical step at the apex, say) — ask about the
        // destination itself, which is all a zero-length cast could report anyway.
        if (distance < Mathf.Epsilon)
            return Physics2D.OverlapBox(to + colliderOffset, size, 0f, WalkableMask) != null;

        return Physics2D.BoxCast(
            from + colliderOffset, size, 0f, delta / distance, distance, WalkableMask).collider != null;
    }

    // Returns the PushBrick occupying the cell one step away in `signedDirection`, or
    // null. Queries every layer so the brick does not have to sit on the Ground layer.
    private PushBrick CheckPushBrick(float signedDirection)
    {
        if (m_Collider == null || Mathf.Approximately(signedDirection, 0f)) return null;

        Bounds b = m_Collider.bounds;
        Vector2 size = new Vector2(b.size.x * k_ProbeShrink, b.size.y * 0.8f);
        Vector2 centre = new Vector2(
            b.center.x + Mathf.Sign(signedDirection) * GridWorld.CellSize, b.center.y);

        int count = Physics2D.OverlapBox(centre, size, 0f, m_NoFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit == m_Collider || hit.isTrigger) continue;

            PushBrick brick = hit.GetComponentInParent<PushBrick>();
            if (brick == null) continue;

            // Ignore a brick the player is standing ON: its top sits at or below the
            // player's feet, so it is a floor, not a wall to push. Without this the brick
            // gets shoved out from under the player the moment they land on top of it.
            if (hit.bounds.max.y <= m_Collider.bounds.min.y + 0.1f) continue;

            return brick;
        }
        return null;
    }

#if UNITY_EDITOR
    // Draws the ground probe (green) and the two destination-cell probes (cyan) so the
    // collider size and the cell alignment can be checked in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Collider2D col = m_Collider != null ? m_Collider : GetComponent<Collider2D>();
        if (col == null) return;

        Bounds b = col.bounds;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, b.min.y - k_GroundProbeThickness * 0.5f, 0f),
            new Vector3(b.size.x * k_ProbeShrink, k_GroundProbeThickness, 0f));

        // The blocked-cell sweeps: drawn as the region each cast covers, so the reach can
        // be checked against the cell boundaries it has to land between.
        Gizmos.color = Color.cyan;
        float span = GridWorld.CellSize * 1.5f - 0.05f + GridWorld.CellSize * 0.05f;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Gizmos.DrawWireCube(
                new Vector3(b.center.x + sign * span * 0.5f, b.center.y, 0f),
                new Vector3(span, b.size.y * 0.8f, 0f));
        }
    }
#endif

    // Settles the player onto the grid after every move/jump, eliminating the
    // floating-point drift that accumulates over a command.
    //
    // BOTH axes snap to a whole unit, because cell centres are at integer world
    // coordinates (see GridWorld). Y used to snap to the 0.5 grid to "sit flush on
    // platforms placed on the half grid" - but a half-integer Y is exactly a body
    // straddling two rows, which is the drift this method exists to remove. It made the
    // snap preserve the fractional positions the physics solver produced instead of
    // correcting them. Any platform that genuinely needs a half-unit surface must be
    // moved onto the cell grid instead.
    private void SnapToGrid()
    {
        m_Rigidbody.position = GridWorld.SnapToCell(m_Rigidbody.position);
    }

    // ─── Turn End / Abort ────────────────────────────────────────────────────────

    private void EndTurn()
    {
        m_IsGamePlaying = false;
        m_IsWalking = false;
        m_EndTurnCoroutine = StartCoroutine(WaitForEndStuff());
    }

    // Stops the execution coroutine immediately (called on spike/win triggers)
    private void AbortExecution()
    {
        m_IsGamePlaying = false;

        if (m_ExecutionCoroutine != null)
        {
            StopCoroutine(m_ExecutionCoroutine);
            m_ExecutionCoroutine = null;
        }

        if (m_EndTurnCoroutine != null)
        {
            StopCoroutine(m_EndTurnCoroutine);
            m_EndTurnCoroutine = null;
        }

        // StopCoroutine above can kill MoveHorizontal / PerformJump / a waypoint transport
        // part-way through, so the lines that would have cleared these never run. Left set,
        // m_IsScriptedMotion would permanently disable the passive settle in FixedUpdate
        // and the player would hover the next time the ground went away.
        m_IsScriptedMotion = false;
        m_IsWalking = false;
        m_IsPushing = false;
        m_IsAirborne = false;
        m_IsGroundPounding = false;
        m_PassiveFallSpeed = 0f;
        m_PassiveFallDistance = 0f;
    }

    // Short delay before resetting position and unlocking UI
    private IEnumerator WaitForEndStuff()
    {
        // Restore time scale in case any slow-motion was still active.
        // WaitForSecondsRealtime is unaffected by Time.timeScale so the reset always fires on time.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        yield return new WaitForSecondsRealtime(0.5f);

        // Hazards are NOT disarmed here. The turn ending doesn't make the player any
        // less of a body standing in the level — it just resets them to spawn, where
        // they stay hittable like anywhere else.

        // Use rigidbody position reset (not transform) to keep physics state consistent
        m_Rigidbody.position = m_StartPosition;
        m_PassiveFallSpeed = 0f;
        m_PassiveFallDistance = 0f;
        m_IsPushing = false;
        m_IsGroundPounding = false;
        transform.localScale = m_OriginalScale;   // restore spawn facing
        m_EndTurnCoroutine = null;

        // A turn that ended without winning is a failed attempt — count it toward the
        // combined tally; the doctor gloats on every Nth failure before input resets.
        if (EvilDoctorAnimationController.Instance != null)
            yield return EvilDoctorAnimationController.Instance.RegisterFailureRoutine();

        GameManager.Instance?.PlayEnded();
    }

    // ─── Collision ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!m_IsHazardable || GameManager.Instance == null) return;

        if (IsLethalSpike(other))
        {
            AbortExecution();
            StartCoroutine(DeathRoutine());
        }
        else if (other.CompareTag("Door"))
        {
            // The door's collider spans the whole doorway, so this fires again on every
            // step the player takes into it. Only the first touch owns the win.
            if (!m_IsWinning && IsDoorOpen(other))
            {
                m_IsWinning = true;
                StartCoroutine(WinRoutine(other));
            }
        }
    }

    // Normally the door is open because the battery went into its socket, which is what
    // IsKeyCollected records. The early levels have no battery and no socket at all, so
    // their door is authored to open on its own — that door says so itself, and touching it
    // is the win.
    private bool IsDoorOpen(Collider2D door)
    {
        if (GameManager.Instance.IsKeyCollected) return true;

        LevelExitDoor exit = door.GetComponentInParent<LevelExitDoor>();
        return exit != null && exit.OpensWithoutKey;
    }

    private IEnumerator WinRoutine(Collider2D door)
    {
        // The level is won — don't let a hazard kill the player during the entry or
        // the fade out.
        m_IsHazardable = false;

        // The door interrupts whatever was running outright: the command in flight is
        // dropped where it stands, mid-arc if need be, and the portal takes over from that
        // exact spot. Also cancels a pending end-of-turn reset, which would otherwise yank
        // the player back to spawn in the middle of the win.
        AbortExecution();

        AudioManager.Instance?.SetWalking(false);
        AudioManager.Instance?.PlayWin();

        // Spin and travel TOGETHER, rather than walking in and then spinning on the spot.
        // The exit portal animation owns the spin and the shrink; the travel underneath it
        // only moves the body, at the pace the player walks — the two are no longer fitted
        // to a shared duration, so whichever finishes first simply waits for the other.
        // Both are awaited, so the outro below starts only once the player is standing on
        // the interaction point AND fully spun out.
        Coroutine spin = StartCoroutine(PlayExitPortalRoutine());
        yield return TravelToDoorInteractionPoint(door);
        yield return spin;

        // The player is now inside the door — on its interaction point and spun down to
        // nothing. The burst goes off there, punctuating the moment they vanish. Spawned
        // from the resolved point rather than the body's position, so a travel that hit its
        // timeout still puts the effect in the doorway where it belongs.
        SpawnParticleEffect(
            m_DoorEnterEffect, ResolveDoorInteractionPoint(door), m_DoorEnterEffectScale);

        // Parks the body for the rest of the outro. The portal hands the motion flags back
        // when it finishes — correct for the spawn spin, which returns the player to normal
        // play — but here the player has just been deposited on the door's interaction
        // point, and a doorway point is usually in mid-air. Handed back, the passive settle
        // in FixedUpdate immediately starts dropping them: invisible at zero scale, but the
        // camera follows the player and would sink with them through the doctor reaction and
        // the fade. Never restored, because the scene is on its way out behind this.
        m_IsScriptedMotion = true;

        // The door shuts behind them. Awaited rather than fired and forgotten, so the doctor's
        // reaction and the fade play over a closed doorway instead of starting while it is
        // still swinging shut. The door owns the animation and its doorway effect; all this
        // knows is that the player is in and the level is over.
        LevelExitDoor exit = door != null ? door.GetComponentInParent<LevelExitDoor>() : null;
        if (exit != null)
            yield return exit.CloseRoutine();

        // Doctor reacts (sad) — wait for the full reaction before leaving the level.
        if (EvilDoctorAnimationController.Instance != null)
            yield return EvilDoctorAnimationController.Instance.PlayLevelCompletedRoutine();

        yield return new WaitForSecondsRealtime(0.2f);
        if (UIManager.Instance != null)
            yield return StartCoroutine(UIManager.Instance.FadeRoutine(0f, 1f));
        // Recorded BEFORE the scene load is requested, and null-guarded: LevelManager is a
        // menu-scene singleton, so it simply doesn't exist when a level scene is played
        // directly. Unguarded and ordered the other way round, this threw a
        // NullReferenceException on every win and the completion was never saved.
        if (LevelManager.Instance != null)
            LevelManager.Instance.CompleteLevel(SceneManager.GetActiveScene().buildIndex, 0);

        GameManager.Instance.LoadNextLevel();
    }

    // Carries the player from wherever the door caught them to the doorway's interaction
    // point. Position ONLY — the exit portal animation running alongside owns the spin, the
    // shrink and the sprite, so this must not touch the transform's scale or rotation.
    //
    // Deliberately does NOT settle the player on the ground first, and does not wait for the
    // command in flight to finish. The walk-in this replaced did both: a win taken mid-jump
    // had its arc cut off in the air and then dropped the player straight down the door's
    // face before the walk across could even start. Travelling from the exact spot the door
    // was touched is what makes it read as the door pulling them in.
    //
    // Travels at exactly the pace a Left/Right command walks, in the SAME one-cell steps
    // through the SAME MoveOverTime — so the last stretch into the doorway is
    // frame-for-frame the motion the player was already watching, not a separate glide.
    //
    // It is therefore NOT fitted to the spin's duration. Fitting it was what made the pace
    // arbitrary: the same one-second lerp covered whatever distance the door happened to be
    // away, so a win taken from two cells out crawled and one taken from across the doorway
    // shot across. The spin runs alongside on its own clock and simply carries on spinning
    // on the spot once the player arrives — which is the common case, since the door is
    // normally caught a cell or two from its interaction point.
    //
    // Nothing here manages m_IsScriptedMotion: m_IsPortalAnimating is already set for the
    // whole of this, which is what holds the passive settle in FixedUpdate off the body.
    // Position ONLY — the portal alongside owns the scale and the rotation.
    //
    // The step loop is capped by k_DoorApproachTimeout so a mis-placed interaction point
    // can't leave the player travelling forever at a door that ends the level.
    private IEnumerator TravelToDoorInteractionPoint(Collider2D door)
    {
        Vector2 target = ResolveDoorInteractionPoint(door);
        float startTime = Time.time;

        while (Vector2.Distance(m_Rigidbody.position, target) > 0.01f &&
               Time.time - startTime < k_DoorApproachTimeout)
        {
            Vector2 toTarget = target - m_Rigidbody.position;
            float remaining = toTarget.magnitude;

            // A whole cell per step, at the command's per-cell duration. The interaction
            // point is normally a whole number of cells away, so every step is a full one;
            // the short final step only exists for a point placed off-grid, and it takes
            // proportionally less time so even that fragment travels at the same speed.
            float stepDistance = Mathf.Min(remaining, GridWorld.CellSize);

            yield return MoveOverTime(
                m_Rigidbody.position + toTarget * (stepDistance / remaining),
                CommandStepDuration * (stepDistance / GridWorld.CellSize));
        }

        m_Rigidbody.position = target;
    }

    // A door carrying LevelExitDoor names its own interaction point. Anything else — a
    // level authored before that component existed — falls back to the middle of the
    // doorway at the height the player is already standing at, so the walk-in still
    // happens without every door needing to be re-wired.
    private Vector2 ResolveDoorInteractionPoint(Collider2D door)
    {
        if (door == null) return m_Rigidbody.position;

        LevelExitDoor exit = door.GetComponentInParent<LevelExitDoor>();
        if (exit != null) return exit.InteractionPosition;

        return new Vector2(door.bounds.center.x, m_Rigidbody.position.y);
    }

    private System.Collections.IEnumerator DeathRoutine()
    {
        // Runs before the first yield, so the hazard that triggered this death cannot
        // fire again on the following frame and start a second death routine.
        m_IsHazardable = false;

        AudioManager.Instance?.SetWalking(false);
        AudioManager.Instance?.PlayDeath();

        // Byte is blown apart rather than played through a death clip: the body vanishes
        // on the same frame the explosion lands, and the debris pieces are what the player
        // watches until the fade takes over.
        m_IsDead = true;
        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = false;

        // Unparented, so both effects keep playing where the player died while the reset
        // below teleports the body back to spawn underneath them.
        ParticleEffectSpawner.Spawn(m_DeathExplosion, transform.position);
        ParticleEffectSpawner.Spawn(m_DeathDebris, transform.position);

        CameraController.Instance?.Shake(m_DeathShakeMagnitude, m_DeathShakeDuration);

        yield return new WaitForSecondsRealtime(1f);

        // Count this death toward the combined failure tally — the doctor gloats on
        // every Nth failure. Awaited so the restart waits for the full reaction.
        if (EvilDoctorAnimationController.Instance != null)
            yield return EvilDoctorAnimationController.Instance.RegisterFailureRoutine();

        // Reset the level in place instead of reloading the scene, so the per-scene
        // failure tally survives: fade out → reset → fade back in.
        if (UIManager.Instance != null)
            yield return StartCoroutine(UIManager.Instance.FadeRoutine(0f, 1f));

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        GameManager.Instance?.SoftResetLevel();
        m_Rigidbody.position = m_StartPosition;
        m_PassiveFallSpeed = 0f;
        m_PassiveFallDistance = 0f;
        m_IsPushing = false;
        m_IsGroundPounding = false;
        transform.localScale = m_OriginalScale;   // restore spawn facing

        // Death done — put the body back on screen and hand it to the normal
        // idle/run/jump animation. Both happen behind the black fade.
        m_IsDead = false;
        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = true;
        m_Animator?.Play(PlayerAnimState.Idle);

        // A normal body again, back at spawn — hazards apply once more. (If spawn itself
        // sits in a beam this kills again immediately, which is the honest answer: the
        // level would be unplayable and should be fixed there, not masked here.)
        m_IsHazardable = true;

        if (UIManager.Instance != null)
            yield return StartCoroutine(UIManager.Instance.FadeRoutine(1f, 0f));

        DeviceInputProvider.Instance?.SetEnabled(true);
    }

    /// <summary>Called by LaserShooter when the player touches any active laser segment.</summary>
    public void OnLaserHit()
    {
        if (!m_IsHazardable) return;
        AbortExecution();
        StartCoroutine(DeathRoutine());
    }
}

