using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The memory shard hidden in a level. Walking the player into it collects it permanently:
    /// it is written to the save file and stays gone, so on the next load of this level the
    /// object hides itself in <see cref="Start"/>.
    ///
    /// It does <b>not</b> reset on turn/level reset — collection is permanent progress, cleared
    /// only by New Game or Tools ▸ Memory Shards ▸ Reset Progress.
    ///
    /// Whether the shard exists at all comes from the level's <c>LevelConfig.memoryShard</c>,
    /// not from this object. Drop the prefab in and the level's config decides whether it is
    /// there, so moving it around the scene never changes anything.
    ///
    /// The shard is a single still: <c>Sprites/Collectibles/memory shards/MEMORY SHARD.png</c>,
    /// with no animation. The <c>Shard Variant</c> field below is left over from an older
    /// five-frame art set; with one sprite every value resolves to the same picture.
    ///
    /// Collecting a shard never plays a story here. It raises
    /// <see cref="MemoryShardService.OnStoryUnlocked"/> if the new total crossed a threshold,
    /// and the cutscene itself waits for the end of the level — see
    /// <see cref="MemoryStoryPresenter"/>. That keeps a puzzle from being interrupted mid-solve.
    ///
    /// Setup:
    ///   • Add a trigger Collider2D (this component enforces it).
    ///   • Give it a SpriteRenderer — the shard sprite is applied automatically.
    ///   • Tick Place Shard on the scene's LevelConfig ▸ Collectables.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MemoryShardPickup : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private string m_PlayerTag = "Player";

        [Header("Visuals")]
        [Tooltip("Renderer the shard sprite is written to. Falls back to one on this object or a child.")]
        [SerializeField] private SpriteRenderer m_Renderer;

        [Tooltip("Dress the shard in the database's art on load. Turn off to keep hand-placed art.")]
        [SerializeField] private bool m_ApplyShardSprite = true;

        [Header("Feedback")]
        [Tooltip("Optional shine/idle effect shown while uncollected.")]
        [SerializeField] private GameObject m_ShineEffect;

        [Tooltip("Optional VFX prefab spawned at pickup (e.g. a Cartoon FX burst).")]
        [SerializeField] private GameObject m_CollectEffectPrefab;

        [Header("Identity Override")]
        [Tooltip("Ignore the level's LevelConfig and behave as if this level hides a shard, using " +
                 "the level number below. For test scenes and one-offs; normal levels leave this off.")]
        [SerializeField] private bool m_OverrideAssignment;

        [Tooltip("ONLY used when Override Assignment is ticked. The shard's save id is built from " +
                 "this number, so two test scenes sharing it share one shard.")]
        [SerializeField, Min(1)] private int m_LevelNumber = 1;

        [Tooltip("ONLY used when Override Assignment is ticked. Which shard sprite to show, or 0 " +
                 "to pick automatically from the level number. There is one sprite today.")]
        [SerializeField, Range(0, MemoryShardIds.VariantCount)] private int m_ShardVariant;

        private bool m_Collected;
        private bool m_HasIdentity;
        private int m_ResolvedLevel;
        private int m_ResolvedVariantIndex;

        /// <summary>The level whose shard this is — its save identity. Only meaningful once resolved.</summary>
        public int LevelNumber => m_ResolvedLevel;

        private void Reset()
        {
            // Make the collider a trigger by default when the component is first added.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            if (m_Renderer == null) m_Renderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            // Deferred to Start so LevelContext.Awake has resolved this scene's config.
            m_HasIdentity = ResolveIdentity(out m_ResolvedLevel, out m_ResolvedVariantIndex);

            if (!m_HasIdentity)
            {
                // This level hides no shard — nothing to collect here.
                gameObject.SetActive(false);
                return;
            }

            if (MemoryShardService.IsCollected(m_ResolvedLevel))
            {
                gameObject.SetActive(false);
                return;
            }

            if (m_ApplyShardSprite) ApplyShardSprite();
            Show(m_ShineEffect, true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Collected || !m_HasIdentity) return;
            if (!other.CompareTag(m_PlayerTag)) return;
            Collect();
        }

        private void Collect()
        {
            m_Collected = true;

            // Raises OnStoryUnlocked for any threshold this crosses. The cutscene is not played
            // from here — MemoryStoryPresenter drains the queue when the level ends.
            MemoryShardService.Collect(m_ResolvedLevel);

            AudioManager.Instance?.PlayPickup();

            if (m_CollectEffectPrefab != null)
                Instantiate(m_CollectEffectPrefab, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Works out whether this level hides a shard and which sprite it wears: the inspector
        /// override when enabled, otherwise the level's <c>LevelConfig.memoryShard</c>.
        /// Returns false when the level hides no shard.
        /// </summary>
        private bool ResolveIdentity(out int levelNumber, out int variantIndex)
        {
            if (m_OverrideAssignment)
            {
                levelNumber = Mathf.Max(1, m_LevelNumber);
                variantIndex = m_ShardVariant <= 0
                    ? MemoryShardIds.AutoVariantIndex(levelNumber)
                    : Mathf.Clamp(m_ShardVariant, 1, MemoryShardIds.VariantCount) - 1;
                return true;
            }

            levelNumber = 0;
            variantIndex = 0;

            var context = LevelContext.Instance;
            var config = context != null ? context.Config : null;
            if (config == null)
            {
                Debug.LogWarning("[MemoryShardPickup] No LevelConfig for this scene; the shard cannot " +
                                 "know which level it belongs to. Tick Override Assignment or assign a config.", this);
                return false;
            }

            var assignment = config.memoryShard;
            if (assignment == null || !assignment.placeShard) return false;

            // The level number is the shard's identity, so it has to be a real one. Falling back
            // to the build index (what LevelContext does) would give two configs-less scenes the
            // same id, and 0 is not a level at all.
            levelNumber = context.CurrentLevel;
            if (levelNumber <= 0)
            {
                Debug.LogWarning("[MemoryShardPickup] This level resolves to level number " +
                                 $"{levelNumber}, which cannot identify a shard. Set LevelConfig.levelNumber.", this);
                return false;
            }

            variantIndex = assignment.VariantIndex(levelNumber);
            return true;
        }

        /// <summary>
        /// Dresses the pickup in the shard art from the database rather than the prefab, so
        /// re-drawing <c>MEMORY SHARD.png</c> reaches every shard after one Build Database.
        /// </summary>
        private void ApplyShardSprite()
        {
            if (m_Renderer == null) m_Renderer = GetComponentInChildren<SpriteRenderer>();
            if (m_Renderer == null) return;

            var sprite = MemoryShardService.GetShardSprite(m_ResolvedVariantIndex);
            if (sprite != null) m_Renderer.sprite = sprite;
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null) go.SetActive(visible);
        }
    }
}
