using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TutorialSystem;

/// <summary>
/// Plays a tutorial once the player has queued a set number of inputs this turn.
///
/// Drop one of these into each scene that needs it and configure it per scene:
///   • Level 1: Required Inputs = 4, Sequence = the "after first moves" tutorial.
///   • Other scenes: their own count and their own tutorial asset.
///
/// It watches <see cref="SequenceManager.OnSequenceChanged"/> (fired on every add / undo / clear)
/// and fires the first time <see cref="SequenceManager.SequenceLength"/> reaches the threshold.
/// No UI buttons required — works with the runtime-generated input prefabs.
/// </summary>
public class TutorialInputCountTrigger : MonoBehaviour
{
    [Header("Trigger condition")]
    [Tooltip("Number of queued inputs that pops the tutorial (e.g. 4 for level 1).")]
    [SerializeField] private int m_RequiredInputs = 4;

    [Tooltip("Fire only the first time the count is reached this play session.")]
    [SerializeField] private bool m_Once = true;

    [Tooltip("Seconds to wait (unscaled) after the count is reached before playing. 0 = immediately.")]
    [SerializeField] private float m_Delay = 0f;

    [Header("What to play (pick ONE route)")]
    [Tooltip("Tutorial to play directly. Leave empty if you instead route through a TutorialTrigger " +
             "via the event id below.")]
    [SerializeField] private TutorialSequenceData m_Sequence;

    [Tooltip("Ignore the 'already completed' save and always replay (handy while authoring).")]
    [SerializeField] private bool m_ForceReplay = false;

    [Tooltip("Optional: instead of (or as well as) playing a sequence directly, fire this " +
             "TutorialEventBus id — a TutorialTrigger configured with the same id will play.")]
    [SerializeField] private string m_FireEventId = "";

    [Tooltip("Raised when the count is reached — hook SFX, analytics, etc.")]
    [SerializeField] private UnityEvent m_OnReached;

    private bool m_HasFired;

    private void OnEnable()
    {
        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnSequenceChanged += OnSequenceChanged;
    }

    private void OnDisable()
    {
        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnSequenceChanged -= OnSequenceChanged;
    }

    private void Start()
    {
        // SequenceManager.Instance may have been null during OnEnable (Awake order) — ensure now.
        if (SequenceManager.Instance != null)
        {
            SequenceManager.Instance.OnSequenceChanged -= OnSequenceChanged;
            SequenceManager.Instance.OnSequenceChanged += OnSequenceChanged;
        }
    }

    private void OnSequenceChanged()
    {
        if (m_Once && m_HasFired) return;
        if (SequenceManager.Instance == null) return;
        if (SequenceManager.Instance.SequenceLength < m_RequiredInputs) return;

        m_HasFired = true;

        if (m_Delay > 0f) StartCoroutine(PlayAfterDelay());
        else Play();
    }

    private IEnumerator PlayAfterDelay()
    {
        yield return new WaitForSecondsRealtime(m_Delay);
        Play();
    }

    private void Play()
    {
        m_OnReached?.Invoke();

        if (!string.IsNullOrEmpty(m_FireEventId))
            TutorialEventBus.Fire(m_FireEventId);

        if (m_Sequence == null) return;

        if (TutorialManager.Instance == null)
        {
            Debug.LogWarning("[TutorialInputCountTrigger] No TutorialManager in the scene. " +
                             "Run Tools ▸ Tutorial System ▸ Setup Tutorial System.", this);
            return;
        }

        if (m_ForceReplay) TutorialManager.Instance.ResetProgress(m_Sequence);
        TutorialManager.Instance.PlaySequence(m_Sequence, m_ForceReplay);
    }

    /// <summary>Re-arms a one-shot trigger so it can fire again this session.</summary>
    public void ResetTrigger() => m_HasFired = false;
}
