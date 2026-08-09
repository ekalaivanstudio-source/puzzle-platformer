using UnityEngine;

namespace LevelSelection
{
    /// <summary>
    /// Configuration data for a single level selection arc/world.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArcData", menuName = "Level Selection/Arc Data")]
    public class ArcData : ScriptableObject
    {
        public string arcName = "ARC 1";
        public Sprite arcTitleSprite; // Custom header sprite for this arc
        public int totalLevelsInArc = 15;

        [Header("Grid Layout Settings")]
        public int columns = 5;
        public float horizontalSpacing = 200f;
        public float verticalSpacing = 150f;
        public Vector2 startOffset = new Vector2(-400f, 200f);
    }
}
