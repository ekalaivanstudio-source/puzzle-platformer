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
    /// <summary>Wildcard slot — any action entered here is always accepted.</summary>
    Any,
    JumpRight,
    JumpLeft
}
