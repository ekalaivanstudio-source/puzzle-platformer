/// <summary>
/// Contract for any input provider (mouse/toggle, keyboard, gamepad, etc.).
/// <see cref="InputModeManager"/> calls SetEnabled to activate or deactivate
/// a provider when the user changes their control mode in settings.
/// Adding a new input mode = implementing this interface.
/// </summary>
public interface IInputProvider
{
    /// <summary>Enables or disables this provider. Called by InputModeManager on mode switch.</summary>
    void SetEnabled(bool enabled);

    /// <summary>Whether this provider is currently accepting input.</summary>
    bool IsEnabled { get; }
}
