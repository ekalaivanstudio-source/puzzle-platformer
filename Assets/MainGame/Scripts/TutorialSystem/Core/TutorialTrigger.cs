using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace TutorialSystem
{
    /// <summary>
    /// Drop-in component that plays a <see cref="TutorialSequenceData"/> "when needed" — from
    /// gameplay code, a UnityEvent / button, or a named <see cref="TutorialEventBus"/> event.
    /// This is the production-ready replacement for the Example_TutorialStarter.
    ///
    /// Typical "teach → let the player act → teach again" flow:
    ///   1) The first tutorial teaches the controls and finishes.
    ///   2) The player makes a move; your gameplay code fires
    ///      <c>TutorialEventBus.Fire("first_move_done");</c>
    ///   3) A <see cref="TutorialTrigger"/> configured with Play On Event Id = "first_move_done"
    ///      plays the next tutorial.
    ///
    /// You can also just call <see cref="Trigger()"/> directly (from code or a Button OnClick),
    /// or call <c>TutorialManager.Instance.PlaySequence(seq)</c> yourself if you don't need a
    /// scene component at all.
    /// </summary>
    public class TutorialTrigger : MonoBehaviour
    {
        [Header("What to play")]
        [Tooltip("The tutorial sequence to play when this trigger fires.")]
        [SerializeField] private TutorialSequenceData m_Sequence;

        [Tooltip("Ignore the 'already completed' save and always replay (handy while authoring).")]
        [SerializeField] private bool m_ForceReplay = false;

        [Tooltip("Fire at most once per play session, even if the trigger condition happens again. " +
                 "(Separate from PlayOnce, which is persisted across sessions on the sequence asset.)")]
        [SerializeField] private bool m_Once = true;

        [Header("When to play")]
        [Tooltip("Play automatically when this object's Start() runs.")]
        [SerializeField] private bool m_OnStart = false;

        [Tooltip("If set, this trigger plays when TutorialEventBus.Fire(<this id>) is called from " +
                 "gameplay code. Leave blank to disable event-bus triggering.")]
        [SerializeField] private string m_PlayOnEventId = "";

        [Tooltip("Seconds to wait (unscaled) after the trigger condition before playing. 0 = now.")]
        [SerializeField] private float m_Delay = 0f;

        [Tooltip("Raised right before the sequence is queued — hook SFX, analytics, camera moves, etc.")]
        [SerializeField] private UnityEvent m_OnTriggered;

        private bool m_HasFired;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(m_PlayOnEventId))
                TutorialEventBus.OnEvent += OnBusEvent;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(m_PlayOnEventId))
                TutorialEventBus.OnEvent -= OnBusEvent;
        }

        private void Start()
        {
            if (m_OnStart) Trigger();
        }

        private void OnBusEvent(string eventId)
        {
            if (eventId == m_PlayOnEventId) Trigger();
        }

        /// <summary>
        /// Plays the assigned tutorial. Safe to hook to a Button OnClick or any UnityEvent, or to
        /// call directly from gameplay code (e.g. after the player makes their first move).
        /// </summary>
        public void Trigger()
        {
            if (m_Once && m_HasFired) return;

            if (m_Sequence == null)
            {
                Debug.LogWarning("[TutorialTrigger] No sequence assigned.", this);
                return;
            }
            if (TutorialManager.Instance == null)
            {
                Debug.LogWarning("[TutorialTrigger] No TutorialManager in the scene. " +
                                 "Run Tools ▸ Tutorial System ▸ Setup Tutorial System.", this);
                return;
            }

            m_HasFired = true;
            m_OnTriggered?.Invoke();

            if (m_Delay > 0f) StartCoroutine(PlayAfterDelay());
            else Play();
        }

        /// <summary>Plays an explicit sequence instead of the configured one (code-only convenience).</summary>
        public void Trigger(TutorialSequenceData sequence)
        {
            m_Sequence = sequence;
            Trigger();
        }

        /// <summary>Re-arms a one-shot trigger so it can fire again this session.</summary>
        public void ResetTrigger() => m_HasFired = false;

        private IEnumerator PlayAfterDelay()
        {
            yield return new WaitForSecondsRealtime(m_Delay);
            Play();
        }

        private void Play()
        {
            if (m_ForceReplay) TutorialManager.Instance.ResetProgress(m_Sequence);
            TutorialManager.Instance.PlaySequence(m_Sequence, m_ForceReplay);
        }
    }
}
