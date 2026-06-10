using UnityEngine;

/// <summary>
/// Global game settings applied once at launch, before any scene loads.
/// Acts as the game launcher: runs automatically via [RuntimeInitializeOnLoadMethod]
/// no matter which scene is opened first, so individual levels never set this themselves.
/// </summary>
public static class SettingsManager
{
    /// <summary>Frame rate the game runs at across every scene.</summary>
    public const int TargetFrameRate = 60;

    /// <summary>
    /// Called automatically by Unity once when the game starts, before the first scene loads.
    /// Caps the frame rate to avoid the lag caused by an unbounded high frame rate.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // vSync must be off, otherwise it overrides targetFrameRate and the cap is ignored.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
