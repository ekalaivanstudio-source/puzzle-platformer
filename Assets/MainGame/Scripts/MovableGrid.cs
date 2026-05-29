using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A grid object that can be snapped one unit at a time by <see cref="MovableGridManager"/>.
/// Does not handle input directly — the manager selects which grid is active and
/// routes arrow-key commands here via <see cref="TryMove"/>.
///
/// Boundaries are an explicit list of allowed grid positions (integer X/Y offsets
/// from the start position), supporting any irregular shape.
/// A Gizmo cell is drawn for each allowed position in the editor.
///
/// Reset hook: GameManager.OnTurnReset.
/// </summary>
public class MovableGrid : MonoBehaviour
{
    [Tooltip("How fast the grid snaps to the target position (units per second).")]
    [SerializeField] private float m_SnapSpeed = 12f;

    [Header("Movement Bounds")]
    [Tooltip("All grid positions the grid is allowed to occupy, as integer (X, Y) offsets "
           + "from the start position. (0,0) — the start — is always valid. "
           + "Add one entry per reachable cell to define any irregular shape.")]
    [SerializeField] private Vector2Int[] m_AllowedOffsets = new Vector2Int[0];

    [Header("Boundary Markers")]
    [Tooltip("Sprite placed at each allowed position at runtime to show boundaries in-game.")]
    [SerializeField] private Sprite m_MarkerSprite;
    [Tooltip("Tint and opacity of the marker sprites.")]
    [SerializeField] private Color m_MarkerColor = new Color(0.4f, 1f, 0.4f, 0.5f);
    [Tooltip("Sorting layer the marker sprites render on.")]
    [SerializeField] private string m_MarkerSortingLayer = "Default";
    [Tooltip("Order in layer for the marker sprites.")]
    [SerializeField] private int m_MarkerOrderInLayer = 0;

    /// <summary>True while a snap-move coroutine is running. Manager checks this before sending a new move.</summary>
    public bool IsMoving => m_IsMoving;

    private HashSet<Vector2Int> m_AllowedSet;
    private GameObject[] m_MarkerObjects;
    private bool m_IsMoving;
    private Vector3 m_StartPosition;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        m_StartPosition = transform.position;
        // (0,0) is always valid so the grid can stay at its start position.
        m_AllowedSet = new HashSet<Vector2Int>(m_AllowedOffsets) { Vector2Int.zero };
    }

    private void Start()
    {
        SpawnMarkers();
    }

    private void OnDestroy()
    {
        if (m_MarkerObjects == null) return;
        foreach (GameObject obj in m_MarkerObjects)
            if (obj != null) Destroy(obj);
    }

    private void OnEnable()  => GameManager.OnTurnReset += HandleTurnReset;
    private void OnDisable() => GameManager.OnTurnReset -= HandleTurnReset;

    // ─── Marker Spawning ──────────────────────────────────────────────────────

    private void SpawnMarkers()
    {
        if (m_MarkerSprite == null || m_AllowedOffsets == null || m_AllowedOffsets.Length == 0)
            return;

        m_MarkerObjects = new GameObject[m_AllowedOffsets.Length];

        for (int i = 0; i < m_AllowedOffsets.Length; i++)
        {
            Vector2Int offset = m_AllowedOffsets[i];
            Vector3 worldPos = m_StartPosition + new Vector3(offset.x, offset.y, 0f);

            GameObject marker = new GameObject($"BoundaryMarker_{offset.x}_{offset.y}");
            marker.transform.position = worldPos;

            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = m_MarkerSprite;
            sr.color = m_MarkerColor;
            sr.sortingLayerName = m_MarkerSortingLayer;
            sr.sortingOrder = m_MarkerOrderInLayer;

            marker.SetActive(false); // hidden until execution starts

            m_MarkerObjects[i] = marker;
        }
    }

    // ─── GameManager event handlers ──────────────────────────────────────────

    private void HandleTurnReset()
    {
        StopAllCoroutines();
        transform.position = m_StartPosition;
        m_IsMoving = false;
        SetMarkersVisible(false);
    }

    // ─── Public API (called by MovableGridManager) ───────────────────────────

    /// <summary>Makes this grid the active selection: shows boundary markers.</summary>
    public void Activate() => SetMarkersVisible(true);

    /// <summary>Removes this grid from active selection: hides markers, cancels any in-progress move.</summary>
    public void Deactivate()
    {
        StopAllCoroutines();
        m_IsMoving = false;
        SetMarkersVisible(false);
    }

    /// <summary>
    /// Attempts to move the grid one unit in <paramref name="direction"/>.
    /// Returns <c>true</c> if the move was accepted (within bounds and not already moving).
    /// </summary>
    public bool TryMove(Vector2 direction)
    {
        if (m_IsMoving) return false;
        Vector3 target = transform.position + (Vector3)direction;
        if (!IsWithinBounds(target)) return false;
        StartCoroutine(SnapMove(direction));
        return true;
    }

    private bool IsWithinBounds(Vector3 targetPosition)
    {
        int relX = Mathf.RoundToInt(targetPosition.x - m_StartPosition.x);
        int relY = Mathf.RoundToInt(targetPosition.y - m_StartPosition.y);
        return m_AllowedSet.Contains(new Vector2Int(relX, relY));
    }

    // ─── Movement ────────────────────────────────────────────────────────────

    private IEnumerator SnapMove(Vector2 direction)
    {
        m_IsMoving = true;

        Vector3 origin = transform.position;
        Vector3 target = origin + (Vector3)direction; // exactly 1 unit

        while (Vector2.Distance(transform.position, target) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, m_SnapSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
        m_IsMoving = false;
    }

    // ─── Editor Gizmos ────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 origin = Application.isPlaying ? m_StartPosition : transform.position;
        Vector3 cellSize = new Vector3(0.92f, 0.92f, 0.05f); // slightly inset so adjacent cells have a visible gap

        // Draw the start cell in yellow, all other allowed cells in green.
        Gizmos.color = new Color(1f, 1f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(origin, cellSize);

        if (m_AllowedOffsets == null) return;
        foreach (Vector2Int offset in m_AllowedOffsets)
        {
            if (offset == Vector2Int.zero) continue; // already drawn above
            Vector3 cellCenter = origin + new Vector3(offset.x, offset.y, 0f);
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.18f);
            Gizmos.DrawCube(cellCenter, cellSize);
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(cellCenter, cellSize);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetMarkersVisible(bool visible)
    {
        if (m_MarkerObjects == null) return;
        foreach (GameObject obj in m_MarkerObjects)
            if (obj != null) obj.SetActive(visible);
    }
}
