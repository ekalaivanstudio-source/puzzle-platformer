using UnityEngine;

/// <summary>
/// Selects and switches the active input mode at runtime.
/// Call <see cref="SetMode"/> from a settings UI button to change control behavior instantly.
///
/// To add a new input mode in the future:
///   1. Implement <see cref="IInputProvider"/> (and optionally <see cref="ISequenceSource"/>)
///   2. Add an entry to <see cref="InputMode"/>
///   3. Add a serialized field and a case in <see cref="SetMode"/>
/// </summary>
public class InputModeManager : MonoBehaviour
{
    /// <summary>The available input control modes.</summary>
    public enum InputMode
    {
        /// <summary>UI toggle grid controlled by mouse (original approach).</summary>
        Mouse,

        /// <summary>Keyboard and/or gamepad key presses build the sequence.</summary>
        Device
    }

    [SerializeField] private InputMode m_DefaultMode = InputMode.Mouse;
    [SerializeField] private MouseInputProvider m_MouseProvider;
    [SerializeField] private DeviceInputProvider m_DeviceProvider;

    /// <summary>The currently active input mode.</summary>
    public InputMode CurrentMode { get; private set; }

    private void Awake()
    {
        if (m_MouseProvider == null) Debug.LogError("[InputModeManager] MouseInputProvider is not assigned.", this);
        if (m_DeviceProvider == null) Debug.LogError("[InputModeManager] DeviceInputProvider is not assigned.", this);
    }

    private void Start() => SetMode(m_DefaultMode);

    /// <summary>
    /// Switches to the specified input mode. Safe to call at runtime from a settings menu.
    /// Automatically enables the new provider and disables all others.
    /// </summary>
    public void SetMode(InputMode mode)
    {
        CurrentMode = mode;
        m_MouseProvider?.SetEnabled(mode == InputMode.Mouse);
        m_DeviceProvider?.SetEnabled(mode == InputMode.Device);
        Debug.Log($"[InputModeManager] Switched to {mode} input mode.");
    }

    // ─── Convenience methods — wire directly to UI setting buttons ───────────

    /// <summary>Switches to mouse/toggle UI mode.</summary>
    public void SetMouseMode() => SetMode(InputMode.Mouse);

    /// <summary>Switches to keyboard and gamepad mode.</summary>
    [ContextMenu("Set Device Mode")]
    public void SetDeviceMode() => SetMode(InputMode.Device);
}
