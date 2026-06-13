using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Authored definition of one collectible robot part (Head, Body, Weapon, Core, …). One asset
    /// per part. Designers add/remove parts purely by creating/deleting these assets and listing
    /// them on a <see cref="RoboData"/> — no code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "Part_", menuName = "Collections/Robot Part", order = 0)]
    public class RobotPartData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id within its Robo (the save key). e.g. \"head\", \"left_arm\".")]
        [SerializeField] private string m_PartId = "part";

        [Tooltip("Display name, e.g. \"Left Arm\".")]
        [SerializeField] private string m_PartName = "New Part";

        [Header("Art")]
        [Tooltip("Artwork shown once the part is collected.")]
        [SerializeField] private Sprite m_CollectedSprite;

        [Tooltip("Silhouette / locked image shown before collection. If null, the slot shows an " +
                 "empty placeholder instead.")]
        [SerializeField] private Sprite m_SilhouetteSprite;

        public string PartId => m_PartId;
        public string PartName => m_PartName;
        public Sprite CollectedSprite => m_CollectedSprite;
        public Sprite SilhouetteSprite => m_SilhouetteSprite;
    }
}
