using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Receives a laser beam and fires one or two outgoing segments using child LineRenderers.
///
/// Cross          - 1 output: turns the incoming beam 90 degrees CCW in local space.
/// StraightThrough- 2 outputs: always fires along local +Y and local -Y (up and down).
///                  Rotate 90 degrees to fire left and right instead.
/// LShape         - 2 outputs: always fires along local +X and local +Y (right and up).
///                  Rotate for other corner orientations.
///
/// Setup:
///   Cross          : assign one child LineRenderer to Line Renderer 1.
///   StraightThrough: assign two child LineRenderers (one per output direction).
///   LShape         : assign two child LineRenderers (one per output direction).
/// </summary>
public class LaserRedirector : MonoBehaviour
{
    public enum RedirectorType
    {
        Cross,
        StraightThrough,
        LShape,
    }

    [SerializeField] private RedirectorType m_Type = RedirectorType.Cross;

    [Header("Beam Settings")]
    [Tooltip("Layers that terminate this redirector beam segment.")]
    [SerializeField] private LayerMask m_BlockingLayers;

    [Tooltip("Layer mask that identifies other LaserRedirector colliders.")]
    [SerializeField] private LayerMask m_RedirectorLayer;

    [Tooltip("Maximum distance this redirector beam segment can travel.")]
    [SerializeField] private float m_MaxDistance = 50f;

    [Tooltip("Maximum number of additional redirections allowed from this point.")]
    [SerializeField] private int m_MaxBounces = 8;

    [Tooltip("Local-space offset where the first outgoing beam (Line Renderer 1) starts.")]
    [SerializeField] private Vector2 m_BeamOffset1 = Vector2.zero;

    [Tooltip("Local-space offset where the second outgoing beam (Line Renderer 2) starts.")]
    [SerializeField] private Vector2 m_BeamOffset2 = Vector2.zero;

    [Header("Line Renderers (child GameObjects)")]
    [Tooltip("First output segment. Used by all types.")]
    [SerializeField] private LineRenderer m_LineRenderer1;

    [Tooltip("Second output segment. Used by StraightThrough and LShape only.")]
    [SerializeField] private LineRenderer m_LineRenderer2;

    // -------------------------------------------------------------------------

    private static readonly List<LaserRedirector> s_All = new List<LaserRedirector>();

    // All active beam segments this frame: (start, end) pairs. Populated by FireSegment,
    // cleared by ClearAll. LaserShooter reads this to check player contact.
    public static readonly List<(Vector2 start, Vector2 end)> ActiveSegments
        = new List<(Vector2, Vector2)>();

    private bool m_Active;

    private void Awake()
    {
        InitRenderer(m_LineRenderer1);
        InitRenderer(m_LineRenderer2);
    }

    private void OnEnable() => s_All.Add(this);
    private void OnDisable()
    {
        s_All.Remove(this);
        HideRenderer(m_LineRenderer1);
        HideRenderer(m_LineRenderer2);
    }

    /// <summary>Called by LaserShooter before each cast to hide all segments.</summary>
    public static void ClearAll()
    {
        ActiveSegments.Clear();
        foreach (var r in s_All)
        {
            r.m_Active = false;
            HideRenderer(r.m_LineRenderer1);
            HideRenderer(r.m_LineRenderer2);
        }
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when the incoming beam hits this redirector.
    /// Fires one or two outgoing segments depending on type.
    /// </summary>
    public void ActivateBeam(Vector2 inDir, float remaining, int bouncesLeft,
                             LayerMask blockingLayers, LayerMask redirectorLayer)
    {
        float dist = Mathf.Min(remaining, m_MaxDistance);
        int bounces = Mathf.Min(bouncesLeft, m_MaxBounces);
        LayerMask blocks = m_BlockingLayers != 0 ? m_BlockingLayers : blockingLayers;
        LayerMask redir = m_RedirectorLayer != 0 ? m_RedirectorLayer : redirectorLayer;

        if (m_Active || bounces <= 0) return;
        m_Active = true;

        Vector2 beamStart1 = (Vector2)transform.position + (Vector2)transform.TransformDirection(m_BeamOffset1);
        Vector2 beamStart2 = (Vector2)transform.position + (Vector2)transform.TransformDirection(m_BeamOffset2);

        switch (m_Type)
        {
            case RedirectorType.Cross:
                FireSegment(RedirectCross(inDir.normalized), beamStart1, dist, bounces, blocks, redir, m_LineRenderer1);
                break;

            case RedirectorType.StraightThrough:
                FireSegment((Vector2)transform.TransformDirection(Vector2.up), beamStart1, dist, bounces, blocks, redir, m_LineRenderer1);
                FireSegment((Vector2)transform.TransformDirection(Vector2.down), beamStart2, dist, bounces, blocks, redir, m_LineRenderer2);
                break;

            case RedirectorType.LShape:
                FireSegment((Vector2)transform.TransformDirection(Vector2.right), beamStart1, dist, bounces, blocks, redir, m_LineRenderer1);
                FireSegment((Vector2)transform.TransformDirection(Vector2.up), beamStart2, dist, bounces, blocks, redir, m_LineRenderer2);
                break;
        }
    }

    // -------------------------------------------------------------------------

    private void FireSegment(Vector2 outDir, Vector2 beamStart, float dist, int bounces,
                             LayerMask blocks, LayerMask redir, LineRenderer lr)
    {
        if (lr == null || outDir == Vector2.zero) return;

        Vector2 origin = beamStart + outDir * 0.05f;
        int combinedMask = blocks | redir;

        // Use RaycastAll and skip this redirector's own colliders to prevent the
        // outgoing segment from immediately re-hitting the object it originates from.
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, outDir, dist, combinedMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit2D hit = default;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<LaserRedirector>() == this) continue;
            hit = h;
            break;
        }

        Vector2 endPoint;

        if (hit.collider != null)
        {
            endPoint = hit.point;
            LaserRedirector next = hit.collider.GetComponentInParent<LaserRedirector>();
            if (next != null && next != this)
            {
                float nextRemaining = dist - hit.distance;
                next.ActivateBeam(outDir, nextRemaining, bounces - 1, blocks, redir);
            }
        }
        else
        {
            endPoint = origin + outDir * dist;
        }

        lr.enabled = true;
        lr.SetPosition(0, beamStart);
        lr.SetPosition(1, endPoint);
        ActiveSegments.Add((beamStart, endPoint));
    }

    // -------------------------------------------------------------------------

    private Vector2 RedirectCross(Vector2 inDir)
    {
        Vector2 localIn = transform.InverseTransformDirection(inDir);
        Vector2 snapped = SnapToCardinal(localIn);
        Vector2 localOut = new Vector2(-snapped.y, snapped.x); // 90 CCW
        return ((Vector2)transform.TransformDirection(localOut)).normalized;
    }

    private static Vector2 SnapToCardinal(Vector2 v)
    {
        return Mathf.Abs(v.x) >= Mathf.Abs(v.y)
            ? new Vector2(Mathf.Sign(v.x), 0f)
            : new Vector2(0f, Mathf.Sign(v.y));
    }

    private static void InitRenderer(LineRenderer lr)
    {
        if (lr == null) return;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.enabled = false;
    }

    private static void HideRenderer(LineRenderer lr)
    {
        if (lr != null) lr.enabled = false;
    }
}
