using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton UI manager. Owns every UI reference in the game:
/// fade overlay, level label and popups.
/// All other scripts call UIManager.Instance — no UI fields live elsewhere.
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager m_Instance;
    public static UIManager Instance => m_Instance;

    [Header("Fade")]
    [Tooltip("Full-screen dark overlay CanvasGroup. Alpha 1 = black, 0 = clear.")]
    [SerializeField] private CanvasGroup m_FadeOverlay;
    [SerializeField] private float m_FadeDuration = 1f;

    [Header("Level Label")]
    [Tooltip("Top-of-screen text showing which level the player is on.")]
    [SerializeField] private TextMeshProUGUI m_LevelLabel;
    [Tooltip("Level label format string. {0} is the level number.")]
    [SerializeField] private string m_LevelLabelFormat = "LEVEL {0}";

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;
    }

    // LevelContext runs at execution order -1000, so its Instance is already set by Start.
    private void Start() => RefreshLevelLabel();

    /// <summary>Called by the Play button. Starts gameplay via <see cref="GameManager"/>.</summary>
    public void OnPlayClicked()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[UIManager] GameManager instance not found.", this);
            return;
        }

        AudioManager.Instance?.PlayButton();
        GameManager.Instance.OnPlayClicked();
    }

    /// <summary>Called by the Clear button.</summary>
    public void OnClearClicked()
    {
        AudioManager.Instance?.PlayClear();
        SequenceManager.Instance?.ClearSequence();
    }

    // ─── Level Label ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the top-of-screen level label from <see cref="LevelContext"/>.
    /// Runs automatically on scene start; call it again if the level number changes at runtime.
    /// Hides the label when the scene has no <see cref="LevelContext"/> (e.g. menu scenes).
    /// </summary>
    public void RefreshLevelLabel()
    {
        if (m_LevelLabel == null) return;

        LevelContext context = LevelContext.Instance;
        if (context == null)
        {
            m_LevelLabel.gameObject.SetActive(false);
            return;
        }

        m_LevelLabel.gameObject.SetActive(true);
        m_LevelLabel.text = string.Format(m_LevelLabelFormat, context.CurrentLevel);
    }

    // ─── Fade ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Animates the fade overlay from <paramref name="from"/> to <paramref name="to"/>.
    /// yield return StartCoroutine(UIManager.Instance.FadeRoutine(0f, 1f)) to await it.
    /// FadeRoutine(1f, 0f) is the level fade-in — <see cref="PlayerController"/> awaits it
    /// on scene load and only then brings the player in.
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

}
