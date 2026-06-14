using System.Collections;
using UnityEngine;

public class UIFloatEffect : MonoBehaviour
{
    [SerializeField] private RectTransform m_Target;

    [Header("Movement")]
    [SerializeField] private float m_MoveDistance = 150f;
    [SerializeField] private float m_MoveUpDuration = 0.5f;
    [SerializeField] private float m_StayDuration = 2f;
    [SerializeField] private float m_MoveDownDuration = 0.5f;

    private Vector2 m_StartPosition;
    private Coroutine m_CurrentRoutine;

    private void Awake()
    {
        if (m_Target == null)
            m_Target = GetComponent<RectTransform>();

        m_StartPosition = m_Target.anchoredPosition;
    }

    public void Play()
    {
        if (m_CurrentRoutine != null)
            StopCoroutine(m_CurrentRoutine);

        m_CurrentRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        Vector2 targetPos = m_StartPosition + Vector2.up * m_MoveDistance;

        // Move Up
        yield return MoveTo(m_StartPosition, targetPos, m_MoveUpDuration);

        // Stay
        yield return new WaitForSeconds(m_StayDuration);

        // Move Down
        yield return MoveTo(targetPos, m_StartPosition, m_MoveDownDuration);
    }

    private IEnumerator MoveTo(Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth easing
            t = Mathf.SmoothStep(0f, 1f, t);

            m_Target.anchoredPosition = Vector2.Lerp(from, to, t);

            yield return null;
        }

        m_Target.anchoredPosition = to;
    }
}