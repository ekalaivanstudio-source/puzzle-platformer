using UnityEngine;
using UnityEngine.EventSystems;

namespace TutorialSystem
{
    /// <summary>
    /// A drop-in convenience component that fires a <see cref="TutorialEventBus"/> event without any
    /// scripting. Use it to complete WaitForObjectInteraction / CustomEvent steps for objects that
    /// don't already raise their own events.
    ///
    /// Ways to fire:
    ///   • Hook <see cref="Fire()"/> to a Button's OnClick / any UnityEvent in the inspector.
    ///   • Tick "Fire On Pointer Click" to fire when the object (UI or 3D with a collider + the
    ///     PhysicsRaycaster) is clicked/tapped.
    ///   • Call <see cref="Fire()"/> from your own code.
    ///
    /// The event id defaults to the sibling <see cref="TutorialTarget"/>'s id when left blank, so a
    /// step that targets this object "just works" without typing the id twice.
    /// </summary>
    public class TutorialEventTrigger : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Event id to fire. If blank, uses the sibling TutorialTarget's id.")]
        [SerializeField] private string m_EventId = "";

        [Tooltip("Also fire automatically when this object is clicked/tapped.")]
        [SerializeField] private bool m_FireOnPointerClick = false;

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(m_EventId)) return m_EventId;
            TutorialTarget t = GetComponent<TutorialTarget>();
            return t != null ? t.TargetId : null;
        }

        /// <summary>Fires the configured event id (or the sibling target's id).</summary>
        public void Fire() => TutorialEventBus.Fire(ResolveId());

        /// <summary>Fires an explicit event id, ignoring the configured one.</summary>
        public void Fire(string eventId) => TutorialEventBus.Fire(eventId);

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            if (m_FireOnPointerClick) Fire();
        }
    }
}
