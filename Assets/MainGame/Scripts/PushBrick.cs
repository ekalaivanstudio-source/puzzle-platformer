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

        // Check whether the path to the destination is clear before committing.
        // Uses BoxCast so it reliably detects walls the brick is already touching.
        if (m_Collider != null && IsPathBlocked(signDir))
            yield break; // Blocked — brick doesn't move; player is still stopped.

        // Slide smoothly to targetX.
        Vector3 target = new Vector3(targetX, transform.position.y, transform.position.z);

        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            // Mid-slide safety: stop immediately if a wall is encountered.
            if (m_Collider != null && IsPathBlocked(signDir))
            {
                // Snap back to the nearest grid position to avoid partial wall overlap.
                float snappedX = signDir > 0f
                    ? Mathf.Floor(transform.position.x)
                    : Mathf.Ceil(transform.position.x);
                transform.position = new Vector3(snappedX, transform.position.y, transform.position.z);
                yield break;
            }

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

    // Returns true if the next grid cell in signDir is occupied by the ground layer.
    // Fires a raycast from the side face of the brick toward the adjacent grid position.
    private bool IsPathBlocked(float signDir)
    {
        // Start the ray from the side edge of the brick at three vertical heights
        // (top, centre, bottom) so a wall that only partially overlaps is caught.
        float sideX = signDir > 0f ? m_Collider.bounds.max.x : m_Collider.bounds.min.x;
        float centerY = m_Collider.bounds.center.y;
        float halfH = m_Collider.bounds.extents.y * 0.9f; // slightly inset to avoid edge noise

        // Ray length = one grid unit (m_PushDistance) so only the next cell is checked.
        float rayLength = m_PushDistance;
        Vector2 direction = new Vector2(signDir, 0f);

        float[] checkYOffsets = { 0f, halfH, -halfH };
        foreach (float yOffset in checkYOffsets)
        {
            Vector2 origin = new Vector2(sideX, centerY + yOffset);
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, rayLength, m_BlockingLayers);
            if (hit.collider != null && hit.collider != m_Collider)
                return true;
        }

        return false;
    }
}
