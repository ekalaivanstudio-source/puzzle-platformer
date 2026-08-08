using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu entry point for <see cref="AutoPlayTester"/>, so a level's solution can be run from
/// the toolbar during play mode without reaching for the on-screen button.
/// </summary>
public static class AutoPlayTesterMenu
{
    private const string k_MenuPath = "Tools/Auto Play/Run Level Solution %#a";

    [MenuItem(k_MenuPath)]
    private static void RunLevelSolution()
    {
        if (AutoPlayTester.Instance == null)
        {
            Debug.LogWarning("[AutoPlayTester] Not available — enter play mode on a level whose " +
                             "Managers object carries an AutoPlayTester.");
            return;
        }

        AutoPlayTester.Instance.RunAutoPlay();
    }

    // Greyed out outside play mode: the tool queues into runtime systems that only exist then.
    [MenuItem(k_MenuPath, isValidateFunction: true)]
    private static bool ValidateRunLevelSolution() => Application.isPlaying;
}
