using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The robot part hidden in a level. Walking the player into it collects it permanently:
    /// it is written to the save file and stays gone, so on the next load of this level the
    /// object hides itself in <see cref="Start"/>.
    ///
    /// It does <b>not</b> reset on turn/level reset — collection is permanent progress,
    /// cleared only by Tools ▸ Robot Collection ▸ Reset Progress.
    ///
    /// Its identity comes from the level's <c>LevelConfig.robotPart</c> assignment, not from
    /// this object, which is what enforces "one part per scene": drop the prefab in, and the
    /// level's config decides which robot part it is and which sprite it wears.
    ///
    /// Setup:
    ///   • Add a trigger Collider2D (this component enforces it).
    ///   • Give it a SpriteRenderer — the part sprite is applied automatically.
    ///   • Make sure the scene's LevelContext has a LevelConfig with Place Part ticked.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RobotPartPickup : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private string m_PlayerTag = "Player";

        [Header("Visuals")]
        [Tooltip("Renderer the part sprite is written to. Falls back to one on this object or a child.")]
        [SerializeField] private SpriteRenderer m_Renderer;

        [Tooltip("Apply the assigned part's sprite to the renderer on load. Turn off to keep hand-placed art.")]
        [SerializeField] private bool m_ApplyPartSprite = true;

        [Header("Feedback")]
        [Tooltip("Optional shine/idle effect shown while uncollected.")]
        [SerializeField] private GameObject m_ShineEffect;

        [Tooltip("Optional VFX prefab spawned at pickup (e.g. a Cartoon FX burst).")]
        [SerializeField] private GameObject m_CollectEffectPrefab;

        [Header("Identity Override")]
        [Tooltip("Ignore the level's LevelConfig assignment and use the robot + part below. " +
                 "For test scenes and one-offs; normal levels leave this off.")]
        [SerializeField] private bool m_OverrideAssignment;

        [Tooltip("ONLY used when Override Assignment is ticked. Otherwise the robot comes from " +
                 "this level's LevelConfig ▸ Collectables and editing this changes nothing.")]
        [SerializeField] private RobotId m_Robot = RobotId.Echo;

        [Tooltip("ONLY used when Override Assignment is ticked. Otherwise the part number comes " +
                 "from this level's LevelConfig ▸ Collectables and editing this changes nothing.")]
        [SerializeField, Range(1, RobotIds.PartsPerRobot)]
        private int m_PartNumber = 1;

        private bool m_Collected;
        private bool m_HasIdentity;
        private RobotId m_ResolvedRobot;
        private int m_ResolvedPartIndex;

        /// <summary>The robot this pickup belongs to. Only meaningful once resolved.</summary>
        public RobotId Robot => m_ResolvedRobot;

        /// <summary>The 0-based part slot this pickup fills. Only meaningful once resolved.</summary>
        public int PartIndex => m_ResolvedPartIndex;

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
            m_HasIdentity = ResolveIdentity(out m_ResolvedRobot, out m_ResolvedPartIndex);

            if (!m_HasIdentity)
            {
                // No part assigned to this level — nothing to collect here.
                gameObject.SetActive(false);
                return;
            }

            if (RobotCollectionService.IsCollected(m_ResolvedRobot, m_ResolvedPartIndex))
            {
                gameObject.SetActive(false);
                return;
            }

            if (m_ApplyPartSprite) ApplyPartSprite();
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

            RobotCollectionService.Collect(m_ResolvedRobot, m_ResolvedPartIndex);

            AudioManager.Instance?.PlayPickup();

            if (m_CollectEffectPrefab != null)
                Instantiate(m_CollectEffectPrefab, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Works out which part this is: the inspector override when enabled, otherwise the
        /// level's <c>LevelConfig.robotPart</c>. Returns false when the level hides no part.
        /// </summary>
        private bool ResolveIdentity(out RobotId robot, out int partIndex)
        {
            if (m_OverrideAssignment)
            {
                robot = m_Robot;
                partIndex = Mathf.Clamp(m_PartNumber, 1, RobotIds.PartsPerRobot) - 1;
                return true;
            }

            robot = RobotId.Echo;
            partIndex = 0;

            var context = LevelContext.Instance;
            var config = context != null ? context.Config : null;
            if (config == null)
            {
                Debug.LogWarning("[RobotPartPickup] No LevelConfig for this scene; the pickup cannot " +
                                 "know which part it is. Tick Override Assignment or assign a config.", this);
                return false;
            }

            var assignment = config.robotPart;
            if (assignment == null || !assignment.placePart) return false;

            robot = assignment.robot;
            partIndex = assignment.PartIndex;
            return true;
        }

        /// <summary>Dresses the pickup in the sprite of the part it represents.</summary>
        private void ApplyPartSprite()
        {
            if (m_Renderer == null) m_Renderer = GetComponentInChildren<SpriteRenderer>();
            if (m_Renderer == null) return;

            var definition = RobotCollectionService.GetDefinition(m_ResolvedRobot);
            var sprite = definition != null ? definition.GetPickupSprite(m_ResolvedPartIndex) : null;
            if (sprite != null) m_Renderer.sprite = sprite;
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null) go.SetActive(visible);
        }
    }
}
