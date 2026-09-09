using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Zero-allocation object pool for micro pixel-art particles:
    /// - Spark bursts (button confirm, panel slam)
    /// - Inspection data float (collection robot focus)
    /// - Corner activation shards
    /// All particles use point-filtered procedural pixel sprites to preserve retro pixel-art sharpness.
    /// </summary>
    public class RoboticPixelFXPool : MonoBehaviour
    {
        private class PixelParticle
        {
            public GameObject root;
            public RectTransform rect;
            public Image image;
            public CanvasGroup canvasGroup;
            public bool isBusy;
        }

        private static RoboticPixelFXPool s_Instance;
        public static RoboticPixelFXPool Instance
        {
            get
            {
                if (s_Instance == null) s_Instance = FindAnyObjectByType<RoboticPixelFXPool>();
                return s_Instance;
            }
        }

        private const int POOL_SIZE = 36;
        private readonly List<PixelParticle> m_Pool = new List<PixelParticle>(POOL_SIZE);
        private Transform m_PoolContainer;
        private Sprite m_PixelSquareSprite;
        private Sprite m_PixelSmallSprite;

        private void Awake()
        {
            s_Instance = this;
            InitializePool();
        }

        public void InitializePool()
        {
            if (m_Pool.Count > 0) return;

            CreateSprites();

            GameObject containerObj = new GameObject("RoboticPixelPool_Container", typeof(RectTransform));
            containerObj.transform.SetParent(transform, false);
            m_PoolContainer = containerObj.transform;

            RectTransform containerRt = containerObj.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;
            containerRt.anchoredPosition = Vector2.zero;

            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject pObj = new GameObject($"PixelFX_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                pObj.transform.SetParent(m_PoolContainer, false);

                RectTransform rt = pObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(4f, 4f);

                Image img = pObj.GetComponent<Image>();
                img.sprite = m_PixelSquareSprite;
                img.raycastTarget = false;

                CanvasGroup cg = pObj.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                pObj.SetActive(false);

                m_Pool.Add(new PixelParticle
                {
                    root = pObj,
                    rect = rt,
                    image = img,
                    canvasGroup = cg,
                    isBusy = false
                });
            }
        }

        private void CreateSprites()
        {
            if (m_PixelSquareSprite != null) return;

            // 4x4 crisp point-filtered square pixel
            Texture2D tex4 = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols4 = new Color[16];
            for (int i = 0; i < 16; i++) cols4[i] = Color.white;
            tex4.SetPixels(cols4);
            tex4.Apply();
            m_PixelSquareSprite = Sprite.Create(tex4, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);

            // 2x2 tiny crisp pixel
            Texture2D tex2 = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols2 = new Color[4];
            for (int i = 0; i < 4; i++) cols2[i] = Color.white;
            tex2.SetPixels(cols2);
            tex2.Apply();
            m_PixelSmallSprite = Sprite.Create(tex2, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        }

        private PixelParticle GetAvailable()
        {
            for (int i = 0; i < m_Pool.Count; i++)
            {
                if (!m_Pool[i].isBusy)
                {
                    m_Pool[i].isBusy = true;
                    return m_Pool[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Spawns a fast mechanical spark burst (4-8 tiny pixels radiating outward and disappearing in 0.12s).
        /// Ideal for button confirm punches and dialog impact slams.
        /// </summary>
        public void SpawnSparkBurst(Vector2 centerPosition, Transform targetParent, Color color, int count = 6, float radius = 16f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;

            for (int i = 0; i < count; i++)
            {
                PixelParticle p = GetAvailable();
                if (p == null) break;

                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float dist = radius * UnityEngine.Random.Range(0.6f, 1.2f);
                Vector2 targetPos = centerPosition + dir * dist;

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = (i % 2 == 0) ? new Vector2(4f, 4f) : new Vector2(3f, 3f);
                p.rect.anchoredPosition = centerPosition;
                p.image.sprite = m_PixelSquareSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateBurstParticle(p, centerPosition, targetPos, 0.12f));
            }
        }

        /// <summary>
        /// Spawns subtle data pixels that float upward and dissolve (0.24s).
        /// Used for Collection robot inspection and diagnostics readouts.
        /// </summary>
        public void SpawnDataFloat(Vector2 centerPosition, Transform targetParent, Color color, int count = 5, float spread = 40f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;

            for (int i = 0; i < count; i++)
            {
                PixelParticle p = GetAvailable();
                if (p == null) break;

                float offsetX = UnityEngine.Random.Range(-spread, spread);
                float offsetY = UnityEngine.Random.Range(-15f, 15f);
                Vector2 start = centerPosition + new Vector2(offsetX, offsetY);
                Vector2 end = start + new Vector2(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(20f, 38f));

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = new Vector2(3f, 3f);
                p.rect.anchoredPosition = start;
                p.image.sprite = m_PixelSmallSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateFloatParticle(p, start, end, UnityEngine.Random.Range(0.20f, 0.28f)));
            }
        }

        private IEnumerator AnimateBurstParticle(PixelParticle p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Quick outward deceleration
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                p.rect.anchoredPosition = Vector2.Lerp(start, end, ease);
                p.canvasGroup.alpha = 1f - t;
                yield return null;
            }

            p.root.SetActive(false);
            p.rect.SetParent(m_PoolContainer, false);
            p.isBusy = false;
        }

        private IEnumerator AnimateFloatParticle(PixelParticle p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            float wobbleFreq = UnityEngine.Random.Range(2f, 4f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float wobble = Mathf.Sin(t * Mathf.PI * wobbleFreq) * 3f;
                Vector2 pos = Vector2.Lerp(start, end, t);
                pos.x += wobble;

                p.rect.anchoredPosition = pos;
                p.canvasGroup.alpha = (t < 0.2f) ? (t / 0.2f) : (1f - (t - 0.2f) / 0.8f);
                yield return null;
            }

            p.root.SetActive(false);
            p.rect.SetParent(m_PoolContainer, false);
            p.isBusy = false;
        }

        public void ResetPool()
        {
            StopAllCoroutines();
            for (int i = 0; i < m_Pool.Count; i++)
            {
                m_Pool[i].root.SetActive(false);
                m_Pool[i].rect.SetParent(m_PoolContainer, false);
                m_Pool[i].isBusy = false;
            }
        }
    }
}
