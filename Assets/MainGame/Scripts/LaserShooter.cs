using UnityEngine;

/// <summary>
/// Shoots a single laser segment from its position along local +X.
/// The segment ends at the first thing it hits:
///   • A LaserRedirector  → that redirector activates its own LineRenderer segment.
///   • A blocking layer   → beam terminates here.
///   • Nothing            → beam terminates at m_MaxDistance.
///
/// Setup:
///   • Add a LineRenderer to this GameObject (world space, 2 points).
///   • Rotate the GameObject so local +X faces the fire direction.
///   • Assign Blocking Layers (Ground, walls) and Redirector Layer in the Inspector.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LaserShooter : MonoBehaviour
{
    [Header("Beam Settings")]
    [Tooltip("Layers the beam terminates on (Ground, walls, etc.).")]
    [SerializeField] private LayerMask m_BlockingLayers;

    [Tooltip("Layer mask that identifies LaserRedirector colliders.")]
    [SerializeField] private LayerMask m_RedirectorLayer;

    [Tooltip("Maximum beam travel distance before forced termination.")]
    [SerializeField] private float m_MaxDistance = 50f;

    [Tooltip("Maximum number of redirections in the whole chain.")]
    [SerializeField] private int m_MaxBounces = 8;

    [Tooltip("Local-space offset from this object's position where the beam starts.")]
    [SerializeField] private Vector2 m_BeamOffset = Vector2.zero;

    [Header("Player Kill")]
    [Tooltip("Layer the player is on. Used for laser-touch detection.")]
    [SerializeField] private LayerMask m_PlayerLayer;

    [Tooltip("How close the player's collider centre must be to the beam line to count as a hit.")]
    [SerializeField] private float m_PlayerCheckRadius = 0.18f;

    private LineRenderer m_LineRenderer;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    // Tracks whether this shooter is currently contributing to the shared laser hum,
    // so enable/disable stays balanced and the loop is never double-counted.
    private bool m_HumOn;

    private void Awake()
    {
        m_LineRenderer = GetComponent<LineRenderer>();
        m_LineRenderer.useWorldSpace = true;
        m_LineRenderer.positionCount = 2;
    }

    // Start as well as OnEnable: on the very first enable AudioManager may not have
    // run its Awake yet, so Start guarantees the hum begins once everything exists.
    private void OnEnable() => TryStartHum();
    private void Start() => TryStartHum();

    private void OnDisable()
    {
        if (!m_HumOn) return;
        AudioManager.Instance?.NotifyLaserActive(false);
        m_HumOn = false;
    }

    private void TryStartHum()
    {
        if (m_HumOn || AudioManager.Instance == null) return;
        AudioManager.Instance.NotifyLaserActive(true);
        m_HumOn = true;
    }

    private void LateUpdate()
    {
        CastBeam();
    }

    // ─── Beam casting ─────────────────────────────────────────────────────────

    private void CastBeam()
    {
        // Tell every redirector to hide its segment — only those hit this frame will re-enable.
        LaserRedirector.ClearAll();

        Vector2 origin = (Vector2)transform.position + (Vector2)(transform.TransformDirection(m_BeamOffset));
        Vector2 direction = transform.right;

        int combinedMask = m_BlockingLayers | m_RedirectorLayer;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, m_MaxDistance, combinedMask);

        Vector2 endPoint;

        if (hit.collider != null)
        {
            endPoint = hit.point;

            // If a redirector was hit, activate its chain.
            LaserRedirector redirector = hit.collider.GetComponentInParent<LaserRedirector>();
            if (redirector != null)
            {
                float remaining = m_MaxDistance - hit.distance;
                redirector.ActivateBeam(direction, remaining, m_MaxBounces,
                                        m_BlockingLayers, m_RedirectorLayer);
            }
            else
            {
                // Notify a laser-destructible push brick if the beam terminates on one.
                hit.collider.GetComponentInParent<PushBrick>()?.OnLaserHit();
            }
        }
        else
        {
            // Nothing hit — draw to max range.
            endPoint = origin + direction * m_MaxDistance;
        }

        // This object only owns the segment from its own position to the first hit.
        m_LineRenderer.SetPosition(0, origin);
        m_LineRenderer.SetPosition(1, endPoint);

        CheckPlayerKill(origin, endPoint);
    }

    private void CheckPlayerKill(Vector2 shooterStart, Vector2 shooterEnd)
    {
        if (PlayerController.Instance == null) return;
        Bounds playerBounds = PlayerController.Instance.ColliderBounds;

        // Check shooter's own segment.
        if (SegmentHitsBounds(shooterStart, shooterEnd, playerBounds))
        {
            KillPlayer();
            return;
        }

        // Check all redirector segments accumulated this frame.
        foreach (var seg in LaserRedirector.ActiveSegments)
        {
            if (SegmentHitsBounds(seg.start, seg.end, playerBounds))
            {
                KillPlayer();
                return;
            }
        }
    }

    private static void KillPlayer()
    {
        if (PlayerController.Instance != null)
            PlayerController.Instance.OnLaserHit();
    }

    // Returns true if the beam segment (a, b) passes within m_PlayerCheckRadius of
    // the player's collider bounds. Testing against the whole body — not the foot
    // pivot — means a beam that grazes the top of a brick the player stands on no
    // longer registers as a hit. And because each segment already stops at its first
    // blocker, a player standing on (or shielded behind) that blocker sits beyond
    // the segment's end, so the closest point falls outside their bounds.
    private bool SegmentHitsBounds(Vector2 a, Vector2 b, Bounds bounds)
    {
        Vector2 center = bounds.center;
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        Vector2 closest = len2 < 0.0001f
            ? a
            : a + Mathf.Clamp01(Vector2.Dot(center - a, ab) / len2) * ab;
        return bounds.SqrDistance(closest) <= m_PlayerCheckRadius * m_PlayerCheckRadius;
    }

    // ─── Editor Gizmos ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
    // Draws the beam and the capsule (radius = m_PlayerCheckRadius) within which the
    // player's collider counts as hit, so the kill tolerance can be tuned visually.
    // Shows only when this shooter is selected to keep the Scene view uncluttered.
    private void OnDrawGizmosSelected()
    {
        Vector2 origin = (Vector2)transform.position + (Vector2)transform.TransformDirection(m_BeamOffset);
        Vector2 direction = transform.right;

        // Mirror CastBeam's termination so the preview matches the live segment
        // length. This is a read-only raycast — safe in both edit and play mode.
        int mask = m_BlockingLayers | m_RedirectorLayer;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, m_MaxDistance, mask);
        Vector2 end = hit.collider != null ? hit.point : origin + direction * m_MaxDistance;

        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawLine(origin, end);

        // Capsule edges: two parallel lines offset by the radius, plus rounded caps.
        Vector2 dir = (end - origin).sqrMagnitude > 0.0001f ? (end - origin).normalized : direction;
        Vector2 normal = new Vector2(-dir.y, dir.x) * m_PlayerCheckRadius;
        Gizmos.DrawLine(origin + normal, end + normal);
        Gizmos.DrawLine(origin - normal, end - normal);
        DrawWireCircle(origin, m_PlayerCheckRadius);
        DrawWireCircle(end, m_PlayerCheckRadius);
    }

    private static void DrawWireCircle(Vector2 center, float radius, int segments = 24)
    {
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 next = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
