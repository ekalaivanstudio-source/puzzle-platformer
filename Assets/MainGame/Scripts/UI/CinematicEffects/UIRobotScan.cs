using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// High-level coordinator for robot analysis scanning, telemetry data float,
    /// and surface reconstruction in the Collection screen.
    /// </summary>
    public class UIRobotScan : MonoBehaviour
    {
        [Header("Scanner Configuration")]
        [SerializeField] private Color m_ScanColor = new Color(0.35f, 0.88f, 1.0f, 0.95f);
        [SerializeField] private Color m_CoreColor = new Color(0.35f, 1.0f, 0.65f, 1.0f);
        [SerializeField] private float m_ScanDuration = 0.28f;

        private UICollectionScanner m_Scanner;

        private void Awake()
        {
            m_Scanner = GetComponent<UICollectionScanner>() ?? GetComponentInParent<UICollectionScanner>();
        }

        public void PlayRobotAnalysis(RectTransform targetArea, Graphic robotPortrait = null, Action onCore = null, Action onComplete = null)
        {
            if (robotPortrait != null)
            {
                CinematicUIEffect fx = robotPortrait.GetComponent<CinematicUIEffect>() ?? robotPortrait.gameObject.AddComponent<CinematicUIEffect>();
                fx.PlayCharacterScan(m_ScanDuration, 1.0f, onCore, null);
            }

            if (m_Scanner != null)
            {
                m_Scanner.PlayScan(targetArea, onCore, onComplete);
            }
            else
            {
                if (CinematicUIFXManager.Instance != null && CinematicUIFXManager.Instance.CollectionScanner != null)
                {
                    CinematicUIFXManager.Instance.CollectionScanner.PlayScan(targetArea, onCore, onComplete);
                }
                else
                {
                    onCore?.Invoke();
                    onComplete?.Invoke();
                }
            }

            // Emit floating telemetry data particles
            if (targetArea != null && CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnDataFloat(targetArea.anchoredPosition, targetArea.parent, m_ScanColor, 6, 40f);
            }
        }

        public void StopActiveScan()
        {
            if (m_Scanner != null) m_Scanner.StopActiveScan();
        }
    }
}
