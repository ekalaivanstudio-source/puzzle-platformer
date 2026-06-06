using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A slot where the player places a <see cref="PlaceableKey"/> to unlock a door/mechanism.
///
/// Flow:
///   1. Player picks up a <see cref="PlaceableKey"/> (it sets <c>PlaceableKey.IsCarried</c>).
///   2. Player walks into this slot's trigger zone while carrying the key.
///   3. Prompt appears — press E to place the key.
///   4. Key is placed → <see cref="m_LinkedKey"/>'s Place() is called,
///      and the slot notifies <see cref="GameManager.KeyCollected()"/> so the
///      door-collision logic already in <see cref="PlayerController"/> works unchanged.
///   5. Optional door collider is disabled and/or a slot-filled visual activates.
///
/// Resets on <see cref="GameManager.OnTurnReset"/>: slot empties, visuals restore.
///
/// Setup:
///   • Add a Collider2D with Is Trigger = true.
///   • Assign the PlaceableKey this slot accepts.
///   • Optionally assign a door Collider2D to disable on placement.
///   • Optionally assign Empty/Filled visuals (swapped on placement).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KeySlot : MonoBehaviour
{
    [Header("Key")]
    [Tooltip("The PlaceableKey that fits this slot.")]
    [SerializeField] private PlaceableKey m_LinkedKey;

    [Header("Proximity")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("UI")]
    [Tooltip("Shown when player is nearby AND carrying the key.")]
    [SerializeField] private GameObject m_PlaceIcon;
    [Tooltip("Visual shown while the slot is empty.")]
    [SerializeField] private GameObject m_EmptyVisual;
    [Tooltip("Visual shown after the key is placed.")]
    [SerializeField] private GameObject m_FilledVisual;

    [Header("Door")]
    [Tooltip("BoxCollider2D that gets enabled when the key is placed (e.g. the door's trigger). Starts disabled.")]
    [SerializeField] private BoxCollider2D m_DoorCollider;

    // ─── Private state ────────────────────────────────────────────────────────

    private Collider2D m_Collider;
    private InputAction m_PlaceAction;
    private bool m_PlayerInRange;
    private bool m_Filled;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();

        m_PlaceAction = new InputAction("KeySlotPlace", InputActionType.Button);
        m_PlaceAction.AddBinding("<Keyboard>/e");
        m_PlaceAction.AddBinding("<Keyboard>/z");
        m_PlaceAction.AddBinding("<Gamepad>/buttonWest");

        Show(m_PlaceIcon, false);
        Show(m_EmptyVisual, true);
        Show(m_FilledVisual, false);
        if (m_DoorCollider != null) m_DoorCollider.enabled = false;
    }

    private void OnEnable() => GameManager.OnTurnReset += ResetSlot;
    private void OnDisable() => GameManager.OnTurnReset -= ResetSlot;

    private void OnDestroy()
    {
        GameManager.OnTurnReset -= ResetSlot;
        m_PlaceAction?.Dispose();
    }

    // ─── Trigger ──────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_Filled || !other.CompareTag(m_PlayerTag)) return;
        m_PlayerInRange = true;
        RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(m_PlayerTag)) return;
        m_PlayerInRange = false;
        Show(m_PlaceIcon, false);
        UnbindPlace();
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    // Re-check every frame so the prompt appears/disappears as the key is picked up.
    private void Update()
    {
        if (m_Filled || !m_PlayerInRange) return;
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        bool canPlace = PlaceableKey.IsCarried;
        Show(m_PlaceIcon, canPlace);

        if (canPlace) BindPlace();
        else UnbindPlace();
    }

    // ─── Input ────────────────────────────────────────────────────────────────

    private void BindPlace()
    {
        m_PlaceAction.performed -= OnPlace;
        m_PlaceAction.performed += OnPlace;
        m_PlaceAction.Enable();
    }

    private void UnbindPlace()
    {
        m_PlaceAction.performed -= OnPlace;
        m_PlaceAction.Disable();
    }

    private void OnPlace(InputAction.CallbackContext ctx)
    {
        if (!m_PlayerInRange || m_Filled || !PlaceableKey.IsCarried) return;
        PlaceKey();
    }

    // ─── Placement ────────────────────────────────────────────────────────────

    private void PlaceKey()
    {
        m_Filled = true;

        Show(m_PlaceIcon, false);
        Show(m_EmptyVisual, false);
        Show(m_FilledVisual, true);

        UnbindPlace();

        // Tell the key it is now placed.
        m_LinkedKey?.Place();

        // Enable the door collider so the player can trigger it.
        if (m_DoorCollider != null) m_DoorCollider.enabled = true;

        // Notify GameManager so the door trigger fires the win.
        GameManager.Instance?.KeyCollected();
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    private void ResetSlot()
    {
        m_Filled = false;
        m_PlayerInRange = false;

        Show(m_PlaceIcon, false);
        Show(m_EmptyVisual, true);
        Show(m_FilledVisual, false);
        if (m_DoorCollider != null) m_DoorCollider.enabled = false;

        UnbindPlace();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void Show(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }
}
