using System.Collections;
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
    [Header("Command Settings")]
    [Tooltip("Horizontal distance (units) the player travels per Left or Right command.")]
    [SerializeField] private float m_MoveDistancePerCommand = 2f;

    [Tooltip("Duration (seconds) to complete one Left or Right command.")]
    [SerializeField] private float m_MoveDuration = 0.4f;

    [Tooltip("Peak height (units) of a Jump command. Vertical velocity is derived from this and gravity.")]
    [SerializeField] private float m_JumpHeight = 3f;

    [Tooltip("Horizontal distance (units) traveled during a JumpRight or JumpLeft command.")]
    [SerializeField] private float m_JumpForwardDistance = 2f;

    [Tooltip("Pause (seconds) between commands so the player can see each action clearly.")]
    [SerializeField] private float m_BeatGapTime = 0.05f;
    [Tooltip("Max seconds a single movement command may run. If exceeded the turn ends (safety net for getting stuck).")]
    [SerializeField] private float m_CommandTimeout = 3f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask m_GroundLayer;
    [Tooltip("Raycast distance below the player used to detect ground contact.")]
    [SerializeField] private float m_GroundCheckDistance = 0.1f;

    [Header("Interaction")]
    [Tooltip("Radius of the overlap circle used to detect interactable objects.")]
    [SerializeField] private float m_InteractRadius = 0.5f;
    [SerializeField] private LayerMask m_InteractLayer;

    [Header("References")]
    [Tooltip("Assign the SequenceSourceRouter — routes reads to the active input mode automatically.")]
    [SerializeField] private MonoBehaviour m_SequenceSourceObject;
    [Tooltip("UI object shown for 2 seconds when the player reaches the door without the key.")]
    [SerializeField] private GameObject m_NoKeyPopup;
    [Tooltip("UI object shown for 2 seconds on win before restarting.")]
    [SerializeField] private GameObject m_WowObject;
    [Tooltip("Particle prefab instantiated at the player's position on spike death.")]
    [SerializeField] private GameObject m_DeathParticle;
    [SerializeField] private AudioClip m_DeathClip;
    [SerializeField] private float m_DeathShakeMagnitude = 0.2f;
    [SerializeField] private float m_DeathShakeDuration = 0.4f;
    [Tooltip("Full-screen dark overlay CanvasGroup. Starts at alpha 1, fades to 0 on load; fades back to 1 before any reload.")]
    [SerializeField] private CanvasGroup m_FadeOverlay;
    [SerializeField] private float m_FadeDuration = 1f;

    private ISequenceSource m_SequenceSource;
    private Rigidbody2D m_Rigidbody;
    private Collider2D m_Collider;

    private int m_MaxTimeIndex;        // snapshotted from source at turn start
    private int m_CurrentCommandIndex; // index of the command currently executing
    private bool m_IsGamePlaying;
    private Coroutine m_ExecutionCoroutine;  // stored so it can be stopped on abort

    private Vector3 m_StartPosition;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_Collider = GetComponent<Collider2D>();
        m_StartPosition = transform.position;

        m_SequenceSource = m_SequenceSourceObject as ISequenceSource;

        if (m_SequenceSource == null)
            Debug.LogError("[PlayerController] SequenceSourceObject must implement ISequenceSource — assign a SequenceSourceRouter.", this);
    }

    private void Start()
    {
        // Fade from black to clear when the level loads
        if (m_FadeOverlay != null)
        {
            m_FadeOverlay.alpha = 1f;
            StartCoroutine(FadeRoutine(1f, 0f));
        }
    }

    private void OnValidate()
    {
        if (m_MoveDistancePerCommand <= 0f) m_MoveDistancePerCommand = 2f;
        if (m_MoveDuration <= 0f) m_MoveDuration = 0.4f;
        if (m_JumpHeight <= 0f) m_JumpHeight = 3f;
        if (m_JumpForwardDistance <= 0f) m_JumpForwardDistance = 2f;
        if (m_BeatGapTime < 0f) m_BeatGapTime = 0f;
        if (m_GroundCheckDistance <= 0f) m_GroundCheckDistance = 0.1f;
        if (m_InteractRadius <= 0f) m_InteractRadius = 0.5f;
    }

    // Animation-only update — movement is driven by coroutines, not Update
    private void Update()
    {
        if (!m_IsGamePlaying) return;
        UpdateGroundedAnimation();
        UpdateFallingAnimation();
        CheckSpikeOverlap();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the execution turn. Called by <see cref="GameManager.OnPlayClicked"/>.
    /// Launches the coroutine-based command loop.
    /// </summary>
    public void OnGamePlayStart()
    {
        if (m_SequenceSource == null || !m_SequenceSource.CanExecute)
        {
            Debug.LogError("[PlayerController] Cannot start — no sequence source or sequence is empty.", this);
            return;
        }

        m_MaxTimeIndex = m_SequenceSource.SequenceLength;
        m_IsGamePlaying = true;
        m_ExecutionCoroutine = StartCoroutine(ExecutionLoop());
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
    // A Jump immediately followed by Right or Left is treated as a combined directional jump,
    // consuming both slots as a single command.
    private IEnumerator ExecutionLoop(int startIndex = 0)
    {
        int i = startIndex;
        while (i < m_MaxTimeIndex && m_IsGamePlaying)
        {
            m_CurrentCommandIndex = i;
            ActionTypeEnum? action = m_SequenceSource.GetActionAt(i);

            if (action != null)
            {
                PlayBeatAudio(action.Value);

                if (action.Value == ActionTypeEnum.Jump)
                {
                    // Peek at the next slot — Jump+Right/Left = directional jump (consumes 2 slots)
                    ActionTypeEnum? next = m_SequenceSource.GetActionAt(i + 1);

                    if (next == ActionTypeEnum.Right)
                    {
                        i++; // consume the Right slot
                        m_CurrentCommandIndex = i;
                        yield return JumpRightCommand();
                    }
                    else if (next == ActionTypeEnum.Left)
                    {
                        i++; // consume the Left slot
                        m_CurrentCommandIndex = i;
                        yield return JumpLeftCommand();
                    }
                    else
                    {
                        yield return JumpCommand();
                    }
                }
                else
                {
                    switch (action.Value)
                    {
                        case ActionTypeEnum.Left: yield return MoveLeftCommand(); break;
                        case ActionTypeEnum.Right: yield return MoveRightCommand(); break;
                        case ActionTypeEnum.Interact: yield return InteractCommand(); break;
                    }
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
        float startX = m_Rigidbody.position.x;
        float targetX = startX + distance;
        float speed = distance / m_MoveDuration;

        transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
        AudioManager.Instance?.PlayPlayerWalk(true);

        // Position-based loop: keep moving until targetX is reached.
        // If the platform ends mid-move, stop, fall, land, then resume the remaining distance.
        bool hitWall = false;
        float commandElapsed = 0f;
        while (!HasReachedTarget(m_Rigidbody.position.x, targetX, speed) && !hitWall)
        {
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
                AudioManager.Instance?.PlayPlayerWalk(false);
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

                    AudioManager.Instance?.PlayPlayerWalk(false);

                    yield return WaitUntilGrounded();
                    if (!CheckIsGrounded()) yield break;

                    transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
                    AudioManager.Instance?.PlayPlayerWalk(true);
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

                    AudioManager.Instance?.PlayPlayerWalk(false);

                    yield return WaitUntilGrounded();
                    if (!CheckIsGrounded()) yield break;

                    transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
                    AudioManager.Instance?.PlayPlayerWalk(true);
                }
            }
        }

        // Snap to exact target X — only when not stopped by a wall.
        m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);
        if (!hitWall)
            m_Rigidbody.position = new Vector2(targetX, m_Rigidbody.position.y);
        yield return new WaitForFixedUpdate();
        m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);

        AudioManager.Instance?.PlayPlayerWalk(false);
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
        float g = Mathf.Abs(Physics2D.gravity.y) * m_Rigidbody.gravityScale;
        float v0y = Mathf.Sqrt(2f * g * m_JumpHeight);          // v = sqrt(2gh)

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
                m_GroundLayer);

            // dY = height difference between landing surface and current surface.
            float dY = hit.collider != null
                ? (hit.point.y + footOffset) - startY   // elevated (+) or lowered (-)
                : 0f;                                    // no hit → assume flat ground

            // Solve for descending-arc landing time: dY = v0y*t - 0.5*g*t^2
            //   → t = (v0y + sqrt(v0y^2 - 2*g*dY)) / g
            float disc = v0y * v0y - 2f * g * dY;
            if (disc >= 0f)
            {
                float tLand = (v0y + Mathf.Sqrt(disc)) / g;
                vx = tLand > 0f ? horizontalDistance / tLand : horizontalDistance / (2f * v0y / g);
            }
            else
            {
                // Jump height can't reach dY — fallback to flat-ground vx
                vx = horizontalDistance / (2f * v0y / g);
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
        TryInteract();
        yield return new WaitForSeconds(m_MoveDuration);
    }

    // ─── Interaction ─────────────────────────────────────────────────────────────

    private void TryInteract()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, m_InteractRadius, m_InteractLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact();
                break; // interact with first valid target only
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

        if (other.CompareTag("Spike"))
        {
            AbortExecution();
            StartCoroutine(DeathRoutine());
        }
    }
    private void CheckSpikeOverlap()
    {
        if (m_Collider == null) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f);

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (!hit.CompareTag("Spike")) continue;

            AbortExecution();
            StartCoroutine(DeathRoutine());
            return;
        }
    }

    private bool CheckIsGrounded()
    {
        // Cast from the bottom of the collider bounds so the ray reaches the ground
        // regardless of where the character pivot is placed.
        float bottom = m_Collider != null ? m_Collider.bounds.min.y : transform.position.y;
        return Physics2D.Raycast(new Vector2(transform.position.x, bottom), Vector2.down, m_GroundCheckDistance, m_GroundLayer);
    }

    // Casts downward from the leading edge of the collider (front foot) in the
    // direction of travel. Returns false when that foot steps off a platform.
    private bool CheckGroundAhead(float moveDirection)
    {
        float sign = Mathf.Sign(moveDirection);
        float bottom = m_Collider != null ? m_Collider.bounds.min.y : transform.position.y;
        float frontX = transform.position.x + sign * (m_Collider != null ? m_Collider.bounds.extents.x : 0.5f);
        return Physics2D.Raycast(new Vector2(frontX, bottom), Vector2.down, m_GroundCheckDistance, m_GroundLayer);
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

        return Physics2D.OverlapBox(sideCenter, sideSize, 0f, m_GroundLayer) != null;
    }

    private void UpdateFallingAnimation()
    {
        float falling = m_Rigidbody.linearVelocity.y < 0f ? 1f : 0f;
    }

    // ─── Audio ───────────────────────────────────────────────────────────────────

    private void PlayBeatAudio(ActionTypeEnum action)
    {
        AudioClip clip = m_SequenceSource.GetClipForAction(action);
        AudioManager.Instance?.PlayBeatTune(clip, Random.Range(0.8f, 1.2f));
    }

    // ─── Turn End / Abort ────────────────────────────────────────────────────────

    private void EndTurn()
    {
        m_IsGamePlaying = false;
        m_Rigidbody.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayPlayerWalk(false);
        StartCoroutine(WaitForEndStuff());
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

        m_Rigidbody.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayPlayerWalk(false);
    }

    // Short delay before resetting position and unlocking UI
    private IEnumerator WaitForEndStuff()
    {
        // Restore time scale immediately — slow-motion from KeyPickupZone may still be active
        // if the proximity zone triggered right as the last command finished or was never exited.
        // WaitForSecondsRealtime is unaffected by Time.timeScale so the reset always fires on time.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        yield return new WaitForSecondsRealtime(0.5f);

        // Use rigidbody position reset (not transform) to keep physics state consistent
        m_Rigidbody.position = m_StartPosition;
        m_Rigidbody.linearVelocity = Vector2.zero;
        GameManager.Instance?.PlayEnded();
    }

    // ─── Collision ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!m_IsGamePlaying || GameManager.Instance == null) return;

        if (other.CompareTag("Spike"))
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
            else
            {
                Debug.Log("[PlayerController] Key not found.");
                StartCoroutine(NoKeyRoutine());
            }
        }
    }

    private System.Collections.IEnumerator WinRoutine()
    {
        if (m_WowObject != null) m_WowObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2f);
        if (m_WowObject != null) m_WowObject.SetActive(false);
        yield return StartCoroutine(FadeRoutine(0f, 1f));
        GameManager.Instance.LoadNextLevel();
    }

    private System.Collections.IEnumerator DeathRoutine()
    {
        if (m_DeathParticle != null)
            Instantiate(m_DeathParticle, transform.position, Quaternion.identity);

        AudioManager.Instance?.PlayPlayerDeath(m_DeathClip);

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        StartCoroutine(ShakeCamera(m_DeathShakeMagnitude, m_DeathShakeDuration));

        yield return new WaitForSecondsRealtime(1f);
        yield return StartCoroutine(FadeRoutine(0f, 1f));
        GameManager.Instance.ReloadLevel();
    }

    private System.Collections.IEnumerator FadeRoutine(float from, float to)
    {
        if (m_FadeOverlay == null) yield break;
        m_FadeOverlay.gameObject.SetActive(true);
        float elapsed = 0f;
        m_FadeOverlay.alpha = from;
        while (elapsed < m_FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_FadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / m_FadeDuration);
            yield return null;
        }
        m_FadeOverlay.alpha = to;
        if (to == 0f) m_FadeOverlay.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator ShakeCamera(float magnitude, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 origin = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - (elapsed / duration);
            cam.transform.localPosition = origin + (Vector3)UnityEngine.Random.insideUnitCircle * magnitude * t;
            yield return null;
        }
        cam.transform.localPosition = origin;
    }

    private System.Collections.IEnumerator NoKeyRoutine()
    {
        AbortExecution();
        if (m_NoKeyPopup != null) m_NoKeyPopup.SetActive(true);
        yield return new WaitForSecondsRealtime(2f);
        if (m_NoKeyPopup != null) m_NoKeyPopup.SetActive(false);
        GameManager.Instance.ReloadLevel();
    }
}

