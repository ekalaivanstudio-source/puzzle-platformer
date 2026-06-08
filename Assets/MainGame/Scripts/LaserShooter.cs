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
        Vector2 playerPos = PlayerController.Instance.transform.position;

        // Check shooter's own segment.
        if (PointNearSegment(playerPos, shooterStart, shooterEnd, m_PlayerCheckRadius))
        {
            KillPlayer();
            return;
        }

        // Check all redirector segments accumulated this frame.
        foreach (var seg in LaserRedirector.ActiveSegments)
        {
            if (PointNearSegment(playerPos, seg.start, seg.end, m_PlayerCheckRadius))
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

    // Returns true if point p is within radius of the line segment (a, b).
    private static bool PointNearSegment(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a) <= radius;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        Vector2 closest = a + t * ab;
        return Vector2.Distance(p, closest) <= radius;
    }
}
