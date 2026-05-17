using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the player character during a timeline execution turn.
/// Each beat tick reads one action from the active <see cref="ISequenceSource"/>
/// (mouse toggle grid or keyboard/gamepad sequence) and executes movement,
/// jump, interaction, animations, and audio accordingly.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Timeline Settings")]
    [Tooltip("Duration in seconds between each beat execution.")]
    [SerializeField] private float m_TimeInterval = 1f;

    [Header("Movement Settings")]
    [SerializeField] private float m_MoveSpeed = 5f;
    [SerializeField] private float m_JumpForce = 10f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask m_GroundLayer;
    [Tooltip("Raycast distance below the player used to detect ground.")]
    [SerializeField] private float m_GroundCheckDistance = 0.1f;

    [Header("Interaction")]
    [Tooltip("Radius of the circle overlap used to detect interactable objects.")]
    [SerializeField] private float m_InteractRadius = 0.5f;
    [SerializeField] private LayerMask m_InteractLayer;

    [Header("References")]
    [Tooltip("Assign the SequenceSourceRouter — routes reads to the active input mode automatically.")]
    [SerializeField] private MonoBehaviour m_SequenceSourceObject;

    private ISequenceSource m_SequenceSource;
    private Rigidbody2D m_Rigidbody;
    private Animator m_Animator;

    private int m_MaxTimeIndex;      // snapshotted from source at turn start
    private int m_CurrentTimeIndex;
    private float m_Timer;
    private bool m_IsGamePlaying;
    private bool m_IsMovingLeft;
    private bool m_IsMovingRight;
    private bool m_IsGrounded;
    private bool m_IsGroundCheckDelayed;

    private Vector3 m_StartPosition;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody2D>();
        m_Animator = GetComponent<Animator>();
        m_StartPosition = transform.position;

        m_SequenceSource = m_SequenceSourceObject as ISequenceSource;

        if (m_SequenceSource == null)
            Debug.LogError("[PlayerController] SequenceSourceObject must implement ISequenceSource — assign a SequenceSourceRouter.", this);
    }

    private void OnValidate()
    {
        // Clamp inspector values to safe minimums
        if (m_TimeInterval <= 0f) m_TimeInterval = 1f;
        if (m_MoveSpeed <= 0f) m_MoveSpeed = 5f;
        if (m_JumpForce <= 0f) m_JumpForce = 10f;
        if (m_InteractRadius <= 0f) m_InteractRadius = 0.5f;
        if (m_GroundCheckDistance <= 0f) m_GroundCheckDistance = 0.1f;
    }

    private void Update()
    {
        if (!m_IsGamePlaying) return;

        CheckGrounded();
        HandleMovement();
        HandleFallingAnimation();

        m_Timer += Time.deltaTime;

        if (m_Timer < m_TimeInterval) return;

        m_Timer = 0f;

        if (m_CurrentTimeIndex >= m_MaxTimeIndex)
        {
            EndTurn();
            return;
        }

        ExecuteBeat(m_CurrentTimeIndex);
        m_CurrentTimeIndex++;
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the execution turn. Called by <see cref="GameManager.OnPlayClicked"/>.
    /// Resets the timeline index, timer, and movement state before beginning the beat loop.
    /// </summary>
    public void OnGamePlayStart()
    {
        if (m_SequenceSource == null || !m_SequenceSource.CanExecute)
        {
            Debug.LogError("[PlayerController] Cannot start — no sequence source or sequence is empty.", this);
            return;
        }

        // Snapshot sequence length so mid-execution changes don't affect the current turn
        m_MaxTimeIndex = m_SequenceSource.SequenceLength;
        m_CurrentTimeIndex = 0;
        m_Timer = 0f;
        m_IsMovingLeft = false;
        m_IsMovingRight = false;
        m_IsGamePlaying = true;
    }

    // ─── Beat Execution ─────────────────────────────────────────────────────────

    // Reads the single action for this beat from the active source and executes it
    private void ExecuteBeat(int beatIndex)
    {
        ActionTypeEnum? action = m_SequenceSource.GetActionAt(beatIndex);
        if (action == null) return;

        // Play beat audio feedback
        AudioClip clip = m_SequenceSource.GetClipForAction(action.Value);
        AudioManager.Instance?.PlayBeatTune(clip, Random.Range(0.8f, 1.2f));

        switch (action.Value)
        {
            case ActionTypeEnum.Left:
                m_IsMovingLeft = true;
                m_IsMovingRight = false;
                break;

            case ActionTypeEnum.Right:
                m_IsMovingRight = true;
                m_IsMovingLeft = false;
                break;

            case ActionTypeEnum.Jump:
                TryJump();
                break;

            case ActionTypeEnum.Interact:
                TryInteract();
                break;
        }
    }

    // ─── Actions ────────────────────────────────────────────────────────────────

    private void TryJump()
    {
        if (!m_IsGrounded) return;

        m_Rigidbody.linearVelocity = new Vector2(m_Rigidbody.linearVelocity.x, m_JumpForce);
        m_Animator.SetTrigger("Jump");

        // Briefly suppress ground check so the raycast doesn't immediately re-ground the player
        StartCoroutine(DelayGroundCheck());
    }

    private void TryInteract()
    {
        // Find all colliders within interact radius on the interact layer
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, m_InteractRadius, m_InteractLayer);

        foreach (Collider2D hit in hits)
        {
            // Use TryGetComponent to avoid boxing on interface check
            if (hit.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact();
                break; // Only interact with the first valid target
            }
        }
    }

    // ─── Movement & Animation ────────────────────────────────────────────────────

    private void HandleMovement()
    {
        float moveDir = 0f;

        if (m_IsMovingRight) moveDir = 1f;
        else if (m_IsMovingLeft) moveDir = -1f;

        // Preserve the vertical velocity (gravity/jump) while overriding horizontal speed
        m_Rigidbody.linearVelocity = new Vector2(moveDir * m_MoveSpeed, m_Rigidbody.linearVelocity.y);

        bool isRunning = moveDir != 0f;
        m_Animator.SetBool("IsRunning", isRunning);

        // Flip sprite by scaling X; does not affect child colliders
        if (moveDir != 0f)
            transform.localScale = new Vector3(Mathf.Sign(moveDir), 1f, 1f);

        AudioManager.Instance?.PlayPlayerWalk(isRunning && m_IsGrounded);
    }

    private void CheckGrounded()
    {
        // Skip the check briefly after a jump to prevent false re-grounding
        if (m_IsGroundCheckDelayed)
        {
            m_IsGrounded = false;
            m_Animator.SetBool("IsGrounded", false);
            return;
        }

        m_IsGrounded = Physics2D.Raycast(transform.position, Vector2.down, m_GroundCheckDistance, m_GroundLayer);
        m_Animator.SetBool("IsGrounded", m_IsGrounded);
    }

    private void HandleFallingAnimation()
    {
        // Falling param: 0 = moving up or idle, 1 = falling down
        float falling = m_Rigidbody.linearVelocity.y < 0f ? 1f : 0f;
        m_Animator.SetFloat("Falling", falling);
    }

    // ─── Coroutines ─────────────────────────────────────────────────────────────

    // Suppresses the ground check for one physics frame after a jump
    private IEnumerator DelayGroundCheck()
    {
        m_IsGroundCheckDelayed = true;
        yield return new WaitForSeconds(0.1f);
        m_IsGroundCheckDelayed = false;
    }

    private void EndTurn()
    {
        m_IsGamePlaying = false;
        m_IsMovingLeft = false;
        m_IsMovingRight = false;
        m_Rigidbody.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayPlayerWalk(false);
        StartCoroutine(WaitForEndStuff());
    }

    // Short delay before resetting position and unlocking UI, giving the player a moment to see the result
    private IEnumerator WaitForEndStuff()
    {
        yield return new WaitForSeconds(0.5f);
        transform.position = m_StartPosition;
        GameManager.Instance?.PlayEnded();
    }

    // ─── Collision ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore collisions when not actively executing a turn
        if (!m_IsGamePlaying || GameManager.Instance == null) return;

        if (other.CompareTag("Spike"))
        {
            GameManager.Instance.GameOver();
        }
        else if (other.CompareTag("Door") && GameManager.Instance.IsKeyCollected)
        {
            GameManager.Instance.GameWin();
        }
    }
}
