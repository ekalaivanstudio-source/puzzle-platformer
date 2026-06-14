using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace TutorialSystem
{
    /// <summary>
    /// The brain of the tutorial system. It is the ONLY component your game talks to:
    ///
    /// <code>
    /// TutorialManager.Instance.PlaySequence(myTutorialAsset);
    /// </code>
    ///
    /// For each step it: resolves the Target Id → live object, drives the
    /// <see cref="TutorialHighlightSystem"/>, <see cref="TutorialArrowController"/> and
    /// <see cref="TutorialPopupUI"/>, then waits for the step's completion condition
    /// (tap / button click / <see cref="TutorialEventBus"/> event), saves progress, and moves on.
    ///
    /// It is a persistent singleton (survives scene loads). Drop one in your boot scene — or just
    /// run Tools ▸ Tutorial System ▸ Setup Tutorial System, which creates and wires everything.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [Header("Wiring (auto-filled by the setup tool)")]
        [Tooltip("RectTransform of the Screen Space Overlay tutorial canvas.")]
        [SerializeField] private RectTransform m_CanvasRect;
        [SerializeField] private TutorialPopupUI m_Popup;
        [SerializeField] private TutorialArrowController m_Arrow;
        [SerializeField] private TutorialHighlightSystem m_Highlight;

        [Tooltip("Camera used to project WORLD-space targets to screen. Leave empty to use " +
                 "Camera.main automatically (re-resolved per frame, so scene changes are fine).")]
        [SerializeField] private Camera m_WorldCamera;

        [Header("Behaviour")]
        [Tooltip("Tutorials to auto-play when this manager starts (respects PlayOnce / save data). " +
                 "Played in order.")]
        [SerializeField] private List<TutorialSequenceData> m_PlayOnStart = new List<TutorialSequenceData>();

        [Tooltip("How long to wait (seconds) for a step's Target Id to appear before giving up and " +
                 "showing the message without a target.")]
        [SerializeField] private float m_TargetResolveTimeout = 5f;

        [Tooltip("Keep this manager (and its canvas) alive across scene loads.")]
        [SerializeField] private bool m_Persistent = true;

        [Tooltip("While any tutorial step is on screen, block GAMEPLAY input (move / jump / " +
                 "interact / submit, etc.). The UI clicks the tutorial itself requires still work. " +
                 "Input code reads this via the static TutorialManager.GameplayInputBlocked flag.")]
        [SerializeField] private bool m_BlockGameplayInput = true;

        // ─── Singleton ────────────────────────────────────────────────────────────
        public static TutorialManager Instance { get; private set; }

        /// <summary>
        /// True while a tutorial is on screen and that tutorial blocks gameplay input. Static so any
        /// input code can gate on it without an Instance reference; reads false when no tutorial is
        /// running. <see cref="DeviceInputProvider"/> already checks this.
        /// </summary>
        public static bool GameplayInputBlocked { get; private set; }

        // Clear statics when entering play mode with "Reload Domain" disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            GameplayInputBlocked = false;
        }

        // ─── Public events (for analytics, gating input, SFX, etc.) ─────────────────
        /// <summary>Raised when a sequence begins playing.</summary>
        public event Action<TutorialSequenceData> OnSequenceStarted;
        /// <summary>Raised when a sequence finishes (or is stopped). Bool = completed fully.</summary>
        public event Action<TutorialSequenceData, bool> OnSequenceEnded;
        /// <summary>Raised when a step starts. Args: sequence, step index.</summary>
        public event Action<TutorialSequenceData, int> OnStepStarted;

        /// <summary>True while any tutorial step is on screen.</summary>
        public bool IsPlaying { get; private set; }

        private readonly Queue<TutorialSequenceData> m_Queue = new Queue<TutorialSequenceData>();
        private Coroutine m_Driver;
        private bool m_StepComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (m_Persistent) DontDestroyOnLoad(gameObject);

            if (m_Arrow != null) m_Arrow.Initialize(m_CanvasRect, m_WorldCamera);
            if (m_Highlight != null) m_Highlight.Initialize(m_WorldCamera);
            if (m_Popup != null) m_Popup.Initialize(m_CanvasRect, m_WorldCamera);
        }

        private void Start()
        {
            foreach (TutorialSequenceData seq in m_PlayOnStart)
                PlaySequence(seq);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Queues a tutorial to play (or plays it now if idle). Skipped silently if it is a
        /// PlayOnce tutorial that has already been completed, unless <paramref name="force"/>.
        /// </summary>
        public void PlaySequence(TutorialSequenceData sequence, bool force = false)
        {
            if (sequence == null) return;
            if (!force && sequence.PlayOnce && TutorialSaveSystem.IsCompleted(sequence.SequenceId))
                return;

            m_Queue.Enqueue(sequence);
            if (m_Driver == null) m_Driver = StartCoroutine(DriveQueue());
        }

        /// <summary>Stops the current tutorial immediately and clears the queue. Does NOT mark complete.</summary>
        public void StopAll()
        {
            m_Queue.Clear();
            if (m_Driver != null) { StopCoroutine(m_Driver); m_Driver = null; }
            CleanUpUI();
            IsPlaying = false;
        }

        /// <summary>Clears saved progress for a tutorial so it can play again (QA / replay button).</summary>
        public void ResetProgress(TutorialSequenceData sequence)
        {
            if (sequence != null) TutorialSaveSystem.ResetSequence(sequence.SequenceId);
        }

        // ─── Queue driver ────────────────────────────────────────────────────────────

        private IEnumerator DriveQueue()
        {
            while (m_Queue.Count > 0)
            {
                TutorialSequenceData seq = m_Queue.Dequeue();
                yield return RunSequence(seq);
            }
            m_Driver = null;
        }

        private IEnumerator RunSequence(TutorialSequenceData seq)
        {
            IsPlaying = true;
            OnSequenceStarted?.Invoke(seq);

            int startIndex = seq.Resumable ? Mathf.Clamp(TutorialSaveSystem.GetResumeIndex(seq.SequenceId), 0, seq.StepCount) : 0;

            for (int i = startIndex; i < seq.StepCount; i++)
            {
                TutorialStepData step = seq.GetStep(i);
                if (step == null) continue;

                OnStepStarted?.Invoke(seq, i);
                yield return RunStep(step);

                if (seq.Resumable) TutorialSaveSystem.SetResumeIndex(seq.SequenceId, i + 1);
            }

            TutorialSaveSystem.MarkCompleted(seq.SequenceId);
            CleanUpUI();
            IsPlaying = false;
            OnSequenceEnded?.Invoke(seq, true);
        }

        // ─── Single step ──────────────────────────────────────────────────────────────

        private IEnumerator RunStep(TutorialStepData step)
        {
            // Block gameplay input while this step is on screen — EXCEPT for event-driven steps
            // (WaitForObjectInteraction / DragAndDrop / CustomEvent), where the player must be able
            // to act in-world to complete the step. UI clicks the tutorial needs always work because
            // they go through the tutorial canvas, not the gameplay input path.
            GameplayInputBlocked = m_BlockGameplayInput && !step.IsEventDriven;

            // 1) Resolve the target (waiting for it to spawn, up to the timeout).
            TutorialTarget target = null;
            if (step.HasTarget)
            {
                float waited = 0f;
                while ((target = TutorialTargetRegistry.Get(step.TargetId)) == null &&
                       waited < m_TargetResolveTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (target == null)
                    Debug.LogWarning($"[TutorialManager] Target '{step.TargetId}' never appeared; " +
                                     $"showing the message without a target.");
            }

            // 2) Drive the visuals. The arrow & highlight self-follow the target every frame.
            if (m_Highlight != null)
                m_Highlight.Show(target, step.DimBackground, step.ShowHighlight, step.HighlightPadding);
            if (m_Arrow != null)
            {
                if (step.ShowArrow && target != null) m_Arrow.Follow(target);
                else m_Arrow.Hide();
            }
            if (m_Popup != null)
            {
                bool showNext = !RequiresExternalAction(step);
                m_Popup.Show(step.CharacterSprite, step.Message, step.PopupAnchor, target, showNext);
            }

            // 3) Wait for the step's completion condition.
            m_StepComplete = false;
            yield return WaitForCompletion(step, target);
        }

        private IEnumerator WaitForCompletion(TutorialStepData step, TutorialTarget target)
        {
            void Complete() => m_StepComplete = true;

            switch (step.ActionType)
            {
                case TutorialActionType.WaitForButtonClick:
                {
                    Button btn = target != null ? target.GetComponentInChildren<Button>() : null;
                    if (btn == null)
                    {
                        Debug.LogWarning($"[TutorialManager] WaitForButtonClick step targets " +
                            $"'{step.TargetId}', which has no Button. Falling back to tap-to-continue.");
                        yield return WaitForTap(step, Complete);
                        yield break;
                    }
                    if (m_Highlight != null) m_Highlight.SetTapToContinue(false, null);
                    btn.onClick.AddListener(Complete);
                    while (!m_StepComplete) yield return null;
                    btn.onClick.RemoveListener(Complete);
                    break;
                }

                case TutorialActionType.WaitForObjectInteraction:
                case TutorialActionType.DragAndDrop:
                case TutorialActionType.CustomEvent:
                {
                    string wanted = step.CompletionEventId;
                    void OnEvt(string id) { if (id == wanted) Complete(); }
                    if (m_Highlight != null) m_Highlight.SetTapToContinue(false, null);
                    TutorialEventBus.OnEvent += OnEvt;
                    while (!m_StepComplete) yield return null;
                    TutorialEventBus.OnEvent -= OnEvt;
                    break;
                }

                default: // PopupOnly / Highlight
                    yield return WaitForTap(step, Complete);
                    break;
            }
        }

        /// <summary>Advances when the player taps the dim / Next button, or after an auto-delay.</summary>
        private IEnumerator WaitForTap(TutorialStepData step, Action complete)
        {
            void OnNext() => complete();
            if (m_Highlight != null) m_Highlight.SetTapToContinue(step.AllowTapToContinue, complete);
            if (m_Popup != null) m_Popup.OnNextClicked += OnNext;

            float t = 0f;
            bool firstFrame = true;
            while (!m_StepComplete)
            {
                // Keyboard shortcut: pressing "E" advances the same as clicking the Next button.
                // Skip the frame this step first appears on, otherwise a single press would still
                // read as "pressed this frame" in the next step and skip it (E key stays "pressed"
                // for the whole frame, and the next step's wait begins on that same frame).
                if (!firstFrame)
                {
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    {
                        complete();
                        break;
                    }
                }
                firstFrame = false;

                if (step.AutoAdvanceDelay > 0f)
                {
                    t += Time.unscaledDeltaTime;
                    if (t >= step.AutoAdvanceDelay) break;
                }
                yield return null;
            }

            if (m_Popup != null) m_Popup.OnNextClicked -= OnNext;
            if (m_Highlight != null) m_Highlight.SetTapToContinue(false, null);
        }

        /// <summary>True if the step waits on the player doing something other than a tap/Next.</summary>
        private static bool RequiresExternalAction(TutorialStepData step)
        {
            return step.ActionType == TutorialActionType.WaitForButtonClick ||
                   step.IsEventDriven;
        }

        private void CleanUpUI()
        {
            GameplayInputBlocked = false;
            if (m_Highlight != null) { m_Highlight.SetTapToContinue(false, null); m_Highlight.HideAll(); }
            if (m_Arrow != null) m_Arrow.Hide();
            if (m_Popup != null) m_Popup.Hide();
        }
    }
}
