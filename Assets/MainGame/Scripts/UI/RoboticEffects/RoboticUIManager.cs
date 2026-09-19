using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Central manager and service locator for the Robotic UI Effect System.
    /// Coordinates the pixel particle pool, digital inspection scan, CRT overlay, and panel effects.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoboticUIManager : MonoBehaviour
    {
        private static RoboticUIManager s_Instance;

        public static RoboticUIManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = UnityEngine.Object.FindAnyObjectByType<RoboticUIManager>();
                    if (s_Instance == null)
                    {
                        Canvas rootCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                        Transform parent = rootCanvas != null ? rootCanvas.transform : null;

                        GameObject go = new GameObject("RoboticUIManager", typeof(RectTransform), typeof(RoboticUIManager));
                        if (parent != null)
                        {
                            go.transform.SetParent(parent, false);
                            go.transform.SetAsLastSibling();
                            RectTransform rt = go.GetComponent<RectTransform>();
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.sizeDelta = Vector2.zero;
                            rt.anchoredPosition = Vector2.zero;
                        }
                        s_Instance = go.GetComponent<RoboticUIManager>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("Components")]
        [SerializeField] private RoboticPixelFXPool m_PixelPool;
        [SerializeField] private RoboticDigitalScan m_DigitalScan;
        [SerializeField] private RoboticCRTOverlay m_CRTOverlay;

        public RoboticPixelFXPool PixelPool => m_PixelPool;
        public RoboticDigitalScan DigitalScan => m_DigitalScan;
        public RoboticCRTOverlay CRTOverlay => m_CRTOverlay;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            EnsureCanvasParent();
            EnsureComponents();
        }

        private void Start()
        {
            EnsureBackgroundAtmosphere();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void EnsureCanvasParent()
        {
            if (transform.parent == null)
            {
                Canvas rootCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                if (rootCanvas != null)
                {
                    transform.SetParent(rootCanvas.transform, false);
                    transform.SetAsLastSibling();
                    RectTransform rt = GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.sizeDelta = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        private void EnsureComponents()
        {
            if (m_PixelPool == null)
            {
                m_PixelPool = GetComponentInChildren<RoboticPixelFXPool>(true);
                if (m_PixelPool == null)
                {
                    GameObject poolObj = new GameObject("RoboticPixelFXPool", typeof(RectTransform), typeof(RoboticPixelFXPool));
                    poolObj.transform.SetParent(transform, false);
                    m_PixelPool = poolObj.GetComponent<RoboticPixelFXPool>();
                }
            }
            m_PixelPool.InitializePool();

            if (m_DigitalScan == null)
            {
                m_DigitalScan = GetComponentInChildren<RoboticDigitalScan>(true);
                if (m_DigitalScan == null)
                {
                    GameObject scanObj = new GameObject("RoboticDigitalScan", typeof(RectTransform), typeof(RoboticDigitalScan));
                    scanObj.transform.SetParent(transform, false);
                    m_DigitalScan = scanObj.GetComponent<RoboticDigitalScan>();
                }
            }
            m_DigitalScan.SetPixelPool(m_PixelPool);

            if (m_CRTOverlay == null)
            {
                m_CRTOverlay = GetComponentInChildren<RoboticCRTOverlay>(true);
                if (m_CRTOverlay == null)
                {
                    // Create subtle overlay as the last sibling of ScreenManager or this transform
                    GameObject crtObj = new GameObject("RoboticCRTOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(RoboticCRTOverlay));
                    crtObj.transform.SetParent(transform, false);

                    RectTransform rt = crtObj.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;

                    m_CRTOverlay = crtObj.GetComponent<RoboticCRTOverlay>();
                }
            }
        }

        private void EnsureBackgroundAtmosphere()
        {
            GameObject bgGo = GameObject.Find("Backagrond") ?? GameObject.Find("Background");
            if (bgGo == null)
            {
                Transform screenMgr = transform.parent != null ? transform.parent : transform;
                Transform bg = screenMgr.Find("Backagrond") ?? screenMgr.Find("Background");
                if (bg != null) bgGo = bg.gameObject;
            }

            if (bgGo != null && bgGo.GetComponent<RoboticBackgroundAtmosphere>() == null)
            {
                bgGo.AddComponent<RoboticBackgroundAtmosphere>();
            }
        }

        public void TriggerScreenGlitch(float duration = 0.08f)
        {
            if (m_CRTOverlay != null)
            {
                m_CRTOverlay.TriggerGlitch(duration);
            }
        }

        public void SpawnSparkBurst(Vector2 screenPos, Transform parent, Color color, int count = 6, float radius = 16f)
        {
            if (m_PixelPool != null)
            {
                m_PixelPool.SpawnSparkBurst(screenPos, parent, color, count, radius);
            }
        }

        public void SpawnDataFloat(Vector2 screenPos, Transform parent, Color color, int count = 5, float spread = 40f)
        {
            if (m_PixelPool != null)
            {
                m_PixelPool.SpawnDataFloat(screenPos, parent, color, count, spread);
            }
        }

        public void PlayInspectionScan(RectTransform targetArea, Color tint, float duration = 0.16f, Action onComplete = null)
        {
            if (m_DigitalScan != null)
            {
                m_DigitalScan.PlayScan(targetArea, tint, duration, onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Ensures a RoboticUIPanelEffect exists on the target Graphic and returns it.
        /// </summary>
        public static RoboticUIPanelEffect GetOrAddPanelEffect(Graphic graphic, Color? edgeColor = null, bool cornerBrackets = true, bool scanShimmer = false)
        {
            if (graphic == null) return null;

            RoboticUIPanelEffect fx = graphic.GetComponent<RoboticUIPanelEffect>();
            if (fx == null)
            {
                fx = graphic.gameObject.AddComponent<RoboticUIPanelEffect>();
                if (edgeColor.HasValue) fx.EdgeColor = edgeColor.Value;
            }
            return fx;
        }
    }
}
