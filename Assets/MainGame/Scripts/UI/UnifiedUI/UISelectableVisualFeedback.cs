using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Attach this script to your Settings row GameObjects (e.g. Master Volume, Music) or their buttons.
    /// It swaps the target Image sprite and shows/hides selection elements when the setting gains or loses focus.
    /// </summary>
    public class UISelectableVisualFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Sprite Settings")]
        [Tooltip("The Image component (e.g., the button background image) whose sprite will change.")]
        [SerializeField] private Image m_TargetImage;

        [Tooltip("Sprite used when this setting row is selected/hovered.")]
        [SerializeField] private Sprite m_SelectedSprite;

        [Tooltip("Sprite used when this setting row is in its normal state.")]
        [SerializeField] private Sprite m_NormalSprite;

        [Header("Pointers")]
        [Tooltip("Optional selection pointer GameObject on the left of this row.")]
        [SerializeField] private GameObject m_LeftPointer;

        private void Awake()
        {
            if (m_TargetImage == null)
            {
                m_TargetImage = GetComponent<Image>();
            }

            // Start in normal state
            SetSelectedState(false);
        }

        private void OnDisable()
        {
            SetSelectedState(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetSelectedState(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelectedState(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                SetSelectedState(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                SetSelectedState(false);
            }
        }

        private void SetSelectedState(bool selected)
        {
            if (m_TargetImage != null)
            {
                Sprite newSprite = selected ? m_SelectedSprite : m_NormalSprite;
                if (newSprite != null)
                {
                    m_TargetImage.sprite = newSprite;
                }
            }

            if (m_LeftPointer != null)
            {
                m_LeftPointer.SetActive(selected);
            }
        }
    }
}
