using UnityEngine;

public class EvilDoctorAnimationController : MonoBehaviour
{
    public enum DoctorAnimation
    {
        Happy,
        Sad
    }
    public static EvilDoctorAnimationController Instance { get; private set; }

    [SerializeField] private UIImageAnimator m_Animator;
    [SerializeField] private DoctorDialogController m_DialogController;

    [SerializeField] private int m_DeathCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

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
        m_DeathCount++;
        if (m_DeathCount > 2)
        {
            // Player loses -> Doctor happy
            OnPlayerdead();
            m_DeathCount = 0;
        }


    }
    public void OnPlayerdead()
    {
        // Player loses -> Doctor happy
        m_DialogController.ShowDialog(DoctorAnimation.Happy);
        m_Animator.ShowReaction(DoctorAnimation.Happy);

    }

    public int DeathCount()
    {
        return m_DeathCount;
    }
}