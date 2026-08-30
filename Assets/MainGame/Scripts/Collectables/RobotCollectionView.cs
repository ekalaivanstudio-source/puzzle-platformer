using TMPro;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Drives a set of <see cref="RobotCollectionSlot"/>s from the save file. This is the one
    /// component that subscribes to <see cref="RobotCollectionService"/>, so slots stay dumb
    /// and the same view works in both places it is used:
    ///
    ///   • the level HUD, pinned to the right of the screen, showing all four robots;
    ///   • the home screen's Collection tab, showing the same four larger.
    ///
    /// Put it on the parent of the slots and leave <see cref="m_Slots"/> empty to pick up
    /// every child slot automatically.
    /// </summary>
    public class RobotCollectionView : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("One slot per robot, in database order. Leave empty to collect child slots automatically.")]
        [SerializeField] private RobotCollectionSlot[] m_Slots;

        [Header("Total (optional)")]
        [Tooltip("Shows parts found across every robot, e.g. 7/20.")]
        [SerializeField] private TMP_Text m_TotalLabel;

        [Tooltip("Format string with {0} = collected, {1} = total.")]
        [SerializeField] private string m_TotalFormat = "{0}/{1}";

        private bool m_Subscribed;
        private bool m_Bound;

        private void Awake()
        {
            EnsureSlots();
        }

        private void OnEnable()
        {
            // Re-bind on every enable: the database can arrive after a domain reload, and the
            // home-screen tab is enabled long after Awake ran.
            BindSlots();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            // The service's events are static, so an unsubscribe here is what stops a
            // destroyed HUD from being kept alive by the next scene's pickups.
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (m_Subscribed) return;
            RobotCollectionService.OnProgressChanged += Refresh;
            RobotCollectionService.OnPartCollected += HandlePartCollected;
            m_Subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_Subscribed) return;
            RobotCollectionService.OnProgressChanged -= Refresh;
            RobotCollectionService.OnPartCollected -= HandlePartCollected;
            m_Subscribed = false;
        }

        private void EnsureSlots()
        {
            if (m_Slots != null && m_Slots.Length > 0) return;
            m_Slots = GetComponentsInChildren<RobotCollectionSlot>(includeInactive: true);
        }

        /// <summary>Points each slot at its robot. Extra slots beyond the database are hidden.</summary>
        private void BindSlots()
        {
            EnsureSlots();
            if (m_Slots == null || m_Bound) return;

            var database = RobotCollectionService.Database;
            if (database == null) return;

            for (int i = 0; i < m_Slots.Length; i++)
            {
                if (m_Slots[i] == null) continue;
                m_Slots[i].Bind(database.GetAt(i));
            }

            m_Bound = true;
        }

        /// <summary>Repaints every slot and the total label from current save state.</summary>
        public void Refresh()
        {
            if (m_Slots != null)
            {
                for (int i = 0; i < m_Slots.Length; i++)
                    if (m_Slots[i] != null) m_Slots[i].Refresh();
            }

            if (m_TotalLabel != null)
            {
                m_TotalLabel.text = string.Format(
                    m_TotalFormat, RobotCollectionService.TotalCollected, RobotCollectionService.TotalParts);
            }
        }

        private void HandlePartCollected(RobotId robot, int partIndex)
        {
            // Punch only the slot that changed. Every slot repaints a moment later anyway:
            // the service always raises OnProgressChanged straight after OnPartCollected.
            if (m_Slots == null) return;

            for (int i = 0; i < m_Slots.Length; i++)
            {
                var slot = m_Slots[i];
                if (slot == null || slot.Definition == null || slot.Definition.robot != robot) continue;
                slot.PlayCollectFeedback(partIndex);
                return;
            }
        }
    }
}
