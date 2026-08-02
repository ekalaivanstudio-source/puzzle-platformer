using UnityEngine;
using TMPro;

public class DoctorDialogController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text m_DialogText;
    [SerializeField] private UIFloatEffect m_UIFloatEffect;

    [Header("Dialogs")]
    [SerializeField] private DoctorDialog[] m_HappyDialogs;
    [SerializeField] private DoctorDialog[] m_SadDialogs;


    public void ShowDialog(EvilDoctorAnimationController.DoctorAnimation animation)
    {
        DoctorDialog[] pool = animation switch
        {
            EvilDoctorAnimationController.DoctorAnimation.Happy => m_HappyDialogs,
            EvilDoctorAnimationController.DoctorAnimation.Sad => m_SadDialogs,
            _ => null
        };

        SetRandomDialog(pool);
    }

    private void SetRandomDialog(DoctorDialog[] dialogs)
    {
        if (m_DialogText == null)
        {
            Debug.LogWarning($"[{nameof(DoctorDialogController)}] {nameof(m_DialogText)} is not assigned.", this);
            return;
        }

        if (dialogs == null || dialogs.Length == 0)
        {
            m_DialogText.text = string.Empty;
            return;
        }

        DoctorDialog dialog = dialogs[Random.Range(0, dialogs.Length)];

        m_DialogText.text = dialog.dialogText;

        // Play dialog.audioClip here if needed.
        if(dialog.audioClip != null)
        {
            AudioManager.Instance?.PlayVoice(dialog.audioClip);

            if(m_UIFloatEffect != null)
                m_UIFloatEffect.SetStayDuration(dialog.audioClip.length +.2f);
        }
    }
}

[System.Serializable]
public class DoctorDialog
{
    [TextArea]
    public string dialogText;

    public AudioClip audioClip;
}