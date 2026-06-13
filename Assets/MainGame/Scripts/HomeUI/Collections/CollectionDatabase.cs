using System.Collections.Generic;
using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Ordered list of every Robo in the game (Robo 1..4, or more). Single source of truth for the
    /// Collections screen and the unlock chain: completing the Robo at index i unlocks index i+1.
    /// </summary>
    [CreateAssetMenu(fileName = "CollectionDatabase", menuName = "Collections/Collection Database", order = 2)]
    public class CollectionDatabase : ScriptableObject
    {
        [Tooltip("Every Robo, in unlock / tab order. The first is unlocked by default.")]
        [SerializeField] private List<RoboData> m_Robos = new List<RoboData>();

        public int Count => m_Robos.Count;
        public IReadOnlyList<RoboData> Robos => m_Robos;

        public RoboData GetByIndex(int index) =>
            (index >= 0 && index < m_Robos.Count) ? m_Robos[index] : null;

        public int IndexOf(string roboId)
        {
            for (int i = 0; i < m_Robos.Count; i++)
                if (m_Robos[i] != null && m_Robos[i].RoboId == roboId) return i;
            return -1;
        }

        public RoboData GetById(string roboId)
        {
            int i = IndexOf(roboId);
            return i >= 0 ? m_Robos[i] : null;
        }

        public RoboData FirstRobo => m_Robos.Count > 0 ? m_Robos[0] : null;

        public RoboData GetNext(string roboId)
        {
            int i = IndexOf(roboId);
            return i >= 0 ? GetByIndex(i + 1) : null;
        }
    }
}
