using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton camera utility. Owns all camera effects (shake, etc.).
/// Call CameraController.Instance.Shake(magnitude, duration) from any script.
/// </summary>
public class CameraController : MonoBehaviour
{
    private static CameraController m_Instance;

    /// <summary>
    /// The live controller, created on demand.
    ///
    /// Self-provisioning because no scene in the project carries this component: every
    /// Shake() call in the game was reaching a null Instance and silently doing nothing.
    /// Spawning the holder on first use fixes all of them at once, and keeps a scene that
    /// wants to place one by hand (to tune it, or to keep the hierarchy explicit) working
    /// exactly as before — Awake claims the slot first, and this never runs.
    /// </summary>
    public static CameraController Instance
    {
        get
        {
            if (m_Instance != null) return m_Instance;
            if (!Application.isPlaying) return null;

            var holder = new GameObject("[CameraController]");
            m_Instance = holder.AddComponent<CameraController>();
            return m_Instance;
        }
    }

    // The shake in progress, and the camera it is displacing. Held on the controller
    // rather than in the coroutine so a new shake can cancel and undo the old one.
    private Coroutine m_ShakeRoutine;
    private Transform m_ShakenCamera;
    private Vector3 m_ShakeOrigin;

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
        Camera cam = Camera.main;
        if (cam == null) return;

        // Snap any shake still running back home before starting this one. Without it a
        // shake that lands mid-shake captures the DISPLACED position as its origin, and
        // restores the camera to that offset when it ends — leaving it permanently off
        // centre. Cheap here, and pushes shake often enough to overlap.
        StopShake();

        m_ShakenCamera = cam.transform;
        m_ShakeOrigin = m_ShakenCamera.localPosition;
        m_ShakeRoutine = StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    /// <summary>
    /// Ends any shake in progress and puts the camera back where it started.
    /// </summary>
    public void StopShake()
    {
        if (m_ShakeRoutine != null)
        {
            StopCoroutine(m_ShakeRoutine);
            m_ShakeRoutine = null;
        }

        if (m_ShakenCamera != null)
        {
            m_ShakenCamera.localPosition = m_ShakeOrigin;
            m_ShakenCamera = null;
        }
    }

    /// <summary>
    /// Shakes the main camera by a shared <see cref="CameraShakeSettings"/> preset.
    /// Ignores presets left at zero, so a caller can hold a shake field it has not
    /// filled in and still call this every time without a guard of its own.
    /// </summary>
    public void Shake(CameraShakeSettings settings)
    {
        if (!settings.IsActive) return;
        Shake(settings.Magnitude, settings.Duration);
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && m_ShakenCamera != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - (elapsed / duration);
            m_ShakenCamera.localPosition = m_ShakeOrigin + (Vector3)Random.insideUnitCircle * magnitude * t;
            yield return null;
        }

        m_ShakeRoutine = null;
        StopShake();
    }
}
