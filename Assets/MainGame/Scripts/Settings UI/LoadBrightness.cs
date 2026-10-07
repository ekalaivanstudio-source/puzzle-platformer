using Setting.Menu;
using UnityEngine;
using UnityEngine.UI;

public class LoadBrightness : MonoBehaviour
{
    [SerializeField] private Image brightnessOverlay;

    #region Private Fields

    private SettingsData currentSettings;
    private SettingsData lastAppliedSettings;

    #endregion

    /// <summary>
    /// Initializes the manager, registers event handlers, loads settings, updates the UI, and applies the settings.
    /// </summary>
    private void Awake()
    {   SettingsData loadedSettings = SettingsSaveSystem.LoadSettings();
        currentSettings = loadedSettings.Clone();
        ApplyBrightnessSettings();
    }
    private void ApplyBrightnessSettings()
    {
        if (currentSettings == null)
        {
            return;
        }

        ApplyBrightnessValue(currentSettings.Brightness);
    }
    private void ApplyBrightnessValue(float brightness)
    {
        if (brightnessOverlay == null)
        {
            Transform panel = transform.Find("Panel");
            if (panel != null)
            {
                brightnessOverlay = panel.GetComponent<Image>();
            }
        }

        // Fallback: Adjust a UI screen overlay if one is assigned
        if (brightnessOverlay != null)
        {
            // Max brightness (1.0) -> overlay is completely transparent (alpha = 0)
            // Min brightness (0.0) -> overlay is 95% black (alpha = 0.95)
            float alpha = Mathf.Lerp(0.95f, 0.0f, brightness);
            brightnessOverlay.color = new Color(0f, 0f, 0f, alpha);
        }
    }

}
