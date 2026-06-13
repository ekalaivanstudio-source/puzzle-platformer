using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// View for one Robo tab. Shows the Robo name/icon and completion, a lock icon when locked
    /// (and becomes non-interactable), and a selected-highlight. Reports clicks via a callback.
    /// Pure view — unlock/percent are computed by <see cref="CollectionManager"/> and passed in.
    /// </summary>
    public class RoboTabUI : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private TextMeshProUGUI m_ProgressText;
        [SerializeField] private Image m_IconImage;
        [SerializeField] private GameObject m_LockIcon;
        [SerializeField] private GameObject m_SelectedHighlight;

        private RoboData m_Robo;
        private Action<RoboData> m_OnClicked;

        private void Awake()
        {
            if (m_Button == null) m_Button = GetComponent<Button>();
            if (m_Button != null) m_Button.onClick.AddListener(() => m_OnClicked?.Invoke(m_Robo));
        }

        /// <summary>Paints the tab from data + computed progress.</summary>
        public void Setup(RoboData robo, bool unlocked, int collected, int total, Action<RoboData> onClicked)
        {
            m_Robo = robo;
            m_OnClicked = onClicked;

            if (m_NameText != null) m_NameText.text = robo.RoboName;
            if (m_IconImage != null && robo.Icon != null) m_IconImage.sprite = robo.Icon;
            if (m_ProgressText != null)
                m_ProgressText.text = unlocked ? $"{collected}/{total}" : "Locked";

            if (m_Button != null) m_Button.interactable = unlocked;
            if (m_LockIcon != null) m_LockIcon.SetActive(!unlocked);
        }

        /// <summary>Toggles the selected-tab highlight.</summary>
        public void SetSelected(bool selected)
        {
            if (m_SelectedHighlight != null) m_SelectedHighlight.SetActive(selected);
        }
    }
}
