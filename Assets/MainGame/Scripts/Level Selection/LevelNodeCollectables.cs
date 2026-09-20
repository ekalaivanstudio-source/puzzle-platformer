using System;
using Collectables;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// The collectables a level hides, drawn as bare icons sitting <b>on the path line</b> rather
    /// than on the level marker itself.
    ///
    /// The map is already dense — a marker, a lock, a number and a route line all inside 100
    /// units — so the collectables are parked on the empty stretch of line leaving that level,
    /// where nothing competes with them. <see cref="SetLineAnchor"/> is how they get there: the
    /// generator hands each node the midpoint of the segment it owns, because only the generator
    /// knows where the neighbouring node ended up.
    ///
    /// No panel, no backing plate — just the icons, lit when the player has them and greyed when
    /// they are still out there.
    ///
    /// Everything shown comes from <see cref="LevelCollectableService"/>; the only state held
    /// here is the level number it was last bound to.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelNodeCollectables : MonoBehaviour
    {
        #region Types

        /// <summary>
        /// A stand-in icon for one robot, used in place of its part artwork.
        ///
        /// Every robot-part sprite in the project is near-black: the pickup art is the whole robot
        /// in shadow with one part lit, and the UI layers are a handful of pixels each. An Image
        /// tint only ever multiplies, so against the map's dark background those icons cannot be
        /// made to read at all, lit or unlit. A light silhouette tints the way the shard does.
        ///
        /// The cost is that it says "a part of this robot" rather than which part. Clear the entry
        /// to go back to the real part sprite.
        /// </summary>
        [Serializable]
        public class RobotIconOverride
        {
            public RobotId robot = RobotId.Pixel;

            [Tooltip("Light, flat artwork meant to be tinted. Leave empty to use the part's own sprite.")]
            public Sprite icon;
        }

        #endregion

        #region Inspector Fields

        [Header("Layout")]
        [Tooltip("Holds the icons. Moved onto the path line by the generator; hidden outright when " +
                 "the level hides nothing.")]
        [SerializeField] private RectTransform m_Root;

        [Tooltip("Where the icons sit relative to the level marker. Overwritten per node by the " +
                 "generator, which puts it halfway to the next level.")]
        [SerializeField] private Vector2 m_LineAnchor = new Vector2(175f, 0f);

        [Tooltip("Icon box in UI units. Square, so pixel art keeps its proportions.")]
        [SerializeField] private float m_IconSize = 44f;

        [Tooltip("Centre-to-centre spacing when a level hides both a part and a shard. A level " +
                 "hiding one draws it centred instead.")]
        [SerializeField] private float m_IconGap = 50f;

        [Header("Icons")]
        [Tooltip("The robot part. Drawn first, so it sits left of the shard when both are present.")]
        [SerializeField] private Image m_RobotPartIcon;

        [Tooltip("The memory shard.")]
        [SerializeField] private Image m_MemoryShardIcon;

        [Tooltip("Stand-in icons for the part slot, per robot. Empty list = always use the part's " +
                 "own sprite.")]
        [SerializeField] private RobotIconOverride[] m_RobotIcons = new RobotIconOverride[0];

        [Header("Tint")]
        [Tooltip("An icon the player already has.")]
        [SerializeField] private Color m_CollectedTint = Color.white;

        [Tooltip("An icon still out there. Also what a locked level shows — the map is a teaser, " +
                 "so the icons stay readable everywhere, just unlit until they are found. Keep it " +
                 "mid-toned: it has to read against the dark background and against the route line " +
                 "it sits on.")]
        [SerializeField] private Color m_UncollectedTint = new Color(0.38f, 0.45f, 0.56f, 1f);

        #endregion

        #region Private Fields

        private int m_LevelNumber = -1;
        private bool m_Subscribed;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            Subscribe();

            // The map is regenerated on open, but a node can also be re-enabled without being
            // rebound (arc paging reuses the objects), so repaint from the live save here.
            if (m_LevelNumber > 0) Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            // OnDisable already runs for a normal teardown; this covers a destroy while inactive.
            Unsubscribe();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Moves the icons onto the path line. <paramref name="offsetFromNode"/> is in the node's
        /// own space, so the generator passes half the step to the neighbouring level and the
        /// icons land on the middle of that segment.
        /// </summary>
        public void SetLineAnchor(Vector2 offsetFromNode)
        {
            m_LineAnchor = offsetFromNode;
            if (m_Root != null) m_Root.anchoredPosition = m_LineAnchor;
        }

        /// <summary>
        /// Points this indicator at a level and repaints it. Called from
        /// <see cref="LevelNodeUI.SetupNode"/>, so every path that sets a node's state — first
        /// generation, arc paging, the unlock animation — refreshes the icons too.
        /// </summary>
        public void Bind(int levelNumber)
        {
            m_LevelNumber = levelNumber;
            Refresh();
        }

        /// <summary>Repaints from the current save without changing which level is bound.</summary>
        public void Refresh()
        {
            if (m_Root == null) return;

            if (m_LevelNumber <= 0)
            {
                m_Root.gameObject.SetActive(false);
                return;
            }

            LevelCollectableService.LevelCollectables held = LevelCollectableService.For(m_LevelNumber);

            // A level that hides nothing shows nothing — an icon on every segment would make the
            // levels that do hide something stop standing out.
            if (!held.Any)
            {
                m_Root.gameObject.SetActive(false);
                return;
            }

            m_Root.gameObject.SetActive(true);
            m_Root.anchoredPosition = m_LineAnchor;

            // Laid out from the count rather than fixed slots: one collectable draws centred on
            // the line instead of hanging off to one side of a gap that has nothing in it.
            int total = held.Total;
            int placed = 0;

            if (held.HasRobotPart)
            {
                PlaceIcon(m_RobotPartIcon, IconOffset(placed++, total),
                          ResolveRobotPartIcon(held.Robot, held.PartIndex), held.RobotPartCollected);
            }
            else
            {
                Hide(m_RobotPartIcon);
            }

            if (held.HasMemoryShard)
            {
                PlaceIcon(m_MemoryShardIcon, IconOffset(placed, total),
                          LevelCollectableService.MemoryShardIcon(), held.MemoryShardCollected);
            }
            else
            {
                Hide(m_MemoryShardIcon);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>Horizontal offset of the <paramref name="index"/>th of <paramref name="total"/> icons, centred as a group.</summary>
        private float IconOffset(int index, int total) => (index - (total - 1) * 0.5f) * m_IconGap;

        /// <summary>
        /// The icon for the part slot: this robot's stand-in when one is set, otherwise the part's
        /// own artwork. See <see cref="RobotIconOverride"/> for why the stand-in exists.
        /// </summary>
        private Sprite ResolveRobotPartIcon(RobotId robot, int partIndex)
        {
            if (m_RobotIcons != null)
            {
                for (int i = 0; i < m_RobotIcons.Length; i++)
                {
                    RobotIconOverride entry = m_RobotIcons[i];
                    if (entry != null && entry.robot == robot && entry.icon != null) return entry.icon;
                }
            }

            return LevelCollectableService.RobotPartIcon(robot, partIndex);
        }

        private void PlaceIcon(Image icon, float x, Sprite sprite, bool collected)
        {
            if (icon == null) return;

            if (sprite == null)
            {
                Hide(icon);
                return;
            }

            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = collected ? m_CollectedTint : m_UncollectedTint;

            var rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(m_IconSize, m_IconSize);
        }

        private static void Hide(Image icon)
        {
            if (icon != null) icon.gameObject.SetActive(false);
        }

        private void Subscribe()
        {
            if (m_Subscribed) return;
            m_Subscribed = true;
            LevelCollectableService.OnProgressChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (!m_Subscribed) return;
            m_Subscribed = false;
            LevelCollectableService.OnProgressChanged -= Refresh;
        }

        #endregion
    }
}
