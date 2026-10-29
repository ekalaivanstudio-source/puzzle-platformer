using System.Collections;
using MainGame.UI.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Collectables
{
    /// <summary>
    /// The card that pops up after every shard pickup: "Memory Shards 4 / 9 — collect 5 more to
    /// unlock the memory clip". It pops in, fills its bar up to the new total, sweeps a shine
    /// across the card and the title, holds, then slides out and switches its own GameObject off.
    ///
    /// It is told what to say by <see cref="MemoryShardCounterHUD"/>, which owns the service
    /// subscriptions — this component only animates. A pickup that crosses a threshold calls
    /// <see cref="ShowProgress"/> and then <see cref="ShowUnlocked"/> in the same frame; the
    /// second simply restarts the card with the unlock wording.
    ///
    /// Everything runs on unscaled time, so a paused or slowed level still plays it.
    /// Built into the HUD prefab by Tools ▸ Memory Shards ▸ 2. Build Prefabs.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MemoryShardProgressToast : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("The card that pops and slides. Usually a child, so this root can stay put.")]
        [SerializeField] private RectTransform m_Card;

        [SerializeField] private TextMeshProUGUI m_TitleLabel;
        [SerializeField] private TextMeshProUGUI m_HintLabel;

        [Tooltip("Optional shard icon; given a punch when the card lands.")]
        [SerializeField] private Image m_Icon;

        [Tooltip("Optional bar fill (Image Type ▸ Filled, Horizontal).")]
        [SerializeField] private Image m_BarFill;

        [Tooltip("Optional diagonal band swept across the card. Needs a RectMask2D on the card.")]
        [SerializeField] private RectTransform m_ShineBand;

        [Header("Wording")]
        [Tooltip("{0} shards collected, {1} shards the next memory needs.")]
        [SerializeField] private string m_ProgressTitle = "Memory Shards  {0} / {1}";

        [Tooltip("{0} shards still needed, {1} the next memory's title.")]
        [SerializeField] private string m_ProgressHint = "Collect {0} more to unlock memory clip — {1}";

        [Tooltip("{0} shards still needed — used when exactly one is left.")]
        [SerializeField] private string m_ProgressHintOne = "Just 1 more shard to unlock memory clip — {1}";

        [SerializeField] private string m_UnlockedTitle = "Memory Unlocked!";

        [Tooltip("{0} is the story's title.")]
        [SerializeField] private string m_UnlockedHint = "{0} plays when you finish this level";

        [Tooltip("{0} shards collected.")]
        [SerializeField] private string m_AllDoneTitle = "Memory Shards  {0}";

        [SerializeField] private string m_AllDoneHint = "Every memory clip is unlocked";

        [Header("Timing")]
        [Min(0.05f)] [SerializeField] private float m_InDuration = 0.4f;
        [Min(0.1f)] [SerializeField] private float m_HoldDuration = 2.6f;
        [Min(0.05f)] [SerializeField] private float m_OutDuration = 0.35f;
        [Min(0.05f)] [SerializeField] private float m_BarFillDuration = 0.6f;
        [Min(0.05f)] [SerializeField] private float m_ShineDuration = 0.7f;

        [Tooltip("Seconds after the card starts landing before the shine sweeps.")]
        [SerializeField] private float m_ShineDelay = 0.35f;

        [Tooltip("Pixels the card drops in from, and rises by on the way out.")]
        [SerializeField] private float m_SlideDistance = 60f;

        [Header("Shine")]
        [SerializeField] private Color m_TextShineColor = new Color(1f, 0.97f, 0.75f, 1f);

        [Tooltip("Width of the text glint, as a fraction of the title's width.")]
        [Range(0.05f, 1f)] [SerializeField] private float m_TextShineWidth = 0.25f;

        private CanvasGroup m_Group;
        private Vector2 m_CardRestPosition;
        private bool m_RestCaptured;
        private Coroutine m_Routine;

        private void Awake()
        {
            m_Group = GetComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;
            CaptureRest();
            EnsureShineSprite();
        }

        // ─── Entry points ─────────────────────────────────────────────────────────

        /// <summary>Shows the running total against the next locked memory.</summary>
        public void ShowProgress(int collected)
        {
            MemoryStoryEntry next = NextLockedStory(collected, out int previousThreshold);

            string title, hint;
            float from, to;

            if (next == null)
            {
                title = string.Format(m_AllDoneTitle, collected);
                hint = m_AllDoneHint;
                from = to = 1f;
            }
            else
            {
                int remaining = next.requiredShards - collected;
                title = string.Format(m_ProgressTitle, collected, next.requiredShards);
                hint = string.Format(remaining == 1 ? m_ProgressHintOne : m_ProgressHint, remaining, next.title);

                // The bar measures the stretch towards the next memory, not the whole game, so
                // it starts empty again after each unlock instead of creeping along at 50%.
                float span = Mathf.Max(1, next.requiredShards - previousThreshold);
                from = Mathf.Clamp01((collected - 1 - previousThreshold) / span);
                to = Mathf.Clamp01((collected - previousThreshold) / span);
            }

            Play(title, hint, from, to);
        }

        /// <summary>Shows that this pickup has just unlocked a memory clip.</summary>
        public void ShowUnlocked(MemoryStoryEntry story)
        {
            if (story == null) return;
            Play(m_UnlockedTitle, string.Format(m_UnlockedHint, story.title), 0.85f, 1f);
        }

        // ─── Animation ────────────────────────────────────────────────────────────

        private void Play(string title, string hint, float barFrom, float barTo)
        {
            // Activating runs Awake the first time, so m_Group is ready below.
            gameObject.SetActive(true);

            if (m_Routine != null) StopCoroutine(m_Routine);

            if (m_TitleLabel != null) m_TitleLabel.text = title;
            if (m_HintLabel != null) m_HintLabel.text = hint;

            m_Routine = StartCoroutine(PlayRoutine(barFrom, barTo));
        }

        private IEnumerator PlayRoutine(float barFrom, float barTo)
        {
            if (m_BarFill != null) m_BarFill.fillAmount = barFrom;
            SetShineBand(-1f);

            // The title's mesh is cached once so the glint can tint it per character and
            // restore it exactly afterwards.
            Color32[][] titleBase = CacheVertexColors(m_TitleLabel);

            float total = m_InDuration + m_HoldDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;

                // Land: drop in, overshoot, settle.
                float tin = Mathf.Clamp01(elapsed / m_InDuration);
                m_Group.alpha = UIEasing.Evaluate(EasingType.EaseOutQuad, tin);
                float pop = UIEasing.Evaluate(EasingType.EaseOutBack, tin, 1.7f);
                SetCard(Mathf.LerpUnclamped(0.6f, 1f, pop), (1f - pop) * m_SlideDistance);

                // Icon punch, just as the card lands.
                if (m_Icon != null)
                {
                    float punch = Mathf.Clamp01((elapsed - m_InDuration * 0.6f) / 0.35f);
                    m_Icon.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(punch * Mathf.PI) * 0.3f);
                }

                // Bar fills up to the new total once the card is mostly in.
                if (m_BarFill != null)
                {
                    float tb = Mathf.Clamp01((elapsed - m_InDuration * 0.5f) / m_BarFillDuration);
                    m_BarFill.fillAmount = Mathf.Lerp(barFrom, barTo,
                        UIEasing.Evaluate(EasingType.EaseOutCubic, tb));
                }

                // One shine pass over the card and the title together.
                float ts = (elapsed - m_ShineDelay) / m_ShineDuration;
                float sweep = ts >= 0f && ts <= 1f ? UIEasing.Evaluate(EasingType.EaseInOutQuad, ts) : -1f;
                SetShineBand(sweep);
                ApplyTextShine(m_TitleLabel, titleBase, sweep);

                yield return null;
            }

            SetShineBand(-1f);
            ApplyTextShine(m_TitleLabel, titleBase, -1f);

            // Leave: fade and rise.
            elapsed = 0f;
            while (elapsed < m_OutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = UIEasing.Evaluate(EasingType.EaseInQuad, elapsed / m_OutDuration);
                m_Group.alpha = 1f - t;
                SetCard(Mathf.Lerp(1f, 0.9f, t), -t * m_SlideDistance * 0.5f);
                yield return null;
            }

            m_Group.alpha = 0f;
            SetCard(1f, 0f);
            m_Routine = null;
            gameObject.SetActive(false);
        }

        private void SetCard(float scale, float yOffset)
        {
            if (m_Card == null) return;
            CaptureRest();
            m_Card.localScale = new Vector3(scale, scale, 1f);
            m_Card.anchoredPosition = m_CardRestPosition + new Vector2(0f, yOffset);
        }

        /// <summary>Moves the band across the card; <paramref name="t"/> outside 0–1 parks it off-card.</summary>
        private void SetShineBand(float t)
        {
            if (m_ShineBand == null || m_Card == null) return;

            bool visible = t >= 0f && t <= 1f;
            m_ShineBand.gameObject.SetActive(visible);
            if (!visible) return;

            float half = m_Card.rect.width * 0.5f + m_ShineBand.rect.width;
            m_ShineBand.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, t), 0f);
        }

        // ─── Text glint ───────────────────────────────────────────────────────────

        private static Color32[][] CacheVertexColors(TMP_Text label)
        {
            if (label == null) return null;

            label.ForceMeshUpdate();
            TMP_TextInfo info = label.textInfo;

            var cache = new Color32[info.meshInfo.Length][];
            for (int i = 0; i < info.meshInfo.Length; i++)
                cache[i] = (Color32[])info.meshInfo[i].colors32.Clone();
            return cache;
        }

        /// <summary>
        /// Brightens the characters under a moving band, left to right. <paramref name="sweep"/>
        /// outside 0–1 restores the cached colours.
        /// </summary>
        private void ApplyTextShine(TMP_Text label, Color32[][] baseColors, float sweep)
        {
            if (label == null || baseColors == null) return;

            TMP_TextInfo info = label.textInfo;
            if (info.meshInfo.Length != baseColors.Length) return;

            // Run the band from just off the left edge to just off the right, so it enters and
            // leaves rather than starting on the first letter.
            float pad = m_TextShineWidth;
            float centre = Mathf.Lerp(-pad, 1f + pad, sweep);

            Bounds bounds = label.textBounds;
            float width = Mathf.Max(1f, bounds.size.x);

            for (int c = 0; c < info.characterCount; c++)
            {
                TMP_CharacterInfo ch = info.characterInfo[c];
                if (!ch.isVisible) continue;

                Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                Color32[] source = baseColors[ch.materialReferenceIndex];

                for (int v = 0; v < 4; v++)
                {
                    int index = ch.vertexIndex + v;
                    if (index >= colors.Length || index >= source.Length) continue;

                    float amount = 0f;
                    if (sweep >= 0f && sweep <= 1f)
                    {
                        float x = (info.meshInfo[ch.materialReferenceIndex].vertices[index].x - bounds.min.x) / width;
                        amount = 1f - Mathf.Clamp01(Mathf.Abs(x - centre) / pad);
                        amount *= amount;
                    }

                    Color32 tinted = Color32.Lerp(source[index], m_TextShineColor, amount);
                    tinted.a = source[index].a;
                    colors[index] = tinted;
                }
            }

            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private void CaptureRest()
        {
            if (m_RestCaptured || m_Card == null) return;
            m_CardRestPosition = m_Card.anchoredPosition;
            m_RestCaptured = true;
        }

        /// <summary>
        /// A spriteless band draws as a hard-edged slab. Give it a soft horizontal falloff
        /// generated once, so no texture asset has to exist for the shine to look right.
        /// </summary>
        private void EnsureShineSprite()
        {
            if (m_ShineBand == null) return;
            var image = m_ShineBand.GetComponent<Image>();
            if (image == null || image.sprite != null) return;

            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int x = 0; x < width; x++)
            {
                float t = Mathf.Sin(x / (float)(width - 1) * Mathf.PI);
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t));
            }
            texture.Apply();

            image.sprite = Sprite.Create(texture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// The lowest-threshold story not yet reached, and the threshold before it (0 for the
        /// first). Null when every authored story is already unlocked.
        /// </summary>
        private static MemoryStoryEntry NextLockedStory(int collected, out int previousThreshold)
        {
            previousThreshold = 0;
            MemoryShardDatabase database = MemoryShardService.Database;
            if (database == null) return null;

            foreach (var story in database.StoriesByThreshold())
            {
                if (story.requiredShards > collected) return story;
                previousThreshold = story.requiredShards;
            }
            return null;
        }
    }
}
