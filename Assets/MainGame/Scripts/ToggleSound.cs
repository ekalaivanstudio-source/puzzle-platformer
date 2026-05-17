using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically attached to each beat Toggle by <see cref="ActionTimelineController"/> at runtime.
/// Plays the row's beat clip at a randomized pitch whenever the toggle is switched on.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ToggleSound : MonoBehaviour
{
    /// <summary>Pitch applied when playing the beat sound. Set by ActionTimelineController.OnActionReset().</summary>
    public float pitch = 1f;

    /// <summary>Reference to the parent row controller, used to retrieve the assigned beat AudioClip.</summary>
    public ActionTimelineController controller;

    private void Awake()
    {
        Toggle toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        // Clean up listener to prevent memory leaks
        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (!isOn || controller == null || AudioManager.Instance == null) return;
        AudioManager.Instance.PlayBeatTune(controller.BeatIndex, pitch);
    }
}
