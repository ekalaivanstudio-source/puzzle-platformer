using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// Immutable, designer-authored description of a single level.
    ///
    /// One asset per level. Created via the project window:
    ///   Create → Level Selection → Level Data
    ///
    /// This object holds only *static* level information (what the level IS).
    /// Runtime progress (completed? how many stars?) is kept separately in
    /// <see cref="LevelProgress"/> so that authored content and player state
    /// never bleed into each other.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Level_",
        menuName = "Level Selection/Level Data",
        order = 0)]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, unique id for this level. Used as the save key and to " +
                 "determine unlock order — it does NOT have to match the scene index. " +
                 "Never change an id once players have progress saved against it.")]
        [SerializeField] private int m_LevelId = 1;

        [Tooltip("Human-readable name shown in UI / debugging (e.g. \"The Spider's Lair\").")]
        [SerializeField] private string m_LevelName = "New Level";

        [Header("Presentation")]
        [Tooltip("Thumbnail shown on the level button when the level is unlocked.")]
        [SerializeField] private Sprite m_Thumbnail;

        [Header("Scene")]
        [Tooltip("Name of the scene to load when this level is selected. The scene " +
                 "MUST be added to File → Build Settings → Scenes In Build.")]
        [SerializeField] private string m_SceneName;

        /// <summary>Stable unique id, also used as the save key and unlock-order key.</summary>
        public int LevelId => m_LevelId;

        /// <summary>Human-readable display name.</summary>
        public string LevelName => m_LevelName;

        /// <summary>Thumbnail shown when the level is unlocked.</summary>
        public Sprite Thumbnail => m_Thumbnail;

        /// <summary>Scene to load when the level is chosen (must be in Build Settings).</summary>
        public string SceneName => m_SceneName;
    }
}
