using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsHeader : MonoBehaviour
{

    [SerializeField] TMP_Text titleText;
    [SerializeField] Image iconImg;
    [SerializeField] string title;
    [SerializeField] Sprite icon;

    void Awake()
    {
        SetHeader(title, icon);
    }

    public void SetHeader(string text, Sprite sprite)
    {
        titleText.text = text;
        iconImg.sprite = sprite;
    }
}