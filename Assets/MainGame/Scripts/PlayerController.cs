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
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Command Settings")]
    [Tooltip("Horizontal distance (units) the player travels per Left or Right command.")]
    [SerializeField] private float m_MoveDistancePerCommand = 2f;

    [Tooltip("Duration (seconds) to complete one Left or Right command.")]
    [SerializeField] private float m_MoveDuration = 0.4f;

    [Tooltip("Peak height (units) of a Jump command. Vertical velocity is derived from this and gravity.")]
    [SerializeField] private float m_JumpHeight = 2.5f;

    [Tooltip("Horizontal distance (units) traveled during a JumpRight or JumpLeft command.")]
    [SerializeField] private float m_JumpForwardDistance = 2f;

    [Tooltip("Pause (seconds) between commands so the player can see each action clearly.")]
    [SerializeField] private float m_BeatGapTime = 0.05f;

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

    private ISequenceSource m_SequenceSource;
    private Rigidbody2D m_Rigidbody;
    private Animator m_Animator;
    private Collider2D m_Collider;

    private int m_MaxTimeIndex;        // snapshotted from source at turn start
    private bool m_IsGamePlaying;
    private Coroutine m_ExecutionCoroutine;  // stored so it can be stopped on abort

    private Vector3 m_StartPosition;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_Animator = GetComponent<Animator>();
        m_Collider = GetComponent<Collider2D>();
        m_StartPosition = transform.position;

        m_SequenceSource = m_SequenceSourceObject as ISequenceSource;

        if (m_SequenceSource == null)
            Debug.LogError("[PlayerController] SequenceSourceObject must implement ISequenceSource — assign a SequenceSourceRouter.", this);
    }

    private void OnValidate()
    {
        if (m_MoveDistancePerCommand <= 0f) m_MoveDistancePerCommand = 2f;
        if (m_MoveDuration <= 0f) m_MoveDuration = 0.4f;
        if (m_JumpHeight <= 0f) m_JumpHeight = 2.5f;
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

    // ─── Execution Loop ─────────────────────────────────────────────────────────

    // Iterates through each beat slot, executing one command per beat.
    // A Jump immediately followed by Right or Left is treated as a combined directional jump,
    // consuming both slots as a single command.
    private IEnumerator ExecutionLoop()
    {
        int i = 0;
        while (i < m_MaxTimeIndex && m_IsGamePlaying)
        {
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
                        yield return JumpRightCommand();
                    }
                    else if (next == ActionTypeEnum.Left)
                    {
                        i++; // consume the Left slot
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
        float elapsed = 0f;

        // Face movement direction
        transform.localScale = new Vector3(distance > 0f ? 1f : -1f, 1f, 1f);
        m_Animator.SetBool("IsRunning", true);
        AudioManager.Instance?.PlayPlayerWalk(true);

        while (elapsed < m_MoveDuration)
        {
            // Override horizontal velocity each physics step; preserve vertical (gravity/jump arc)
            m_Rigidbody.linearVelocity = new Vector2(speed, m_Rigidbody.linearVelocity.y);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Zero horizontal velocity, then snap to exact X via MovePosition (respects colliders).
        // Yield one physics step so the snap is committed before the next command reads position.
        m_Rigidbody.linearVelocity = new Vector2(0f, m_Rigidbody.linearVelocity.y);
        m_Rigidbody.MovePosition(new Vector2(targetX, m_Rigidbody.position.y));
        yield return new WaitForFixedUpdate();

        m_Animator.SetBool("IsRunning", false);
        AudioManager.Instance?.PlayPlayerWalk(false);
    }

    // ─── Jump Logic ──────────────────────────────────────────────────────────────

    // Performs a jump with deterministic height and optional horizontal distance.
    // Initial vertical velocity is derived from m_JumpHeight using kinematics: v = sqrt(2gh).
    // Horizontal velocity is derived from jump forward distance / expected air time.
    // The player is snapped to the exact target X on landing to guarantee determinism.
    private IEnumerator PerformJump(float horizontalDistance)
    {
        float g = Mathf.Abs(Physics2D.gravity.y) * m_Rigidbody.gravityScale;
        float v0y = Mathf.Sqrt(2f * g * m_JumpHeight);          // v = sqrt(2gh)
        float airTime = 2f * v0y / g;                               // total time in air
        float vx = Mathf.Approximately(horizontalDistance, 0f)
                             ? 0f
                             : horizontalDistance / airTime;

        float startX = m_Rigidbody.position.x;
        float targetX = startX + horizontalDistance;

        // Face direction for lateral jumps
        if (!Mathf.Approximately(horizontalDistance, 0f))
            transform.localScale = new Vector3(horizontalDistance > 0f ? 1f : -1f, 1f, 1f);

        // Apply initial velocity — set once, physics handles the arc naturally
        m_Rigidbody.linearVelocity = new Vector2(vx, v0y);
        m_Animator.SetTrigger("Jump");
        m_Animator.SetBool("IsGrounded", false);

        // Allow two physics steps before polling for landing
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        yield return WaitUntilGrounded();

        // Zero velocity first so no residual vx carries into the snap, then snap to exact target X.
        // Yield one physics step so the snap is committed before the coroutine returns.
        m_Rigidbody.linearVelocity = Vector2.zero;
        m_Rigidbody.MovePosition(new Vector2(targetX, m_Rigidbody.position.y));
        yield return new WaitForFixedUpdate();
    }

    // Waits until the player is grounded. Brief initial delay ensures the
    // player has fully left the ground before polling begins.
    private IEnumerator WaitUntilGrounded(float timeout = 6f)
    {
        yield return new WaitForSeconds(0.15f);

        float elapsed = 0f;
        while (!CheckIsGrounded() && elapsed < timeout)
        {
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
        m_Animator.SetBool("IsGrounded", CheckIsGrounded());
    }

    private bool CheckIsGrounded()
    {
        // Cast from the bottom of the collider bounds so the ray reaches the ground
        // regardless of where the character pivot is placed.
        float bottom = m_Collider != null ? m_Collider.bounds.min.y : transform.position.y;
        return Physics2D.Raycast(new Vector2(transform.position.x, bottom), Vector2.down, m_GroundCheckDistance, m_GroundLayer);
    }

    private void UpdateFallingAnimation()
    {
        float falling = m_Rigidbody.linearVelocity.y < 0f ? 1f : 0f;
        m_Animator.SetFloat("Falling", falling);
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
        m_Animator.SetBool("IsRunning", false);
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
            GameManager.Instance.ReloadLevel();
        }
        else if (other.CompareTag("Door") && GameManager.Instance.IsKeyCollected)
        {
            AbortExecution();
            GameManager.Instance.GameWin();
        }
    }
}

