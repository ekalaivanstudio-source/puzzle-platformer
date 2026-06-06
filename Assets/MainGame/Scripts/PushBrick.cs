using System.Collections;
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

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_StartPosition = transform.position;
        m_Collider = GetComponent<Collider2D>();
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
        float targetX = transform.position.x + signDir * m_PushDistance;

        // Check whether the destination is clear before committing to the move.
        if (m_Collider != null && IsDestinationBlocked(signDir, targetX))
            yield break; // Blocked — brick doesn't move; player is still stopped.

        // Slide smoothly to targetX.
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

        if (m_DestroyParticle != null)
            Instantiate(m_DestroyParticle, transform.position, Quaternion.identity);

        CameraController.Instance?.Shake(m_ShakeMagnitude, m_ShakeDuration);

        gameObject.SetActive(false);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Returns true if the brick's future position would overlap something on m_BlockingLayers.
    private bool IsDestinationBlocked(float signDir, float targetX)
    {
        // Cast the full collider bounds shifted by one push-unit.
        Vector2 futureCenter = new Vector2(targetX, m_Collider.bounds.center.y);
        // Slightly shrink the check box to avoid edge-of-tile false positives.
        Vector2 checkSize = (Vector2)m_Collider.bounds.size * 0.85f;

        Collider2D blocker = Physics2D.OverlapBox(futureCenter, checkSize, 0f, m_BlockingLayers);
        // Ignore self — only true blockers count.
        return blocker != null && blocker != m_Collider;
    }
}
