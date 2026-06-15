using UnityEngine;
using TMPro;

/// <summary>
/// Shows a random line of doctor dialog for a given reaction.
/// </summary>
public class DoctorDialogController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text m_DialogText;

    [Header("Dialogs")]
    [SerializeField] private string[] m_HappyDialogs;
    [SerializeField] private string[] m_SadDialogs;

    public void ShowDialog(EvilDoctorAnimationController.DoctorAnimation animation)
    {
        string[] pool = animation switch
        {
            EvilDoctorAnimationController.DoctorAnimation.Happy => m_HappyDialogs,
            EvilDoctorAnimationController.DoctorAnimation.Sad => m_SadDialogs,
            _ => null
        };

        SetRandomDialog(pool);
    }

    private void SetRandomDialog(string[] dialogs)
    {
        if (m_DialogText == null)
        {
            Debug.LogWarning($"[{nameof(DoctorDialogController)}] {nameof(m_DialogText)} is not assigned.", this);
            return;
        }

        m_DialogText.text = (dialogs != null && dialogs.Length > 0)
            ? dialogs[Random.Range(0, dialogs.Length)]
            : string.Empty;
    }
}
