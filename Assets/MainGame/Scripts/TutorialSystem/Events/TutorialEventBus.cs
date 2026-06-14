using System;
using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// A tiny, global publish/subscribe channel that decouples gameplay from the tutorial.
    ///
    /// Gameplay code never needs to know a tutorial is running. When something happens
    /// ("key collected", "brick pushed", "boost purchased"), it simply fires a named event:
    ///
    /// <code>TutorialEventBus.Fire("collected_first_key");</code>
    ///
    /// The <see cref="TutorialManager"/> listens here to complete WaitForObjectInteraction /
    /// DragAndDrop / CustomEvent steps. Because it's a static bus there is nothing to wire in the
    /// inspector and no scene reference to break.
    /// </summary>
    public static class TutorialEventBus
    {
        /// <summary>Raised when any event is fired. Argument is the event id.</summary>
        public static event Action<string> OnEvent;

        /// <summary>Fires an event by id. Safe to call even if nothing is listening.</summary>
        public static void Fire(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            OnEvent?.Invoke(eventId);
        }

        /// <summary>Removes all subscribers. Call on play-mode exit if domain reload is disabled.</summary>
        public static void Reset() => OnEvent = null;

        // Belt-and-braces: when entering play mode with "Reload Domain" turned off, static events
        // keep their old subscribers. Clearing here guarantees a clean bus each session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnEvent = null;
    }
}
