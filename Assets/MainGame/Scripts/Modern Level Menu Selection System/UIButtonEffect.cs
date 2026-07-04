using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private float duration = 0.08f;

    private Vector3 originalScale;
    private Coroutine routine;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Animate(originalScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Animate(originalScale);
    }

    private void Animate(Vector3 target)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(Scale(target));
    }

    private IEnumerator Scale(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float t = 0;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }

        transform.localScale = target;
    }
}