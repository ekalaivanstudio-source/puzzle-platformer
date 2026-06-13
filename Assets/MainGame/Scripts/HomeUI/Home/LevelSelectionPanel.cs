using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Thin <see cref="UIPanel"/> wrapper that hosts the existing
    /// <see cref="LevelSelectionSystem.LevelSelectionUI"/> as an in-screen panel, so Play
    /// transitions to level selection without a scene load. The level grid (build + unlock logic)
    /// is entirely owned by LevelSelectionUI in this panel's hierarchy; this wrapper only adds a
    /// Back button to return to the Home screen.
    ///
    /// Selecting an unlocked level still loads that level's scene (handled by LevelSelectionUI).
    /// </summary>
    public class LevelSelectionPanel : UIPanel
    {
        [SerializeField] private Button m_BackButton;

        protected override void Awake()
        {
            base.Awake();
            if (m_BackButton != null) m_BackButton.onClick.AddListener(OnBack);
        }

        private void OnBack()
        {
            AudioManager.Instance?.PlayButton();
            ScreenManager.Instance?.Back();
        }
    }
}
