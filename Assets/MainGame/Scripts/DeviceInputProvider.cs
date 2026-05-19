using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input provider for keyboard and gamepad using Unity's New Input System.
/// Both devices are handled by the same provider — the InputActionAsset defines
/// which physical keys/buttons map to which actions. No code changes are needed
/// to add new device support; just add bindings in the Input Actions editor.
///
/// Actions mapped:
///   Left/Right/Jump/Interact → adds to SequenceManager queue
///   Submit (Enter / Gamepad Start) → triggers execution via GameManager
///   Undo (Backspace / Gamepad B)   → removes last queued action
///   Clear (Delete / Gamepad Select) → clears entire queue
/// </summary>
public class DeviceInputProvider : MonoBehaviour, IInputProvider
{
    [Tooltip("Assign the PlayerInputActions asset (Assets/PlayerInputActions.inputactions).")]
    [SerializeField] private InputActionAsset m_InputActionAsset;

    [SerializeField] private SequenceManager m_SequenceManager;

    private InputAction m_LeftAction;
    private InputAction m_RightAction;
    private InputAction m_JumpAction;
    private InputAction m_InteractAction;
    private InputAction m_SubmitAction;
    private InputAction m_UndoAction;
    private InputAction m_ClearAction;

    // ─── IInputProvider ──────────────────────────────────────────────────────

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Enables or disables the entire InputActionAsset.
    /// Called by InputModeManager on mode switch and by GameManager during execution.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (enabled) m_InputActionAsset?.Enable();
        else m_InputActionAsset?.Disable();
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (m_InputActionAsset == null) { Debug.LogError("[DeviceInputProvider] InputActionAsset not assigned.", this); return; }
        if (m_SequenceManager == null) { Debug.LogError("[DeviceInputProvider] SequenceManager not assigned.", this); return; }

        InputActionMap map = m_InputActionAsset.FindActionMap("Player", throwIfNotFound: false);
        if (map == null) { Debug.LogError("[DeviceInputProvider] 'Player' action map not found in asset.", this); return; }

        m_LeftAction = map.FindAction("Left", throwIfNotFound: false);
        m_RightAction = map.FindAction("Right", throwIfNotFound: false);
        m_JumpAction = map.FindAction("Jump", throwIfNotFound: false);
        m_InteractAction = map.FindAction("Interact", throwIfNotFound: false);
        m_SubmitAction = map.FindAction("Submit", throwIfNotFound: false);
        m_UndoAction = map.FindAction("Undo", throwIfNotFound: false);
        m_ClearAction = map.FindAction("Clear", throwIfNotFound: false);
    }

    private void OnEnable() => RegisterListeners(true);
    private void OnDisable() => RegisterListeners(false);

    private void RegisterListeners(bool register)
    {
        if (m_InputActionAsset == null) return;

        if (register)
        {
            if (m_LeftAction != null) m_LeftAction.performed += OnLeft;
            if (m_RightAction != null) m_RightAction.performed += OnRight;
            if (m_JumpAction != null) m_JumpAction.performed += OnJump;
            if (m_InteractAction != null) m_InteractAction.performed += OnInteract;
            if (m_SubmitAction != null) m_SubmitAction.performed += OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed += OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed += OnClear;
        }
        else
        {
            if (m_LeftAction != null) m_LeftAction.performed -= OnLeft;
            if (m_RightAction != null) m_RightAction.performed -= OnRight;
            if (m_JumpAction != null) m_JumpAction.performed -= OnJump;
            if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
            if (m_SubmitAction != null) m_SubmitAction.performed -= OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed -= OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed -= OnClear;
        }
    }

    // ─── Callbacks ───────────────────────────────────────────────────────────

    private void OnLeft(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.AddAction(ActionTypeEnum.Left); }
    private void OnRight(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.AddAction(ActionTypeEnum.Right); }
    private void OnJump(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.AddAction(ActionTypeEnum.Jump); }
    private void OnInteract(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.AddAction(ActionTypeEnum.Interact); }
    private void OnUndo(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.RemoveLastAction(); }
    private void OnClear(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.ClearSequence(); }

    // Submit triggers execution through GameManager (same path as the UI Play button)
    private void OnSubmit(InputAction.CallbackContext c) { if (IsEnabled) GameManager.Instance?.OnPlayClicked(); }
}
