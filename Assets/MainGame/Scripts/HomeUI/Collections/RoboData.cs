using System.Collections.Generic;
using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Authored definition of one Robo (Robo 1..4) — its display name, icon, and the list of parts
    /// that make it up. Completion = all parts collected. Fully data-driven: change the parts list
    /// here and the UI and progress tracking adapt automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "Robo_", menuName = "Collections/Robo", order = 1)]
    public class RoboData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id (the save key), e.g. \"robo1\".")]
        [SerializeField] private string m_RoboId = "robo1";

        [Tooltip("Display name shown on the tab, e.g. \"Robo 1\".")]
        [SerializeField] private string m_RoboName = "Robo 1";

        [Tooltip("Optional icon for the tab.")]
        [SerializeField] private Sprite m_Icon;

        [Header("Parts")]
        [Tooltip("All collectible parts for this Robo. Add/remove freely — no code changes.")]
        [SerializeField] private List<RobotPartData> m_Parts = new List<RobotPartData>();

        public string RoboId => m_RoboId;
        public string RoboName => m_RoboName;
        public Sprite Icon => m_Icon;

        /// <summary>Number of parts in this Robo.</summary>
        public int PartCount => m_Parts.Count;

        /// <summary>Read-only view of the parts list.</summary>
        public IReadOnlyList<RobotPartData> Parts => m_Parts;
    }
}
