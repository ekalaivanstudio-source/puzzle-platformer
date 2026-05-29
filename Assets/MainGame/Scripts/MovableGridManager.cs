using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages all <see cref="MovableGrid"/> instances in a level.
///
/// Responsibilities:
///   • Re-enables arrow-key and Tab actions (disabled by DeviceInputProvider during execution).
///   • Selects the first grid automatically when execution begins.
///   • Routes arrow-key presses to the currently selected grid via <see cref="MovableGrid.TryMove"/>.
///   • Cycles selection to the next grid when the player presses Tab (GridSelection action).
///   • Deactivates all grids and hides markers when the turn resets.
///
/// Setup: attach to any scene GameObject, assign the same InputActionAsset used by
/// DeviceInputProvider, then add all MovableGrid instances in the level to the Grids list.
/// </summary>
public class MovableGridManager : MonoBehaviour
{
    [Tooltip("The same InputActionAsset used by DeviceInputProvider.")]
    [SerializeField] private InputActionAsset m_InputActionAsset;

    [Tooltip("All MovableGrid objects in this level. The first one is selected when execution starts.")]
    [SerializeField] private MovableGrid[] m_Grids;

    private InputAction m_UpAction;
    private InputAction m_DownAction;
    private InputAction m_LeftAction;
    private InputAction m_RightAction;
    private InputAction m_GridSelectionAction;

    private int m_SelectedIndex = -1; // -1 = nothing selected
    private bool m_IsExecuting;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (m_InputActionAsset == null)
        {
            Debug.LogError("[MovableGridManager] InputActionAsset is not assigned.", this);
            return;
        }

        InputActionMap map = m_InputActionAsset.FindActionMap("Player", throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError("[MovableGridManager] 'Player' action map not found in InputActionAsset.", this);
            return;
        }

        m_UpAction = map.FindAction("Jump", throwIfNotFound: false);
        m_DownAction = map.FindAction("Down", throwIfNotFound: false);
        m_LeftAction = map.FindAction("Left", throwIfNotFound: false);
        m_RightAction = map.FindAction("Right", throwIfNotFound: false);
        m_GridSelectionAction = map.FindAction("GridSelection", throwIfNotFound: false);

        if (m_GridSelectionAction == null)
            Debug.LogWarning("[MovableGridManager] 'GridSelection' action not found — Tab cycling will not work.", this);
    }

    private void OnEnable()
    {
        GameManager.OnExecutionStarted += HandleExecutionStarted;
        GameManager.OnTurnReset += HandleTurnReset;
    }

    private void OnDisable()
    {
        GameManager.OnExecutionStarted -= HandleExecutionStarted;
        GameManager.OnTurnReset -= HandleTurnReset;
    }

    // ─── GameManager event handlers ──────────────────────────────────────────

    private void HandleExecutionStarted()
    {
        if (m_Grids == null || m_Grids.Length == 0) return;

        m_IsExecuting = true;

        // DeviceInputProvider disabled the whole asset — re-enable just the actions
        // this manager needs so player sequence-building shortcuts stay blocked.
        m_UpAction?.Enable();
        m_DownAction?.Enable();
        m_LeftAction?.Enable();
        m_RightAction?.Enable();
        m_GridSelectionAction?.Enable();

        // No grid is selected yet — the first Tab press will select Grid[0].
    }

    private void HandleTurnReset()
    {
        m_IsExecuting = false;

        DeactivateAll();
        m_SelectedIndex = -1;
    }

    // ─── Update ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!m_IsExecuting || m_Grids == null) return;

        // Tab — first press selects Grid[0]; subsequent presses cycle to the next grid.
        if (m_GridSelectionAction != null && m_GridSelectionAction.WasPressedThisFrame())
        {
            if (m_SelectedIndex < 0)
                SelectGrid(0);
            else if (m_Grids.Length > 1)
                SelectGrid((m_SelectedIndex + 1) % m_Grids.Length);
            return; // consume this frame's input
        }

        if (m_SelectedIndex < 0) return; // no grid selected yet

        MovableGrid active = m_Grids[m_SelectedIndex];
        if (active == null || active.IsMoving) return;

        // Arrow keys — send a move command to the selected grid.
        Vector2 direction = Vector2.zero;

        if (m_UpAction != null && m_UpAction.WasPressedThisFrame()) direction = Vector2.up;
        else if (m_DownAction != null && m_DownAction.WasPressedThisFrame()) direction = Vector2.down;
        else if (m_LeftAction != null && m_LeftAction.WasPressedThisFrame()) direction = Vector2.left;
        else if (m_RightAction != null && m_RightAction.WasPressedThisFrame()) direction = Vector2.right;

        if (direction != Vector2.zero)
            active.TryMove(direction);
    }

    // ─── Selection ───────────────────────────────────────────────────────────

    private void SelectGrid(int index)
    {
        // Deactivate the previously selected grid.
        if (m_SelectedIndex >= 0 && m_SelectedIndex < m_Grids.Length)
            m_Grids[m_SelectedIndex]?.Deactivate();

        m_SelectedIndex = index;

        // Activate the newly selected grid.
        if (m_SelectedIndex >= 0 && m_SelectedIndex < m_Grids.Length)
            m_Grids[m_SelectedIndex]?.Activate();
    }

    private void DeactivateAll()
    {
        if (m_Grids == null) return;
        foreach (MovableGrid grid in m_Grids)
            grid?.Deactivate();
    }
}
