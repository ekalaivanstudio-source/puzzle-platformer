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

    private DoctorDialog m_CurrentDialog;

    private void Awake()
    {
        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.OnReachedTop += PlayVoiceOver;
        }
    }

    private void OnDestroy()
    {
        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.OnReachedTop -= PlayVoiceOver;
        }
    }

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
            m_CurrentDialog = null;
            return;
        }

        m_CurrentDialog = dialogs[Random.Range(0, dialogs.Length)];
        m_DialogText.text = m_CurrentDialog.dialogText;

        if (m_CurrentDialog.audioClip != null)
        {
            if (m_UIFloatEffect != null)
                m_UIFloatEffect.SetStayDuration(m_CurrentDialog.audioClip.length + 0.2f);
        }
        else
        {
            if (m_UIFloatEffect != null)
                m_UIFloatEffect.SetStayDuration(2f);
        }
    }

    private void PlayVoiceOver()
    {
        if (m_CurrentDialog != null && m_CurrentDialog.audioClip != null)
        {
            AudioManager.Instance?.PlayVoice(m_CurrentDialog.audioClip);
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