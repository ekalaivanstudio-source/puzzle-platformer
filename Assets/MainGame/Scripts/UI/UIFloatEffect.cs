using System;
using System.Collections;
using UnityEngine;

public class UIFloatEffect : MonoBehaviour
{
    public event Action OnReachedTop;
    public event Action OnBeforeMoveDown;

    [SerializeField] private RectTransform m_Target;

    [Header("Movement")]
    [SerializeField] private float m_MoveDistance = 150f;
    [SerializeField] private float m_MoveUpDuration = 0.5f;
    [SerializeField] private float m_StayDuration = 2f;
    [SerializeField] private float m_MoveDownDuration = 0.5f;

    private Vector2 m_StartPosition;
    private Coroutine m_Routine;

    [SerializeField]
    private GameObject m_DialogBox;

    private void Awake()
    {
        if (m_Target == null)
            m_Target = GetComponent<RectTransform>();

        m_StartPosition = m_Target.anchoredPosition;
        m_DialogBox?.SetActive(false);
    }

    public void Play()
    {
        if (m_Routine != null)
            StopCoroutine(m_Routine);

        m_Target.anchoredPosition = m_StartPosition;

        m_Routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        Vector2 targetPos = m_StartPosition + Vector2.up * m_MoveDistance;

        yield return MoveTo(m_StartPosition, targetPos, m_MoveUpDuration,true);

        OnReachedTop?.Invoke();
        m_DialogBox?.SetActive(true);
        yield return new WaitForSecondsRealtime(m_StayDuration);
        m_DialogBox?.SetActive(false);
        OnBeforeMoveDown?.Invoke();

        yield return MoveTo(targetPos, m_StartPosition, m_MoveDownDuration,false);

        m_Routine = null;
    }

    private IEnumerator MoveTo(Vector2 from,Vector2 to,float duration,bool moveUp)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            if (moveUp)
            {
                // Smooth acceleration + deceleration
                t = Mathf.SmoothStep(0f, 1f, t);
            }
            else
            {
                // Smooth fall
                t = 1f - Mathf.Pow(1f - t, 3f);
            }

            m_Target.anchoredPosition =
                Vector2.LerpUnclamped(from, to, t);

            yield return null;
        }

        m_Target.anchoredPosition = to;
    }
}