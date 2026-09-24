using UnityEditor;
using UnityEngine;

/// <summary>
/// Wipes every piece of player progress from the editor — the same set the in-game
/// "New Game" button clears (level unlocks/stars, robot parts, memory shards).
/// Settings (audio, graphics) are preferences, not progress, and are left alone.
/// </summary>
public static class PlayerDataResetMenu
{
    [MenuItem("Tools/Player Data/Reset All Progress", priority = 0)]
    public static void ResetAllProgress()
    {
        ModernLevelSelection.SaveManager.ResetProgress();
        Collectables.RobotCollectionService.ResetAll();
        Collectables.MemoryShardService.ResetAll();

        Debug.Log("[PlayerData] Progress reset — level progress, robot parts and memory shards cleared.");
    }
}
