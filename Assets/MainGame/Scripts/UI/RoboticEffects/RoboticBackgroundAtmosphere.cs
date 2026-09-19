using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Subtle background atmosphere for the game menu:
    /// - Very slow ambient machine breathing on background layers (scale 1.00 -> 1.006 over 8s)
    /// - 2-3 very faint horizontal technical lines (alpha < 0.05)
    /// - 3-4 tiny slow-drifting data pixels (5-8 px/sec)
    /// Preserves 100% of existing yellow artwork and character art; strictly non-distracting.
    /// </summary>
    public class RoboticBackgroundAtmosphere : MonoBehaviour
    {
        private RectTransform m_Layer1;
        private RectTransform m_Layer2;
        private Coroutine m_BreathingRoutine;
        private readonly List<RectTransform> m_AmbientParticles = new List<RectTransform>();
        private readonly List<Vector2> m_ParticleVelocities = new List<Vector2>();

        private void Start()
        {
            FindBackgroundLayers();
            CreateTechnicalDetails();
            StartAtmosphere();
        }

        private void FindBackgroundLayers()
        {
            Transform l1 = transform.Find("Layer 01");
            if (l1 != null) m_Layer1 = l1 as RectTransform;

            Transform l2 = transform.Find("Layer 02");
            if (l2 != null) m_Layer2 = l2 as RectTransform;
        }

        private void CreateTechnicalDetails()
        {
            // 1. Create a container for subtle technical overlay elements
            GameObject techContainer = new GameObject("Atmosphere_TechGrid", typeof(RectTransform));
            techContainer.transform.SetParent(transform, false);
            techContainer.transform.SetSiblingIndex(2); // Between layer 02 and foreground screens

            RectTransform containerRt = techContainer.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;
            containerRt.anchoredPosition = Vector2.zero;

            // 2. Faint horizontal technical lines (measurements)
            float[] linePositionsY = new float[] { 220f, -180f, -380f };
            for (int i = 0; i < linePositionsY.Length; i++)
            {
                GameObject lineObj = new GameObject($"TechLine_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                lineObj.transform.SetParent(containerRt, false);

                RectTransform lrt = lineObj.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.05f, 0.5f);
                lrt.anchorMax = new Vector2(0.95f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(0f, linePositionsY[i]);
                lrt.sizeDelta = new Vector2(0f, 1f); // 1px thin line

                Image img = lineObj.GetComponent<Image>();
                img.color = new Color(0.25f, 0.20f, 0.10f, 0.04f); // Ultra-faint dark gold/amber matching yellow BG
                img.raycastTarget = false;
            }

            // 3. 3-4 tiny slow ambient data pixels
            Texture2D tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            Sprite pixelSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 1f);

            for (int i = 0; i < 4; i++)
            {
                GameObject pObj = new GameObject($"AmbientPixel_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pObj.transform.SetParent(containerRt, false);

                RectTransform prt = pObj.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.5f, 0.5f);
                prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(3f, 3f);
                prt.anchoredPosition = new Vector2(Random.Range(-800f, 800f), Random.Range(-400f, 400f));

                Image img = pObj.GetComponent<Image>();
                img.sprite = pixelSprite;
                img.color = new Color(0.85f, 0.65f, 0.20f, 0.08f); // Faint ambient dust/sparkle
                img.raycastTarget = false;

                m_AmbientParticles.Add(prt);
                m_ParticleVelocities.Add(new Vector2(Random.Range(-4f, 4f), Random.Range(3f, 7f)));
            }
        }

        private void StartAtmosphere()
        {
            if (m_BreathingRoutine != null) StopCoroutine(m_BreathingRoutine);
            m_BreathingRoutine = StartCoroutine(AtmosphereLoopRoutine());
        }

        private IEnumerator AtmosphereLoopRoutine()
        {
            float breathTimer = 0f;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                breathTimer += dt * 0.4f; // ~8s cycle

                // Extremely slow, subtle machine breathing (scale 1.000 -> 1.005)
                float breath = (Mathf.Sin(breathTimer) + 1f) * 0.5f;
                float currentScale = 1.0f + breath * 0.005f;

                if (m_Layer2 != null)
                {
                    m_Layer2.localScale = new Vector3(currentScale, currentScale, 1f);
                }

                // Drift ambient particles slowly
                for (int i = 0; i < m_AmbientParticles.Count; i++)
                {
                    RectTransform prt = m_AmbientParticles[i];
                    if (prt == null) continue;

                    Vector2 pos = prt.anchoredPosition + m_ParticleVelocities[i] * dt;
                    if (pos.y > 540f) pos.y = -540f;
                    if (pos.x > 960f) pos.x = -960f;
                    if (pos.x < -960f) pos.x = 960f;
                    prt.anchoredPosition = pos;
                }

                yield return null;
            }
        }

        private void OnDisable()
        {
            if (m_BreathingRoutine != null)
            {
                StopCoroutine(m_BreathingRoutine);
                m_BreathingRoutine = null;
            }
        }
    }
}
