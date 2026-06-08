using UnityEngine;

/// <summary>
/// A key the player picks up automatically by walking near it, then carries
/// until placed into a <see cref="KeySlot"/>. Unlike the original Key, the
/// object does NOT notify GameManager on collection — the door only opens when
/// the key is placed in a slot.
///
/// State resets on <see cref="GameManager.OnTurnReset"/>:
///   • Key reappears at its original position.
///   • m_IsCarried is cleared so KeySlots also reset.
///
/// Setup:
///   • Add a Collider2D (non-trigger) for physics, or a trigger for overlap detection.
///   • Assign Carry Indicator (optional UI/sprite that shows while key is held).
///   • Assign Pick Icon (optional prompt shown when player is nearby).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlaceableKey : MonoBehaviour
{
    // ─── Static carried state ─────────────────────────────────────────────────

    /// <summary>True while the player is holding this key.</summary>
    public static bool IsCarried { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Proximity")]
    [Tooltip("Distance at which the pick prompt activates.")]
    [SerializeField] private float m_ProximityDistance = 1.5f;
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("UI")]
    [Tooltip("Shown when player is nearby and the key is not yet collected.")]
    [SerializeField] private GameObject m_PickIcon;
    [Tooltip("Shown while the player is carrying the key.")]
    [SerializeField] private GameObject m_CarryIndicator;

    // ─── Private state ────────────────────────────────────────────────────────

    private Transform m_PlayerTransform;
    private bool m_Collected;
    private SpriteRenderer m_SpriteRenderer;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_SpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, false);
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag(m_PlayerTag);
        if (player != null) m_PlayerTransform = player.transform;
    }

    private void OnEnable() => GameManager.OnKeyReset += ResetKey;
    private void OnDisable() => GameManager.OnKeyReset -= ResetKey;

    private void OnDestroy()
    {
        GameManager.OnKeyReset -= ResetKey;
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (m_Collected || m_PlayerTransform == null) return;

        bool inRange = Vector2.Distance(transform.position, m_PlayerTransform.position) <= m_ProximityDistance;

        // Auto-collect the moment the player walks within range — no button press.
        if (inRange) Collect();
    }

    // ─── Collect ─────────────────────────────────────────────────────────────

    private void Collect()
    {
        m_Collected = true;
        IsCarried = true;

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, true);

        // Hide sprite — key is now "in the player's hands".
        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = false;

        // Disable collider so player doesn't interact with it again.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    private void ResetKey()
    {
        m_Collected = false;
        IsCarried = false;

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, false);

        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }

    // ─── Public API for KeySlot ───────────────────────────────────────────────

    /// <summary>Called by KeySlot when the key is successfully placed.</summary>
    public void Place()
    {
        IsCarried = false;
        Show(m_CarryIndicator, false);
        // Key stays invisible — it's now in the slot.
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void Show(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }
}
