using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// View for a single collectible part slot. Collected → shows the real artwork; uncollected →
    /// shows the silhouette (or a dim empty placeholder if no silhouette is authored). Pure view:
    /// it is told its state via <see cref="Setup"/> and renders accordingly.
    /// </summary>
    public class PartSlotUI : MonoBehaviour
    {
        [SerializeField] private Image m_PartImage;
        [SerializeField] private GameObject m_LockedOverlay;
        [SerializeField] private TextMeshProUGUI m_PartNameText;

        [Tooltip("Tint applied to the image when the part is not yet collected.")]
        [SerializeField] private Color m_UncollectedTint = new Color(0.1f, 0.1f, 0.1f, 1f);
        [Tooltip("Tint applied when collected (usually white = full color).")]
        [SerializeField] private Color m_CollectedTint = Color.white;

        /// <summary>Paints the slot for a part and whether it has been collected.</summary>
        public void Setup(RobotPartData part, bool collected)
        {
            if (m_PartNameText != null) m_PartNameText.text = part.PartName;

            if (m_PartImage != null)
            {
                if (collected)
                {
                    m_PartImage.sprite = part.CollectedSprite;
                    m_PartImage.color = m_CollectedTint;
                }
                else
                {
                    // Prefer an authored silhouette; otherwise dim the collected art as a fallback.
                    m_PartImage.sprite = part.SilhouetteSprite != null ? part.SilhouetteSprite : part.CollectedSprite;
                    m_PartImage.color = part.SilhouetteSprite != null ? Color.white : m_UncollectedTint;
                }
                m_PartImage.enabled = m_PartImage.sprite != null;
            }

            if (m_LockedOverlay != null) m_LockedOverlay.SetActive(!collected);
        }
    }
}
