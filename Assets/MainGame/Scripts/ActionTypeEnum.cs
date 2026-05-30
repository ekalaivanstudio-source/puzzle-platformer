/// <summary>
/// Defines all possible player actions that can be programmed
/// onto the timeline grid.
/// </summary>
public enum ActionTypeEnum
{
    Left,
    Right,
    Jump,
    Interact,
    /// <summary>
    /// Wildcard slot in a correct-sequence definition — the player may enter
    /// any action here and it will always be accepted.
    /// </summary>
    Any,
    JumpRight,
    JumpLeft,
    /// <summary>Quick horizontal burst in the player's current facing direction.</summary>
    Dash,
    /// <summary>Slams the player straight down at high speed until they hit ground.</summary>
    GroundPound
}
