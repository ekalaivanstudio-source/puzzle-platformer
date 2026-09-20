using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Collectables
{
    /// <summary>
    /// The shard counter shown during a level, and the brief toast that appears when a pickup
    /// unlocks a story.
    ///
    /// The toast is all the feedback the unlock gets at pickup time — the cutscene itself is
    /// queued for the end of the level (see <see cref="MemoryStoryPresenter"/>), so the toast is
    /// what tells the player their shard did something without taking the puzzle away from them.
    ///
    /// Reads everything through <see cref="MemoryShardService"/>, so it works in a level, on the
    /// home screen, or anywhere else it is dropped.
    ///
    /// Build it into every level scene with Tools ▸ Memory Shards ▸ Run Full Setup.
    /// </summary>
    public class MemoryShardCounterHUD : MonoBehaviour
    {
        [Header("Counter")]
        [Tooltip("Shows the running total. Formatted with Count Format below.")]
        [SerializeField] private TextMeshProUGUI m_CountLabel;

        [Tooltip("Optional shard icon. Dressed in the first shard sprite from the database.")]
        [SerializeField] private Image m_Icon;

        [Tooltip("{0} is the shards collected, {1} the total the last story needs.")]
        [SerializeField] private string m_CountFormat = "{0} / {1}";

        [Tooltip("Show only the collected count, with no target. Use while the story thresholds " +
                 "are still being decided and a denominator would be misleading.")]
        [SerializeField] private bool m_HideTarget;

        [Header("Unlock Toast")]
        [Tooltip("Optional. Switched on for a few seconds when a shard unlocks a story.")]
        [SerializeField] private GameObject m_ToastRoot;

        [Tooltip("Optional label inside the toast. Formatted with Toast Format below.")]
        [SerializeField] private TextMeshProUGUI m_ToastLabel;

        [Tooltip("{0} is the story's title.")]
        [SerializeField] private string m_ToastFormat = "New memory unlocked — {0}";

        [Tooltip("Seconds the toast stays on screen.")]
        [Min(0.1f)] [SerializeField] private float m_ToastDuration = 2.5f;

        private Coroutine m_ToastRoutine;

        private void Awake()
        {
            if (m_ToastRoot != null) m_ToastRoot.SetActive(false);
        }

        private void OnEnable()
        {
            MemoryShardService.OnProgressChanged += Refresh;
            MemoryShardService.OnStoryUnlocked += HandleStoryUnlocked;
            Refresh();
        }

        /// <summary>
        /// Unsubscribing here is not optional: these are static events and they outlive the scene
        /// that subscribed to them, so a HUD left attached would be called on a destroyed object
        /// for the rest of the session.
        /// </summary>
        private void OnDisable()
        {
            MemoryShardService.OnProgressChanged -= Refresh;
            MemoryShardService.OnStoryUnlocked -= HandleStoryUnlocked;
        }

        /// <summary>Repaints the counter from the service. Safe to call any time.</summary>
        public void Refresh()
        {
            if (m_Icon != null)
            {
                Sprite sprite = MemoryShardService.GetShardSprite(0);
                if (sprite != null) m_Icon.sprite = sprite;
            }

            if (m_CountLabel == null) return;

            int collected = MemoryShardService.TotalCollected;
            int target = MemoryShardService.ShardsForAllStories;

            // With no stories authored yet there is no honest denominator to show, so fall back
            // to the bare count rather than printing "3 / 0".
            m_CountLabel.text = m_HideTarget || target <= 0
                ? collected.ToString()
                : string.Format(m_CountFormat, collected, target);
        }

        private void HandleStoryUnlocked(MemoryStoryEntry story)
        {
            if (m_ToastRoot == null || story == null) return;

            if (m_ToastLabel != null)
                m_ToastLabel.text = string.Format(m_ToastFormat, story.title);

            if (m_ToastRoutine != null) StopCoroutine(m_ToastRoutine);
            m_ToastRoutine = StartCoroutine(ShowToastRoutine());
        }

        private IEnumerator ShowToastRoutine()
        {
            m_ToastRoot.SetActive(true);
            yield return new WaitForSecondsRealtime(m_ToastDuration);
            m_ToastRoot.SetActive(false);
            m_ToastRoutine = null;
        }
    }
}
