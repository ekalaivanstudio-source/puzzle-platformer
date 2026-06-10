using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A brick that the player can push one unit at a time by walking into it.
/// Only pushing is possible — the player cannot pull.
///
/// When the player's horizontal movement hits this brick:
///   • The brick slides one unit in the movement direction (if not blocked).
///   • The player stops at their current position.
///   • The remaining distance of the current move command is abandoned.
///   • Execution continues with the next command in the sequence.
///
/// Two variants controlled by <see cref="m_IsLaserDestructible"/>:
///   false — brick ignores laser hits.
///   true  — brick deactivates when hit by a laser; restored on turn reset.
///
/// Setup:
///   • Place on the Ground layer so the player wall-check detects it.
///   • Assign Blocking Layers (what stops the brick from being pushed further).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PushBrick : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("If true, a laser beam destroys this brick for the current turn.")]
    [SerializeField] private bool m_IsLaserDestructible = false;

    [Tooltip("Speed at which the brick slides after being pushed (units / sec).")]
    [SerializeField] private float m_PushSpeed = 10f;

    [Tooltip("Distance the brick travels per push (should match the level grid unit).")]
    [SerializeField] private float m_PushDistance = 1f;

    [Tooltip("Layers that block the brick's movement (Ground, walls, other bricks, etc.).")]
    [SerializeField] private LayerMask m_BlockingLayers;

    [Header("Destroy FX")]
    [Tooltip("Particle prefab spawned at the brick's position when destroyed by a laser.")]
    [SerializeField] private GameObject m_DestroyParticle;
    [SerializeField] private float m_ShakeMagnitude = 0.15f;
    [SerializeField] private float m_ShakeDuration = 0.3f;

    // ─── State ────────────────────────────────────────────────────────────────

    private Vector3 m_StartPosition;
    private Collider2D m_Collider;
    private Rigidbody2D m_Rigidbody;

    // Reused by GetAllowedDistance so the sweep allocates no garbage.
    private readonly List<RaycastHit2D> m_CastResults = new List<RaycastHit2D>();
    private ContactFilter2D m_BlockingFilter;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_StartPosition = transform.position;
        m_Collider = GetComponent<Collider2D>();
        m_BlockingFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        m_BlockingFilter.SetLayerMask(m_BlockingLayers);

        // Force the body to be physics-immovable: the brick must move ONLY via the
        // scripted unit Push() (left/right player movement). Kinematic bodies ignore
        // all contact forces, so jumping onto the brick or lightly touching it can no
        // longer nudge it — yet it stays a solid obstacle the player collides with.
        m_Rigidbody = GetComponent<Rigidbody2D>();
        if (m_Rigidbody != null)
        {
            m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            m_Rigidbody.linearVelocity = Vector2.zero;
            m_Rigidbody.angularVelocity = 0f;
        }
    }

    private void OnEnable() => GameManager.OnFullReset += ResetBrick;
    private void OnDisable() => GameManager.OnFullReset -= ResetBrick;

    private void ResetBrick()
    {
        gameObject.SetActive(true);
        transform.position = m_StartPosition;
    }

    // ─── Push ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="PlayerController"/> when the player walks into this brick.
    /// Slides the brick one unit in <paramref name="signDir"/> (+1 right, -1 left)
    /// unless the destination is blocked.
    /// </summary>
    public IEnumerator Push(float signDir)
    {
        if (m_Collider == null) yield break;

        Vector2 dir = new Vector2(Mathf.Sign(signDir), 0f);

        // Sweep the brick's footprint across the FULL push distance to find the
        // first blocker. This catches ground/walls in any cell along the path —
        // not just the final destination — which matters whenever m_PushDistance
        // spans more than one grid unit.
        float allowed = GetAllowedDistance(dir);
        if (allowed <= 0.01f)
            yield break; // Blocked immediately — brick doesn't move; player is still stopped.

        AudioManager.Instance?.PlayBrickPush();

        float rawTargetX = transform.position.x + dir.x * allowed;

        // Snap the landing spot to the grid, biased toward the start so the brick
        // never overshoots into the obstacle that stopped it. When the path is
        // clear this is a no-op (start + integer distance is already on-grid).
        float targetX = dir.x > 0f ? Mathf.Floor(rawTargetX) : Mathf.Ceil(rawTargetX);

        Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);

        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, m_PushSpeed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        transform.position = target;
    }

    // ─── Laser ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="LaserShooter"/> when a laser beam terminates on this brick.
    /// Has no effect if <see cref="m_IsLaserDestructible"/> is false.
    /// </summary>
    public void OnLaserHit()
    {
        if (!m_IsLaserDestructible) return;

        AudioManager.Instance?.PlayBrickDestroy();

        if (m_DestroyParticle != null)
            Instantiate(m_DestroyParticle, transform.position, Quaternion.identity);

        CameraController.Instance?.Shake(m_ShakeMagnitude, m_ShakeDuration);

        gameObject.SetActive(false);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Returns how far (≤ m_PushDistance) the brick can travel in dir before its
    // footprint would overlap a blocker. Returns m_PushDistance when the path is clear.
    private float GetAllowedDistance(Vector2 dir)
    {
        Bounds b = m_Collider.bounds;

        // Shrink the footprint slightly so the ground the brick rests on (directly
        // below) and colliders merely touching its sides aren't treated as blockers.
        Vector2 size = b.size * 0.95f;

        int count = Physics2D.BoxCast(
            b.center, size, 0f, dir, m_BlockingFilter, m_CastResults, m_PushDistance);

        float allowed = m_PushDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = m_CastResults[i];
            if (hit.collider == null || hit.collider == m_Collider) continue;
            allowed = Mathf.Min(allowed, hit.distance);
        }

        return allowed;
    }
}
