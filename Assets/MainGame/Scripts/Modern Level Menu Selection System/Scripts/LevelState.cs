using System;

namespace ModernLevelSelection
{
    /// <summary>
    /// Represents the visual and logical state of a level button.
    /// </summary>
    public enum LevelState
    {
        /// <summary>Level can be played.</summary>
        Unlocked = 0,

        /// <summary>Level exists in playable scenes but is not yet unlocked.</summary>
        Locked = 1,

        /// <summary>Level is beyond the available build scenes.</summary>
        ComingSoon = 2
    }
}
