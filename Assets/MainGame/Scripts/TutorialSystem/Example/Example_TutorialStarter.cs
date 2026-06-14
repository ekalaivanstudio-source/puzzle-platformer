using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// A tiny example showing the two things your game code ever needs to do:
    ///   1) start a tutorial, and
    ///   2) tell the tutorial when something happened.
    ///
    /// Drop this on any object, assign a sequence, and press Play. Then call
    /// <see cref="NotifyInteraction"/> (e.g. from a Key's <c>Interact()</c>, a button's OnClick, or
    /// anywhere in gameplay) to complete WaitForObjectInteraction / CustomEvent steps.
    ///
    /// Delete this file once you understand the pattern — it isn't part of the framework.
    /// </summary>
    public class Example_TutorialStarter : MonoBehaviour
    {
        [Tooltip("Tutorial to start. Leave the manager's own 'Play On Start' empty if you use this.")]
        [SerializeField] private TutorialSequenceData m_Sequence;

        [Tooltip("Start the tutorial automatically on Start().")]
        [SerializeField] private bool m_PlayOnStart = true;

        [Tooltip("If true, ignores the 'already completed' save so the tutorial always replays " +
                 "(handy while authoring).")]
        [SerializeField] private bool m_ForceReplay = false;

        private void Start()
        {
            if (m_PlayOnStart) Play();
        }

        /// <summary>Starts the assigned tutorial. Hook to a button to offer a "Replay tutorial" option.</summary>
        public void Play()
        {
            if (TutorialManager.Instance == null)
            {
                Debug.LogWarning("[Example_TutorialStarter] No TutorialManager in the scene. " +
                                 "Run Tools ▸ Tutorial System ▸ Setup Tutorial System.");
                return;
            }
            if (m_ForceReplay && m_Sequence != null)
                TutorialManager.Instance.ResetProgress(m_Sequence);
            TutorialManager.Instance.PlaySequence(m_Sequence, m_ForceReplay);
        }

        /// <summary>
        /// Call this from gameplay to complete an interaction/custom-event step. The id must match
        /// the step's Custom Event Id (or its Target Id, if no custom id was set).
        /// </summary>
        public void NotifyInteraction(string eventId) => TutorialEventBus.Fire(eventId);
    }
}
