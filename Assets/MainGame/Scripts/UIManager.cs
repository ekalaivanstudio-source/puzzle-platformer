using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton UI manager. Owns every UI reference in the game:
/// fade overlay and popups.
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

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;
    }

    /// <summary>Called by the Play button. Starts gameplay via <see cref="GameManager"/>.</summary>
    public void OnPlayClicked()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[UIManager] GameManager instance not found.", this);
            return;
        }

        GameManager.Instance.OnPlayClicked();
    }

    /// <summary>Called by the Clear button.</summary>
    public void OnClearClicked()
    {
        SequenceManager.Instance?.ClearSequence();
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

}
