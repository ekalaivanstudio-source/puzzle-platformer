using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boot splash. Bounces the company logo in from nothing, holds it, fades out and loads the home
/// screen. Lives in the Launcher scene, which is build index 0 so it is the first thing to run.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CompanySplashScreen : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scene loaded once the splash finishes. Must be in Build Settings.")]
    [SerializeField] private string m_NextSceneName = "HomeScreen";

    [Header("Logo")]
    [Tooltip("Logo image that plays the bounce.")]
    [SerializeField] private RectTransform m_Logo;

    [Header("Bounce")]
    [Tooltip("Scale the logo overshoots to before settling back on 1.")]
    [SerializeField] private float m_OvershootScale = 1.2f;

    [Tooltip("Seconds to grow from 0 up to the overshoot scale.")]
    [SerializeField] private float m_GrowDuration = 0.45f;

    [Tooltip("Seconds to settle from the overshoot scale back down to 1.")]
    [SerializeField] private float m_SettleDuration = 0.25f;

    [Header("Timing")]
    [Tooltip("Total seconds the logo is on screen, bounce included, before the fade out starts.")]
    [SerializeField] private float m_DisplayDuration = 3f;

    [Tooltip("Seconds the fade out takes before the next scene loads.")]
    [SerializeField] private float m_FadeOutDuration = 0.35f;

    private CanvasGroup m_CanvasGroup;

    private void Awake()
    {
        m_CanvasGroup = GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha = 1f;

        // Collapsed before the first frame renders, otherwise the logo flashes at full size.
        if (m_Logo != null) m_Logo.localScale = Vector3.zero;
    }

    private void Start()
    {
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // Unscaled throughout: a splash must run even if something left Time.timeScale at 0.
        float startTime = Time.unscaledTime;

        yield return ScaleLogo(0f, m_OvershootScale, m_GrowDuration, EaseOutCubic);
        yield return ScaleLogo(m_OvershootScale, 1f, m_SettleDuration, EaseInOutQuad);

        // Hold out the rest of the display window, so retuning the bounce never changes how long
        // the splash takes overall.
        float remaining = m_DisplayDuration - (Time.unscaledTime - startTime);
        if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

        yield return FadeOut();

        if (!Application.CanStreamedLevelBeLoaded(m_NextSceneName))
        {
            Debug.LogError($"[CompanySplashScreen] Scene '{m_NextSceneName}' is not in Build Settings.", this);
            yield break;
        }

        SceneManager.LoadScene(m_NextSceneName);
    }

    private IEnumerator ScaleLogo(float from, float to, float duration, System.Func<float, float> ease)
    {
        if (m_Logo == null) yield break;

        if (duration <= 0f)
        {
            m_Logo.localScale = Vector3.one * to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            m_Logo.localScale = Vector3.one * Mathf.Lerp(from, to, t);
            yield return null;
        }

        m_Logo.localScale = Vector3.one * to;
    }

    private IEnumerator FadeOut()
    {
        if (m_FadeOutDuration <= 0f)
        {
            m_CanvasGroup.alpha = 0f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < m_FadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_CanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / m_FadeOutDuration);
            yield return null;
        }

        m_CanvasGroup.alpha = 0f;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseInOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
}
