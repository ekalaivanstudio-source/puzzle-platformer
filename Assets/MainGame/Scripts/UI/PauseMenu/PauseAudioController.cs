using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.PauseMenu
{
    /// <summary>
    /// Synchronized audio controller for the Pause Menu system.
    /// Routes all audio directly through the project's existing UIFeedbackAudio
    /// and AudioManager systems with pitch variation and anti-fatigue debouncing.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseAudioController : MonoBehaviour
    {
        [Header("Custom Overrides (Optional)")]
        [Tooltip("Optional custom clip for Pause Menu entrance.")]
        [SerializeField] private AudioClip m_CustomOpenClip;

        [Tooltip("Optional custom clip for Pause Menu exit/resume.")]
        [SerializeField] private AudioClip m_CustomCloseClip;

        public void PlayPauseOpen()
        {
            if (m_CustomOpenClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(m_CustomOpenClip);
                return;
            }

            UIFeedbackAudio.PlaySfx(UISfxType.PopupSlam, 1.0f, 0.02f);
        }

        public void PlayPauseClose()
        {
            if (m_CustomCloseClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUi(m_CustomCloseClip);
                return;
            }

            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.9f, 0.02f);
        }

        public void PlayFocusMove()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Navigate, 0.85f, 0.03f);
        }

        public void PlayButtonConfirm()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Confirm, 1.0f, 0.02f);
        }

        public void PlayWarningPopup()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.WarningAlert, 1.0f, 0.02f);
        }

        public void PlayConfirmYes()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Confirm, 1.0f, 0.02f);
        }

        public void PlayCancelNo()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Back, 0.95f, 0.02f);
        }

        public void PlayLevelSelectionOpen()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.9f, 0.02f);
        }

        public void PlayLevelSelected()
        {
            UIFeedbackAudio.PlaySfx(UISfxType.MajorTransitionImpact, 1.0f, 0.02f);
        }
    }
}
