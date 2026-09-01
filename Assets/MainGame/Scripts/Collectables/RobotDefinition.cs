using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// All authoring data for one collectable robot: its id, its display name, and the
    /// art used to draw it in the collection UI.
    ///
    /// The UI draws a robot as a stack: <see cref="silhouette"/> at the bottom, then one
    /// layer per collected part on top. Every sprite is the same canvas size and each part
    /// sprite contains only that part's pixels, so the layers line up and a collected part
    /// simply lights up its region of the silhouette.
    ///
    /// <b>Array order is draw order</b> — <c>partSprites[0]</c> is drawn first (furthest
    /// back) and <c>partSprites[4]</c> last (in front). Reorder when a part should sit on
    /// top of another (e.g. eyes over a head).
    ///
    /// Create via: Assets ▸ Create ▸ Collectables ▸ Robot Definition, or let
    /// Tools ▸ Robot Collection ▸ Run Full Setup build all four from the sprite folders.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotDefinition", menuName = "Collectables/Robot Definition", order = 0)]
    public class RobotDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which robot this asset describes. Must be unique across the database.")]
        public RobotId robot = RobotId.Echo;

        [Tooltip("Name shown in the collection UI.")]
        public string displayName = "ECHO";

        [Tooltip("Highlight colour for this robot's label / progress bar.")]
        public Color accentColor = new Color(0.35f, 0.78f, 1f, 1f);

        [Header("Art")]
        [Tooltip("The unlit chassis, drawn underneath every part layer.")]
        public Sprite silhouette;

        [Tooltip("One sprite per part, in draw order (index 0 = furthest back). " +
                 "Each sprite holds only that part's pixels on the shared canvas.")]
        public Sprite[] partSprites = new Sprite[RobotIds.PartsPerRobot];

        [Tooltip("What the part looks like lying in a level: the artist's full artwork, with " +
                 "the whole robot dark and this part lit. The UI layers use partSprites instead " +
                 "— several of those are only a few pixels and would vanish in the world.")]
        public Sprite[] pickupSprites = new Sprite[RobotIds.PartsPerRobot];

        [Tooltip("Optional per-part names for tooltips. Leave empty to fall back to 'Part N'.")]
        public string[] partNames = new string[RobotIds.PartsPerRobot];

        /// <summary>Stable string id for this robot, e.g. "echo".</summary>
        public string RobotKey => RobotIds.RobotKey(robot);

        /// <summary>Number of part slots this robot has (always <see cref="RobotIds.PartsPerRobot"/>).</summary>
        public int PartCount => RobotIds.PartsPerRobot;

        /// <summary>Stable string id for one of this robot's parts, e.g. "echo_3".</summary>
        public string PartKey(int partIndex) => RobotIds.PartKey(robot, partIndex);

        /// <summary>The sprite for a part, or null when the index is out of range / unassigned.</summary>
        public Sprite GetPartSprite(int partIndex)
        {
            if (partSprites == null || partIndex < 0 || partIndex >= partSprites.Length) return null;
            return partSprites[partIndex];
        }

        /// <summary>
        /// The sprite a part wears while it sits in a level. Falls back to the UI layer
        /// sprite when no pickup art was assigned.
        /// </summary>
        public Sprite GetPickupSprite(int partIndex)
        {
            if (pickupSprites != null && partIndex >= 0 && partIndex < pickupSprites.Length
                && pickupSprites[partIndex] != null)
            {
                return pickupSprites[partIndex];
            }
            return GetPartSprite(partIndex);
        }

        /// <summary>A part's display name, falling back to "Part N" when none was authored.</summary>
        public string GetPartName(int partIndex)
        {
            if (partNames != null && partIndex >= 0 && partIndex < partNames.Length
                && !string.IsNullOrWhiteSpace(partNames[partIndex]))
            {
                return partNames[partIndex];
            }
            return $"Part {partIndex + 1}";
        }

        private void OnValidate()
        {
            // Keep the authoring arrays at the fixed part count so the inspector can't
            // drift out of sync with the save format.
            ResizeTo(ref partSprites, RobotIds.PartsPerRobot);
            ResizeTo(ref pickupSprites, RobotIds.PartsPerRobot);
            ResizeTo(ref partNames, RobotIds.PartsPerRobot);
        }

        private static void ResizeTo<T>(ref T[] array, int length)
        {
            if (array != null && array.Length == length) return;

            var resized = new T[length];
            if (array != null)
            {
                int copy = Mathf.Min(array.Length, length);
                for (int i = 0; i < copy; i++) resized[i] = array[i];
            }
            array = resized;
        }
    }
}
