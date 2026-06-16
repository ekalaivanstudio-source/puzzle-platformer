using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("Max seconds a single movement command may run. If exceeded the turn ends (safety net for getting stuck).")]
    [SerializeField] private float m_CommandTimeout = 3f;

    [Header("Ground Check")]
    [Tooltip("All layers that count as walkable ground / solid walls (Ground, Laser, etc.).")]
    [SerializeField] private LayerMask[] m_GroundLayers;

    // Cached union of m_GroundLayers, computed once in Awake. The previous
    // property recomputed this every ground/wall check by looping the array.
    private int m_WalkableMask;
    private LayerMask WalkableMask => m_WalkableMask;
    [Tooltip("Radius of the overlap circle used to detect ground contact. Increase if the player gets stuck when half-inside a surface.")]
    [SerializeField] private float m_GroundCheckRadius = 0.15f;
    [Tooltip("How far below the collider bottom the circle centre is placed.")]
    [SerializeField] private float m_GroundCheckDistance = 0.05f;

    [Tooltip("Left foot origin. A ray is cast straight down from here to detect ground under the player's left corner.")]
    [SerializeField] private Transform m_LeftGroundCheck;
    [Tooltip("Right foot origin. A ray is cast straight down from here to detect ground under the player's right corner.")]
    [SerializeField] private Transform m_RightGroundCheck;
    [Tooltip("Length of the downward rays cast from the left/right foot origins.")]
    [SerializeField] private float m_GroundRayLength = 0.15f;

    [Header("Interaction")]
    [Tooltip("Radius of the overlap circle used to detect interactable objects.")]
    [SerializeField] private float m_InteractRadius = 0.5f;
    [SerializeField] private LayerMask m_InteractLayer;

    [Header("References")]

    [Tooltip("Particle prefab instantiated at the player's position on spike death.")]
    [SerializeField] private GameObject m_DeathParticle;
    [SerializeField] private float m_DeathShakeMagnitude = 0.2f;
    [SerializeField] private float m_DeathShakeDuration = 0.4f;

    private Rigidbody2D m_Rigidbody;
    private Collider2D m_Collider;

    private int m_MaxTimeIndex;        // snapshotted from source at turn start
    private int m_CurrentCommandIndex; // index of the command currently executing
    private bool m_IsGamePlaying;
    private Coroutine m_ExecutionCoroutine;  // stored so it can be stopped on abort
    private Coroutine m_EndTurnCoroutine;    // stored so checkpoint can cancel a pending reset

    private Vector3 m_StartPosition;
    private float m_OriginalGravityScale;
    private Vector3 m_OriginalScale;   // spawn facing — restored on respawn so a left-facing death doesn't persist

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
        m_StartPosition = transform.position;
        m_OriginalGravityScale = m_Rigidbody.gravityScale;
        m_OriginalScale = transform.localScale;

        // Cache the walkable layer union once — m_GroundLayers never changes at runtime.
        int combined = 0;
        if (m_GroundLayers != null)
            foreach (LayerMask lm in m_GroundLayers) combined |= lm.value;
        m_WalkableMask = combined;

        m_NoFilter = ContactFilter2D.noFilter;
        m_InteractFilter = new ContactFilter2D { useTriggers = true };
        m_InteractFilter.SetLayerMask(m_InteractLayer);
    }

    private void Start()
    {
        UIManager.Instance?.StartLevelFadeIn();
    }

    private void OnValidate()
    {
        if (m_MoveDistancePerCommand <= 0f) m_MoveDistancePerCommand = 2f;
        if (m_MoveDuration <= 0f) m_MoveDuration = 0.4f;
        if (m_JumpHeight <= 0f) m_JumpHeight = 3f;
        if (m_JumpForwardDistance <= 0f) m_JumpForwardDistance = 2f;
        if (m_JumpDuration <= 0f) m_JumpDuration = 0.6f;
        if (m_BeatGapTime < 0f) m_BeatGapTime = 0f;
        if (m_GroundCheckDistance < 0f) m_GroundCheckDistance = 0.05f;
        if (m_GroundCheckRadius <= 0f) m_GroundCheckRadius = 0.15f;
        if (m_InteractRadius <= 0f) m_InteractRadius = 0.5f;
    }

    // Animation-only update — movement is driven by coroutines, not Update
    private void Update()
    {
        if (!m_IsGamePlaying)
        {
            AudioManager.Instance?.SetWalking(false);
            return;
        }
        UpdateGroundedAnimation();
        UpdateFallingAnimation();
        CheckSpikeOverlap();
        UpdateWalkAudio();
    }

    // Drives the looping footstep sound: on while moving horizontally and roughly
    // level (so it doesn't trigger during the airborne portion of a jump or a fall).
    private void UpdateWalkAudio()
    {
        Vector2 v = m_Rigidbody.linearVelocity;
        bool walking = Mathf.Abs(v.x) > 0.1f && Mathf.Abs(v.y) < 0.5f;
        AudioManager.Instance?.SetWalking(walking);
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
        // Safety: ensure gravity is at its normal value before the turn starts, in case a
        // prior abort left it zeroed (gravity is temporarily disabled during edge-walks/jumps).
        m_Rigidbody.gravityScale = m_OriginalGravityScale;
        m_IsGamePlaying = true;
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
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        // Do NOT update m_StartPosition � death and turn-end still reset to the
        // original spawn. The checkpoint only repositions the player for this turn.
        m_Rigidbody.position = checkpointPosition;
        m_Rigidbody.linearVelocity = Vector2.zero;
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
        float savedGravity = m_Rigidbody.gravityScale;
        m_Rigidbody.gravityScale = 0f;
        m_Rigidbody.linearVelocity = Vector2.zero;
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

        m_Rigidbody.gravityScale = savedGravity;
        m_Rigidbody.linearVelocity = Vector2.zero;
        if (m_Collider != null) m_Collider.enabled = true;

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
        float savedGravity = m_Rigidbody.gravityScale;
        m_Rigidbody.gravityScale = 0f;
        m_Rigidbody.linearVelocity = Vector2.zero;
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

        m_Rigidbody.gravityScale = savedGravity;
        m_Rigidbody.linearVelocity = Vector2.zero;
        if (m_Collider != null) m_Collider.enabled = true;

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

            if (m_BeatGapTime > 0f && m_IsGamePlaying)
                yield return new WaitForSeconds(m_BeatGapTime);

            i++;
        }

        if (m_IsGamePlaying)
            EndTurn();
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

    // Moves the player horizontally by the given signed distance over m_MoveDuration seconds.
    // Velocity is set directly each FixedUpdate (no AddForce, no momentum).
    // Position is snapped to the exact target at completion for determinism.
    private IEnumerator MoveHorizontal(float distance)
    {
        if (!m_IsGamePlaying) yield break;

        float startX = m_Rigidbody.position.x;
        float targetX = startX + distance;
        float speed = distance / m_MoveDuration;

        transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);

        // Position-based loop: keep moving until targetX is reached.
        // If the platform ends mid-move, stop, fall, land, then resume the remaining distance.
        bool hitWall = false;
        float commandElapsed = 0f;
        while (!HasReachedTarget(m_Rigidbody.position.x, targetX, speed) && !hitWall)
        {            // Push-brick check � must come before the regular wall check so the
            // push fires instead of silently stopping the player.
            PushBrick pushBrick = CheckPushBrick(speed);
            if (pushBrick != null)
            {
                m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);
                yield return StartCoroutine(pushBrick.Push(Mathf.Sign(speed)));
                hitWall = true;
                break;
            }
            // Wall on the path — stop flush and end this command.
            if (CheckHorizontalWall(speed))
            {
                m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);
                hitWall = true;
                break;
            }

            m_Rigidbody.linearVelocity = new Vector2(speed, m_Rigidbody.linearVelocity.y);
            yield return new WaitForFixedUpdate();
            commandElapsed += Time.fixedDeltaTime;

            // Safety net: if the command has been running too long the player is stuck.
            // Abort execution and end the turn exactly like a wrong-input run.
            if (commandElapsed >= m_CommandTimeout)
            {
                m_Rigidbody.linearVelocity = Vector2.zero;
                AbortExecution();
                StartCoroutine(WaitForEndStuff());
                yield break;
            }

            // Case 1 — early detection: front foot is off the edge but centre is still
            //          over the platform. Walk the remaining unit first, then fall.
            if (CheckIsGrounded() && !CheckGroundAhead(speed))
            {
                float sign = Mathf.Sign(speed);

                // Advance to the end of the current 1-unit segment relative to startX,
                // then clamp so we never overshoot targetX.
                // Works correctly whether the total command distance is 1, 2, or 3 units.
                float fallEdgeX = sign > 0f
                    ? startX + Mathf.Floor(m_Rigidbody.position.x - startX) + 1f
                    : startX - Mathf.Floor(startX - m_Rigidbody.position.x) - 1f;
                fallEdgeX = sign > 0f
                    ? Mathf.Min(fallEdgeX, targetX)
                    : Mathf.Max(fallEdgeX, targetX);

                // Disable gravity so the player walks the unit straight (no early fall).
                float savedGravity = m_Rigidbody.gravityScale;
                m_Rigidbody.gravityScale = 0f;

                while (!HasReachedTarget(m_Rigidbody.position.x, fallEdgeX, speed))
                {
                    if (CheckHorizontalWall(speed))
                    {
                        m_Rigidbody.gravityScale = savedGravity;
                        m_Rigidbody.linearVelocity = Vector2.zero;
                        hitWall = true;
                        break;
                    }
                    m_Rigidbody.linearVelocity = new Vector2(speed, 0f);
                    yield return new WaitForFixedUpdate();
                }
                if (hitWall) break;
                m_Rigidbody.linearVelocity = Vector2.zero;
                // Direct assignment — bypasses physics integration for an exact snap.
                m_Rigidbody.position = new Vector2(fallEdgeX, m_Rigidbody.position.y);
                yield return new WaitForFixedUpdate();
                m_Rigidbody.linearVelocity = Vector2.zero;
                m_Rigidbody.gravityScale = savedGravity;

                // Fall if there is no ground at the edge — even when fallEdgeX == targetX.
                if (!CheckIsGrounded())
                {
                    float g = Mathf.Abs(Physics2D.gravity.y) * m_Rigidbody.gravityScale;
                    m_Rigidbody.linearVelocity = new Vector2(0f, -Mathf.Sqrt(2f * g * m_JumpHeight));


                    yield return WaitUntilGrounded();
                    if (!CheckIsGrounded()) yield break;

                    transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
                }
            }
            // Case 2 — late detection: the centre has already crossed the edge in a
            //          single physics step and CheckIsGrounded() is already false.
            //          Require a clearly negative y-velocity (several frames of free-fall)
            //          to avoid false-positives from single-frame ground-check flickers.
            //          Still complete 1 unit to the next grid boundary before falling.
            else if (!CheckIsGrounded() && m_Rigidbody.linearVelocity.y < -1f)
            {
                float sign = Mathf.Sign(speed);

                // Same boundary logic as Case 1.
                float fallEdgeX = sign > 0f
                    ? startX + Mathf.Floor(m_Rigidbody.position.x - startX) + 1f
                    : startX - Mathf.Floor(startX - m_Rigidbody.position.x) - 1f;
                fallEdgeX = sign > 0f
                    ? Mathf.Min(fallEdgeX, targetX)
                    : Mathf.Max(fallEdgeX, targetX);

                float savedGravity = m_Rigidbody.gravityScale;
                m_Rigidbody.gravityScale = 0f;

                while (!HasReachedTarget(m_Rigidbody.position.x, fallEdgeX, speed))
                {
                    if (CheckHorizontalWall(speed))
                    {
                        m_Rigidbody.gravityScale = savedGravity;
                        m_Rigidbody.linearVelocity = Vector2.zero;
                        hitWall = true;
                        break;
                    }
                    m_Rigidbody.linearVelocity = new Vector2(speed, 0f);
                    yield return new WaitForFixedUpdate();
                }
                if (hitWall) break;
                m_Rigidbody.linearVelocity = Vector2.zero;
                // Direct assignment — bypasses physics integration for an exact snap.
                m_Rigidbody.position = new Vector2(fallEdgeX, m_Rigidbody.position.y);
                yield return new WaitForFixedUpdate();
                m_Rigidbody.linearVelocity = Vector2.zero;
                m_Rigidbody.gravityScale = savedGravity;

                // Fall if there is no ground at the edge — even when fallEdgeX == targetX.
                if (!CheckIsGrounded())
                {
                    float g = Mathf.Abs(Physics2D.gravity.y) * m_Rigidbody.gravityScale;
                    m_Rigidbody.linearVelocity = new Vector2(0f, -Mathf.Sqrt(2f * g * m_JumpHeight));


                    yield return WaitUntilGrounded();
                    if (!CheckIsGrounded()) yield break;

                    transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
                }
            }
        }

        // Snap to exact target X — only when not stopped by a wall.
        m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);
        if (!hitWall)
            m_Rigidbody.position = new Vector2(targetX, m_Rigidbody.position.y);
        yield return new WaitForFixedUpdate();
        m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);

        // Safety: if the snap placed the player past a platform edge (center raycast
        // returned true at fallEdgeX but the player is actually beyond the tile),
        // wait for them to fall and land before the next command begins.
        if (!hitWall && !CheckIsGrounded())
        {
            yield return WaitUntilGrounded();
            m_Rigidbody.linearVelocity = Vector2.zero;
        }

        SnapToGrid();
    }

    // Returns true once the player has reached or passed targetX in the direction of travel.
    private bool HasReachedTarget(float currentX, float targetX, float speed)
    {
        return speed > 0f ? currentX >= targetX - 0.01f : currentX <= targetX + 0.01f;
    }

    // ─── Jump Logic ──────────────────────────────────────────────────────────────

    // Performs a jump with deterministic height and optional horizontal distance.
    // Initial vertical velocity is derived from m_JumpHeight using kinematics: v = sqrt(2gh).
    // Horizontal velocity is derived so the player reaches targetX exactly when they land —
    // including elevated or lowered platforms. A downward raycast at targetX finds the landing
    // surface height; kinematics then gives the exact air time to that height.
    private IEnumerator PerformJump(float horizontalDistance)
    {
        if (!m_IsGamePlaying) yield break;

        AudioManager.Instance?.PlayJump();

        // Derive effective gravity so the arc peaks at m_JumpHeight in exactly half of m_JumpDuration.
        // g_eff = 2h / t_half^2   →   v0y = g_eff * t_half = 2h / t_half
        float tHalf = m_JumpDuration * 0.5f;
        float gEff = 2f * m_JumpHeight / (tHalf * tHalf);
        float v0y = gEff * tHalf;

        float savedGravityScale = m_Rigidbody.gravityScale;
        m_Rigidbody.gravityScale = gEff / Mathf.Abs(Physics2D.gravity.y);

        float startX = m_Rigidbody.position.x;
        float startY = m_Rigidbody.position.y;
        float targetX = startX + horizontalDistance;

        // --- Compute vx accounting for elevated/lowered landing surface ---
        float vx = 0f;
        if (!Mathf.Approximately(horizontalDistance, 0f))
        {
            // footOffset = distance from rigidbody centre to bottom of collider.
            // The player centre when standing on any surface = surfaceY + footOffset.
            float footOffset = m_Collider != null
                ? startY - m_Collider.bounds.min.y
                : 0f;

            // Raycast straight down at targetX from just above the jump peak.
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(targetX, startY + m_JumpHeight + 1f),
                Vector2.down,
                m_JumpHeight + 20f,
                WalkableMask);

            // dY = height difference between landing surface and current surface.
            float dY = hit.collider != null
                ? (hit.point.y + footOffset) - startY   // elevated (+) or lowered (-)
                : 0f;                                    // no hit → assume flat ground

            // Solve for descending-arc landing time using effective gravity.
            // dY = v0y*t - 0.5*gEff*t^2  →  t = (v0y + sqrt(v0y^2 - 2*gEff*dY)) / gEff
            float disc = v0y * v0y - 2f * gEff * dY;
            if (disc >= 0f)
            {
                float tLand = (v0y + Mathf.Sqrt(disc)) / gEff;
                vx = tLand > 0f ? horizontalDistance / tLand : horizontalDistance / m_JumpDuration;
            }
            else
            {
                // Jump height can't reach dY — fallback to flat-ground vx
                vx = horizontalDistance / m_JumpDuration;
            }
        }

        // Face direction for lateral jumps
        if (!Mathf.Approximately(horizontalDistance, 0f))
            transform.localScale = new Vector3(horizontalDistance > 0f ? 1f : -1f, 1f, 1f);

        // Apply initial velocity — set once, physics handles the arc naturally
        m_Rigidbody.linearVelocity = new Vector2(vx, v0y);

        // Allow two physics steps before polling for landing
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        yield return WaitUntilGrounded();

        m_Rigidbody.gravityScale = savedGravityScale;
        m_Rigidbody.linearVelocity = Vector2.zero;

        // Only snap to targetX if the player landed close to it (normal arc).
        // If the arc was disrupted by a ceiling collision the player falls back to
        // a completely different X; snapping there would look like a teleport.
        // With the corrected vx a normal arc lands within ~0.1 units of targetX,
        // so a 0.5-unit tolerance cleanly distinguishes the two cases.
        if (Mathf.Abs(m_Rigidbody.position.x - targetX) < 0.5f)
        {
            m_Rigidbody.MovePosition(new Vector2(targetX, m_Rigidbody.position.y));
            yield return new WaitForFixedUpdate();
        }
        m_Rigidbody.linearVelocity = Vector2.zero;
        SnapToGrid();
    }

    // Waits until the player is grounded on the DESCENDING side of the arc.
    // Checking linearVelocity.y <= 0 prevents false triggers when the raycast
    // detects a platform below while the player is still ascending through it
    // (Physics2D raycasts ignore one-way platform direction).
    private IEnumerator WaitUntilGrounded(float timeout = 6f)
    {
        yield return new WaitForSeconds(0.15f);

        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (m_Rigidbody.linearVelocity.y <= 0f && CheckIsGrounded())
                yield break;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
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

    private void UpdateGroundedAnimation()
    {
        //        m_Animator.SetBool("IsGrounded", CheckIsGrounded());
    }

    // Continuous spike check — covers moving spikes that slide into the player via
    // transform.position, which don’t reliably fire OnTriggerEnter2D without a Rigidbody2D.

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!m_IsGamePlaying) return;

        if (other.CompareTag("Spike") && !other.TryGetComponent(out EnemyMovement _))
        {
            AbortExecution();
            StartCoroutine(DeathRoutine());
        }
    }
    private void CheckSpikeOverlap()
    {
        if (m_Collider == null) return;

        int count = Physics2D.OverlapBox(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f, m_NoFilter, m_OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit.gameObject == gameObject) continue;
            if (!hit.CompareTag("Spike")) continue;
            if (hit.TryGetComponent(out EnemyMovement _)) continue; // enemy, not a spike

            AbortExecution();
            StartCoroutine(DeathRoutine());
            return;
        }
    }

    private bool CheckIsGrounded()
    {
        // Two downward rays — one from the left foot, one from the right foot.
        // Grounded if EITHER ray hits, so the player stays grounded when only one
        // corner rests on a platform (the center-based check used to read empty
        // space in that case, since Z-rotation is frozen and the body stays level).
        if (m_LeftGroundCheck != null || m_RightGroundCheck != null)
        {
            bool leftHit = m_LeftGroundCheck != null &&
                Physics2D.Raycast(m_LeftGroundCheck.position, Vector2.down, m_GroundRayLength, WalkableMask);
            if (leftHit) return true;

            bool rightHit = m_RightGroundCheck != null &&
                Physics2D.Raycast(m_RightGroundCheck.position, Vector2.down, m_GroundRayLength, WalkableMask);
            return rightHit;
        }

        // Fallback (foot transforms not assigned): OverlapCircle centred just
        // below the collider bottom — wider than a single ray, still detects
        // ground when the collider is partially embedded after snapping.
        float bottom = m_Collider != null ? m_Collider.bounds.min.y : transform.position.y;
        Vector2 centre = new Vector2(transform.position.x, bottom - m_GroundCheckDistance);
        return Physics2D.OverlapCircle(centre, m_GroundCheckRadius, WalkableMask);
    }

#if UNITY_EDITOR
    // Visualises the two ground-check rays in the Scene view so the foot
    // transforms can be positioned at the player's left/right corners.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (m_LeftGroundCheck != null)
            Gizmos.DrawLine(m_LeftGroundCheck.position,
                m_LeftGroundCheck.position + Vector3.down * m_GroundRayLength);
        if (m_RightGroundCheck != null)
            Gizmos.DrawLine(m_RightGroundCheck.position,
                m_RightGroundCheck.position + Vector3.down * m_GroundRayLength);
    }
#endif

    // Rounds the rigidbody position to the nearest 0.5-unit grid, eliminating the
    // floating-point drift that causes colliders to slightly intersect surfaces when
    // Z-rotation is frozen (preventing the physics solver from self-correcting).
    private void SnapToGrid()
    {
        Vector2 pos = m_Rigidbody.position;
        pos.x = Mathf.Round(pos.x * 2f) / 2f;
        pos.y = Mathf.Round(pos.y * 2f) / 2f;
        m_Rigidbody.position = pos;
    }

    // Casts downward from the leading edge of the collider (front foot) in the
    // direction of travel. Returns false when that foot steps off a platform.
    private bool CheckGroundAhead(float moveDirection)
    {
        float sign = Mathf.Sign(moveDirection);
        float bottom = m_Collider != null ? m_Collider.bounds.min.y : transform.position.y;
        float frontX = transform.position.x + sign * (m_Collider != null ? m_Collider.bounds.extents.x : 0.5f);
        return Physics2D.Raycast(new Vector2(frontX, bottom), Vector2.down, m_GroundCheckDistance, WalkableMask);
    }

    // BoxCasts horizontally to detect a wall in the direction of travel.
    // Returns true if something in m_GroundLayer blocks the next step.
    private bool CheckHorizontalWall(float signedSpeed)
    {
        if (m_Collider == null || Mathf.Approximately(signedSpeed, 0f)) return false;

        float sign = Mathf.Sign(signedSpeed);
        // OverlapBox on a thin slice just outside the player's side face.
        // Unlike BoxCast, OverlapBox detects walls the player is already touching.
        Vector2 sideCenter = new Vector2(
            m_Collider.bounds.center.x + sign * (m_Collider.bounds.extents.x + 0.04f),
            m_Collider.bounds.center.y);
        Vector2 sideSize = new Vector2(0.08f, m_Collider.bounds.size.y * 0.8f);

        return Physics2D.OverlapBox(sideCenter, sideSize, 0f, WalkableMask) != null;
    }

    // Returns the PushBrick at the player's side face, or null if none is present.
    // Uses all layers so the brick does not need to be on the Ground layer.
    private PushBrick CheckPushBrick(float signedSpeed)
    {
        if (m_Collider == null || Mathf.Approximately(signedSpeed, 0f)) return null;

        float sign = Mathf.Sign(signedSpeed);
        Vector2 sideCenter = new Vector2(
            m_Collider.bounds.center.x + sign * (m_Collider.bounds.extents.x + 0.04f),
            m_Collider.bounds.center.y);
        Vector2 sideSize = new Vector2(0.08f, m_Collider.bounds.size.y * 0.8f);

        int count = Physics2D.OverlapBox(sideCenter, sideSize, 0f, m_NoFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit == m_Collider || hit.isTrigger) continue;
            PushBrick brick = hit.GetComponentInParent<PushBrick>();
            if (brick == null) continue;

            // Ignore a brick the player is standing ON: its top sits at (or below)
            // the player's feet, so it's a floor — not a wall to push. This stops
            // the brick being shoved sideways out from under the player when they
            // land on top of it mid-move (walk/fall off an edge onto the brick).
            // A genuinely pushable brick rises beside the body, presenting a side face.
            if (hit.bounds.max.y <= m_Collider.bounds.min.y + 0.1f) continue;

            return brick;
        }
        return null;
    }

    private void UpdateFallingAnimation()
    {
        float falling = m_Rigidbody.linearVelocity.y < 0f ? 1f : 0f;
    }
    // ─── Turn End / Abort ────────────────────────────────────────────────────────

    private void EndTurn()
    {
        m_IsGamePlaying = false;
        m_Rigidbody.linearVelocity = Vector2.zero;
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

        m_Rigidbody.linearVelocity = Vector2.zero;

        // StopCoroutine above can kill MoveHorizontal/PerformJump/waypoint transport while
        // they have gravity temporarily zeroed (edge-walk, jump arc), so their restore line
        // never runs. Reset to the original here, otherwise the player keeps drifting
        // horizontally through the air on the next turn (gravityScale stuck at 0).
        m_Rigidbody.gravityScale = m_OriginalGravityScale;
    }

    // Short delay before resetting position and unlocking UI
    private IEnumerator WaitForEndStuff()
    {
        // Restore time scale in case any slow-motion was still active.
        // WaitForSecondsRealtime is unaffected by Time.timeScale so the reset always fires on time.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        yield return new WaitForSecondsRealtime(0.5f);

        // Use rigidbody position reset (not transform) to keep physics state consistent
        m_Rigidbody.position = m_StartPosition;
        m_Rigidbody.linearVelocity = Vector2.zero;
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
        if (!m_IsGamePlaying || GameManager.Instance == null) return;

        if (other.CompareTag("Spike") && !other.TryGetComponent(out EnemyMovement _))
        {
            AbortExecution();
            StartCoroutine(DeathRoutine());
        }
        else if (other.CompareTag("Door"))
        {
            if (GameManager.Instance.IsKeyCollected)
            {
                AbortExecution();
                StartCoroutine(WinRoutine());
            }
        }
    }

    private System.Collections.IEnumerator WinRoutine()
    {
        AudioManager.Instance?.SetWalking(false);
        AudioManager.Instance?.PlayWin();

        // Doctor reacts (sad) — wait for the full reaction before leaving the level.
        if (EvilDoctorAnimationController.Instance != null)
            yield return EvilDoctorAnimationController.Instance.PlayLevelCompletedRoutine();

        yield return new WaitForSecondsRealtime(0.2f);
        if (UIManager.Instance != null)
            yield return StartCoroutine(UIManager.Instance.FadeRoutine(0f, 1f));
        GameManager.Instance.LoadNextLevel();
    }

    private System.Collections.IEnumerator DeathRoutine()
    {
        AudioManager.Instance?.SetWalking(false);
        AudioManager.Instance?.PlayDeath();

        if (m_DeathParticle != null)
            Instantiate(m_DeathParticle, transform.position, Quaternion.identity);


        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

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
        m_Rigidbody.linearVelocity = Vector2.zero;
        transform.localScale = m_OriginalScale;   // restore spawn facing
        if (sr != null) sr.enabled = true;

        if (UIManager.Instance != null)
            yield return StartCoroutine(UIManager.Instance.FadeRoutine(1f, 0f));

        DeviceInputProvider.Instance?.SetEnabled(true);
    }

    /// <summary>Called by LaserShooter when the player touches any active laser segment.</summary>
    public void OnLaserHit()
    {
        if (!m_IsGamePlaying) return;
        AbortExecution();
        StartCoroutine(DeathRoutine());
    }
}

