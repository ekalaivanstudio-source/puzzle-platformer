namespace Collectables
{
    /// <summary>
    /// The kinds of persistent collectables in the game.
    /// Robot Parts contribute to a single game-wide total (e.g. 0/56).
    /// Memory Shards are grouped into level-range "tiers" that unlock a story
    /// when the tier's required count is reached (see <see cref="CollectableConstants"/>).
    /// </summary>
    public enum CollectableType
    {
        RobotPart = 0,
        MemoryShard = 1,
    }
}
