using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton camera utility. Owns all camera effects (shake, etc.).
/// Call CameraController.Instance.Shake(magnitude, duration) from any script.
/// </summary>
public class CameraController : MonoBehaviour
{
    private static CameraController m_Instance;
    public static CameraController Instance => m_Instance;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;
    }

    /// <summary>
    /// Shakes the main camera with a magnitude that fades out over the duration.
    /// Uses unscaled time so it works correctly under Time.timeScale changes.
    /// </summary>
    public void Shake(float magnitude, float duration)
    {
        StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Vector3 origin = cam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - (elapsed / duration);
            cam.transform.localPosition = origin + (Vector3)Random.insideUnitCircle * magnitude * t;
            yield return null;
        }

        cam.transform.localPosition = origin;
    }
}
