using UnityEngine;
using UnityEngine.InputSystem;
using TutorialSystem;

/// <summary>
/// Singleton input provider for keyboard and gamepad using Unity's New Input System.
/// Queues actions into <see cref="SequenceManager"/> on key press.
/// Access via <see cref="Instance"/>.
///
/// Actions mapped (configure bindings in the Input Actions editor):
///   Left / Right / Jump             → queues into SequenceManager
///   Submit (Enter / Gamepad Start)  → triggers execution via GameManager
///   Undo (Backspace / Gamepad B)    → removes last queued action
///   Clear (Delete / Gamepad Select) → clears entire queue
///   Restart (R)                     → reloads the current level
/// </summary>
public class DeviceInputProvider : MonoBehaviour
{
    public static DeviceInputProvider Instance { get; private set; }

    [SerializeField] private InputActionAsset m_InputActionAsset;
    public InputActionAsset InputActionAsset => m_InputActionAsset;

    private InputAction m_LeftAction;
    private InputAction m_RightAction;
    private InputAction m_JumpAction;
    private InputAction m_SubmitAction;
    private InputAction m_UndoAction;
    private InputAction m_ClearAction;
    private InputAction m_RestartAction;

    private bool m_IsJumpHeld;
    private bool m_JumpComboQueued;

    // Gameplay input is allowed only when this provider is locally enabled AND no tutorial is
    // currently blocking input. The tutorial gate is global (TutorialManager.GameplayInputBlocked)
    // so it composes with the GameManager's existing enable/disable without conflicting.
    private bool m_LocallyEnabled;
    public bool IsEnabled => m_LocallyEnabled && !TutorialManager.GameplayInputBlocked;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (m_InputActionAsset == null)
        {
            Debug.LogError("[DeviceInputProvider] InputActionAsset not assigned.", this);
            return;
        }

        InputActionMap map = m_InputActionAsset.FindActionMap("Player", throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogError("[DeviceInputProvider] 'Player' action map not found in asset.", this);
            return;
        }

        m_LeftAction = map.FindAction("Left", throwIfNotFound: false);
        m_RightAction = map.FindAction("Right", throwIfNotFound: false);
        m_JumpAction = map.FindAction("Jump", throwIfNotFound: false);
        // Interact (E) is NOT a sequence action — it triggers immediate interaction
        // (e.g. entering laser mover/rotator control mode) handled directly by the
        // LaserRedirector*InputResetter scripts, which subscribe to the action themselves.
        m_SubmitAction = map.FindAction("Submit", throwIfNotFound: false);
        m_UndoAction = map.FindAction("Undo", throwIfNotFound: false)
                        ?? map.FindAction("Back", throwIfNotFound: false);
        m_ClearAction = map.FindAction("Clear", throwIfNotFound: false);
        m_RestartAction = map.FindAction("Restart", throwIfNotFound: false);

        m_InputActionAsset.Enable();
        m_LocallyEnabled = true;
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
            if (m_JumpAction != null) { m_JumpAction.started += OnJumpStarted; m_JumpAction.canceled += OnJumpCanceled; }
            if (m_SubmitAction != null) m_SubmitAction.performed += OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed += OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed += OnClear;
            if (m_RestartAction != null) m_RestartAction.performed += OnRestart;
        }
        else
        {
            if (m_LeftAction != null) m_LeftAction.performed -= OnLeft;
            if (m_RightAction != null) m_RightAction.performed -= OnRight;
            if (m_JumpAction != null) { m_JumpAction.started -= OnJumpStarted; m_JumpAction.canceled -= OnJumpCanceled; }
            if (m_SubmitAction != null) m_SubmitAction.performed -= OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed -= OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed -= OnClear;
            if (m_RestartAction != null) m_RestartAction.performed -= OnRestart;
        }
    }

    // ─── Enable / Disable ────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables gameplay inputs.
    /// Called by <see cref="GameManager"/> to block input during execution.
    /// Restart always remains active so R can reload at any time.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        m_LocallyEnabled = enabled;
        if (!enabled)
        {
            m_IsJumpHeld = false;
            m_JumpComboQueued = false;
        }
        if (enabled)
            m_InputActionAsset?.Enable();
    }

    // ─── Input Callbacks ─────────────────────────────────────────────────────

    private void OnLeft(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;
        AudioManager.Instance?.PlayQueue();
        bool jumpHeld = m_IsJumpHeld || (m_JumpAction != null && m_JumpAction.IsPressed());
        if (jumpHeld && !m_JumpComboQueued)
        {
            m_JumpComboQueued = true;
            SequenceManager.Instance?.AddAction(ActionTypeEnum.JumpLeft);
            return;
        }
        SequenceManager.Instance?.AddAction(ActionTypeEnum.Left);
    }

    private void OnRight(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;
        AudioManager.Instance?.PlayQueue();
        bool jumpHeld = m_IsJumpHeld || (m_JumpAction != null && m_JumpAction.IsPressed());
        if (jumpHeld && !m_JumpComboQueued)
        {
            m_JumpComboQueued = true;
            SequenceManager.Instance?.AddAction(ActionTypeEnum.JumpRight);
            return;
        }
        SequenceManager.Instance?.AddAction(ActionTypeEnum.Right);
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
        {
            SequenceManager.Instance?.AddAction(ActionTypeEnum.Jump);
            AudioManager.Instance?.PlayQueue();
        }
        m_IsJumpHeld = false;
        m_JumpComboQueued = false;
    }

    private void OnUndo(InputAction.CallbackContext c) { if (IsEnabled) { SequenceManager.Instance?.RemoveLastAction(); AudioManager.Instance?.PlayUndo(); } }
    private void OnClear(InputAction.CallbackContext c) { if (IsEnabled) { SequenceManager.Instance?.ClearSequence(); AudioManager.Instance?.PlayClear(); } }
    private void OnRestart(InputAction.CallbackContext c) { if (TutorialManager.GameplayInputBlocked) return; GameManager.Instance?.ReloadLevel(); }
    private void OnSubmit(InputAction.CallbackContext c) { if (IsEnabled) { GameManager.Instance?.OnPlayClicked(); AudioManager.Instance?.PlaySubmit(); } }
}
