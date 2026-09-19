using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UI.Feedback
{
    /// <summary>
    /// Attach to any selectable UI element (Button, Toggle, etc.) to give it
    /// responsive, controller-first tactile feedback:
    /// - Controller/Keyboard selection: Shifts -4px toward player, slight angular tilt (+1.2 deg), subtle scale lift, soft mechanical tick SFX.
    /// - Mouse Hover: Ignored to preserve controller-first focus hierarchy.
    /// - Confirm / Click: Squash (-6%), forward punch (+6px), spark burst particles, crisp confirm SFX.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    [DisallowMultipleComponent]
    public class UIButtonFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [Header("Feedback Presets")]
        [SerializeField] private bool m_UseCustomPresets = false;
        [SerializeField] private UIFeedbackPreset m_FocusPreset;
        [SerializeField] private UIFeedbackPreset m_ConfirmPreset;

        [Header("Options")]
        [Tooltip("When true, plays Back SFX/feedback instead of Confirm.")]
        [SerializeField] private bool m_IsBackButton = false;

        private RectTransform m_RectTransform;
        private Selectable m_Selectable;

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Selectable = GetComponent<Selectable>();

            if (!m_UseCustomPresets || m_FocusPreset == null)
            {
                m_FocusPreset = UIFeedbackPreset.CreateFocusPreset();
            }

            if (!m_UseCustomPresets || m_ConfirmPreset == null)
            {
                m_ConfirmPreset = m_IsBackButton ? UIFeedbackPreset.CreateBackPreset() : UIFeedbackPreset.CreateConfirmPreset();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (m_Selectable != null && !m_Selectable.interactable) return;

            // Trigger focus feedback (controller/keyboard navigation)
            if (UIFeedbackController.Instance != null && m_FocusPreset != null)
            {
                UIFeedbackController.Instance.PlayFeedback(m_RectTransform, m_FocusPreset);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            // State will smoothly restore via UIFeedbackController
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerConfirm();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            TriggerConfirm();
        }

        private void TriggerConfirm()
        {
            if (m_Selectable != null && !m_Selectable.interactable) return;

            if (UIFeedbackController.Instance != null && m_ConfirmPreset != null)
            {
                UIFeedbackController.Instance.PlayFeedback(m_RectTransform, m_ConfirmPreset);
            }
        }
    }
}
