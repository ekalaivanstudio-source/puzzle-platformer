using UnityEngine;

namespace LevelSelection
{
    /// <summary>
    /// Configuration data for a single level selection arc/world.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArcData", menuName = "Level Selection/Arc Data")]
    public class ArcData : ScriptableObject
    {
        [Header("Arc Header & Levels")]
        public string arcName = "ARC 1";
        public Sprite arcTitleSprite; // Custom header sprite for this arc
        public int totalLevelsInArc = 15;

        [Header("Theme Image & Thumbnails")]
        [Tooltip("Theme image for this arc, cut into slices for each level in this arc (e.g. TUT_BG or LVL_BG).")]
        public Texture2D arcThemeImage;

        [Tooltip("Optional pre-sliced sprites array (if pre-cut in sprite editor).")]
        public Sprite[] levelThumbnails;
    }
}
