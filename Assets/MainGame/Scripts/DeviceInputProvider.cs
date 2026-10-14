using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton input provider for keyboard and gamepad using Unity's New Input System.
/// Queues actions into <see cref="SequenceManager"/> on key press.
/// Access via <see cref="Instance"/>.
///
/// Actions mapped (configure bindings in the Input Actions editor):
///   Left / Right (Arrows / D-pad), Jump (Space / RT) → queues into SequenceManager
///   Submit (Enter / Gamepad A)      → triggers execution via GameManager
///   Undo (Backspace / Gamepad Y)    → removes last queued action
///   Clear (Delete / Gamepad Select) → clears entire queue
///   Restart (R / Gamepad RB)        → reloads the level immediately (no confirmation)
///   LastTry (Tab / Gamepad LT)      → raises <see cref="LastTryPressed"/> (cycles the attempt markers)
///
/// Jump and a direction held together queue a directional jump, whichever went down
/// first. A key that ends up part of such a chord queues nothing when it is released;
/// a key released on its own queues its plain action — so directions queue on release,
/// not on press, which is what lets "Right, then Space" still become JumpRight.
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
    private InputAction m_LastTryAction;

    /// <summary>
    /// Raised when the LastTry action (Tab / Gamepad LT) is pressed during gameplay.
    /// <see cref="AttemptGhostService"/> uses it to step through the previous attempts.
    /// </summary>
    public static event System.Action LastTryPressed;

    // Held state per chord key. "Consumed" means the key already took part in a
    // directional jump, so its release must not queue a plain action as well.
    private struct ChordKey { public bool Held; public bool Consumed; }
    private ChordKey m_Jump;
    private ChordKey m_Left;
    private ChordKey m_Right;
    // Which direction went down most recently, for Jump pressed with both held.
    private bool m_RightPressedLast;

    // Gameplay input is allowed only when this provider is locally enabled.
    private bool m_LocallyEnabled;
    public bool IsEnabled => m_LocallyEnabled;

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
        m_LastTryAction = map.FindAction("LastTry", throwIfNotFound: false);

        m_InputActionAsset.Enable();
        m_LocallyEnabled = true;
    }

    private void OnEnable() => RegisterListeners(true);
    private void OnDisable() => RegisterListeners(false);

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RegisterListeners(bool register)
    {
        if (m_InputActionAsset == null) return;

        if (register)
        {
            if (m_LeftAction != null) { m_LeftAction.started += OnLeftStarted; m_LeftAction.canceled += OnLeftCanceled; }
            if (m_RightAction != null) { m_RightAction.started += OnRightStarted; m_RightAction.canceled += OnRightCanceled; }
            if (m_JumpAction != null) { m_JumpAction.started += OnJumpStarted; m_JumpAction.canceled += OnJumpCanceled; }
            if (m_LastTryAction != null) m_LastTryAction.performed += OnLastTry;
            if (m_SubmitAction != null) m_SubmitAction.performed += OnSubmit;
            if (m_UndoAction != null) m_UndoAction.performed += OnUndo;
            if (m_ClearAction != null) m_ClearAction.performed += OnClear;
            if (m_RestartAction != null) m_RestartAction.performed += OnRestart;
        }
        else
        {
            if (m_LeftAction != null) { m_LeftAction.started -= OnLeftStarted; m_LeftAction.canceled -= OnLeftCanceled; }
            if (m_RightAction != null) { m_RightAction.started -= OnRightStarted; m_RightAction.canceled -= OnRightCanceled; }
            if (m_JumpAction != null) { m_JumpAction.started -= OnJumpStarted; m_JumpAction.canceled -= OnJumpCanceled; }
            if (m_LastTryAction != null) m_LastTryAction.performed -= OnLastTry;
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
            ResetChord();
        if (enabled)
            m_InputActionAsset?.Enable();
    }

    // ─── Input Callbacks ─────────────────────────────────────────────────────

    private void OnLeftStarted(InputAction.CallbackContext c) => OnDirectionStarted(ref m_Left, ActionTypeEnum.JumpLeft, false);
    private void OnRightStarted(InputAction.CallbackContext c) => OnDirectionStarted(ref m_Right, ActionTypeEnum.JumpRight, true);
    private void OnLeftCanceled(InputAction.CallbackContext c) => OnKeyReleased(ref m_Left, ActionTypeEnum.Left);
    private void OnRightCanceled(InputAction.CallbackContext c) => OnKeyReleased(ref m_Right, ActionTypeEnum.Right);
    private void OnJumpCanceled(InputAction.CallbackContext c) => OnKeyReleased(ref m_Jump, ActionTypeEnum.Jump);

    // Jump already held → this press completes the chord (Space, then Right).
    private void OnDirectionStarted(ref ChordKey dir, ActionTypeEnum jumpAction, bool isRight)
    {
        if (!IsEnabled) return;
        dir = new ChordKey { Held = true };
        m_RightPressedLast = isRight;
        if (m_Jump.Held && !m_Jump.Consumed)
        {
            m_Jump.Consumed = true;
            dir.Consumed = true;
            TryQueue(jumpAction);
        }
    }

    // A direction already held → this press completes the chord (Right, then Space).
    private void OnJumpStarted(InputAction.CallbackContext c)
    {
        if (!IsEnabled) return;
        m_Jump = new ChordKey { Held = true };

        bool right = m_Right.Held && !m_Right.Consumed;
        bool left = m_Left.Held && !m_Left.Consumed;
        if (right && left) { right = m_RightPressedLast; left = !right; }

        if (right) { m_Right.Consumed = true; m_Jump.Consumed = true; TryQueue(ActionTypeEnum.JumpRight); }
        else if (left) { m_Left.Consumed = true; m_Jump.Consumed = true; TryQueue(ActionTypeEnum.JumpLeft); }
    }

    // A key released without having joined a chord queues its own action.
    private void OnKeyReleased(ref ChordKey key, ActionTypeEnum action)
    {
        bool queue = IsEnabled && key.Held && !key.Consumed;
        key = default;
        if (queue) TryQueue(action);
    }

    private void ResetChord()
    {
        m_Jump = default;
        m_Left = default;
        m_Right = default;
    }

    private void OnLastTry(InputAction.CallbackContext c)
    {
        if (!IsEnabled || MainGame.UI.Unified.PauseMenuScreen.IsPaused) return;
        LastTryPressed?.Invoke();
    }

    /// <summary>
    /// Queues an action and plays the queue sound only if it actually stuck.
    ///
    /// On a level with a correct sequence registered, a wrong input is appended by
    /// <see cref="SequenceManager.AddAction"/> and then pulled straight back out by
    /// <see cref="PlayerInputUIHelper"/>'s validation - all synchronously, inside the
    /// OnSequenceChanged callback, before AddAction returns. Its bool is therefore true
    /// even for a rejected input, so the queue length is what gets compared here (the
    /// same check <see cref="AutoPlayTester"/> makes). Without this the player hears the
    /// confirming "queued" blip on the very input the UI is rejecting with a red blink.
    /// </summary>
    private static bool TryQueue(ActionTypeEnum action)
    {
        SequenceManager sequencer = SequenceManager.Instance;
        if (sequencer == null) return false;

        int before = sequencer.SequenceLength;
        sequencer.AddAction(action);
        if (sequencer.SequenceLength <= before) return false;

        AudioManager.Instance?.PlayQueue();
        return true;
    }

    private void OnUndo(InputAction.CallbackContext c) { if (IsEnabled) { SequenceManager.Instance?.RemoveLastAction(); AudioManager.Instance?.PlayUndo(); } }
    private void OnClear(InputAction.CallbackContext c) { if (IsEnabled) { SequenceManager.Instance?.ClearSequence(); AudioManager.Instance?.PlayClear(); } }
    // R restarts straight away - no confirmation dialog. Skipped while the home-screen
    // confirmation is up so R cannot reload the level from underneath it.
    private void OnRestart(InputAction.CallbackContext c)
    {
        if (RestartConfirmationUI.IsOpen || MainGame.UI.Unified.PauseMenuScreen.IsPaused) return;
        GameManager.Instance?.RestartLevel();
    }
    private void OnSubmit(InputAction.CallbackContext c) { if (IsEnabled) { GameManager.Instance?.OnPlayClicked(); AudioManager.Instance?.PlaySubmit(); } }
}
