using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// The Collections screen controller. Generates a tab per Robo from the
    /// <see cref="CollectionDatabase"/>, shows the selected Robo's parts in a grid (collected art
    /// vs silhouette), and displays per-Robo completion. Locked Robos show a lock and can't be
    /// opened. It auto-refreshes when collection progress changes (e.g. a part collected in-game),
    /// and never destroys panels — only its dynamically spawned tabs/slots.
    /// </summary>
    public class CollectionsPanelUI : UIPanel
    {
        [Header("Data")]
        [SerializeField] private CollectionDatabase m_Database;

        [Header("Tabs")]
        [SerializeField] private RoboTabUI m_TabPrefab;
        [SerializeField] private Transform m_TabContainer;

        [Header("Parts Grid")]
        [SerializeField] private PartSlotUI m_PartSlotPrefab;
        [SerializeField] private Transform m_PartContainer;

        [Header("Header / Progress")]
        [SerializeField] private TextMeshProUGUI m_RoboTitleText;
        [SerializeField] private TextMeshProUGUI m_CompletionText;
        [SerializeField] private Image m_CompletionBarFill;

        [Header("Navigation")]
        [SerializeField] private Button m_BackButton;

        private readonly List<RoboTabUI> m_Tabs = new List<RoboTabUI>();
        private readonly List<PartSlotUI> m_Slots = new List<PartSlotUI>();
        private RoboData m_CurrentRobo;
        private bool m_TabsBuilt;

        protected override void Awake()
        {
            base.Awake();
            if (m_BackButton != null) m_BackButton.onClick.AddListener(OnBack);
        }

        private void OnEnable() => CollectionSaveManager.OnChanged += RefreshAll;
        private void OnDisable() => CollectionSaveManager.OnChanged -= RefreshAll;

        protected override void OnShow()
        {
            CollectionManager.Configure(m_Database);
            BuildTabs();

            // Default to the first unlocked Robo (or the current one if still valid).
            if (m_CurrentRobo == null || !CollectionManager.IsRoboUnlocked(m_CurrentRobo.RoboId))
                m_CurrentRobo = FirstUnlockedRobo();

            RefreshAll();
        }

        private void BuildTabs()
        {
            if (m_TabsBuilt || m_Database == null || m_TabPrefab == null || m_TabContainer == null) return;

            for (int i = 0; i < m_Database.Count; i++)
            {
                RoboData robo = m_Database.GetByIndex(i);
                if (robo == null) continue;

                RoboTabUI tab = Instantiate(m_TabPrefab, m_TabContainer);
                tab.name = $"Tab_{robo.RoboId}";
                m_Tabs.Add(tab);
            }
            m_TabsBuilt = true;
        }

        /// <summary>Refreshes tab states and the currently shown Robo's grid + progress.</summary>
        private void RefreshAll()
        {
            if (m_Database == null) return;

            for (int i = 0; i < m_Tabs.Count; i++)
            {
                RoboData robo = m_Database.GetByIndex(i);
                if (robo == null) continue;

                bool unlocked = CollectionManager.IsRoboUnlocked(robo.RoboId);
                m_Tabs[i].Setup(robo, unlocked,
                    CollectionManager.GetCollectedCount(robo), robo.PartCount, OnTabClicked);
                m_Tabs[i].SetSelected(m_CurrentRobo != null && robo.RoboId == m_CurrentRobo.RoboId);
            }

            ShowRobo(m_CurrentRobo);
        }

        private void OnTabClicked(RoboData robo)
        {
            if (robo == null || !CollectionManager.IsRoboUnlocked(robo.RoboId)) return;
            AudioManager.Instance?.PlayButton();
            m_CurrentRobo = robo;
            RefreshAll();
        }

        /// <summary>Rebuilds the part grid for a Robo and updates the header/progress.</summary>
        private void ShowRobo(RoboData robo)
        {
            ClearSlots();
            if (robo == null) return;

            int collected = CollectionManager.GetCollectedCount(robo);
            int total = robo.PartCount;

            if (m_RoboTitleText != null) m_RoboTitleText.text = robo.RoboName;
            if (m_CompletionText != null)
            {
                int percent = total > 0 ? Mathf.RoundToInt(100f * collected / total) : 0;
                m_CompletionText.text = $"{percent}%  ({collected}/{total})";
            }
            if (m_CompletionBarFill != null)
                m_CompletionBarFill.fillAmount = CollectionManager.GetCompletion(robo);

            if (m_PartSlotPrefab == null || m_PartContainer == null) return;
            foreach (RobotPartData part in robo.Parts)
            {
                if (part == null) continue;
                PartSlotUI slot = Instantiate(m_PartSlotPrefab, m_PartContainer);
                slot.Setup(part, CollectionManager.IsPartCollected(robo.RoboId, part.PartId));
                m_Slots.Add(slot);
            }
        }

        private RoboData FirstUnlockedRobo()
        {
            for (int i = 0; i < m_Database.Count; i++)
            {
                RoboData r = m_Database.GetByIndex(i);
                if (r != null && CollectionManager.IsRoboUnlocked(r.RoboId)) return r;
            }
            return m_Database.FirstRobo;
        }

        private void ClearSlots()
        {
            foreach (PartSlotUI slot in m_Slots)
                if (slot != null) Destroy(slot.gameObject);
            m_Slots.Clear();
        }

        private void OnBack()
        {
            AudioManager.Instance?.PlayButton();
            ScreenManager.Instance?.Back();
        }
    }
}
