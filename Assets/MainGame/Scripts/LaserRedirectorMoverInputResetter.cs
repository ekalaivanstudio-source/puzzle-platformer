using System.Collections.Generic;
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
    [Tooltip("Minimum X offset from the redirector's starting position.")]
    [SerializeField] private float m_MinX = -10f;
    [Tooltip("Maximum X offset from the redirector's starting position.")]
    [SerializeField] private float m_MaxX = 10f;
    [Tooltip("Minimum Y offset from the redirector's starting position.")]
    [SerializeField] private float m_MinY = -10f;
    [Tooltip("Maximum Y offset from the redirector's starting position.")]
    [SerializeField] private float m_MaxY = 10f;

    [Header("UI")]
    [Tooltip("Shown when the player is in range ('Press E to control').")]
    [SerializeField] private GameObject m_PromptUI;

    [Tooltip("Shown while in control mode ('Arrows to move, E to confirm').")]
    [SerializeField] private GameObject m_ControlUI;

    [Tooltip("Tag used to identify the player.")]
    [SerializeField] private string m_PlayerTag = "Player";

    // ─── Private state ────────────────────────────────────────────────────────

    private Collider2D m_Collider;
    private InputAction m_InteractAction;
    private bool m_PlayerInRange;
    private bool m_InControlMode;
    private Vector3 m_InitialPosition;

    // Reused so the per-frame overlap check in UpdatePlayerInRange allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_NoFilter;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        if (m_Collider == null)
            Debug.LogError("[LaserRedirectorMoverInputResetter] No Collider2D found.", this);

        m_NoFilter = ContactFilter2D.noFilter;

        if (m_TargetRedirector != null)
            m_InitialPosition = m_TargetRedirector.transform.position;
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
        GameManager.OnFullReset += OnFullReset;
    }

    private void OnDisable()
    {
        if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
        if (m_InControlMode) ExitControlMode();
        GameManager.OnFullReset -= OnFullReset;
    }

    private void OnFullReset()
    {
        if (m_TargetRedirector != null)
            m_TargetRedirector.transform.position = m_InitialPosition;
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
        int count = Physics2D.OverlapBox(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f, m_NoFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D h = m_OverlapResults[i];
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
        if (keyboard.leftArrowKey.wasPressedThisFrame) delta = Vector2.left;
        if (keyboard.rightArrowKey.wasPressedThisFrame) delta = Vector2.right;
        if (keyboard.upArrowKey.wasPressedThisFrame) delta = Vector2.up;
        if (keyboard.downArrowKey.wasPressedThisFrame) delta = Vector2.down;

        if (delta == Vector2.zero) return;

        Vector3 pos = m_TargetRedirector.transform.position;
        pos.x = Mathf.Clamp(pos.x + delta.x * m_MoveStep, m_InitialPosition.x + m_MinX, m_InitialPosition.x + m_MaxX);
        pos.y = Mathf.Clamp(pos.y + delta.y * m_MoveStep, m_InitialPosition.y + m_MinY, m_InitialPosition.y + m_MaxY);

        if (pos != m_TargetRedirector.transform.position)
            AudioManager.Instance?.PlayLaserMove();

        m_TargetRedirector.transform.position = pos;
    }

    // ─── Control mode ─────────────────────────────────────────────────────────

    private void EnterControlMode()
    {
        m_InControlMode = true;
        AudioManager.Instance?.PlayControlEnter();
        Show(m_PromptUI, false);
        Show(m_ControlUI, true);

        // Abort execution at the player's current position, clear sequence, fire OnTurnReset.
        if (PlayerController.Instance != null)
            PlayerController.Instance.ResetAtCheckpoint(transform.position);


        // Lock normal input — arrow keys now control the redirector.
        DeviceInputProvider.Instance?.SetEnabled(false);
    }

    private void ExitControlMode()
    {
        m_InControlMode = false;
        AudioManager.Instance?.PlayControlExit();
        Show(m_ControlUI, false);
        DeviceInputProvider.Instance?.SetEnabled(true);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void Show(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }

    // Visualise the movement boundary in the Scene view.
    // Always visible in Scene view so you can see the boundary while placing objects.
    private void OnDrawGizmos()
    {
        // Use the target's current transform position as origin (works in editor before play).
        Vector3 origin = m_TargetRedirector != null
            ? m_TargetRedirector.transform.position
            : transform.position;

        float worldMinX = origin.x + m_MinX;
        float worldMaxX = origin.x + m_MaxX;
        float worldMinY = origin.y + m_MinY;
        float worldMaxY = origin.y + m_MaxY;

        Vector3 center = new Vector3((worldMinX + worldMaxX) * 0.5f, (worldMinY + worldMaxY) * 0.5f, 0f);
        Vector3 size = new Vector3(worldMaxX - worldMinX, worldMaxY - worldMinY, 0.01f);

        // Filled semi-transparent interior.
        Gizmos.color = new Color(0f, 1f, 1f, 0.08f);
        Gizmos.DrawCube(center, size);

        // Solid outline.
        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, size);

        // Draw a wire square at each reachable grid position.
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Vector3 cellSize = new Vector3(m_MoveStep, m_MoveStep, 0.01f);
        for (float x = worldMinX; x <= worldMaxX + 0.001f; x += m_MoveStep)
        {
            for (float y = worldMinY; y <= worldMaxY + 0.001f; y += m_MoveStep)
            {
                Gizmos.DrawWireCube(new Vector3(x, y, 0f), cellSize);
            }
        }

        // Highlight the target redirector's current position inside the boundary.
        if (m_TargetRedirector != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_TargetRedirector.transform.position, m_MoveStep * 0.25f);
        }
    }

    // Richer detail when selected.
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = m_TargetRedirector != null
            ? m_TargetRedirector.transform.position
            : transform.position;

        float worldMinX = origin.x + m_MinX;
        float worldMaxX = origin.x + m_MaxX;
        float worldMinY = origin.y + m_MinY;
        float worldMaxY = origin.y + m_MaxY;

        // Draw min/max labels as coloured dots for X and Y bounds.
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(new Vector3(worldMinX, (worldMinY + worldMaxY) * 0.5f, 0f), m_MoveStep * 0.12f);
        Gizmos.DrawSphere(new Vector3(worldMaxX, (worldMinY + worldMaxY) * 0.5f, 0f), m_MoveStep * 0.12f);
        Gizmos.DrawSphere(new Vector3((worldMinX + worldMaxX) * 0.5f, worldMinY, 0f), m_MoveStep * 0.12f);
        Gizmos.DrawSphere(new Vector3((worldMinX + worldMaxX) * 0.5f, worldMaxY, 0f), m_MoveStep * 0.12f);
    }
}
