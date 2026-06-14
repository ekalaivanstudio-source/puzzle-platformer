using UnityEngine;

public class EvilDoctorAnimationController : MonoBehaviour
{
    public enum DoctorAnimation
    {
        Happy,
        Sad
    }

    [SerializeField] private UIImageAnimator m_Animator;
    [SerializeField] private DoctorDialogController m_DialogController;
    [ContextMenu("OnLevelCompleted")]
    public void OnLevelCompleted()
    {
        // Player wins -> Doctor sad
        m_DialogController.ShowDialog(DoctorAnimation.Sad);
        m_Animator.ShowReaction(DoctorAnimation.Sad);
    }
    [ContextMenu("OnLevelFailed")]
    public void OnLevelFailed()
    {
        // Player loses -> Doctor happy
        m_DialogController.ShowDialog(DoctorAnimation.Happy);
        m_Animator.ShowReaction(DoctorAnimation.Happy);
    }
}