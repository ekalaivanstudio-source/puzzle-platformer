using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A brick that the player can push one unit at a time by walking into it.
/// Only pushing is possible — the player cannot pull.
///
/// When the player's horizontal movement hits this brick:
///   • The brick slides one push distance in the movement direction, stopping short
///     if something blocks it or if it reaches a cell with no ground under it — in
///     which case it falls into the gap rather than sliding across.
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

    [Tooltip("If true, the brick returns to its start position whenever the player respawns " +
             "at spawn (turn completed / soft reset), not only on a full restart. Enable this " +
             "for bricks that ride a moving platform so they don't stay displaced. Leave OFF to " +
             "keep the default behaviour where a pushed brick persists across turns.")]
    [SerializeField] private bool m_ResetOnRespawn = false;

    [Tooltip("Speed at which the brick slides after being pushed (units / sec).")]
    [SerializeField] private float m_PushSpeed = 10f;

    [Tooltip("Distance the brick travels per push. Usually matches the player's Move " +
             "Distance Per Command so brick and player stay in step; it may span several " +
             "grid units, and the brick still stops at the first unsupported unit.")]
    [SerializeField] private float m_PushDistance = 1f;

    [Tooltip("Layers that block the brick's movement (Ground, walls, other bricks, etc.).")]
    [SerializeField] private LayerMask m_BlockingLayers;

    [Header("Falling")]
    [Tooltip("Speed at which the brick drops when unsupported (units / sec).")]
    private float m_FallSpeed = 12f;

    [Tooltip("If the brick falls more than this many units without landing, it shatters.")]
    private int m_MaxFallUnits = 20;

    [Tooltip("Extra layers the brick lands on while falling, beyond Blocking Layers " +
             "(e.g. the Laser emitter layer). Ground is already covered by Blocking Layers.")]
    [SerializeField] private LayerMask m_LandingLayers;

    [Tooltip("Falling brick also lands on top of colliders with this tag (e.g. spikes), " +
             "whatever layer they sit on. Leave blank to disable tag-based landing.")]
    [SerializeField] private string m_LandingTag = "Spike";

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
    // Reused by the ground probe so IsSupportedForFall allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_BlockingFilter;

    // Fall landing uses NO layer mask (detects every collider in the sweep) and then
    // accepts hits via IsLandingSurface — this lets it catch spikes by tag even though
    // they live on the Default layer, without dragging all of Default into a mask.
    private ContactFilter2D m_LandingFilter;

    // Thickness of the probe used to detect ground directly under the brick.
    private const float k_GroundProbeThickness = 0.05f;

    // One cell of the level grid. Every level uses a 1-unit Grid, so pushes and falls
    // advance in 1-unit steps and the brick is support-checked at every cell it enters.
    // This is deliberately NOT m_PushDistance: a push may span several cells, and using
    // the push length as the step would skip over any hole narrower than it.
    private const float k_GridUnit = 1f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_StartPosition = transform.position;
        m_Collider = GetComponent<Collider2D>();
        m_BlockingFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        m_BlockingFilter.SetLayerMask(m_BlockingLayers);

        // No layer mask: catch everything, then filter with IsLandingSurface.
        m_LandingFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

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

    private void OnEnable()
    {
        GameManager.OnFullReset += ResetBrick;
        if (m_ResetOnRespawn) GameManager.OnPlayerRespawn += ResetBrick;
    }

    private void OnDisable()
    {
        GameManager.OnFullReset -= ResetBrick;
        if (m_ResetOnRespawn) GameManager.OnPlayerRespawn -= ResetBrick;
    }

    private void ResetBrick()
    {
        gameObject.SetActive(true);
        transform.position = m_StartPosition;
    }

    // ─── Push ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="PlayerController"/> when the player walks into this brick.
    /// Slides the brick <see cref="m_PushDistance"/> in <paramref name="signDir"/>
    /// (+1 right, -1 left), stopping early at whatever comes first: a blocker in the
    /// path, or the first grid cell with nothing underneath — where it drops instead.
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

        // Slide one grid unit at a time, checking support after each unit. A push can
        // span several cells, so testing only the final destination would let the brick
        // glide straight over a hole narrower than the push and land on the far side.
        // Stopping at the first unsupported cell makes it drop into the gap instead.
        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            float remaining = targetX - transform.position.x;
            float step = Mathf.Min(k_GridUnit, Mathf.Abs(remaining)) * Mathf.Sign(remaining);

            Vector3 stepTarget = new Vector3(
                transform.position.x + step, transform.position.y, transform.position.z);

            while (Mathf.Abs(transform.position.x - stepTarget.x) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, stepTarget, m_PushSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            transform.position = stepTarget;

            // Nothing underneath this cell — abandon the rest of the push and fall.
            if (!IsSupportedForFall()) break;
        }

        // The push may have left the brick hanging over a gap — drop it if so.
        yield return Fall();
    }

    // ─── Fall ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drops the brick one grid unit at a time while nothing supports it from below.
    /// If it falls more than <see cref="m_MaxFallUnits"/> units without landing
    /// (e.g. pushed into a bottomless pit), it shatters instead of falling forever.
    /// </summary>
    private IEnumerator Fall()
    {
        int unitsFallen = 0;

        while (!IsSupportedForFall())
        {
            if (unitsFallen >= m_MaxFallUnits)
            {
                Shatter();
                yield break;
            }

            // Sweep downward to find how far the brick can actually drop this step.
            // This catches a landing surface anywhere inside the step — not just at
            // the unit boundary — so the brick lands exactly on top of it instead of
            // jumping straight through when the surface sits partway into a step.
            float drop = GetFallDistance();
            if (drop <= 0.01f)
                break; // Something is directly below — treat as landed.

            Vector3 target = transform.position + Vector3.down * drop;

            while (Mathf.Abs(transform.position.y - target.y) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target, m_FallSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            transform.position = target;

            // A surface stopped the brick short of a full step → it has landed.
            if (drop < k_GridUnit - 0.01f)
                break;

            unitsFallen++;
        }
    }

    // ─── Laser ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="LaserShooter"/> when a laser beam terminates on this brick.
    /// Has no effect if <see cref="m_IsLaserDestructible"/> is false.
    /// </summary>
    public void OnLaserHit()
    {
        if (!m_IsLaserDestructible) return;
        Shatter();
    }

    /// <summary>
    /// Plays the destroy FX and deactivates the brick. Restored on the next
    /// full reset by <see cref="ResetBrick"/>. Shared by laser hits and falls.
    /// </summary>
    private void Shatter()
    {
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

    // How far (≤ k_GridUnit) the brick can drop before landing on a surface.
    // Unlike GetAllowedDistance this uses the landing rule, so the brick also lands
    // on spikes (by tag) and the laser emitter (by layer), not just Blocking Layers.
    private float GetFallDistance()
    {
        Bounds b = m_Collider.bounds;

        // Shrink the footprint slightly so colliders merely touching the brick's
        // sides aren't treated as landing surfaces.
        Vector2 size = b.size * 0.95f;

        int count = Physics2D.BoxCast(
            b.center, size, 0f, Vector2.down, m_LandingFilter, m_CastResults, k_GridUnit);

        float allowed = k_GridUnit;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = m_CastResults[i];
            if (!IsLandingSurface(hit.collider)) continue;
            allowed = Mathf.Min(allowed, hit.distance);
        }

        return allowed;
    }

    // True when a landing surface sits directly beneath the brick (ground, another
    // brick, a spike, or the laser emitter). A thin probe just below the bottom edge
    // is used so colliders the brick merely touches on its sides aren't mistaken for
    // support.
    private bool IsSupportedForFall()
    {
        Bounds b = m_Collider.bounds;

        // Shrink the width slightly so flush side walls don't read as support,
        // and place a thin probe centred just under the brick's bottom edge.
        Vector2 size = new Vector2(b.size.x * 0.9f, k_GroundProbeThickness);
        Vector2 center = new Vector2(b.center.x, b.min.y - k_GroundProbeThickness * 0.5f);

        int count = Physics2D.OverlapBox(center, size, 0f, m_LandingFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            if (IsLandingSurface(m_OverlapResults[i]))
                return true;
        }

        return false;
    }

    // A collider counts as something the falling brick lands on when it sits on the
    // Blocking Layers (ground/walls/bricks), on the extra Landing Layers (laser
    // emitter, etc.), OR carries the Landing Tag (spikes) regardless of its layer.
    // The brick's own collider never counts.
    private bool IsLandingSurface(Collider2D col)
    {
        if (col == null || col == m_Collider) return false;

        int layerBit = 1 << col.gameObject.layer;
        if ((m_BlockingLayers.value & layerBit) != 0) return true;
        if ((m_LandingLayers.value & layerBit) != 0) return true;
        if (!string.IsNullOrEmpty(m_LandingTag) && col.CompareTag(m_LandingTag)) return true;

        return false;
    }
}
