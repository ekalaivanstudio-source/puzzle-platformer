using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.CinematicEffects
{
    public enum UIParticleType
    {
        ElectricalSpark,
        PixelFragment,
        EnergyDot,
        DataParticle,
        DirectionalStreak,
        BurstParticle,
        TinyDust,
        EnergyBubble,
        DigitalShard,
        SmallArc
    }

    /// <summary>
    /// Advanced zero-allocation 2D UI Particle System supporting 10 stylized pixel-art particle types.
    /// Provides directional bursts, speed energy trails, screen edge waves, and pixel dissolves.
    /// All textures are procedurally generated in point-filtered pixel art to ensure razor-sharp retro visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public class CinematicUIParticleSystem : MonoBehaviour
    {
        private class UIParticleItem
        {
            public GameObject root;
            public RectTransform rect;
            public Image image;
            public CanvasGroup canvasGroup;
            public bool isBusy;
        }

        private static CinematicUIParticleSystem s_Instance;
        public static CinematicUIParticleSystem Instance
        {
            get
            {
                if (s_Instance == null) s_Instance = FindAnyObjectByType<CinematicUIParticleSystem>();
                if (s_Instance == null)
                {
                    Canvas rootCanvas = FindAnyObjectByType<Canvas>();
                    Transform parent = rootCanvas != null ? rootCanvas.transform : null;
                    GameObject go = new GameObject("[CinematicUIParticleSystem]", typeof(RectTransform), typeof(CinematicUIParticleSystem));
                    if (parent != null)
                    {
                        go.transform.SetParent(parent, false);
                        go.transform.SetAsLastSibling();
                    }
                    s_Instance = go.GetComponent<CinematicUIParticleSystem>();
                }
                return s_Instance;
            }
        }

        private const int POOL_SIZE = 64;
        private readonly List<UIParticleItem> m_Pool = new List<UIParticleItem>(POOL_SIZE);
        private Transform m_PoolContainer;

        // Procedural point-filtered pixel sprites
        private static Sprite s_SparkSprite;
        private static Sprite s_FragmentSprite;
        private static Sprite s_DotSprite;
        private static Sprite s_DataSprite;
        private static Sprite s_StreakSprite;
        private static Sprite s_DustSprite;
        private static Sprite s_BubbleSprite;
        private static Sprite s_ShardSprite;
        private static Sprite s_ArcSprite;

        private void Awake()
        {
            s_Instance = this;
            InitializePool();
        }

        public void InitializePool()
        {
            if (m_Pool.Count > 0) return;

            CreateProceduralSprites();

            GameObject containerObj = new GameObject("CinematicUIParticlePool_Container", typeof(RectTransform));
            containerObj.transform.SetParent(transform, false);
            m_PoolContainer = containerObj.transform;

            RectTransform containerRt = containerObj.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;
            containerRt.anchoredPosition = Vector2.zero;

            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject pObj = new GameObject($"UIParticle_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                pObj.transform.SetParent(m_PoolContainer, false);

                RectTransform rt = pObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(4f, 4f);

                Image img = pObj.GetComponent<Image>();
                img.sprite = s_FragmentSprite;
                img.raycastTarget = false;

                CanvasGroup cg = pObj.GetComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                pObj.SetActive(false);

                m_Pool.Add(new UIParticleItem
                {
                    root = pObj,
                    rect = rt,
                    image = img,
                    canvasGroup = cg,
                    isBusy = false
                });
            }
        }

        #region Procedural Sprite Generation

        private static void CreateProceduralSprites()
        {
            if (s_FragmentSprite != null) return;

            // 1. Pixel Fragment (4x4 solid square)
            s_FragmentSprite = GenerateProceduralSprite(4, 4, (x, y) => Color.white);

            // 2. Electrical Spark (5x5 cross / diamond)
            s_SparkSprite = GenerateProceduralSprite(5, 5, (x, y) =>
            {
                bool isCenter = (x == 2 && y == 2);
                bool isCross = (x == 2 || y == 2);
                bool isDiag = (Mathf.Abs(x - 2) == 1 && Mathf.Abs(y - 2) == 1);
                if (isCenter) return Color.white;
                if (isCross) return new Color(1f, 1f, 1f, 0.85f);
                if (isDiag) return new Color(1f, 1f, 1f, 0.40f);
                return Color.clear;
            });

            // 3. Energy Dot (2x2 tiny crisp dot)
            s_DotSprite = GenerateProceduralSprite(2, 2, (x, y) => Color.white);

            // 4. Data Particle (3x3 dot with faded corners)
            s_DataSprite = GenerateProceduralSprite(3, 3, (x, y) =>
            {
                if (x == 1 && y == 1) return Color.white;
                if (x == 1 || y == 1) return new Color(1f, 1f, 1f, 0.75f);
                return Color.clear;
            });

            // 5. Directional Streak (2x8 elongated pixel line)
            s_StreakSprite = GenerateProceduralSprite(2, 8, (x, y) =>
            {
                float fade = 1f - (float)y / 7f;
                return new Color(1f, 1f, 1f, fade);
            });

            // 6. Tiny Dust (3x3 roundish pixel puff)
            s_DustSprite = GenerateProceduralSprite(3, 3, (x, y) =>
            {
                if (x == 1 && y == 1) return Color.white;
                if (x == 1 || y == 1) return new Color(1f, 1f, 1f, 0.5f);
                return Color.clear;
            });

            // 7. Energy Bubble (6x6 ring/donut contour)
            s_BubbleSprite = GenerateProceduralSprite(6, 6, (x, y) =>
            {
                float dx = x - 2.5f;
                float dy = y - 2.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist >= 1.6f && dist <= 2.8f) return Color.white;
                return Color.clear;
            });

            // 8. Digital Shard (4x4 stepped diagonal shard)
            s_ShardSprite = GenerateProceduralSprite(4, 4, (x, y) =>
            {
                if (x == y || x == y + 1) return Color.white;
                return Color.clear;
            });

            // 9. Small Arc (5x5 zigzag lightning segment)
            s_ArcSprite = GenerateProceduralSprite(5, 5, (x, y) =>
            {
                bool p1 = (x == 1 && y == 4);
                bool p2 = (x == 2 && y == 3);
                bool p3 = (x == 2 && y == 2);
                bool p4 = (x == 3 && y == 1);
                bool p5 = (x == 4 && y == 0);
                return (p1 || p2 || p3 || p4 || p5) ? Color.white : Color.clear;
            });
        }

        private static Sprite GenerateProceduralSprite(int width, int height, Func<int, int, Color> colorSampler)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = colorSampler(x, y);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
        }

        #endregion

        private UIParticleItem GetAvailable()
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

        #region Public Particle Spawning APIs

        public void SpawnSparkBurst(Vector3 position, Color color, int count = 6, float radius = 18f)
        {
            Vector2 localPos = (m_PoolContainer != null) ? (Vector2)m_PoolContainer.InverseTransformPoint(position) : (Vector2)position;
            SpawnSparkBurst(localPos, m_PoolContainer, color, count, radius);
        }

        public void SpawnEnergyBubble(Vector3 position, Color color, float duration = 0.22f)
        {
            Vector2 localPos = (m_PoolContainer != null) ? (Vector2)m_PoolContainer.InverseTransformPoint(position) : (Vector2)position;
            SpawnEnergyBubble(localPos, m_PoolContainer, color, duration);
        }

        public void SpawnDataFloat(Vector3 position, Color color, int count = 5, float spread = 40f)
        {
            Vector2 localPos = (m_PoolContainer != null) ? (Vector2)m_PoolContainer.InverseTransformPoint(position) : (Vector2)position;
            SpawnDataFloat(localPos, m_PoolContainer, color, count, spread);
        }

        public void SpawnPixelDissolveShards(Vector3 position, Color color, int count = 16, float spread = 30f)
        {
            Vector2 localPos = (m_PoolContainer != null) ? (Vector2)m_PoolContainer.InverseTransformPoint(position) : (Vector2)position;
            for (int i = 0; i < count; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                Vector2 startPos = localPos + new Vector2(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread));
                Vector2 endPos = startPos + new Vector2(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(-20f, 30f));

                p.rect.SetParent(m_PoolContainer, false);
                p.rect.sizeDelta = (i % 2 == 0) ? new Vector2(4f, 4f) : new Vector2(3f, 3f);
                p.rect.anchoredPosition = startPos;
                p.rect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                p.image.sprite = (i % 2 == 0) ? s_FragmentSprite : s_ShardSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateShardRoutine(p, startPos, endPos, UnityEngine.Random.Range(0.20f, 0.32f)));
            }
        }

        /// <summary>
        /// Spawns stylized electrical sparks radiating outward from a position.
        /// </summary>
        public void SpawnSparkBurst(Vector2 centerPosition, Transform targetParent, Color color, int count = 6, float radius = 18f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;

            for (int i = 0; i < count; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.25f, 0.25f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float dist = radius * UnityEngine.Random.Range(0.6f, 1.25f);
                Vector2 targetPos = centerPosition + dir * dist;

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = (i % 2 == 0) ? new Vector2(6f, 6f) : new Vector2(4f, 4f);
                p.rect.anchoredPosition = centerPosition;
                p.rect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                p.image.sprite = (i % 3 == 0) ? s_SparkSprite : ((i % 3 == 1) ? s_ArcSprite : s_FragmentSprite);
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateBurstRoutine(p, centerPosition, targetPos, UnityEngine.Random.Range(0.12f, 0.18f)));
            }
        }

        /// <summary>
        /// Spawns a directional velocity burst of particles (button confirm punch, directional impact).
        /// </summary>
        public void SpawnDirectionalBurst(Vector2 centerPosition, Transform targetParent, Color color, Vector2 direction, int count = 8, float spreadAngle = 45f, float distance = 35f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                float angle = (baseAngle + UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float dist = distance * UnityEngine.Random.Range(0.5f, 1.2f);
                Vector2 targetPos = centerPosition + dir * dist;

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = new Vector2(4f, 8f);
                p.rect.anchoredPosition = centerPosition;
                p.rect.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                p.image.sprite = s_StreakSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateDirectionalRoutine(p, centerPosition, targetPos, UnityEngine.Random.Range(0.14f, 0.22f)));
            }
        }

        /// <summary>
        /// Spawns a stylized flying spark that floats across the screen with organic sinusoidal sway,
        /// dynamic scaling, and smooth chromatic color transition from startColor to endColor.
        /// </summary>
        public void SpawnFloatingSpark(Vector2 startPos, Transform targetParent, Color startColor, Color endColor, Vector2 velocity, float duration, float size = 5f, float wobbleAmount = 18f, float wobbleFreq = 3f)
        {
            UIParticleItem p = GetAvailable();
            if (p == null) return;

            if (targetParent == null) targetParent = m_PoolContainer;

            p.rect.SetParent(targetParent, false);
            p.rect.sizeDelta = new Vector2(size, size);
            p.rect.anchoredPosition = startPos;
            p.rect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            // Varied point-filtered retro sprites: sparks, glowing dots, and dynamic shards/arcs
            int spriteIndex = UnityEngine.Random.Range(0, 4);
            switch (spriteIndex)
            {
                case 0: p.image.sprite = s_SparkSprite; break;
                case 1: p.image.sprite = s_DotSprite; break;
                case 2: p.image.sprite = s_ArcSprite; break;
                default: p.image.sprite = s_StreakSprite; break;
            }

            p.image.color = startColor;
            p.canvasGroup.alpha = 0f;
            p.root.SetActive(true);

            StartCoroutine(AnimateFloatingSparkRoutine(p, startPos, velocity, duration, startColor, endColor, size, wobbleAmount, wobbleFreq));
        }

        public void SpawnFloatingSpark(Vector3 worldPos, Color startColor, Color endColor, Vector2 velocity, float duration, float size = 5f, float wobbleAmount = 18f, float wobbleFreq = 3f)
        {
            Vector2 localPos = (m_PoolContainer != null) ? (Vector2)m_PoolContainer.InverseTransformPoint(worldPos) : (Vector2)worldPos;
            SpawnFloatingSpark(localPos, m_PoolContainer, startColor, endColor, velocity, duration, size, wobbleAmount, wobbleFreq);
        }

        /// <summary>
        /// Spawns a single subtle speed trail particle behind a moving UI element.
        /// </summary>
        public void SpawnEnergyTrail(Vector2 position, Transform targetParent, Color color, Vector2 velocity)
        {
            UIParticleItem p = GetAvailable();
            if (p == null) return;

            if (targetParent == null) targetParent = m_PoolContainer;

            p.rect.SetParent(targetParent, false);
            p.rect.sizeDelta = new Vector2(3f, 6f);
            p.rect.anchoredPosition = position;

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            p.rect.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
            p.image.sprite = s_StreakSprite;
            p.image.color = color;
            p.canvasGroup.alpha = 0.8f;
            p.root.SetActive(true);

            Vector2 endPos = position - velocity.normalized * UnityEngine.Random.Range(6f, 14f);
            StartCoroutine(AnimateTrailRoutine(p, position, endPos, 0.12f));
        }

        /// <summary>
        /// Spawns pixel fragments scattering across an area during screen transformation (e.g. RETRY dissolve).
        /// </summary>
        public void SpawnPixelDissolveShards(RectTransform sourceRect, Transform targetParent, Color color, int count = 16)
        {
            if (sourceRect == null) return;
            if (targetParent == null) targetParent = m_PoolContainer;

            Rect rect = sourceRect.rect;
            Vector2 center = sourceRect.anchoredPosition;

            for (int i = 0; i < count; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                float startX = center.x + UnityEngine.Random.Range(rect.xMin, rect.xMax);
                float startY = center.y + UnityEngine.Random.Range(rect.yMin, rect.yMax);
                Vector2 startPos = new Vector2(startX, startY);

                float moveX = UnityEngine.Random.Range(-30f, 30f);
                float moveY = UnityEngine.Random.Range(-25f, 35f);
                Vector2 endPos = startPos + new Vector2(moveX, moveY);

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = (i % 2 == 0) ? new Vector2(4f, 4f) : new Vector2(3f, 3f);
                p.rect.anchoredPosition = startPos;
                p.rect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                p.image.sprite = (i % 2 == 0) ? s_FragmentSprite : s_ShardSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateShardRoutine(p, startPos, endPos, UnityEngine.Random.Range(0.20f, 0.32f)));
            }
        }

        /// <summary>
        /// Spawns subtle data pixels floating upward (Collection robot scanner readouts).
        /// </summary>
        public void SpawnDataFloat(Vector2 centerPosition, Transform targetParent, Color color, int count = 5, float spread = 40f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;

            for (int i = 0; i < count; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                float offsetX = UnityEngine.Random.Range(-spread, spread);
                float offsetY = UnityEngine.Random.Range(-15f, 15f);
                Vector2 start = centerPosition + new Vector2(offsetX, offsetY);
                Vector2 end = start + new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(22f, 40f));

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = new Vector2(3f, 3f);
                p.rect.anchoredPosition = start;
                p.image.sprite = s_DataSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateDataFloatRoutine(p, start, end, UnityEngine.Random.Range(0.22f, 0.32f)));
            }
        }

        /// <summary>
        /// Spawns a ring bubble that expands and pops (node activation, circuit highlights).
        /// </summary>
        public void SpawnEnergyBubble(Vector2 centerPosition, Transform targetParent, Color color, float duration = 0.22f)
        {
            UIParticleItem p = GetAvailable();
            if (p == null) return;

            if (targetParent == null) targetParent = m_PoolContainer;

            p.rect.SetParent(targetParent, false);
            p.rect.sizeDelta = new Vector2(6f, 6f);
            p.rect.anchoredPosition = centerPosition;
            p.image.sprite = s_BubbleSprite;
            p.image.color = color;
            p.canvasGroup.alpha = 1f;
            p.root.SetActive(true);

            StartCoroutine(AnimateBubbleRoutine(p, duration));
        }

        /// <summary>
        /// Spawns particles traveling along the screen edge perimeter, converging inward.
        /// </summary>
        public void SpawnScreenEdgeWave(Transform targetParent, Color color, float duration = 0.35f)
        {
            if (targetParent == null) targetParent = m_PoolContainer;

            // Spawn 12 particles distributed along top/bottom/left/right screen bounds
            float halfW = Screen.width * 0.45f;
            float halfH = Screen.height * 0.45f;

            for (int i = 0; i < 12; i++)
            {
                UIParticleItem p = GetAvailable();
                if (p == null) break;

                Vector2 start;
                Vector2 inward;
                int side = i % 4;

                switch (side)
                {
                    case 0: // Top
                        start = new Vector2(UnityEngine.Random.Range(-halfW, halfW), halfH);
                        inward = new Vector2(0f, -60f);
                        break;
                    case 1: // Right
                        start = new Vector2(halfW, UnityEngine.Random.Range(-halfH, halfH));
                        inward = new Vector2(-60f, 0f);
                        break;
                    case 2: // Bottom
                        start = new Vector2(UnityEngine.Random.Range(-halfW, halfW), -halfH);
                        inward = new Vector2(0f, 60f);
                        break;
                    default: // Left
                        start = new Vector2(-halfW, UnityEngine.Random.Range(-halfH, halfH));
                        inward = new Vector2(60f, 0f);
                        break;
                }

                p.rect.SetParent(targetParent, false);
                p.rect.sizeDelta = new Vector2(5f, 5f);
                p.rect.anchoredPosition = start;
                p.image.sprite = (i % 2 == 0) ? s_SparkSprite : s_DotSprite;
                p.image.color = color;
                p.canvasGroup.alpha = 1f;
                p.root.SetActive(true);

                StartCoroutine(AnimateDirectionalRoutine(p, start, start + inward, duration));
            }
        }

        #endregion

        #region Particle Coroutines

        private IEnumerator AnimateBurstRoutine(UIParticleItem p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Quick deceleration
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                p.rect.anchoredPosition = Vector2.Lerp(start, end, ease);
                p.canvasGroup.alpha = 1f - t;
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateDirectionalRoutine(UIParticleItem p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float ease = 1f - Mathf.Pow(1f - t, 2.5f);
                p.rect.anchoredPosition = Vector2.Lerp(start, end, ease);
                p.canvasGroup.alpha = Mathf.Sin((1f - t) * Mathf.PI * 0.5f);
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateTrailRoutine(UIParticleItem p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                p.rect.anchoredPosition = Vector2.Lerp(start, end, t);
                p.canvasGroup.alpha = 0.8f * (1f - t);
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateShardRoutine(UIParticleItem p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            float rotSpeed = UnityEngine.Random.Range(180f, 360f);
            float startRot = p.rect.localEulerAngles.z;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float ease = 1f - Mathf.Pow(1f - t, 2f);
                p.rect.anchoredPosition = Vector2.Lerp(start, end, ease);
                p.rect.localEulerAngles = new Vector3(0f, 0f, startRot + rotSpeed * t);
                p.canvasGroup.alpha = 1f - t;
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateDataFloatRoutine(UIParticleItem p, Vector2 start, Vector2 end, float duration)
        {
            float elapsed = 0f;
            float wobbleFreq = UnityEngine.Random.Range(2.5f, 4.5f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float wobble = Mathf.Sin(t * Mathf.PI * wobbleFreq) * 3.5f;
                Vector2 pos = Vector2.Lerp(start, end, t);
                pos.x += wobble;

                p.rect.anchoredPosition = pos;
                p.canvasGroup.alpha = (t < 0.2f) ? (t / 0.2f) : (1f - (t - 0.2f) / 0.8f);
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateBubbleRoutine(UIParticleItem p, float duration)
        {
            float elapsed = 0f;
            Vector2 baseSize = new Vector2(6f, 6f);
            Vector2 peakSize = new Vector2(16f, 16f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                p.rect.sizeDelta = Vector2.Lerp(baseSize, peakSize, t);
                p.canvasGroup.alpha = 1f - t * t;
                yield return null;
            }

            RecycleParticle(p);
        }

        private IEnumerator AnimateFloatingSparkRoutine(UIParticleItem p, Vector2 startPos, Vector2 velocity, float duration, Color startColor, Color endColor, float baseSize, float wobbleAmount, float wobbleFreq)
        {
            float elapsed = 0f;
            float phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float rotSpeed = UnityEngine.Random.Range(-90f, 90f);
            float startRot = p.rect.localEulerAngles.z;

            // If streak sprite, orient along velocity direction
            bool isStreak = (p.image.sprite == s_StreakSprite);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Base motion from continuous velocity
                Vector2 currentPos = startPos + velocity * elapsed;

                // Organic sinusoidal wind sway (horizontal / drift oscillation)
                float wobble = Mathf.Sin((elapsed * wobbleFreq) + phaseOffset) * wobbleAmount;
                currentPos.x += wobble;

                p.rect.anchoredPosition = currentPos;

                // Dynamic rotation
                if (isStreak)
                {
                    Vector2 currentVel = velocity + new Vector2(Mathf.Cos((elapsed * wobbleFreq) + phaseOffset) * wobbleAmount * wobbleFreq, 0f);
                    float angle = Mathf.Atan2(currentVel.y, currentVel.x) * Mathf.Rad2Deg;
                    p.rect.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
                }
                else
                {
                    p.rect.localEulerAngles = new Vector3(0f, 0f, startRot + rotSpeed * elapsed);
                }

                // Dynamic size twinkle / micro-pulse
                float twinkle = 1f + 0.25f * Mathf.Sin(elapsed * 12f + phaseOffset);
                p.rect.sizeDelta = new Vector2(baseSize * twinkle, baseSize * twinkle);

                // Chromatic Color Transition over lifetime
                p.image.color = Color.Lerp(startColor, endColor, t);

                // Smooth Alpha envelope: Quick fade-in (first 15%), sustained hold, gentle fade-out
                float alpha;
                if (t < 0.15f)
                {
                    alpha = t / 0.15f;
                }
                else if (t > 0.70f)
                {
                    alpha = (1f - t) / 0.30f;
                }
                else
                {
                    alpha = 1f;
                }

                // Micro sparkle twinkle
                if (UnityEngine.Random.value < 0.08f)
                {
                    alpha = Mathf.Min(1f, alpha * 1.35f);
                }

                p.canvasGroup.alpha = Mathf.Clamp01(alpha);

                yield return null;
            }

            RecycleParticle(p);
        }

        private void RecycleParticle(UIParticleItem p)
        {
            p.root.SetActive(false);
            p.rect.SetParent(m_PoolContainer, false);
            p.isBusy = false;
        }

        #endregion

        /// <summary>
        /// Clears all active particles immediately on screen transition.
        /// </summary>
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
