using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lever-controlled patrol platform that travels one movement unit (tile) at a
/// time between two endpoints, carrying anything resting on top of it (the player,
/// the lever, pushable bricks, etc.).
///
/// Toggled by a <see cref="PlatformLever"/> — this component only exposes the
/// state and does the moving/carrying; the lever decides when to flip it.
///
/// Motion rules (match the design spec):
///   • Toggle ON  → the platform begins or resumes moving from its current tile.
///   • Toggle OFF → if moving, it completes the move to the NEXT tile and then
///                  stops. It never stops immediately or between tiles.
///   • Toggle ON again → resumes from the current tile in the same direction it
///                  was travelling before it stopped.
///   • While ON, it advances one tile at a time until it reaches an endpoint or
///     is toggled OFF.
///   • Reaching an endpoint while ON automatically reverses the direction and it
///     keeps travelling toward the opposite endpoint.
///
/// Setup:
///   • Put this on the platform GameObject. It needs a non-trigger Collider2D so
///     the player can stand on it (place it on a layer the player treats as ground).
///   • Set Tiles Forward / Tiles Backward to define the patrol path relative to the
///     platform's start position (measured in Tile Size units).
///   • Set Rider Layers to the layers that should be carried (Player, bricks, the
///     lever, …). Riders parented under the platform in the hierarchy are moved by
///     Unity automatically and are skipped here to avoid double-moving them.
///
/// Restores its start position and OFF state on <see cref="GameManager.OnFullReset"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LeverMovingPlatform : MonoBehaviour
{
    /// <summary>Axis along which the platform travels.</summary>
    public enum MoveAxis { Horizontal, Vertical }

    /// <summary>Which endpoint the platform heads toward first.</summary>
    public enum StartDirection { Forward, Backward }

    [Header("Path")]
    [Tooltip("Axis the platform patrols along.")]
    [SerializeField] private MoveAxis m_Axis = MoveAxis.Horizontal;

    [Tooltip("Size of one movement unit (tile) in world units. Should match the level grid.")]
    [SerializeField] private float m_TileSize = 1f;

    [Tooltip("How many tiles the platform can travel in the positive axis direction from its start position.")]
    [SerializeField] private int m_TilesForward = 4;

    [Tooltip("How many tiles the platform can travel in the negative axis direction from its start position.")]
    [SerializeField] private int m_TilesBackward = 0;

    [Tooltip("Which endpoint the platform heads toward first when switched ON.")]
    [SerializeField] private StartDirection m_StartDirection = StartDirection.Forward;

    [Header("Movement")]
    [Tooltip("Travel speed while moving (units per second).")]
    [SerializeField] private float m_Speed = 3f;

    [Header("Riders")]
    [Tooltip("Tag of the player, which is always carried when standing on the platform.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Tooltip("Thickness of the detection strip straddling the platform's top surface. " +
             "Increase if a rider isn't picked up.")]
    [SerializeField] private float m_RiderProbeHeight = 0.4f;

    // ─── State ──────────────────────────────────────────────────────────────────

    private Collider2D m_Collider;
    private Vector3 m_StartPosition;
    private Vector3 m_EndpointForward;   // start + axis * TilesForward
    private Vector3 m_EndpointBackward;  // start - axis * TilesBackward

    private bool m_IsOn;
    private int m_Direction = 1;         // +1 → forward endpoint, -1 → backward endpoint
    private bool m_HasTarget;            // true while mid-transit toward m_TargetTile
    private Vector3 m_TargetTile;

    // Reused so the per-step rider overlap allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_RiderFilter;

    // Riders already moved this step (a body can own several colliders in the probe).
    private readonly HashSet<Rigidbody2D> m_MovedBodies = new HashSet<Rigidbody2D>();
    private readonly HashSet<Transform> m_MovedTransforms = new HashSet<Transform>();

    private const float k_Arrival = 0.001f;

    /// <summary>Whether the platform is currently switched on (moving / will move).</summary>
    public bool IsOn => m_IsOn;

    // ─── Lifecycle ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        m_StartPosition = transform.position;

        // No layer mask: detect every collider on top, then decide per-hit in
        // CarryRiders (player by tag, everything else by Rider Layers). This keeps
        // the player carried even if Rider Layers is left misconfigured.
        m_RiderFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        RecalculateEndpoints();
        m_Direction = m_StartDirection == StartDirection.Forward ? 1 : -1;
    }

    private void OnValidate()
    {
        if (m_TileSize <= 0f) m_TileSize = 1f;
        if (m_Speed <= 0f) m_Speed = 3f;
        if (m_TilesForward < 0) m_TilesForward = 0;
        if (m_TilesBackward < 0) m_TilesBackward = 0;
        if (m_RiderProbeHeight <= 0f) m_RiderProbeHeight = 0.2f;
    }

    private void OnEnable() => GameManager.OnFullReset += OnFullReset;
    private void OnDisable() => GameManager.OnFullReset -= OnFullReset;

    private void OnFullReset()
    {
        transform.position = m_StartPosition;
        m_IsOn = false;
        m_HasTarget = false;
        m_Direction = m_StartDirection == StartDirection.Forward ? 1 : -1;
    }

    // ─── Public API (called by the lever) ─────────────────────────────────────────

    /// <summary>Flips the platform between ON and OFF.</summary>
    public void Toggle() => SetOn(!m_IsOn);

    /// <summary>
    /// Switches the platform ON or OFF. ON resumes from the current tile in the same
    /// direction; OFF lets the current tile finish before the platform rests.
    /// </summary>
    public void SetOn(bool on) => m_IsOn = on;

    // ─── Movement ──────────────────────────────────────────────────────────────────

    // Physics-stepped so carrying stays in sync with the player's own FixedUpdate movement.
    private void FixedUpdate()
    {
        Vector3 previous = transform.position;
        StepMovement();

        Vector3 delta = transform.position - previous;
        if (delta.sqrMagnitude > k_Arrival * k_Arrival)
            CarryRiders(delta);
    }

    private void StepMovement()
    {
        // Mid-transit: always finish the current tile, even if switched OFF.
        if (m_HasTarget)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, m_TargetTile, m_Speed * Time.fixedDeltaTime);

            if ((transform.position - m_TargetTile).sqrMagnitude <= k_Arrival * k_Arrival)
            {
                transform.position = m_TargetTile;
                m_HasTarget = false;

                // Reached an endpoint → turn the direction inward so the next tile
                // (this step if still ON, or a later ON) heads back the other way.
                if (IsAt(m_EndpointForward)) m_Direction = -1;
                else if (IsAt(m_EndpointBackward)) m_Direction = 1;
            }
            return;
        }

        // Resting on a tile boundary: only advance while switched ON.
        if (!m_IsOn) return;

        Vector3 axis = AxisVector();
        Vector3 next = ClampToPath(transform.position + axis * (m_Direction * m_TileSize));
        if ((next - transform.position).sqrMagnitude > k_Arrival * k_Arrival)
        {
            m_TargetTile = next;
            m_HasTarget = true;
        }
    }

    // ─── Carrying riders ─────────────────────────────────────────────────────────

    // Moves everything resting on top of the platform by the same delta the platform
    // moved this step. Dynamic/kinematic bodies are moved via MovePosition so physics
    // contacts stay valid; plain transforms are shifted directly.
    private void CarryRiders(Vector3 delta)
    {
        if (m_Collider == null) return;

        Bounds b = m_Collider.bounds;
        // A strip straddling the platform's top edge, so a rider that sits flush,
        // slightly sunk in, or hovering a hair above the surface is still caught.
        Vector2 probeCenter = new Vector2(b.center.x, b.max.y);
        Vector2 probeSize = new Vector2(b.size.x * 0.98f, m_RiderProbeHeight);

        int count = Physics2D.OverlapBox(probeCenter, probeSize, 0f, m_RiderFilter, m_OverlapResults);
        if (count == 0) return;

        m_MovedBodies.Clear();
        m_MovedTransforms.Clear();

        Vector2 delta2 = delta;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = m_OverlapResults[i];
            if (col == null || col == m_Collider) continue;

            // Skip our own hierarchy — children move with the parent automatically.
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            Rigidbody2D rb = col.attachedRigidbody;

            // Carry the player (by tag) and movable physics bodies (bricks, crates).
            // Static colliders — ground, walls, platforms — have no Rigidbody2D and
            // are NEVER moved, so the level geometry stays put.
            bool isPlayer = !string.IsNullOrEmpty(m_PlayerTag) && col.CompareTag(m_PlayerTag);
            bool isMovableBody = rb != null && rb.bodyType != RigidbodyType2D.Static;
            if (!isPlayer && !isMovableBody) continue;

            if (rb != null)
            {
                if (m_MovedBodies.Add(rb))
                    rb.MovePosition(rb.position + delta2);
            }
            else // player with no Rigidbody2D (unexpected) — shift the transform directly
            {
                Transform root = col.transform;
                if (m_MovedTransforms.Add(root))
                    root.position += delta;
            }
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────────

    private Vector3 AxisVector() => m_Axis == MoveAxis.Horizontal ? Vector3.right : Vector3.up;

    private void RecalculateEndpoints()
    {
        Vector3 axis = AxisVector();
        m_EndpointForward = m_StartPosition + axis * (m_TilesForward * m_TileSize);
        m_EndpointBackward = m_StartPosition - axis * (m_TilesBackward * m_TileSize);
    }

    // Clamps a candidate position onto the patrol path along the active axis.
    private Vector3 ClampToPath(Vector3 pos)
    {
        if (m_Axis == MoveAxis.Horizontal)
        {
            float min = Mathf.Min(m_EndpointBackward.x, m_EndpointForward.x);
            float max = Mathf.Max(m_EndpointBackward.x, m_EndpointForward.x);
            pos.x = Mathf.Clamp(pos.x, min, max);
            pos.y = m_StartPosition.y;
        }
        else
        {
            float min = Mathf.Min(m_EndpointBackward.y, m_EndpointForward.y);
            float max = Mathf.Max(m_EndpointBackward.y, m_EndpointForward.y);
            pos.y = Mathf.Clamp(pos.y, min, max);
            pos.x = m_StartPosition.x;
        }
        pos.z = m_StartPosition.z;
        return pos;
    }

    private bool IsAt(Vector3 endpoint) =>
        (transform.position - endpoint).sqrMagnitude <= k_Arrival * k_Arrival;

    // ─── Gizmos ─────────────────────────────────────────────────────────────────────

    // Visualise the patrol path and its tile stops in the Scene view.
    private void OnDrawGizmos()
    {
        Vector3 origin = Application.isPlaying ? m_StartPosition : transform.position;
        Vector3 axis = (m_Axis == MoveAxis.Horizontal) ? Vector3.right : Vector3.up;
        float tile = Mathf.Max(0.0001f, m_TileSize);

        Vector3 fwd = origin + axis * (m_TilesForward * tile);
        Vector3 back = origin - axis * (m_TilesBackward * tile);

        // Path line.
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(back, fwd);

        // Endpoints.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(fwd, tile * 0.18f);
        Gizmos.DrawWireSphere(back, tile * 0.18f);

        // Tile stops.
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
        for (int i = -m_TilesBackward; i <= m_TilesForward; i++)
            Gizmos.DrawWireCube(origin + axis * (i * tile), Vector3.one * tile * 0.12f);
    }
}
