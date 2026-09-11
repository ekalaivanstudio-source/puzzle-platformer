using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the tutorial hint animations and shows exactly one of them at a time.
/// Every hint is authored twice — a PC (keyboard) variant and an Xbox (gamepad) variant —
/// and this picks the right one for the build.
///
/// It only draws what it is told to draw. <see cref="TutorialSequenceGuide"/> decides which
/// hint a level shows and when, so this component stays a dumb display.
///
/// A hint POPS in rather than blinking on, with the same overshoot
/// <see cref="LevelBuildDirector"/> pops the rest of the level in with. A hint that simply
/// appears reads as a glitch next to a level that assembles itself.
/// </summary>
public class TutorialAnimationController : MonoBehaviour
{
    public List<TutorialAnim> tutorialAnims;
    public TutorialAnimType animType = TutorialAnimType.Right;

    [Tooltip("Plays animType on Start. Off by default — this canvas ships on every level " +
             "through Managers, and TutorialSequenceGuide drives what it shows. Turn on " +
             "only to preview a single hint while authoring.")]
    public bool playOnStart = false;

    [Tooltip("Seconds a hint takes to pop from nothing to full size as it appears. 0 shows " +
             "it at full size immediately.")]
    [Min(0f)] public float popDuration = 0.22f;

    // Every hint's authored scale, read once before anything has had a chance to animate one.
    // A pop always ends on the value from here rather than on whatever the transform happened
    // to be holding when it started, so an interrupted pop cannot leave a hint permanently
    // shrunk — they are switched off and back on for the whole level.
    private readonly Dictionary<Transform, Vector3> m_AuthoredScales =
        new Dictionary<Transform, Vector3>();

    private Coroutine m_PopRoutine;
    private Transform m_Popping;

    private void Awake()
    {
        foreach (TutorialAnim tutorialAnim in tutorialAnims)
        {
            Remember(tutorialAnim.pcAnim);
            Remember(tutorialAnim.xboxAnim);
        }
    }

    private void Start()
    {
        if (playOnStart) PlayAnimation(animType);
        else TurnOffAllAnimations();
    }

    public void PlayAnimation(TutorialAnimType type)
    {
        TurnOffAllAnimations();

        TutorialAnim selectedAnim = tutorialAnims.Find(x => x.name == type);

        if (selectedAnim == null)
            return;

        SpriteSheetAnimator hint = IsXbox() ? selectedAnim.xboxAnim : selectedAnim.pcAnim;

        if (hint == null)
            return;

        hint.gameObject.SetActive(true);
        Pop(hint.transform);
    }

    public void TurnOffAllAnimations()
    {
        SettlePop();

        foreach (TutorialAnim tutorialAnim in tutorialAnims)
        {
            if (tutorialAnim.pcAnim != null)
                tutorialAnim.pcAnim.gameObject.SetActive(false);

            if (tutorialAnim.xboxAnim != null)
                tutorialAnim.xboxAnim.gameObject.SetActive(false);
        }
    }

    // ─── The pop ─────────────────────────────────────────────────────────────

    private void Remember(SpriteSheetAnimator anim)
    {
        if (anim != null) m_AuthoredScales[anim.transform] = anim.transform.localScale;
    }

    private void Pop(Transform hint)
    {
        if (popDuration <= 0f || !isActiveAndEnabled)
        {
            hint.localScale = AuthoredScale(hint);
            return;
        }

        m_PopRoutine = StartCoroutine(PopRoutine(hint));
    }

    // Hands whatever is mid-pop back its authored size. Always called before a hint is
    // switched off, because a hint put away at half size is a hint that comes back at half
    // size — the next PlayAnimation would pop it from there.
    private void SettlePop()
    {
        if (m_PopRoutine != null)
        {
            StopCoroutine(m_PopRoutine);
            m_PopRoutine = null;
        }

        if (m_Popping == null) return;

        m_Popping.localScale = AuthoredScale(m_Popping);
        m_Popping = null;
    }

    // Unscaled, like the level build it borrows its curve from: a hint appearing is UI, and
    // UI should not slow down because the game did.
    private IEnumerator PopRoutine(Transform hint)
    {
        m_Popping = hint;

        Vector3 scale = AuthoredScale(hint);
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            hint.localScale = scale * Mathf.Max(0f, LevelBuildEase.OutBack(elapsed / popDuration));

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        hint.localScale = scale;

        m_Popping = null;
        m_PopRoutine = null;
    }

    private Vector3 AuthoredScale(Transform hint) =>
        m_AuthoredScales.TryGetValue(hint, out Vector3 scale) ? scale : Vector3.one;

    private bool IsXbox()
    {
#if UNITY_GAMECORE
        return true;
#elif UNITY_XBOXONE
        return true;
#else
        return false;
#endif
    }
}

[System.Serializable]
public class TutorialAnim
{
    public TutorialAnimType name;

    [Header("PC")]
    public SpriteSheetAnimator pcAnim;

    [Header("Xbox")]
    public SpriteSheetAnimator xboxAnim;
}

/// <summary>
/// The hints the tutorial canvas can draw. Values are pinned because they are what the
/// prefab serializes — renaming an entry is safe, reordering is not.
///
/// Names describe the art each entry actually plays, which is why two of them read
/// differently from the input action they teach.
/// </summary>
public enum TutorialAnimType
{
    /// <summary>Backspace / B — undo the last queued action.</summary>
    BackSpace = 0,

    /// <summary>Enter / Start — run the queued sequence.</summary>
    Enter = 1,

    /// <summary>→ on its own. Queues <see cref="ActionTypeEnum.Right"/>.</summary>
    Right = 2,

    /// <summary>↑ on its own. Queues <see cref="ActionTypeEnum.Jump"/>.</summary>
    Up = 3,

    /// <summary>↑ held while → is pressed. Queues <see cref="ActionTypeEnum.JumpRight"/>.</summary>
    JumpRight = 4,

    /// <summary>Byte shoving a brick — the move-brick hint, shown as a level intro.</summary>
    Push = 5,

    /// <summary>← on its own. Queues <see cref="ActionTypeEnum.Left"/>.</summary>
    Left = 6
}
