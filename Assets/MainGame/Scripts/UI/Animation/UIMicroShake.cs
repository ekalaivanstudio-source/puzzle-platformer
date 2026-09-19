using System.Collections;
using UnityEngine;

namespace MainGame.UI.Animation
{
    /// <summary>
    /// Delivers lightweight, zero-allocation micro-shakes and impulse kicks to UI containers
    /// (e.g. ScreenManager or dialog panels) upon button confirmations and heavy panel landings.
    /// Uses unscaled delta time and pure mathematical decaying oscillations.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIMicroShake : MonoBehaviour
    {
        public static UIMicroShake Instance { get; private set; }

        [Header("Target Container")]
        [Tooltip("The RectTransform shaken during impulses. If null, uses this GameObject's RectTransform.")]
        [SerializeField] private RectTransform m_TargetTransform;

        [Header("Default Parameters")]
        [SerializeField] private float m_DefaultIntensity = 4f;
        [SerializeField] private float m_DefaultDuration = 0.10f;
        [SerializeField] private float m_ShakeFrequency = 50f;

        private Vector2 m_RestPosition;
        private Coroutine m_ActiveRoutine;
        private bool m_HasCapturedRest = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (m_TargetTransform == null)
            {
                m_TargetTransform = GetComponent<RectTransform>();
            }

            CaptureRest();
        }

        private void Start()
        {
            CaptureRest();
        }

        public void CaptureRest()
        {
            if (m_HasCapturedRest || m_TargetTransform == null) return;
            m_RestPosition = m_TargetTransform.anchoredPosition;
            m_HasCapturedRest = true;
        }

        public static void Shake(float intensityMultiplier = 1f, float customDuration = -1f)
        {
            if (Instance == null)
            {
                Instance = UnityEngine.Object.FindAnyObjectByType<UIMicroShake>();
                if (Instance == null)
                {
                    var sm = GameObject.Find("ScreenManager");
                    if (sm != null)
                    {
                        Instance = sm.AddComponent<UIMicroShake>();
                    }
                }
            }

            if (Instance != null)
            {
                Instance.PlayShake(intensityMultiplier, customDuration);
            }
        }

        public void PlayShake(float intensityMultiplier = 1f, float customDuration = -1f)
        {
            if (m_TargetTransform == null) return;

            CaptureRest();

            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
            }

            float duration = customDuration > 0f ? customDuration : m_DefaultDuration;
            float intensity = m_DefaultIntensity * intensityMultiplier;

            m_ActiveRoutine = StartCoroutine(ShakeRoutine(duration, intensity));
        }

        private IEnumerator ShakeRoutine(float duration, float intensity)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Exponential decay
                float decay = 1f - progress;
                float decaySq = decay * decay;

                // High-frequency alternating impulse
                float offsetX = Mathf.Sin(elapsed * m_ShakeFrequency) * intensity * decaySq;
                float offsetY = Mathf.Cos(elapsed * (m_ShakeFrequency * 1.3f)) * (intensity * 0.7f) * decaySq;

                if (m_TargetTransform != null)
                {
                    m_TargetTransform.anchoredPosition = m_RestPosition + new Vector2(offsetX, offsetY);
                }

                yield return null;
            }

            if (m_TargetTransform != null)
            {
                m_TargetTransform.anchoredPosition = m_RestPosition;
            }

            m_ActiveRoutine = null;
        }

        private void OnDisable()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_HasCapturedRest && m_TargetTransform != null)
            {
                m_TargetTransform.anchoredPosition = m_RestPosition;
            }
        }
    }
}
