using System.Collections.Generic;
using UnityEngine;

public class TutorialAnimationController : MonoBehaviour
{
    public List<TutorialAnim> tutorialAnims;
    public TutorialAnimType animType = TutorialAnimType.BackSpace;
    private void Start()
    {
        PlayAnimation(animType);
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

public enum TutorialAnimType
{
    BackSpace,
    Enter,
    Right,
    Up,
    Jump,
    Dash
}