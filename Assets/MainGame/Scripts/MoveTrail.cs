using System.Collections;
using UnityEngine;
using Tiny;

/// <summary>
/// Shows a <see cref="Tiny.Trail"/> only while this object is actually moving.
///
/// The Trail component from the MiniGames Trail package renders continuously once
/// enabled, which would leave a permanent smear behind an object that spends most of
/// its life standing still (a <see cref="PushBrick"/>). This driver keeps the Trail
/// component disabled at rest and switches it on the moment the transform starts to
/// move — so a pushed brick streaks while it slides or falls, and nothing shows once
/// it settles.
///
/// Behaviour:
///   • Movement detected  → Trail enabled. Re-enabling makes Trail rebuild its mesh at
///                          the current position, so the streak always starts clean.
///   • Movement stopped   → Trail stays on for <see cref="m_Linger"/> seconds so the
///                          tail can catch up and collapse, then it is disabled.
///   • Teleport detected  → a jump larger than <see cref="m_TeleportDistance"/> in one
///                          frame is a reposition, not motion (PushBrick.ResetBrick
///                          snapping back to its start). The trail is cleared instead of
///                          drawing a streak across the whole level.
///
/// Setup:
///   • Sits next to a Trail component; leave that component ENABLED in the prefab so
///     Trail.Start() can build its mesh — this script disables it on the first frame.
///   • Trail.Points should span the object across the axis it moves perpendicular to.
///     For a 1x1 brick sliding horizontally that is (0, -0.45, 0) → (0, 0.45, 0).
///   • Trail builds its mesh into a plain MeshRenderer, which has no sorting values in
///     a 2D scene by default. Sorting Layer / Sorting Order below are pushed onto that
///     renderer so the streak draws behind the brick instead of at an arbitrary depth.
/// </summary>
[RequireComponent(typeof(Trail))]
[DisallowMultipleComponent]
public class MoveTrail : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Per-frame distance above which the object counts as moving. Small enough " +
             "to catch a slow slide, large enough to ignore float residue.")]
    [SerializeField] private float m_MoveThreshold = 0.0005f;

    [Tooltip("How long the trail keeps rendering after the object stops, in seconds. " +
             "Give it at least the Trail's own Duration so the tail collapses on screen " +
             "instead of vanishing mid-streak.")]
    [SerializeField] private float m_Linger = 0.25f;

    [Tooltip("A single-frame jump this large or larger is treated as a teleport " +
             "(reset / respawn) and clears the trail rather than drawing a streak.")]
    [SerializeField] private float m_TeleportDistance = 2f;

    [Header("2D Sorting")]
    [Tooltip("Sorting layer applied to the generated trail mesh renderer.")]
    [SerializeField] private string m_SortingLayer = "Default";

    [Tooltip("Sorting order applied to the generated trail mesh renderer. Keep it below " +
             "the object's own SpriteRenderer so the streak trails behind the art.")]
    [SerializeField] private int m_SortingOrder = -1;

    // ─── State ────────────────────────────────────────────────────────────────

    private Trail m_Trail;
    private Vector3 m_LastPosition;
    private float m_IdleTime;

    // Trail builds its mesh in Start(). Until that has run, toggling the component
    // would hit a null mesh in its LateUpdate, so all switching waits for this.
    private bool m_Ready;

    // The trail mesh object is created once, on the first enable; its sorting only
    // needs pushing once.
    private bool m_SortingApplied;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Trail = GetComponent<Trail>();
        m_LastPosition = transform.position;
    }

    private IEnumerator Start()
    {
        // One frame of grace: Trail.Start() runs before this resumes, so its mesh and
        // renderer exist. The trail is invisible during that frame — every vertex sits
        // on the object's own position, so the mesh has no area.
        yield return null;

        ApplySorting();

        m_Trail.enabled = false;
        m_Ready = true;
        m_LastPosition = transform.position;
    }

    private void OnEnable()
    {
        // Reactivating the object (PushBrick restoring a shattered brick) must not read
        // as a move from wherever it was destroyed.
        m_LastPosition = transform.position;
        m_IdleTime = 0f;
    }

    private void Update()
    {
        if (!m_Ready) return;

        Vector3 position = transform.position;
        float moved = Vector3.Distance(position, m_LastPosition);
        m_LastPosition = position;

        if (moved >= m_TeleportDistance)
        {
            // Repositioned, not travelled. Drop whatever is on screen and stay off.
            m_Trail.Clear();
            m_Trail.enabled = false;
            m_IdleTime = 0f;
            return;
        }

        if (moved > m_MoveThreshold)
        {
            m_IdleTime = 0f;
            if (!m_Trail.enabled)
            {
                m_Trail.enabled = true;
                ApplySorting();
            }
            return;
        }

        if (!m_Trail.enabled) return;

        m_IdleTime += Time.deltaTime;
        if (m_IdleTime >= m_Linger)
            m_Trail.enabled = false;
    }

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drops the current streak and hides the trail immediately. Call this before
    /// repositioning the object so the jump doesn't get drawn as a smear — the
    /// distance guard covers long jumps, this covers short ones too.
    /// </summary>
    public void Cancel()
    {
        m_LastPosition = transform.position;
        m_IdleTime = 0f;
        if (!m_Ready) return;

        m_Trail.Clear();
        m_Trail.enabled = false;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Puts the generated trail mesh on the project's 2D sorting layers. Without this a
    // MeshRenderer sorts by distance alone and can land in front of the brick or behind
    // the background.
    private void ApplySorting()
    {
        if (m_SortingApplied) return;

        MeshRenderer renderer = m_Trail.TrailRenderer;
        if (renderer == null) return;

        if (!string.IsNullOrEmpty(m_SortingLayer))
            renderer.sortingLayerName = m_SortingLayer;
        renderer.sortingOrder = m_SortingOrder;

        m_SortingApplied = true;
    }
}
