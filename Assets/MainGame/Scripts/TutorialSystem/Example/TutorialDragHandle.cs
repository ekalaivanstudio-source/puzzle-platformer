using UnityEngine;
using UnityEngine.EventSystems;

namespace TutorialSystem
{
    /// <summary>
    /// Minimal, reusable drag-and-drop helper for DragAndDrop tutorial steps.
    ///
    /// Put it on a draggable UI element, set a drop zone, and an event id. While dragging, the
    /// element follows the pointer; on release, if it is within <see cref="m_DropRadius"/> of the
    /// drop zone it fires the event (completing the matching DragAndDrop step) and optionally snaps
    /// into place. If the drop misses, it springs back to its start position.
    ///
    /// This is intentionally simple example code — extend or replace it with your own inventory /
    /// board logic; the only contract the tutorial cares about is "fire the event id on success".
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TutorialDragHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Where this item must be dropped to succeed.")]
        [SerializeField] private RectTransform m_DropZone;

        [Tooltip("Max distance (canvas px) from the drop zone center that still counts as a hit.")]
        [SerializeField] private float m_DropRadius = 120f;

        [Tooltip("Event id fired on a successful drop. If blank, uses the sibling TutorialTarget id.")]
        [SerializeField] private string m_EventId = "";

        [Tooltip("Snap the item onto the drop zone on success (otherwise it stays where dropped).")]
        [SerializeField] private bool m_SnapOnSuccess = true;

        private RectTransform m_Rect;
        private Canvas m_Canvas;
        private Vector2 m_StartPos;

        private void Awake()
        {
            m_Rect = (RectTransform)transform;
            m_Canvas = GetComponentInParent<Canvas>();
            m_StartPos = m_Rect.anchoredPosition;
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData e)
        {
            m_StartPos = m_Rect.anchoredPosition;
        }

        void IDragHandler.OnDrag(PointerEventData e)
        {
            float scale = m_Canvas != null ? m_Canvas.scaleFactor : 1f;
            m_Rect.anchoredPosition += e.delta / scale;
        }

        void IEndDragHandler.OnEndDrag(PointerEventData e)
        {
            bool hit = m_DropZone != null &&
                       Vector2.Distance(m_Rect.position, m_DropZone.position) <=
                       m_DropRadius * (m_Canvas != null ? m_Canvas.scaleFactor : 1f);

            if (hit)
            {
                if (m_SnapOnSuccess && m_DropZone != null) m_Rect.position = m_DropZone.position;
                TutorialEventBus.Fire(ResolveId());
            }
            else
            {
                m_Rect.anchoredPosition = m_StartPos; // missed — spring back.
            }
        }

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(m_EventId)) return m_EventId;
            TutorialTarget t = GetComponent<TutorialTarget>();
            return t != null ? t.TargetId : null;
        }
    }
}
