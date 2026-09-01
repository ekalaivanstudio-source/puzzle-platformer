using System.Collections;
using UnityEngine;

/// <summary>
/// Teaches a tutorial level's solution one key at a time.
///
/// The solution comes from the level's <see cref="LevelConfig"/> via
/// <see cref="LevelSolution"/> — the same authored sequence the Auto Play tester runs — so a
/// tutorial can never drift out of step with the level it is teaching. For each queued
/// action the hint advances to the next action in the solution, and once every action is
/// queued it switches to the Enter hint that tells the player to execute.
///
/// A level may also declare intro hints (<c>tutorial.introHints</c>) — hints that loop
/// before the player's first input, for a mechanic the key hints cannot teach. The
/// move-brick level uses <see cref="TutorialAnimType.Push"/> that way.
///
/// Lives on the Tutorial Anim Canvas inside Managers, so every level carries it and only
/// the levels whose config sets <c>tutorial.showTutorial</c> ever show anything.
///
/// Runs after the default-order Starts so <see cref="PlayerInputUIHelper"/> has already
/// registered the level's correct sequence — the fallback <see cref="LevelSolution"/> uses
/// when a config has no authored solution.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(TutorialAnimationController))]
public class TutorialSequenceGuide : MonoBehaviour
{
    private TutorialAnimationController m_Anims;

    // This level's solution — one hint per entry, in order.
    private ActionTypeEnum[] m_Solution;

    private LevelConfig.TutorialSettings m_Settings;

    // True from the moment a turn is submitted until it ends. Hints stay off while the
    // sequence runs: nothing is being typed, so there is nothing to hint at.
    private bool m_IsExecuting;

    private Coroutine m_IntroRoutine;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Anims = GetComponent<TutorialAnimationController>();

        LevelConfig config = LevelContext.Instance != null ? LevelContext.Instance.Config : null;
        m_Settings = config != null ? config.tutorial : null;

        // Not a tutorial level — switch the whole canvas off before it ever draws a frame.
        if (m_Settings == null || !m_Settings.showTutorial)
        {
            gameObject.SetActive(false);
            return;
        }

        m_Anims.TurnOffAllAnimations();
    }

    private void Start()
    {
        if (!LevelSolution.TryResolve(out m_Solution, out string reason))
        {
            Debug.LogWarning($"[TutorialSequenceGuide] No sequence to teach — {reason}", this);
            gameObject.SetActive(false);
            return;
        }

        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnSequenceChanged += Refresh;

        GameManager.OnExecutionStarted += OnExecutionStarted;
        GameManager.OnTurnReset += OnTurnReset;

        StartIntro();
    }

    private void OnDestroy()
    {
        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnSequenceChanged -= Refresh;

        GameManager.OnExecutionStarted -= OnExecutionStarted;
        GameManager.OnTurnReset -= OnTurnReset;
    }

    // ─── Turn flow ───────────────────────────────────────────────────────────

    private void OnExecutionStarted()
    {
        m_IsExecuting = true;
        StopIntro();
        m_Anims.TurnOffAllAnimations();
    }

    // The turn is over and the queue has just been cleared, so the level is being taught
    // from the top again — including the intro, which the player may well have skipped
    // past on their failed attempt.
    private void OnTurnReset()
    {
        m_IsExecuting = false;
        StartIntro();
    }

    // ─── Intro hints ─────────────────────────────────────────────────────────

    private void StartIntro()
    {
        StopIntro();

        if (m_Settings.introHints == null || m_Settings.introHints.Length == 0)
        {
            Refresh();
            return;
        }

        m_IntroRoutine = StartCoroutine(IntroRoutine());
    }

    private void StopIntro()
    {
        if (m_IntroRoutine == null) return;
        StopCoroutine(m_IntroRoutine);
        m_IntroRoutine = null;
    }

    // Cycles the intro hints until the player queues their first action, then hands the
    // display over to the key hints. Unscaled time so the loop keeps its pace in slow motion.
    private IEnumerator IntroRoutine()
    {
        // A death resets the level before the queue is cleared, so wait a frame: reading
        // QueuedCount on the reset frame would see the dead attempt and skip the intro.
        yield return null;

        int index = 0;
        while (QueuedCount == 0 && !m_IsExecuting)
        {
            m_Anims.PlayAnimation(m_Settings.introHints[index]);
            index = (index + 1) % m_Settings.introHints.Length;

            float elapsed = 0f;
            while (elapsed < m_Settings.introHintDuration && QueuedCount == 0 && !m_IsExecuting)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        m_IntroRoutine = null;
        Refresh();
    }

    // ─── Hint display ────────────────────────────────────────────────────────

    // Shows the hint for the action the player has to queue next, or the Enter hint once
    // the whole solution is queued.
    private void Refresh()
    {
        if (m_IsExecuting || m_Solution == null || m_Solution.Length == 0)
        {
            m_Anims.TurnOffAllAnimations();
            return;
        }

        // The intro owns the display until the player's first input ends it.
        if (m_IntroRoutine != null) return;

        int count = QueuedCount;

        if (count >= m_Solution.Length)
        {
            m_Anims.PlayAnimation(TutorialAnimType.Enter);
            return;
        }

        if (TryGetHint(m_Solution[count], out TutorialAnimType hint))
            m_Anims.PlayAnimation(hint);
        else
            m_Anims.TurnOffAllAnimations();
    }

    // Interact is not a queued movement command and takes no slot in the hint UI, so it is
    // not counted here either — this matches how PlayerInputUIHelper fills its slots.
    private int QueuedCount
    {
        get
        {
            if (SequenceManager.Instance == null) return 0;
            int count = 0;
            foreach (ActionTypeEnum action in SequenceManager.Instance.Sequence)
                if (action != ActionTypeEnum.Interact) count++;
            return count;
        }
    }

    // The hint that teaches how to queue an action. JumpLeft has no art yet, and Any /
    // Interact are not things a player is told to press, so those show nothing.
    private bool TryGetHint(ActionTypeEnum action, out TutorialAnimType hint)
    {
        switch (action)
        {
            case ActionTypeEnum.Right:     hint = TutorialAnimType.Right;     return true;
            case ActionTypeEnum.Left:      hint = TutorialAnimType.Left;      return true;
            case ActionTypeEnum.Jump:      hint = TutorialAnimType.Up;        return true;
            case ActionTypeEnum.JumpRight: hint = TutorialAnimType.JumpRight; return true;
            default:
                hint = default;
                Debug.LogWarning($"[TutorialSequenceGuide] No tutorial hint for {action} — " +
                                 "that step of the solution will show nothing.", this);
                return false;
        }
    }
}
