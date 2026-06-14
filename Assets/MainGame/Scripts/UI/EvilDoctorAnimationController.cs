using UnityEngine;

public class EvilDoctorAnimationController : MonoBehaviour
{
    public enum DoctorAnimation
    {
        Idle,
        Happy,
        Sad
    }

    [SerializeField] private UIImageAnimator m_Animator;

    [Header("Animation Names In UIImageAnimator")]
    [SerializeField] private string m_IdleAnimation = "Idle";
    [SerializeField] private string m_HappyAnimation = "Happy";
    [SerializeField] private string m_SadAnimation = "Sad";

    [SerializeField]
    private DoctorAnimation doctor = DoctorAnimation.Idle;

    private void Start()
    {
        PlayAnimation(doctor);
    }

    public void PlayAnimation(DoctorAnimation animation)
    {
        switch (animation)
        {
            case DoctorAnimation.Idle:
                m_Animator.PlayAnimation(m_IdleAnimation);
                break;

            case DoctorAnimation.Happy:
                m_Animator.PlayAnimation(m_HappyAnimation);
                break;

            case DoctorAnimation.Sad:
                m_Animator.PlayAnimation(m_SadAnimation);
                break;
        }
    }

    /// <summary>
    /// Villain reactions:
    /// Player wins -> Doctor sad
    /// Player loses -> Doctor happy
    /// </summary>
    public void OnLevelCompleted()
    {
        PlayAnimation(DoctorAnimation.Sad);
    }

    public void OnLevelFailed()
    {
        PlayAnimation(DoctorAnimation.Happy);
    }
}