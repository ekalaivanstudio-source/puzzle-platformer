using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TutorialSystem
{
    /// <summary>
    /// Sits on the dim strips and forwards taps to a runtime callback. This is how
    /// "tap anywhere to continue" works for PopupOnly / Highlight steps without hard-wiring a
    /// Button. The callback is set by <see cref="TutorialHighlightSystem"/> and is only honored
    /// while tapping-to-continue is enabled, so it never interferes with WaitForButtonClick steps.
    /// </summary>
    public class TutorialClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Invoked when the catcher is clicked while enabled.</summary>
        public Action OnClicked;

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke();
    }
}
