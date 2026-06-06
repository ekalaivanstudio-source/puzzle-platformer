using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Trigger zone that lets the player rotate a <see cref="LaserRedirector"/> interactively.
///
/// Flow:
///   1. Player enters the zone → prompt UI appears.
///   2. Player presses E → control mode begins.
///        - Current execution is aborted and the sequence is cleared.
///        - Left arrow  → rotate target 90° counter-clockwise.
///        - Right arrow → rotate target 90° clockwise.
///   3. Player presses E again → control mode ends, input is restored.
///
/// Requires a Collider2D (Is Trigger = true) on this GameObject.
/// </summary>
public class LaserRedirectorRotatorInputResetter : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The LaserRedirector that will be rotated.")]
    [SerializeField] private LaserRedirector m_TargetRedirector;

    [Header("UI")]
    [Tooltip("Shown when the player is in range ('Press E to control').")]
    [SerializeField] private GameObject m_PromptUI;

    [Tooltip("Shown while in control mode ('Left / Right to rotate, E to confirm').")]
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
            Debug.LogError("[LaserRedirectorRotatorInputResetter] No Collider2D found.", this);
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
            HandleRotationInput();
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

    // ─── Rotation input ───────────────────────────────────────────────────────

    private void HandleRotationInput()
    {
        if (m_TargetRedirector == null) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.leftArrowKey.wasPressedThisFrame)
            m_TargetRedirector.transform.Rotate(0f, 0f, 90f);   // CCW

        if (keyboard.rightArrowKey.wasPressedThisFrame)
            m_TargetRedirector.transform.Rotate(0f, 0f, -90f);  // CW
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
}
