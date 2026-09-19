namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Explicit state machine states for the Pause Menu system.
    /// Eliminates boolean state flags and strictly controls input routing,
    /// transition guards, and panel visibility.
    /// </summary>
    public enum PauseState
    {
        /// <summary>Gameplay active. Pause Menu UI completely hidden and inactive.</summary>
        Gameplay,

        /// <summary>Playing the cinematic entrance animation and button stagger sequence.</summary>
        Opening,

        /// <summary>Main Pause Menu active (RESET, LEVEL, EXIT buttons navigable).</summary>
        MainPause,

        /// <summary>Universal Confirmation Popup active (YES / NO choices).</summary>
        Confirmation,

        /// <summary>Embedded Level Selection Panel active inside the Pause Menu.</summary>
        LevelSelection,

        /// <summary>Playing the cinematic exit animation and restoring gameplay.</summary>
        Closing
    }
}
