using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Reusable robotic digital inspection scan effect:
    /// Thin (3-5px) horizontal pixel shimmer beam + tiny trailing data particles.
    /// Used specifically during meaningful events like Robot Inspection in Collection screen.
    /// STRICT EXCEPTION: Never used on Level Selection screen.
    /// </summary>
    public class RoboticDigitalScan : MonoBehaviour
    {
        private RectTransform m_ScanBeamRect;
        private Image m_ScanBeamImage;
        private CanvasGroup m_CanvasGroup;
        private Coroutine m_ActiveScanRoutine;
        private RoboticPixelFXPool m_PixelPool;

        private void Awake()
        {
            EnsureScanBeamVisual();
        }

        private void EnsureScanBeamVisual()
        {
            if (m_ScanBeamRect != null) return;

            GameObject beamObj = new GameObject("RoboticScanBeam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            beamObj.transform.SetParent(transform, false);

            m_ScanBeamRect = beamObj.GetComponent<RectTransform>();
            m_ScanBeamRect.anchorMin = new Vector2(0f, 0.5f);
            m_ScanBeamRect.anchorMax = new Vector2(1f, 0.5f);
            m_ScanBeamRect.pivot = new Vector2(0.5f, 0.5f);
            m_ScanBeamRect.sizeDelta = new Vector2(0f, 4f); // 4px thin beam

            m_ScanBeamImage = beamObj.GetComponent<Image>();
            m_ScanBeamImage.color = new Color(0.35f, 0.85f, 1f, 0.85f);
            m_ScanBeamImage.raycastTarget = false;

            m_CanvasGroup = beamObj.GetComponent<CanvasGroup>();
            m_CanvasGroup.blocksRaycasts = false;
            m_CanvasGroup.interactable = false;

            beamObj.SetActive(false);
        }

        public void SetPixelPool(RoboticPixelFXPool pool)
        {
            m_PixelPool = pool;
        }

        /// <summary>
        /// Sweeps a thin horizontal digital shimmer across the target rect from top to bottom (0.14 - 0.20s).
        /// </summary>
        public void PlayScan(RectTransform targetArea, Color tint, float duration = 0.16f, Action onComplete = null)
        {
            if (targetArea == null) return;
            EnsureScanBeamVisual();

            if (m_ActiveScanRoutine != null)
            {
                StopCoroutine(m_ActiveScanRoutine);
            }

            m_ScanBeamRect.SetParent(targetArea, false);
            m_ScanBeamRect.SetAsLastSibling();
            m_ScanBeamImage.color = tint;

            m_ActiveScanRoutine = StartCoroutine(ScanRoutine(targetArea, tint, duration, onComplete));
        }

        private IEnumerator ScanRoutine(RectTransform targetArea, Color tint, float duration, Action onComplete)
        {
            m_ScanBeamRect.gameObject.SetActive(true);
            m_CanvasGroup.alpha = 1f;

            Rect rect = targetArea.rect;
            float startY = rect.yMax;
            float endY = rect.yMin;

            float elapsed = 0f;
            int particleEmitCount = 0;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth linear sweep
                float currentY = Mathf.Lerp(startY, endY, t);
                m_ScanBeamRect.anchoredPosition = new Vector2(0f, currentY);

                // Subtle alpha fade at the start and end of sweep
                float alpha = Mathf.Sin(t * Mathf.PI);
                m_CanvasGroup.alpha = alpha * 0.9f;

                // Emit 3-4 subtle trailing data particles during sweep
                if (m_PixelPool != null && particleEmitCount < 4 && t >= (particleEmitCount + 1) * 0.22f)
                {
                    particleEmitCount++;
                    Vector2 emitPos = new Vector2(UnityEngine.Random.Range(rect.xMin * 0.7f, rect.xMax * 0.7f), currentY);
                    m_PixelPool.SpawnDataFloat(emitPos, targetArea, tint, 1, 15f);
                }

                yield return null;
            }

            m_ScanBeamRect.gameObject.SetActive(false);
            m_ActiveScanRoutine = null;
            onComplete?.Invoke();
        }

        public void StopScan()
        {
            if (m_ActiveScanRoutine != null)
            {
                StopCoroutine(m_ActiveScanRoutine);
                m_ActiveScanRoutine = null;
            }
            if (m_ScanBeamRect != null)
            {
                m_ScanBeamRect.gameObject.SetActive(false);
            }
        }
    }
}
