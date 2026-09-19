using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Procedural electrical energy flow effect for mechanical UI components.
    /// Provides:
    /// - Traveling segmented perimeter pulses (moving clockwise/counter-clockwise along edges).
    /// - Corner arc discharges with directional spark bursts.
    /// - Kinetic lock perimeter surges (double-flash surge upon mechanical lock).
    /// - Subtle ambient idle micro-pulses for living-machine atmosphere.
    /// 100% shader-independent, canvas-native, and preserves all underlying artwork.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIEnergyFlow : MonoBehaviour
    {
        [Header("Energy Configuration")]
        [SerializeField] private Color m_DefaultPulseColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private float m_EdgeThickness = 2.5f;
        [SerializeField] private float m_PulseSegmentRatio = 0.28f;
        [SerializeField] private bool m_ShowPassiveTrace = true;
        [SerializeField] [Range(0f, 0.35f)] private float m_PassiveTraceAlpha = 0.12f;

        private RectTransform m_TargetRect;
        private RectTransform m_PerimeterRoot;

        // 4 Base Edge Traces
        private Image m_TopTrace;
        private Image m_RightTrace;
        private Image m_BottomTrace;
        private Image m_LeftTrace;

        // 4 Traveling Pulse Segments
        private RectTransform m_TopPulseRt;
        private RectTransform m_RightPulseRt;
        private RectTransform m_BottomPulseRt;
        private RectTransform m_LeftPulseRt;

        private Image m_TopPulseImg;
        private Image m_RightPulseImg;
        private Image m_BottomPulseImg;
        private Image m_LeftPulseImg;

        // 4 Corner Arc Nodes (0 = TopLeft, 1 = TopRight, 2 = BottomRight, 3 = BottomLeft)
        private RectTransform[] m_CornerNodes;
        private Image[] m_CornerImages;

        private Coroutine m_ActivePulseRoutine;
        private Coroutine m_SurgeRoutine;
        private Coroutine m_IdleRoutine;
        private bool m_IsInitialized = false;

        private static Sprite s_WhitePixelSprite;

        public static Sprite GetWhitePixelSprite()
        {
            if (s_WhitePixelSprite != null) return s_WhitePixelSprite;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "UIEnergyFlow_Pixel"
            };
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            s_WhitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            return s_WhitePixelSprite;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            m_ActivePulseRoutine = null;
            m_SurgeRoutine = null;
            m_IdleRoutine = null;
            HideAllPulses();
        }

        public void EnsureInitialized()
        {
            if (m_IsInitialized) return;

            if (m_TargetRect == null)
            {
                m_TargetRect = GetComponent<RectTransform>();
            }

            BuildPerimeterHierarchy();
            m_IsInitialized = true;
        }

        private void BuildPerimeterHierarchy()
        {
            if (m_TargetRect == null) return;

            Transform existing = m_TargetRect.Find("__EnergyPerimeter");
            if (existing != null)
            {
                m_PerimeterRoot = existing as RectTransform;
            }
            else
            {
                GameObject pObj = new GameObject("__EnergyPerimeter", typeof(RectTransform));
                pObj.transform.SetParent(m_TargetRect, false);
                m_PerimeterRoot = pObj.GetComponent<RectTransform>();
            }

            m_PerimeterRoot.anchorMin = Vector2.zero;
            m_PerimeterRoot.anchorMax = Vector2.one;
            m_PerimeterRoot.sizeDelta = Vector2.zero;
            m_PerimeterRoot.anchoredPosition = Vector2.zero;
            m_PerimeterRoot.localScale = Vector3.one;

            Sprite pixelSprite = GetWhitePixelSprite();

            // 1. Base Traces
            Color traceColor = m_DefaultPulseColor;
            traceColor.a = m_ShowPassiveTrace ? m_PassiveTraceAlpha : 0f;

            m_TopTrace = CreateEdgeTrace("Trace_Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, m_EdgeThickness), new Vector2(0f, 0f), traceColor, pixelSprite);
            m_RightTrace = CreateEdgeTrace("Trace_Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(m_EdgeThickness, 0f), new Vector2(0f, 0f), traceColor, pixelSprite);
            m_BottomTrace = CreateEdgeTrace("Trace_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, m_EdgeThickness), new Vector2(0f, 0f), traceColor, pixelSprite);
            m_LeftTrace = CreateEdgeTrace("Trace_Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(m_EdgeThickness, 0f), new Vector2(0f, 0f), traceColor, pixelSprite);

            // 2. Pulse Segments
            m_TopPulseRt = CreatePulseSegment("Pulse_Top", pixelSprite, out m_TopPulseImg);
            m_RightPulseRt = CreatePulseSegment("Pulse_Right", pixelSprite, out m_RightPulseImg);
            m_BottomPulseRt = CreatePulseSegment("Pulse_Bottom", pixelSprite, out m_BottomPulseImg);
            m_LeftPulseRt = CreatePulseSegment("Pulse_Left", pixelSprite, out m_LeftPulseImg);

            // 3. Corner Nodes (0: Top-Left, 1: Top-Right, 2: Bottom-Right, 3: Bottom-Left)
            m_CornerNodes = new RectTransform[4];
            m_CornerImages = new Image[4];
            Vector2[] cornerAnchors = new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject cObj = new GameObject($"CornerNode_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cObj.transform.SetParent(m_PerimeterRoot, false);
                RectTransform crt = cObj.GetComponent<RectTransform>();
                crt.anchorMin = cornerAnchors[i];
                crt.anchorMax = cornerAnchors[i];
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(m_EdgeThickness * 2.2f, m_EdgeThickness * 2.2f);
                crt.anchoredPosition = Vector2.zero;

                Image cImg = cObj.GetComponent<Image>();
                cImg.sprite = pixelSprite;
                cImg.color = Color.clear;
                cImg.raycastTarget = false;

                m_CornerNodes[i] = crt;
                m_CornerImages[i] = cImg;
            }

            HideAllPulses();
        }

        private Image CreateEdgeTrace(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, Color color, Sprite sprite)
        {
            Transform existing = m_PerimeterRoot.Find(name);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(m_PerimeterRoot, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            Image img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private RectTransform CreatePulseSegment(string name, Sprite sprite, out Image img)
        {
            Transform existing = m_PerimeterRoot.Find(name);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(m_PerimeterRoot, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.clear;
            img.raycastTarget = false;
            return rt;
        }

        private void HideAllPulses()
        {
            if (m_TopPulseImg != null) m_TopPulseImg.color = Color.clear;
            if (m_RightPulseImg != null) m_RightPulseImg.color = Color.clear;
            if (m_BottomPulseImg != null) m_BottomPulseImg.color = Color.clear;
            if (m_LeftPulseImg != null) m_LeftPulseImg.color = Color.clear;

            if (m_CornerImages != null)
            {
                for (int i = 0; i < m_CornerImages.Length; i++)
                {
                    if (m_CornerImages[i] != null) m_CornerImages[i].color = Color.clear;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fires a traveling electrical energy pulse clockwise or counter-clockwise around the perimeter.
        /// </summary>
        public void TriggerTravelingPulse(float duration, Color? color = null, Action onComplete = null, bool clockwise = true)
        {
            EnsureInitialized();
            if (m_ActivePulseRoutine != null) StopCoroutine(m_ActivePulseRoutine);
            m_ActivePulseRoutine = StartCoroutine(TravelingPulseRoutine(duration, color ?? m_DefaultPulseColor, onComplete, clockwise));
        }

        /// <summary>
        /// Fires a high-energy kinetic lock surge (all edges flash bright, micro-stutter, and discharge sparks).
        /// Ideal for hard mechanical locks, center seam impacts, or button selection.
        /// </summary>
        public void TriggerPerimeterSurge(float duration, Color? color = null, Action onComplete = null)
        {
            EnsureInitialized();
            if (m_SurgeRoutine != null) StopCoroutine(m_SurgeRoutine);
            m_SurgeRoutine = StartCoroutine(PerimeterSurgeRoutine(duration, color ?? m_DefaultPulseColor, onComplete));
        }

        /// <summary>
        /// Fires an instantaneous arc discharge at a specific corner (0 = TL, 1 = TR, 2 = BR, 3 = BL).
        /// </summary>
        public void TriggerCornerDischarge(int cornerIndex, Color? color = null, bool spawnSparks = true)
        {
            EnsureInitialized();
            if (m_CornerImages == null || cornerIndex < 0 || cornerIndex >= m_CornerImages.Length) return;
            StartCoroutine(SingleCornerDischargeRoutine(cornerIndex, color ?? m_DefaultPulseColor, spawnSparks));
        }

        /// <summary>
        /// Enables or disables periodic subtle ambient micro-pulses for living machine presence.
        /// </summary>
        public void SetIdleAtmosphere(bool enable, float minInterval = 3.5f, float maxInterval = 5.5f, Color? color = null)
        {
            if (m_IdleRoutine != null)
            {
                StopCoroutine(m_IdleRoutine);
                m_IdleRoutine = null;
            }

            if (enable && gameObject.activeInHierarchy)
            {
                m_IdleRoutine = StartCoroutine(IdleAtmosphereRoutine(minInterval, maxInterval, color ?? m_DefaultPulseColor));
            }
        }

        // ═════════════════════════════════════════════════════════════════════════════
        // COROUTINES
        // ═════════════════════════════════════════════════════════════════════════════

        private IEnumerator TravelingPulseRoutine(float totalDuration, Color pulseColor, Action onComplete, bool clockwise)
        {
            HideAllPulses();

            float quarterDuration = totalDuration * 0.25f;
            Vector2 size = m_TargetRect != null ? m_TargetRect.rect.size : new Vector2(200f, 60f);
            float pulseWidth = Mathf.Max(18f, size.x * m_PulseSegmentRatio);
            float pulseHeight = Mathf.Max(14f, size.y * m_PulseSegmentRatio);

            // Audio tick for electrical travel start
            UIFeedbackAudio.PlaySfx(UISfxType.Pulse, 0.40f, 0.05f);

            int[] edgeSequence = clockwise ? new int[] { 0, 1, 2, 3 } : new int[] { 0, 3, 2, 1 };

            for (int step = 0; step < 4; step++)
            {
                int edge = edgeSequence[step];
                float elapsed = 0f;

                while (elapsed < quarterDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / quarterDuration);

                    switch (edge)
                    {
                        case 0: // TOP EDGE (Left to Right)
                            if (m_TopPulseRt != null && m_TopPulseImg != null)
                            {
                                m_TopPulseImg.color = pulseColor;
                                m_TopPulseRt.anchorMin = new Vector2(0f, 1f);
                                m_TopPulseRt.anchorMax = new Vector2(0f, 1f);
                                m_TopPulseRt.pivot = new Vector2(0.5f, 1f);
                                m_TopPulseRt.sizeDelta = new Vector2(pulseWidth, m_EdgeThickness * 1.5f);
                                float posX = clockwise ? Mathf.Lerp(0f, size.x, t) : Mathf.Lerp(size.x, 0f, t);
                                m_TopPulseRt.anchoredPosition = new Vector2(posX, 0f);
                            }
                            break;

                        case 1: // RIGHT EDGE (Top to Bottom)
                            if (m_RightPulseRt != null && m_RightPulseImg != null)
                            {
                                m_RightPulseImg.color = pulseColor;
                                m_RightPulseRt.anchorMin = new Vector2(1f, 0f);
                                m_RightPulseRt.anchorMax = new Vector2(1f, 0f);
                                m_RightPulseRt.pivot = new Vector2(1f, 0.5f);
                                m_RightPulseRt.sizeDelta = new Vector2(m_EdgeThickness * 1.5f, pulseHeight);
                                float posY = clockwise ? Mathf.Lerp(size.y, 0f, t) : Mathf.Lerp(0f, size.y, t);
                                m_RightPulseRt.anchoredPosition = new Vector2(0f, posY);
                            }
                            break;

                        case 2: // BOTTOM EDGE (Right to Left)
                            if (m_BottomPulseRt != null && m_BottomPulseImg != null)
                            {
                                m_BottomPulseImg.color = pulseColor;
                                m_BottomPulseRt.anchorMin = new Vector2(0f, 0f);
                                m_BottomPulseRt.anchorMax = new Vector2(0f, 0f);
                                m_BottomPulseRt.pivot = new Vector2(0.5f, 0f);
                                m_BottomPulseRt.sizeDelta = new Vector2(pulseWidth, m_EdgeThickness * 1.5f);
                                float posX = clockwise ? Mathf.Lerp(size.x, 0f, t) : Mathf.Lerp(0f, size.x, t);
                                m_BottomPulseRt.anchoredPosition = new Vector2(posX, 0f);
                            }
                            break;

                        case 3: // LEFT EDGE (Bottom to Top)
                            if (m_LeftPulseRt != null && m_LeftPulseImg != null)
                            {
                                m_LeftPulseImg.color = pulseColor;
                                m_LeftPulseRt.anchorMin = new Vector2(0f, 0f);
                                m_LeftPulseRt.anchorMax = new Vector2(0f, 0f);
                                m_LeftPulseRt.pivot = new Vector2(0f, 0.5f);
                                m_LeftPulseRt.sizeDelta = new Vector2(m_EdgeThickness * 1.5f, pulseHeight);
                                float posY = clockwise ? Mathf.Lerp(0f, size.y, t) : Mathf.Lerp(size.y, 0f, t);
                                m_LeftPulseRt.anchoredPosition = new Vector2(0f, posY);
                            }
                            break;
                    }

                    yield return null;
                }

                // Hide completed edge
                switch (edge)
                {
                    case 0: if (m_TopPulseImg != null) m_TopPulseImg.color = Color.clear; break;
                    case 1: if (m_RightPulseImg != null) m_RightPulseImg.color = Color.clear; break;
                    case 2: if (m_BottomPulseImg != null) m_BottomPulseImg.color = Color.clear; break;
                    case 3: if (m_LeftPulseImg != null) m_LeftPulseImg.color = Color.clear; break;
                }

                // Corner arrival: trigger arc discharge
                int nextCorner = clockwise ? (edge + 1) % 4 : (edge == 0 ? 3 : edge - 1);
                TriggerCornerDischarge(nextCorner, pulseColor, spawnSparks: step == 1 || step == 3);
            }

            HideAllPulses();
            m_ActivePulseRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator PerimeterSurgeRoutine(float duration, Color surgeColor, Action onComplete)
        {
            HideAllPulses();

            // Flash all 4 traces bright white/surgeColor
            Color brightWhite = Color.white;
            brightWhite.a = 1.0f;

            // Phase 1: High energy spike (0.04s)
            SetAllTracesColor(brightWhite, 1.0f);
            yield return new WaitForSecondsRealtime(0.04f);

            // Phase 2: Stutter flicker (0.03s)
            SetAllTracesColor(surgeColor, 0.45f);
            yield return new WaitForSecondsRealtime(0.03f);

            // Phase 3: Secondary electric burst (0.04s)
            SetAllTracesColor(surgeColor, 1.0f);
            for (int i = 0; i < 4; i++)
            {
                TriggerCornerDischarge(i, surgeColor, spawnSparks: i % 2 == 0);
            }

            // Phase 4: Dissipation decay
            float decayDur = Mathf.Max(0.06f, duration - 0.11f);
            float elapsed = 0f;
            while (elapsed < decayDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / decayDur);
                float alpha = Mathf.Lerp(1.0f, m_ShowPassiveTrace ? m_PassiveTraceAlpha : 0f, UIEasing.Evaluate(EasingType.EaseOutQuad, t));
                SetAllTracesColor(surgeColor, alpha);
                yield return null;
            }

            ResetTracesToPassive();
            m_SurgeRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator SingleCornerDischargeRoutine(int cornerIdx, Color arcColor, bool spawnSparks)
        {
            if (m_CornerImages[cornerIdx] == null) yield break;

            m_CornerImages[cornerIdx].color = Color.white;
            if (m_CornerNodes[cornerIdx] != null)
            {
                m_CornerNodes[cornerIdx].localScale = new Vector3(1.6f, 1.6f, 1f);
            }

            if (spawnSparks)
            {
                RoboticPixelFXPool pool = RoboticPixelFXPool.Instance;
                if (pool != null && m_CornerNodes[cornerIdx] != null)
                {
                    pool.SpawnSparkBurst(Vector2.zero, m_CornerNodes[cornerIdx], arcColor, 2, 12f);
                }
            }

            float dischargeDur = 0.08f;
            float elapsed = 0f;
            while (elapsed < dischargeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dischargeDur);
                if (m_CornerImages[cornerIdx] != null)
                {
                    m_CornerImages[cornerIdx].color = Color.Lerp(arcColor, Color.clear, t);
                }
                if (m_CornerNodes[cornerIdx] != null)
                {
                    m_CornerNodes[cornerIdx].localScale = Vector3.Lerp(new Vector3(1.6f, 1.6f, 1f), Vector3.one, t);
                }
                yield return null;
            }

            if (m_CornerImages[cornerIdx] != null)
            {
                m_CornerImages[cornerIdx].color = Color.clear;
            }
        }

        private IEnumerator IdleAtmosphereRoutine(float minInterval, float maxInterval, Color color)
        {
            while (true)
            {
                float wait = UnityEngine.Random.Range(minInterval, maxInterval);
                float timer = 0f;
                while (timer < wait)
                {
                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }

                // Randomly choose between a subtle corner arc discharge or a rapid perimeter micro-pulse
                int choice = UnityEngine.Random.Range(0, 3);
                if (choice == 0)
                {
                    int corner = UnityEngine.Random.Range(0, 4);
                    TriggerCornerDischarge(corner, color, spawnSparks: true);
                }
                else
                {
                    Color faintPulse = color;
                    faintPulse.a = 0.75f;
                    TriggerTravelingPulse(0.38f, faintPulse, null, clockwise: UnityEngine.Random.value > 0.5f);
                }
            }
        }

        private void SetAllTracesColor(Color baseColor, float alpha)
        {
            Color c = baseColor;
            c.a = alpha;
            if (m_TopTrace != null) m_TopTrace.color = c;
            if (m_RightTrace != null) m_RightTrace.color = c;
            if (m_BottomTrace != null) m_BottomTrace.color = c;
            if (m_LeftTrace != null) m_LeftTrace.color = c;
        }

        private void ResetTracesToPassive()
        {
            Color c = m_DefaultPulseColor;
            c.a = m_ShowPassiveTrace ? m_PassiveTraceAlpha : 0f;
            SetAllTracesColor(c, c.a);
        }
    }
}
