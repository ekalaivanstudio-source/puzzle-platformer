using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Slides a UI element up, holds it, then slides it back down — used to present the
/// doctor's reaction. Fires events at each phase so an animator can sync to it, and
/// exposes <see cref="IsPlaying"/> so callers can await the full cycle.
/// </summary>
public class UIFloatEffect : MonoBehaviour
{
    /// <summary>Fired once the element reaches the top (reaction should start).</summary>
    public event Action OnReachedTop;

    /// <summary>Fired just before the element starts moving back down (reaction should stop).</summary>
    public event Action OnBeforeMoveDown;

    /// <summary>Fired once the full up → stay → down cycle has finished.</summary>
    public event Action OnComplete;

    [Header("References")]
    [SerializeField] private RectTransform m_Target;
    [SerializeField] private GameObject m_DialogBox;

    [Header("Movement")]
    [SerializeField] private float m_MoveDistance = 150f;
    [SerializeField] private float m_MoveUpDuration = 0.5f;
    [SerializeField] private float m_StayDuration = 2f;
    [SerializeField] private float m_MoveDownDuration = 0.5f;

    private Vector2 m_StartPosition;
    private Coroutine m_Routine;

    /// <summary>True while a float cycle is in progress.</summary>
    public bool IsPlaying => m_Routine != null;

    private void Awake()
    {
        if (m_Target == null)
            m_Target = GetComponent<RectTransform>();

        if (m_Target == null)
        {
            Debug.LogError($"[{nameof(UIFloatEffect)}] No RectTransform to animate.", this);
            enabled = false;
            return;
        }

        m_StartPosition = m_Target.anchoredPosition;
        if (m_DialogBox != null) m_DialogBox.SetActive(false);
    }

    /// <summary>Starts (or restarts) the float cycle from the rest position.</summary>
    public void Play()
    {
        if (m_Target == null) return;

        if (m_Routine != null)
            StopCoroutine(m_Routine);

        m_Target.anchoredPosition = m_StartPosition;
        m_Routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        Vector2 topPos = m_StartPosition + Vector2.up * m_MoveDistance;

        yield return MoveTo(m_StartPosition, topPos, m_MoveUpDuration, moveUp: true);

        OnReachedTop?.Invoke();
        if (m_DialogBox != null) m_DialogBox.SetActive(true);

        yield return new WaitForSecondsRealtime(m_StayDuration);

        if (m_DialogBox != null) m_DialogBox.SetActive(false);
        OnBeforeMoveDown?.Invoke();

        yield return MoveTo(topPos, m_StartPosition, m_MoveDownDuration, moveUp: false);

        m_Routine = null;
        OnComplete?.Invoke();
    }

    private IEnumerator MoveTo(Vector2 from, Vector2 to, float duration, bool moveUp)
    {
        if (duration <= 0f)
        {
            m_Target.anchoredPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease in/out on the way up; ease out (smooth fall) on the way down.
            t = moveUp ? Mathf.SmoothStep(0f, 1f, t) : 1f - Mathf.Pow(1f - t, 3f);
            m_Target.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }

        m_Target.anchoredPosition = to;
    }
}
