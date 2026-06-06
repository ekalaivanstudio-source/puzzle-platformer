using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Checkpoint trigger. While the player's collider overlaps this zone, a prompt UI
/// is shown. Pressing Interact (E / gamepad button) during that window:
///   1. Immediately stops the current execution run.
///   2. Snaps the player to this object's position.
///   3. Clears the queued sequence and re-enables input.
///   4. Updates the player's reset-position so future turn-ends restart from here.
///
/// Requires a Collider2D (set Is Trigger = true) on this GameObject.
/// The InputActionAsset is sourced automatically from <see cref="DeviceInputProvider"/>.
/// </summary>
public class InputResetter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI shown while the player is inside the zone (e.g. 'Press E to reset').")]
    [SerializeField] private GameObject m_PromptUI;

    [Tooltip("Tag used to identify the player GameObject.")]
    [SerializeField] private string m_PlayerTag = "Player";

    private Collider2D m_Collider;
    private InputAction m_InteractAction;
    private bool m_PlayerInRange;
    private bool m_Used;   // blocks re-trigger until the next turn starts

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        if (m_Collider == null)
            Debug.LogError("[InputResetter] No Collider2D found. Add one and enable Is Trigger.", this);
    }

    private void Start()
    {
        InputActionAsset asset = DeviceInputProvider.Instance?.InputActionAsset;
        if (asset != null)
        {
            InputActionMap map = asset.FindActionMap("Player", throwIfNotFound: false);
            m_InteractAction = map?.FindAction("Interact", throwIfNotFound: false);
            if (m_InteractAction == null)
                Debug.LogWarning("[InputResetter] 'Interact' action not found in 'Player' map.", this);
            // Re-subscribe now that we have the action (OnEnable ran before Start)
            m_InteractAction.performed += OnInteract;
        }
        else
        {
            Debug.LogWarning("[InputResetter] DeviceInputProvider not found — Interact binding unavailable.", this);
        }
    }

    private void OnEnable()
    {
        if (m_InteractAction != null) m_InteractAction.performed += OnInteract;
        GameManager.OnTurnReset += HandleTurnReset;
    }

    private void OnDisable()
    {
        if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
        GameManager.OnTurnReset -= HandleTurnReset;
        SetPrompt(false);
        m_PlayerInRange = false;
    }

    // ─── Overlap detection ────────────────────────────────────────────────────
    // Using OverlapBox (same approach as InvisibleLockPoint) because the Rigidbody2D
    // on the player may not reliably fire OnTriggerEnter2D in all configurations.

    private void Update()
    {
        if (m_Used || m_Collider == null) return;

        bool inside = false;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.CompareTag(m_PlayerTag)) { inside = true; break; }
        }

        if (inside == m_PlayerInRange) return;
        m_PlayerInRange = inside;
        SetPrompt(m_PlayerInRange);
    }

    // ─── Interact callback ────────────────────────────────────────────────────
    // Registered directly on the InputAction so it fires even when
    // DeviceInputProvider has IsEnabled = false (i.e. during execution).

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!m_PlayerInRange || m_Used) return;

        m_Used = true;
        SetPrompt(false);
        m_PlayerInRange = false;

        PlayerController.Instance?.ResetAtCheckpoint(transform.position);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void SetPrompt(bool visible)
    {
        if (m_PromptUI != null) m_PromptUI.SetActive(visible);
    }

    // Allow the zone to be re-used on the next turn so the player can trigger it
    // again if they loop back through this part of the level.
    private void HandleTurnReset() => m_Used = false;
}
