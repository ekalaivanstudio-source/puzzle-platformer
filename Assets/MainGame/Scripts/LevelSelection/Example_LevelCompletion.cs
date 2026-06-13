using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// EXAMPLE ONLY — shows how gameplay code reports a finished level to the system.
    /// Delete or replace this with your real win logic. It is intentionally tiny: the only
    /// thing gameplay has to do is call <c>LevelManager.CompleteLevel(levelId, stars)</c>.
    ///
    /// Place this on a "win trigger" / level-complete handler in a level scene, set the id
    /// to match this level's <see cref="LevelData.LevelId"/>, then call <see cref="WinLevel"/>
    /// from your victory event (collision, button, end-of-puzzle, etc.).
    /// </summary>
    public class Example_LevelCompletion : MonoBehaviour
    {
        [Tooltip("Must match the LevelData.LevelId of THIS level's data asset.")]
        [SerializeField] private int m_ThisLevelId = 1;

        [Tooltip("The level database asset, so completion knows the unlock order. " +
                 "Assign the same LevelDatabase used by the level-select screen.")]
        [SerializeField] private LevelDatabase m_Database;

        /// <summary>
        /// Call this when the player wins. <paramref name="starsEarned"/> is whatever your
        /// scoring decides (time, collectibles, deaths…). The system keeps only the best result.
        /// </summary>
        public void WinLevel(int starsEarned)
        {
            // Overload that takes the database explicitly — handy when LevelManager.Configure
            // was not called in this scene (it normally is, on the level-select screen).
            LevelManager.CompleteLevel(m_Database, m_ThisLevelId, starsEarned);

            // ── Equivalent shorter form, if LevelManager.Configure(database) ran at startup: ──
            // LevelManager.CompleteLevel(m_ThisLevelId, starsEarned);

            Debug.Log($"[Example] Level {m_ThisLevelId} finished with {starsEarned} stars.");
        }

        // Convenience hooks you can wire straight to UI buttons in the Inspector.
        public void WinWithThreeStars() => WinLevel(3);
        public void WinWithTwoStars()   => WinLevel(2);
        public void WinWithOneStar()    => WinLevel(1);
    }
}
