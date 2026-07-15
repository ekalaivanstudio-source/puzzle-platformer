using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// A persistent pickup (Robot Part or Memory Shard). Walking the player into it
    /// collects it, stores it in the save file, and removes it. Once collected it stays
    /// gone — on the next load of this level the object hides itself in <see cref="Start"/>.
    ///
    /// Unlike <see cref="PlaceableKey"/>, a Collectable does NOT reset on turn/level reset:
    /// collection is permanent progress, cleared only by the reset tool.
    ///
    /// Setup:
    ///   • Add a trigger Collider2D (this component enforces it).
    ///   • Give it a SpriteRenderer (on this object or a child).
    ///   • Pick the Type. Each placed instance must have a unique Id — use the
    ///     "Regenerate Id" context menu or the Collectable Tools window; new prefab
    ///     instances auto-generate one on drop.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Collectable : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private CollectableType m_Type = CollectableType.RobotPart;

        [Tooltip("Stable unique id for this placed instance. Auto-filled when empty. " +
                 "Must be unique across every collectable in the game.")]
        [SerializeField] private string m_UniqueId;

        [Header("Detection")]
        [SerializeField] private string m_PlayerTag = "Player";

        [Header("Feedback")]
        [Tooltip("Optional shine/idle effect shown while uncollected.")]
        [SerializeField] private GameObject m_ShineEffect;
        [Tooltip("Optional VFX prefab spawned at pickup (e.g. a Cartoon FX burst).")]
        [SerializeField] private GameObject m_CollectEffectPrefab;

        public CollectableType Type => m_Type;
        public string UniqueId => m_UniqueId;

        private bool m_Collected;

        private void Reset()
        {
            // Make the collider a trigger by default when the component is first added.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            EnsureId();
        }

        private void OnValidate()
        {
            EnsureId();
        }

        private void Start()
        {
            // Deferred to Start so CollectableLevelManager.Awake has resolved the level.
            if (IsAlreadyCollected())
            {
                gameObject.SetActive(false);
                return;
            }

            Show(m_ShineEffect, true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Collected) return;
            if (!other.CompareTag(m_PlayerTag)) return;
            Collect();
        }

        private void Collect()
        {
            m_Collected = true;

            var manager = CollectableLevelManager.Instance;
            if (manager != null)
            {
                manager.Collect(m_UniqueId, m_Type);
            }
            else
            {
                // No manager in scene — still record it so progress isn't lost.
                Debug.LogWarning("[Collectable] No CollectableLevelManager in scene; saving with level 0.", this);
                CollectableSaveSystem.MarkCollected(m_UniqueId, m_Type, 0);
            }

            AudioManager.Instance?.PlayPickup();

            if (m_CollectEffectPrefab != null)
                Instantiate(m_CollectEffectPrefab, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
        }

        private bool IsAlreadyCollected()
        {
            var manager = CollectableLevelManager.Instance;
            return manager != null
                ? manager.IsCollected(m_UniqueId)
                : CollectableSaveSystem.IsCollected(m_UniqueId);
        }

        private void EnsureId()
        {
            if (!string.IsNullOrEmpty(m_UniqueId)) return;

#if UNITY_EDITOR
            // Never stamp an id onto the prefab asset itself, or onto the prefab being
            // edited in Prefab Mode — every placed instance would then share it. Ids are
            // generated per scene instance so each pickup is uniquely tracked.
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;

            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(gameObject)) return;
#endif

            m_UniqueId = System.Guid.NewGuid().ToString("N");
        }

        [ContextMenu("Regenerate Id")]
        private void RegenerateId()
        {
            m_UniqueId = System.Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null) go.SetActive(visible);
        }
    }
}
