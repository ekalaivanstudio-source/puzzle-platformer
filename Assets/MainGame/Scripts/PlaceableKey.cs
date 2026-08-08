using System.Collections;
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
    [SerializeField] private GameObject shineEffect;

    [Header("Collect VFX")]
    [Tooltip("Effect burst spawned once when the battery is picked up (e.g. Flash_ellow). " +
             "Optional. A particle prefab — ParticleEffectSpawner destroys it once its " +
             "systems have finished, so it needs no self-cleanup.")]
    [SerializeField] private GameObject m_CollectEffectPrefab;

    [Tooltip("Uniform scale applied to the collect effect when it spawns. The stock FX " +
             "prefabs are authored for a much larger world than this one — Flash_ellow's " +
             "flash alone is ~16 units across, against a 1-unit grid cell — so they need " +
             "scaling down to pickup size rather than being used at 1.")]
    [SerializeField] private float m_CollectEffectScale = 0.3f;

    [Tooltip("World offset from the key's position where the collect effect spawns.")]
    [SerializeField] private Vector3 m_CollectEffectOffset = Vector3.zero;

    [Tooltip("Seconds the collect effect takes to fade away once it has fired. 0 lets it " +
             "play out to its authored length instead.")]
    [SerializeField] private float m_CollectEffectFadeDuration = 0.5f;

    // ─── Private state ────────────────────────────────────────────────────────

    private Transform m_PlayerTransform;
    private bool m_Collected;
    private SpriteRenderer m_SpriteRenderer;

    // Held so a reset landing between the pickup and the player touching down can cancel the
    // burst, rather than firing it over a key that is already back in the world.
    private Coroutine m_CollectEffectRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_SpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // A key serialized before the field existed deserializes it as 0, which would
        // collapse the burst to nothing instead of just leaving it unscaled.
        if (m_CollectEffectScale <= 0f) m_CollectEffectScale = 0.3f;

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, false);
    }

    private void Start() => ResolvePlayer();

    // FindGameObjectWithTag only ever returns an ACTIVE object, and a level with an entry
    // door starts with its player disabled in the doorway — so the Start lookup comes up
    // empty and the search has to be retried until the door brings the player in. Without
    // the retry the key's proximity check below stayed switched off for the whole level and
    // the key could never be picked up.
    private void ResolvePlayer()
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
        if (m_Collected) return;

        if (m_PlayerTransform == null)
        {
            ResolvePlayer();
            if (m_PlayerTransform == null) return;
        }

        bool inRange = Vector2.Distance(transform.position, m_PlayerTransform.position) <= m_ProximityDistance;

        // Auto-collect the moment the player walks within range — no button press.
        if (inRange) Collect();
    }

    // ─── Collect ─────────────────────────────────────────────────────────────

    private void Collect()
    {
        m_Collected = true;
        IsCarried = true;

        AudioManager.Instance?.PlayPickup();

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, true);

        // Key has been collected — stop the shine effect.
        Show(shineEffect, false);

        m_CollectEffectRoutine = StartCoroutine(CollectEffectRoutine());

        // Hide sprite — key is now "in the player's hands".
        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = false;

        // Disable collider so player doesn't interact with it again.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    // The pickup triggers on proximity, which the player often reaches mid-jump — the arc
    // passes within range well before it comes down. Bursting there would leave the effect
    // hanging in mid-air, disowned by the landing that earned it, so it waits for the feet
    // to be back on the ground. Grounded already (a walk into the battery) costs one frame.
    private IEnumerator CollectEffectRoutine()
    {
        while (PlayerController.Instance != null && PlayerController.Instance.IsAirborne)
            yield return null;

        SpawnCollectEffect();
        m_CollectEffectRoutine = null;
    }

    // Burst at the battery's own depth, so it draws with the pickup rather than at z 0.
    // ParticleEffectSpawner owns the scaling, the fade, and the cleanup.
    private void SpawnCollectEffect() =>
        ParticleEffectSpawner.Spawn(
            m_CollectEffectPrefab,
            transform.position + m_CollectEffectOffset,
            m_CollectEffectScale,
            0f,
            m_CollectEffectFadeDuration);

    // ─── Reset ────────────────────────────────────────────────────────────────

    private void ResetKey()
    {
        m_Collected = false;
        IsCarried = false;

        // A burst still waiting on the player to land belongs to a pickup this reset has
        // just undone — drop it, or it would go off over a key back in the world.
        if (m_CollectEffectRoutine != null)
        {
            StopCoroutine(m_CollectEffectRoutine);
            m_CollectEffectRoutine = null;
        }

        Show(m_PickIcon, false);
        Show(m_CarryIndicator, false);

        // Key is back in the world — bring the shine effect back.
        Show(shineEffect, true);

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
