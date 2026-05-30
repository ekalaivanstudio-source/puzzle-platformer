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
///   Undo/Back (Backspace / Gamepad B) → removes last queued action
///   Clear (Delete / Gamepad Select) → clears entire queue
///   Restart (R) → reloads the current level
/// </summary>
public class DeviceInputProvider : MonoBehaviour, IInputProvider
{
    [Tooltip("Assign the PlayerInputActions asset (Assets/PlayerInputActions.inputactions).")]
    [SerializeField] private InputActionAsset m_InputActionAsset;

    [SerializeField] private SequenceManager m_SequenceManager;

    /// <summary>Fired when Dash (D) is pressed — PlayerController uses this to interrupt current execution.</summary>
    public event System.Action OnDashPressed;
    /// <summary>Fired when GroundPound (S) is pressed — PlayerController uses this to interrupt current execution.</summary>
    public event System.Action OnGroundPoundPressed;

    private InputAction m_LeftAction;
    private InputAction m_RightAction;
    private InputAction m_JumpAction;
    private InputAction m_InteractAction;
    private InputAction m_SubmitAction;
    private InputAction m_UndoAction;
    private InputAction m_ClearAction;
    private InputAction m_RestartAction;
    private InputAction m_DashAction;
    private InputAction m_GroundPoundAction;
    private bool m_IsJumpHeld;
    private bool m_JumpComboQueued;

    // ─── IInputProvider ──────────────────────────────────────────────────────

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Enables or disables gameplay inputs.
    /// Restart remains enabled so R can reload even during execution.
    /// Called by InputModeManager on mode switch and by GameManager during execution.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
        {
            m_IsJumpHeld = false;
            m_JumpComboQueued = false;
        }

        // Only enable the asset explicitly — never disable it.
        // Every queuing callback already guards with `if (!IsEnabled) return`,
        // so nothing gets queued during execution. Dash and GroundPound intentionally
        // skip that guard so they can interrupt at any time.
        if (enabled)
            m_InputActionAsset?.Enable();
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
        // Support both action names used across different input-asset revisions.
        m_UndoAction = map.FindAction("Undo", throwIfNotFound: false)
            ?? map.FindAction("Back", throwIfNotFound: false);
        m_ClearAction = map.FindAction("Clear", throwIfNotFound: false);
        m_RestartAction = map.FindAction("Restart", throwIfNotFound: false);
        m_DashAction = map.FindAction("Dash", throwIfNotFound: false);
        m_GroundPoundAction = map.FindAction("GroundPound", throwIfNotFound: false);
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
            if (m_JumpAction != null)
            {
                m_JumpAction.started += OnJumpStarted;
                m_JumpAction.canceled += OnJumpCanceled;
            }
            if (m_InteractAction != null) m_InteractAction.performed += OnInteract;
            if (m_SubmitAction != null) m_SubmitAction.performed += OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed += OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed += OnClear;
            if (m_RestartAction != null) m_RestartAction.performed += OnRestart;
            if (m_DashAction != null) m_DashAction.performed += OnDash;
            if (m_GroundPoundAction != null) m_GroundPoundAction.performed += OnGroundPound;
        }
        else
        {
            if (m_LeftAction != null) m_LeftAction.performed -= OnLeft;
            if (m_RightAction != null) m_RightAction.performed -= OnRight;
            if (m_JumpAction != null)
            {
                m_JumpAction.started -= OnJumpStarted;
                m_JumpAction.canceled -= OnJumpCanceled;
            }
            if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
            if (m_SubmitAction != null) m_SubmitAction.performed -= OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed -= OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed -= OnClear;
            if (m_RestartAction != null) m_RestartAction.performed -= OnRestart;
            if (m_DashAction != null) m_DashAction.performed -= OnDash;
            if (m_GroundPoundAction != null) m_GroundPoundAction.performed -= OnGroundPound;
        }
    }

    // ─── Callbacks ───────────────────────────────────────────────────────────

    private void OnLeft(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;

        bool isJumpHeldNow = m_IsJumpHeld || (m_JumpAction != null && m_JumpAction.IsPressed());
        if (isJumpHeldNow && !m_JumpComboQueued)
        {
            // Up (held) + Left is a single directional jump input.
            m_JumpComboQueued = true;
            m_SequenceManager?.AddAction(ActionTypeEnum.JumpLeft);
            return;
        }

        m_SequenceManager?.AddAction(ActionTypeEnum.Left);
    }

    private void OnRight(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;

        bool isJumpHeldNow = m_IsJumpHeld || (m_JumpAction != null && m_JumpAction.IsPressed());
        if (isJumpHeldNow && !m_JumpComboQueued)
        {
            // Up (held) + Right is a single directional jump input.
            m_JumpComboQueued = true;
            m_SequenceManager?.AddAction(ActionTypeEnum.JumpRight);
            return;
        }

        m_SequenceManager?.AddAction(ActionTypeEnum.Right);
    }

    private void OnJumpStarted(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;
        m_IsJumpHeld = true;
        m_JumpComboQueued = false;
    }

    private void OnJumpCanceled(InputAction.CallbackContext c)
    {
        if (!IsEnabled)
        {
            m_IsJumpHeld = false;
            m_JumpComboQueued = false;
            return;
        }

        if (!m_JumpComboQueued)
            m_SequenceManager?.AddAction(ActionTypeEnum.Jump);

        m_IsJumpHeld = false;
        m_JumpComboQueued = false;
    }
    private void OnInteract(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.AddAction(ActionTypeEnum.Interact); }
    // No IsEnabled guard — these actions remain active during execution to allow interrupts.
    // PlayerController.OnDashInterruptRequested checks m_IsGamePlaying itself.
    private void OnDash(InputAction.CallbackContext c) { OnDashPressed?.Invoke(); }
    private void OnGroundPound(InputAction.CallbackContext c) { OnGroundPoundPressed?.Invoke(); }
    private void OnUndo(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.RemoveLastAction(); }
    private void OnClear(InputAction.CallbackContext c) { if (IsEnabled) m_SequenceManager?.ClearSequence(); }
    private void OnRestart(InputAction.CallbackContext c) { GameManager.Instance?.ReloadLevel(); }

    // Submit triggers execution through GameManager (same path as the UI Play button)
    private void OnSubmit(InputAction.CallbackContext c) { if (IsEnabled) GameManager.Instance?.OnPlayClicked(); }
}
