using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all UI state: the action timeline popup panel, play/clear buttons,
/// beat-progress timer slider, and the win/lose game-over screen.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("RectTransform of the sliding action timeline panel.")]
    [SerializeField] private RectTransform m_ActionPanel;
    [SerializeField] private float m_PanelHiddenY = -600f;
    [SerializeField] private float m_PanelShownY = 0f;

    [Header("Buttons")]
    [Tooltip("Button that opens/closes the action panel popup.")]
    [SerializeField] private Button m_PopupButton;

    [Header("Timer")]
    [Tooltip("Slider driven by beat progress (0–1 normalized).")]
    [SerializeField] private Slider m_TimerSlider;

    [Header("Game Over")]
    [SerializeField] private GameObject m_GameOverPanel;
    [Tooltip("Shown on win, hidden on lose.")]
    [SerializeField] private GameObject m_NextLevelButton;

    [Header("References")]
    [Tooltip("Routes clear/reset to the active input mode's sequence.")]
    [SerializeField] private SequenceSourceRouter m_SequenceSourceRouter;

    private void Awake()
    {
        if (m_ActionPanel == null) Debug.LogError("[UIManager] ActionPanel is not assigned.", this);
        if (m_PopupButton == null) Debug.LogError("[UIManager] PopupButton is not assigned.", this);
        if (m_GameOverPanel == null) Debug.LogError("[UIManager] GameOverPanel is not assigned.", this);
        if (m_NextLevelButton == null) Debug.LogError("[UIManager] NextLevelButton is not assigned.", this);
        if (m_SequenceSourceRouter == null) Debug.LogWarning("[UIManager] SequenceSourceRouter not assigned — clear button won't work.", this);
    }

    /// <summary>Slides the action panel into the visible position.</summary>
    public void PopUp()
    {
        if (m_ActionPanel == null) return;
        m_ActionPanel.anchoredPosition = new Vector2(m_ActionPanel.anchoredPosition.x, m_PanelShownY);
    }

    /// <summary>Slides the action panel off-screen.</summary>
    public void HidePopup()
    {
        if (m_ActionPanel == null) return;
        m_ActionPanel.anchoredPosition = new Vector2(m_ActionPanel.anchoredPosition.x, m_PanelHiddenY);
    }

    /// <summary>
    /// Called by the Play button. Hides the panel, locks the popup button,
    /// plays a click sound, then starts gameplay via <see cref="GameManager"/>.
    /// </summary>
    public void OnPlayClicked()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[UIManager] GameManager instance not found.", this);
            return;
        }

        AudioManager.Instance?.PlayButtonClick();
        // GameManager.OnPlayClicked handles HidePopup, LockUI, and starting execution
        GameManager.Instance.OnPlayClicked();
    }

    /// <summary>Re-enables the popup button after a turn ends. Called by GameManager.PlayEnded().</summary>
    public void UnlockUI()
    {
        if (m_PopupButton != null)
            m_PopupButton.interactable = true;
    }

    /// <summary>Disables the popup button at turn start. Called by GameManager.OnPlayClicked().</summary>
    public void LockUI()
    {
        if (m_PopupButton != null)
            m_PopupButton.interactable = false;
    }

    /// <summary>
    /// Resets all timeline rows and re-randomizes their beat tunes.
    /// Called by the Clear button.
    /// </summary>
    public void OnClearClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        // Routes to active mode: resets toggle grid (mouse) or clears key sequence (device)
        m_SequenceSourceRouter?.ClearCurrentSequence();
    }

    /// <summary>
    /// Drives the beat-progress slider. Pass a normalized value between 0 and 1.
    /// </summary>
    /// <param name="normalizedTime">Progress within the current beat interval (0–1).</param>
    public void OnTimeUpdated(float normalizedTime)
    {
        if (m_TimerSlider != null)
            m_TimerSlider.value = normalizedTime;
    }

    /// <summary>
    /// Shows the game-over panel. Displays the Next Level button on win, hides it on loss.
    /// </summary>
    /// <param name="isWin">True if the player won, false if they lost.</param>
    public void GameOver(bool isWin)
    {
        if (m_GameOverPanel != null) m_GameOverPanel.SetActive(true);
        if (m_NextLevelButton != null) m_NextLevelButton.SetActive(isWin);
    }
}
