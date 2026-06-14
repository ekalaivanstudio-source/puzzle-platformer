using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// A generic, reusable yes/no confirmation dialog. It carries NO knowledge of what is being
    /// confirmed — callers pass the text and the callbacks, so the same prefab serves "Quit?",
    /// "Delete save?", "Reset settings?", "Restart level?", etc.
    ///
    /// Usage:
    /// <code>
    /// confirmationPopup.Show(
    ///     "Quit Game",
    ///     "Are you sure you want to quit the game?",
    ///     onYes: ApplicationQuit,
    ///     onNo:  null,                 // null = just close
    ///     yesLabel: "Yes", noLabel: "No");
    /// </code>
    ///
    /// It extends <see cref="UIPanel"/> so it shows/hides through the same fade pipeline as every
    /// other screen, but it is meant to overlay the current panel rather than replace it.
    /// </summary>
    public class ConfirmationPopup : UIPanel
    {
        [Header("Popup References")]
        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_MessageText;
        [SerializeField] private Button m_YesButton;
        [SerializeField] private Button m_NoButton;
        [SerializeField] private TextMeshProUGUI m_YesLabel;
        [SerializeField] private TextMeshProUGUI m_NoLabel;

        private Action m_OnYes;
        private Action m_OnNo;

        protected override void Awake()
        {
            base.Awake();
            if (m_YesButton != null) m_YesButton.onClick.AddListener(HandleYes);
            if (m_NoButton != null)  m_NoButton.onClick.AddListener(HandleNo);
        }

        /// <summary>
        /// Configures and shows the popup. Callbacks may be null (No defaults to "just close").
        /// </summary>
        public void Show(string title, string message, Action onYes, Action onNo = null,
                         string yesLabel = "Yes", string noLabel = "No")
        {
            m_OnYes = onYes;
            m_OnNo = onNo;

            if (m_TitleText != null) m_TitleText.text = title;
            if (m_MessageText != null) m_MessageText.text = message;
            if (m_YesLabel != null) m_YesLabel.text = yesLabel;
            if (m_NoLabel != null) m_NoLabel.text = noLabel;

            Show();
        }

        private void HandleYes()
        {
            AudioManager.Instance?.PlayButton();
            Action cb = m_OnYes;
            Hide();
            cb?.Invoke();
        }

        private void HandleNo()
        {
            AudioManager.Instance?.PlayButton();
            Action cb = m_OnNo;
            Hide();
            cb?.Invoke();
        }
    }
}
