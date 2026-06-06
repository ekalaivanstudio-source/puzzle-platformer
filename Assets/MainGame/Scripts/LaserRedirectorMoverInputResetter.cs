using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Trigger zone that lets the player move a <see cref="LaserRedirector"/> one unit
/// at a time along the X and Y axes, within configurable boundaries.
///
/// Flow:
///   1. Player enters the zone → prompt UI appears.
///   2. Player presses E → control mode begins.
///        - Current execution is aborted and the sequence is cleared.
///        - Left / Right arrows → move target one step horizontally.
///        - Up   / Down  arrows → move target one step vertically.
///        - Movement is clamped to [MinX, MaxX] × [MinY, MaxY].
///   3. Player presses E again → control mode ends, input is restored.
///
/// Requires a Collider2D (Is Trigger = true) on this GameObject.
/// </summary>
public class LaserRedirectorMoverInputResetter : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The LaserRedirector that will be moved.")]
    [SerializeField] private LaserRedirector m_TargetRedirector;

    [Header("Movement")]
    [Tooltip("Distance moved per key press (units).")]
    [SerializeField] private float m_MoveStep = 1f;

    [Header("Bounds")]
    [Tooltip("Minimum world X position the redirector may occupy.")]
    [SerializeField] private float m_MinX = -10f;
    [Tooltip("Maximum world X position the redirector may occupy.")]
    [SerializeField] private float m_MaxX =  10f;
    [Tooltip("Minimum world Y position the redirector may occupy.")]
    [SerializeField] private float m_MinY = -10f;
    [Tooltip("Maximum world Y position the redirector may occupy.")]
    [SerializeField] private float m_MaxY =  10f;

    [Header("UI")]
    [Tooltip("Shown when the player is in range ('Press E to control').")]
    [SerializeField] private GameObject m_PromptUI;

    [Tooltip("Shown while in control mode ('Arrows to move, E to confirm').")]
    [SerializeField] private GameObject m_ControlUI;

    [Tooltip("Tag used to identify the player.")]
    [SerializeField] private string m_PlayerTag = "Player";

    // ─── Private state ────────────────────────────────────────────────────────

    private Collider2D  m_Collider;
    private InputAction m_InteractAction;
    private bool        m_PlayerInRange;
    private bool        m_InControlMode;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        if (m_Collider == null)
            Debug.LogError("[LaserRedirectorMoverInputResetter] No Collider2D found.", this);
    }

    private void Start()
    {
        InputActionAsset asset = DeviceInputProvider.Instance?.InputActionAsset;
        if (asset != null)
        {
            InputActionMap map = asset.FindActionMap("Player", throwIfNotFound: false);
            m_InteractAction = map?.FindAction("Interact", throwIfNotFound: false);
        }

        // Guarantee subscription after all Awake calls have run.
        if (m_InteractAction != null)
        {
            m_InteractAction.performed -= OnInteract;
            m_InteractAction.performed += OnInteract;
        }
    }

    private void OnEnable()
    {
        if (m_InteractAction != null) m_InteractAction.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
        if (m_InControlMode) ExitControlMode();
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (m_InControlMode)
        {
            HandleMoveInput();
            return;
        }

        UpdatePlayerInRange();
    }

    // ─── Overlap detection ────────────────────────────────────────────────────

    private void UpdatePlayerInRange()
    {
        if (m_Collider == null) return;

        bool inside = false;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f);
        foreach (var h in hits)
        {
            if (h.gameObject == gameObject) continue;
            if (h.CompareTag(m_PlayerTag)) { inside = true; break; }
        }

        if (inside == m_PlayerInRange) return;
        m_PlayerInRange = inside;
        Show(m_PromptUI, m_PlayerInRange);
    }

    // ─── Interact callback ────────────────────────────────────────────────────

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (m_InControlMode)
        {
            ExitControlMode();
        }
        else if (m_PlayerInRange)
        {
            EnterControlMode();
        }
    }

    // ─── Movement input ───────────────────────────────────────────────────────

    private void HandleMoveInput()
    {
        if (m_TargetRedirector == null) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 delta = Vector2.zero;
        if (keyboard.leftArrowKey.wasPressedThisFrame)  delta = Vector2.left;
        if (keyboard.rightArrowKey.wasPressedThisFrame) delta = Vector2.right;
        if (keyboard.upArrowKey.wasPressedThisFrame)    delta = Vector2.up;
        if (keyboard.downArrowKey.wasPressedThisFrame)  delta = Vector2.down;

        if (delta == Vector2.zero) return;

        Vector3 pos = m_TargetRedirector.transform.position;
        pos.x = Mathf.Clamp(pos.x + delta.x * m_MoveStep, m_MinX, m_MaxX);
        pos.y = Mathf.Clamp(pos.y + delta.y * m_MoveStep, m_MinY, m_MaxY);
        m_TargetRedirector.transform.position = pos;
    }

    // ─── Control mode ─────────────────────────────────────────────────────────

    private void EnterControlMode()
    {
        m_InControlMode = true;
        Show(m_PromptUI, false);
        Show(m_ControlUI, true);

        // Abort execution at the player's current position, clear sequence, fire OnTurnReset.
        if (PlayerController.Instance != null)
            PlayerController.Instance.ResetAtCheckpoint(PlayerController.Instance.transform.position);

        // Lock normal input — arrow keys now control the redirector.
        DeviceInputProvider.Instance?.SetEnabled(false);
    }

    private void ExitControlMode()
    {
        m_InControlMode = false;
        Show(m_ControlUI, false);
        DeviceInputProvider.Instance?.SetEnabled(true);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void Show(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }

    // Visualise the movement boundary in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Vector3 center = new Vector3((m_MinX + m_MaxX) * 0.5f, (m_MinY + m_MaxY) * 0.5f, 0f);
        Vector3 size   = new Vector3(m_MaxX - m_MinX, m_MaxY - m_MinY, 0.1f);
        Gizmos.DrawWireCube(center, size);

        if (m_TargetRedirector != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(m_TargetRedirector.transform.position, 0.2f);
        }
    }
}
