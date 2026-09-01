using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the tutorial hint animations and shows exactly one of them at a time.
/// Every hint is authored twice — a PC (keyboard) variant and an Xbox (gamepad) variant —
/// and this picks the right one for the build.
///
/// It only draws what it is told to draw. <see cref="TutorialSequenceGuide"/> decides which
/// hint a level shows and when, so this component stays a dumb display.
/// </summary>
public class TutorialAnimationController : MonoBehaviour
{
    public List<TutorialAnim> tutorialAnims;
    public TutorialAnimType animType = TutorialAnimType.Right;

    [Tooltip("Plays animType on Start. Off by default — this canvas ships on every level " +
             "through Managers, and TutorialSequenceGuide drives what it shows. Turn on " +
             "only to preview a single hint while authoring.")]
    public bool playOnStart = false;

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

        if (IsXbox())
        {
            if (selectedAnim.xboxAnim != null)
                selectedAnim.xboxAnim.gameObject.SetActive(true);
        }
        else
        {
            if (selectedAnim.pcAnim != null)
                selectedAnim.pcAnim.gameObject.SetActive(true);
        }
    }

    public void TurnOffAllAnimations()
    {
        foreach (TutorialAnim tutorialAnim in tutorialAnims)
        {
            if (tutorialAnim.pcAnim != null)
                tutorialAnim.pcAnim.gameObject.SetActive(false);

            if (tutorialAnim.xboxAnim != null)
                tutorialAnim.xboxAnim.gameObject.SetActive(false);
        }
    }
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
