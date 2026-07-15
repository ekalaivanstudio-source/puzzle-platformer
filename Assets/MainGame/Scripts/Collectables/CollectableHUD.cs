using TMPro;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Shows collectable progress in the level HUD:
    ///   • Robot Parts  → game-wide total, e.g. "12/56".
    ///   • Memory Shards → progress in the current level's story tier, e.g. "3/6".
    ///
    /// Drop this on a UI object, assign the two TMP labels, and it keeps them in sync by
    /// listening to <see cref="CollectableLevelManager.OnCollectableCollected"/>.
    /// </summary>
    public class CollectableHUD : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text m_RobotPartsText;
        [SerializeField] private TMP_Text m_MemoryShardsText;

        [Header("Optional roots (hidden when the value doesn't apply)")]
        [Tooltip("Root of the Memory Shard widget. Hidden in levels not covered by any shard tier.")]
        [SerializeField] private GameObject m_MemoryShardsRoot;

        [Header("Format")]
        [Tooltip("Format string with {0} = collected, {1} = total.")]
        [SerializeField] private string m_Format = "{0}/{1}";

        private CollectableLevelManager m_Manager;

        private void OnEnable()
        {
            m_Manager = CollectableLevelManager.Instance;
            if (m_Manager != null)
                m_Manager.OnCollectableCollected += Refresh;

            Refresh();
        }

        private void Start()
        {
            // Instance may not have existed at OnEnable (script execution order); re-hook.
            if (m_Manager == null)
            {
                m_Manager = CollectableLevelManager.Instance;
                if (m_Manager != null)
                    m_Manager.OnCollectableCollected += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (m_Manager != null)
                m_Manager.OnCollectableCollected -= Refresh;
        }

        /// <summary>Repaints both labels from the current save state.</summary>
        public void Refresh()
        {
            if (m_Manager == null) return;

            if (m_RobotPartsText != null)
            {
                m_RobotPartsText.text = string.Format(
                    m_Format, m_Manager.RobotPartsCollectedTotal, m_Manager.RobotPartsGrandTotal);
            }

            bool hasTier = m_Manager.TryGetCurrentTier(out _);
            if (m_MemoryShardsRoot != null)
                m_MemoryShardsRoot.SetActive(hasTier);

            if (m_MemoryShardsText != null && hasTier)
            {
                m_MemoryShardsText.text = string.Format(
                    m_Format, m_Manager.MemoryShardsCollectedInTier, m_Manager.MemoryShardsTierTarget);
            }
        }
    }
}
