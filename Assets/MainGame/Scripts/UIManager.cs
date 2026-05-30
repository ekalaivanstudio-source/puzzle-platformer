using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton UI manager. Owns every UI reference in the game:
/// action timeline panel, buttons, timer slider, fade overlay,
/// dash cooldown slider, win/lose screen, popups.
/// All other scripts call UIManager.Instance — no UI fields live elsewhere.
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager m_Instance;
    public static UIManager Instance => m_Instance;
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

    [Header("Popups")]
    [Tooltip("Shown for 2 seconds when the player reaches the door without the key.")]
    [SerializeField] private GameObject m_NoKeyPopup;
    [Tooltip("Shown for 2 seconds on level win before loading next level.")]
    [SerializeField] private GameObject m_WowObject;

    [Header("Fade")]
    [Tooltip("Full-screen dark overlay CanvasGroup. Alpha 1 = black, 0 = clear.")]
    [SerializeField] private CanvasGroup m_FadeOverlay;
    [SerializeField] private float m_FadeDuration = 1f;

    [Header("Dash Cooldown")]
    [Tooltip("Slider that visualises dash cooldown. Value 1 = ready, 0 = just used.")]
    [SerializeField] private Slider m_DashCooldownSlider;

    [Header("References")]
    [Tooltip("Routes clear/reset to the active input mode's sequence.")]
    [SerializeField] private SequenceSourceRouter m_SequenceSourceRouter;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;

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

    // ─── Popup / Win Effect ─────────────────────────────────────────────────────

    /// <summary>Shows or hides the win celebration object.</summary>
    public void ShowWinEffect(bool show)
    {
        if (m_WowObject != null) m_WowObject.SetActive(show);
    }

    /// <summary>Shows or hides the "no key" popup.</summary>
    public void ShowNoKeyPopup(bool show)
    {
        if (m_NoKeyPopup != null) m_NoKeyPopup.SetActive(show);
    }

    // ─── Fade ───────────────────────────────────────────────────────────────────────────

    /// <summary>Fades from black to clear. Call on scene load.</summary>
    public void StartLevelFadeIn()
    {
        if (m_FadeOverlay == null) return;
        m_FadeOverlay.gameObject.SetActive(true);
        m_FadeOverlay.alpha = 1f;
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    /// <summary>
    /// Animates the fade overlay from <paramref name="from"/> to <paramref name="to"/>.
    /// yield return StartCoroutine(UIManager.Instance.FadeRoutine(0f, 1f)) to await it.
    /// </summary>
    public IEnumerator FadeRoutine(float from, float to)
    {
        if (m_FadeOverlay == null) yield break;
        m_FadeOverlay.gameObject.SetActive(true);
        float elapsed = 0f;
        m_FadeOverlay.alpha = from;
        while (elapsed < m_FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_FadeOverlay.alpha = Mathf.Lerp(from, to, elapsed / m_FadeDuration);
            yield return null;
        }
        m_FadeOverlay.alpha = to;
        if (to == 0f) m_FadeOverlay.gameObject.SetActive(false);
    }

    // ─── Dash Cooldown ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the dash cooldown slider. Pass 1 when ready, 0 when just used;
    /// it fills back to 1 over the cooldown duration.
    /// </summary>
    public void SetDashCooldownFill(float normalizedValue)
    {
        if (m_DashCooldownSlider != null)
            m_DashCooldownSlider.value = normalizedValue;
    }
}
