using UnityEngine;
using TMPro;

public class DoctorDialogController : MonoBehaviour
{
    [SerializeField] private TMP_Text m_DialogText;

    [Header("Happy Dialogs")]
    [SerializeField]
    private string[] m_HappyDialogs;

    [Header("Sad Dialogs")]
    [SerializeField]
    private string[] m_SadDialogs;

    public void ShowDialog(EvilDoctorAnimationController.DoctorAnimation animation)
    {
        switch (animation)
        {
            case EvilDoctorAnimationController.DoctorAnimation.Happy:
                SetRandomDialog(m_HappyDialogs);
                break;

            case EvilDoctorAnimationController.DoctorAnimation.Sad:
                SetRandomDialog(m_SadDialogs);
                break;
        }
    }

    private void SetRandomDialog(string[] dialogs)
    {
        if (dialogs == null || dialogs.Length == 0)
            return;

        m_DialogText.text = dialogs[Random.Range(0, dialogs.Length)];
    }
}