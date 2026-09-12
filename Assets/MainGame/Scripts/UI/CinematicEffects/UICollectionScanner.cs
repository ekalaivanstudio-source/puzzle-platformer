using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// High-energy character analysis scanner for the Collection screen.
    /// Provides:
    /// - Quantized horizontal pixel scanline sweep top-to-bottom across robot preview
    /// - Robot core energy activation when the scanner intersects the center
    /// - Temporary character outline reaction
    /// - Floating data stream particles and energy dots trailing the scan
    /// </summary>
    [DisallowMultipleComponent]
    public class UICollectionScanner : MonoBehaviour
    {
        [Header("Scanner Visuals")]
        [SerializeField] private Color m_ScanBeamColor = new Color(0.35f, 0.85f, 1.0f, 0.9f);
        [SerializeField] private Color m_CoreEnergyColor = new Color(0.4f, 0.95f, 1.0f, 1.0f);
        [SerializeField] private float m_BeamHeight = 4f;
        [SerializeField] private int m_QuantizeSteps = 24;

        private RectTransform m_ScanBeamRect;
        private Image m_ScanBeamImage;
        private CanvasGroup m_BeamCanvasGroup;
        private Coroutine m_ActiveScanRoutine;

        private void Awake()
        {
            EnsureScanBeam();
        }

        private void EnsureScanBeam()
        {
            if (m_ScanBeamRect != null) return;

            GameObject beamObj = new GameObject("CollectionScanBeam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            beamObj.transform.SetParent(transform, false);

            m_ScanBeamRect = beamObj.GetComponent<RectTransform>();
            m_ScanBeamRect.anchorMin = new Vector2(0f, 0.5f);
            m_ScanBeamRect.anchorMax = new Vector2(1f, 0.5f);
            m_ScanBeamRect.pivot = new Vector2(0.5f, 0.5f);
            m_ScanBeamRect.sizeDelta = new Vector2(0f, m_BeamHeight);

            m_ScanBeamImage = beamObj.GetComponent<Image>();
            m_ScanBeamImage.color = m_ScanBeamColor;
            m_ScanBeamImage.raycastTarget = false;

            m_BeamCanvasGroup = beamObj.GetComponent<CanvasGroup>();
            m_BeamCanvasGroup.blocksRaycasts = false;
            m_BeamCanvasGroup.interactable = false;

            beamObj.SetActive(false);
        }

        /// <summary>
        /// Sweeps the character scanner across targetArea with core activation and data particle emission.
        /// </summary>
        public void PlayScan(RectTransform targetArea, Action onCoreActivated = null, Action onComplete = null)
        {
            if (targetArea == null)
            {
                onComplete?.Invoke();
                return;
            }

            EnsureScanBeam();

            if (m_ActiveScanRoutine != null)
            {
                StopCoroutine(m_ActiveScanRoutine);
            }

            m_ActiveScanRoutine = StartCoroutine(ScanRoutine(targetArea, onCoreActivated, onComplete));
        }

        private IEnumerator ScanRoutine(RectTransform targetArea, Action onCoreActivated, Action onComplete)
        {
            m_ScanBeamRect.SetParent(targetArea, false);
            m_ScanBeamRect.gameObject.SetActive(true);
            m_BeamCanvasGroup.alpha = 1.0f;

            float targetHeight = targetArea.rect.height;
            float topY = targetHeight * 0.5f;
            float bottomY = -targetHeight * 0.5f;

            float duration = 0.26f;
            float elapsed = 0f;
            bool coreTriggered = false;

            // Audio boot
            UIFeedbackAudio.PlaySfx(UISfxType.RobotBoot, 0.70f, 0.02f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Quantize progress to pixel steps
                float steppedProgress = Mathf.Floor(progress * m_QuantizeSteps) / (float)m_QuantizeSteps;
                float currentY = Mathf.Lerp(topY, bottomY, steppedProgress);

                m_ScanBeamRect.anchoredPosition = new Vector2(0f, currentY);

                // Spawn occasional data particles along the beam
                if (UnityEngine.Random.value < 0.45f)
                {
                    float randX = UnityEngine.Random.Range(-targetArea.rect.width * 0.4f, targetArea.rect.width * 0.4f);
                    UIParticleFX.DataParticles(new Vector2(randX, currentY), targetArea, m_ScanBeamColor, 1);
                }

                // Core intersection (midway)
                if (!coreTriggered && progress >= 0.45f)
                {
                    coreTriggered = true;
                    onCoreActivated?.Invoke();

                    // Core activation reaction on target shader
                    CinematicUIEffect effect = targetArea.GetComponent<CinematicUIEffect>() ?? targetArea.GetComponentInChildren<CinematicUIEffect>();
                    if (effect != null)
                    {
                        effect.TriggerRadialPulse(0.28f, m_CoreEnergyColor, 1.2f, 2.5f);
                        effect.TriggerBorderPulse(0.25f, m_CoreEnergyColor, 1.8f);
                    }

                    // Burst of data & spark particles from core
                    UIParticleFX.Sparks(Vector2.zero, targetArea, m_CoreEnergyColor, 5, 22f);
                    UIParticleFX.DataParticles(Vector2.zero, targetArea, m_CoreEnergyColor, 4);

                    UIFeedbackAudio.PlaySfx(UISfxType.Deploy, 0.75f, 0.04f);
                }

                yield return null;
            }

            if (!coreTriggered)
            {
                onCoreActivated?.Invoke();
            }

            // Fade beam
            float fadeElapsed = 0f;
            float fadeDur = 0.06f;
            while (fadeElapsed < fadeDur)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                m_BeamCanvasGroup.alpha = 1f - (fadeElapsed / fadeDur);
                yield return null;
            }

            m_ScanBeamRect.gameObject.SetActive(false);
            m_ActiveScanRoutine = null;
            onComplete?.Invoke();
        }

        public void StopActiveScan()
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
