using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelectionSystem
{
    /// <summary>
    /// Pure view for a single level tile. It knows how to *paint itself* from the data it is
    /// handed and how to report a click — nothing about saving, unlock rules or the database.
    ///
    /// All visual pieces are exposed as Inspector references so the prefab can be reskinned,
    /// recoloured or relaid-out with zero code changes. Drop this on the level button prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LevelButtonUI : MonoBehaviour
    {
        [Header("Core")]
        [Tooltip("The Button that fires the selection. Usually on this same GameObject.")]
        [SerializeField] private Button m_Button;

        [Tooltip("Tile background frame. Optional — used for the 'highlight current level' effect.")]
        [SerializeField] private Image m_Background;

        [Header("Unlocked State")]
        [Tooltip("Level artwork, shown only when the level is unlocked.")]
        [SerializeField] private Image m_Thumbnail;

        [Tooltip("Level number / name label, shown only when the level is unlocked.")]
        [SerializeField] private TextMeshProUGUI m_LevelNumberText;

        [Header("Locked State")]
        [Tooltip("Padlock icon, shown only when the level is locked.")]
        [SerializeField] private GameObject m_LockIcon;

        [Header("Stars")]
        [Tooltip("Exactly three star images, left to right. Filled = earned, dim = not earned.")]
        [SerializeField] private Image[] m_StarImages = new Image[LevelProgress.MaxStars];

        [Tooltip("Sprite for an earned star.")]
        [SerializeField] private Sprite m_StarFilled;

        [Tooltip("Sprite for an unearned star.")]
        [SerializeField] private Sprite m_StarEmpty;

        [Tooltip("Parent object holding the stars; hidden entirely when a level is locked. Optional.")]
        [SerializeField] private GameObject m_StarContainer;

        [Header("Highlight (optional)")]
        [Tooltip("Tint applied to the background of the highest unlocked level. Leave default to disable.")]
        [SerializeField] private Color m_HighlightColor = Color.white;

        [Header("Click Animation (optional)")]
        [Tooltip("Enable a quick scale punch when the tile is pressed.")]
        [SerializeField] private bool m_AnimateClick = true;
        [SerializeField] private float m_PressScale = 0.92f;
        [SerializeField] private float m_PressDuration = 0.08f;

        private LevelData m_Data;
        private Action<LevelData> m_OnClicked;
        private Color m_DefaultBackgroundColor = Color.white;
        private Coroutine m_AnimRoutine;

        private void Awake()
        {
            if (m_Button == null) m_Button = GetComponent<Button>();
            if (m_Background != null) m_DefaultBackgroundColor = m_Background.color;
        }

        /// <summary>
        /// The one entry point the controller uses to (re)paint this tile. Idempotent — safe
        /// to call again whenever progress changes to refresh the visuals in place.
        /// </summary>
        /// <param name="data">Authored level info to display.</param>
        /// <param name="progress">Saved progress, or null if the player has never played it.</param>
        /// <param name="isUnlocked">Whether the level is currently playable.</param>
        /// <param name="onClicked">Callback invoked with this level's data when an unlocked tile is clicked.</param>
        public void Setup(LevelData data, LevelProgress progress, bool isUnlocked, Action<LevelData> onClicked)
        {
            m_Data = data;
            m_OnClicked = onClicked;

            ApplyUnlockState(isUnlocked);
            ApplyStars(progress != null ? progress.StarCount : 0, isUnlocked);

            if (m_LevelNumberText != null)
                m_LevelNumberText.text = data.LevelId.ToString();

            if (m_Thumbnail != null)
                m_Thumbnail.sprite = data.Thumbnail;

            // Single wiring point — clear first so re-Setup never stacks listeners.
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(HandleClick);
            }

            // Default to un-highlighted; the controller calls SetHighlighted afterwards.
            SetHighlighted(false);
        }

        /// <summary>
        /// Toggles the "this is the level to play next" emphasis. Driven by the controller so
        /// the button stays a dumb view.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (m_Background == null) return;
            m_Background.color = highlighted ? m_HighlightColor : m_DefaultBackgroundColor;
        }

        private void ApplyUnlockState(bool isUnlocked)
        {
            if (m_Button != null) m_Button.interactable = isUnlocked;

            // Unlocked content.
            if (m_Thumbnail != null) m_Thumbnail.enabled = isUnlocked;
            if (m_LevelNumberText != null) m_LevelNumberText.gameObject.SetActive(isUnlocked);

            // Locked content.
            if (m_LockIcon != null) m_LockIcon.SetActive(!isUnlocked);
            if (m_StarContainer != null) m_StarContainer.SetActive(isUnlocked);
        }

        private void ApplyStars(int starCount, bool isUnlocked)
        {
            if (m_StarImages == null) return;

            for (int i = 0; i < m_StarImages.Length; i++)
            {
                if (m_StarImages[i] == null) continue;

                bool earned = isUnlocked && i < starCount;
                if (m_StarFilled != null && m_StarEmpty != null)
                    m_StarImages[i].sprite = earned ? m_StarFilled : m_StarEmpty;
            }
        }

        private void HandleClick()
        {
            if (m_AnimateClick && isActiveAndEnabled)
            {
                if (m_AnimRoutine != null) StopCoroutine(m_AnimRoutine);
                m_AnimRoutine = StartCoroutine(PressPunch());
            }

            m_OnClicked?.Invoke(m_Data);
        }

        /// <summary>Tiny scale punch for tactile feedback. Pure cosmetic, no dependency on a tween lib.</summary>
        private IEnumerator PressPunch()
        {
            Transform t = transform;
            Vector3 baseScale = Vector3.one;
            Vector3 pressed = baseScale * m_PressScale;

            yield return Lerp(t, baseScale, pressed, m_PressDuration);
            yield return Lerp(t, pressed, baseScale, m_PressDuration);
            t.localScale = baseScale;
            m_AnimRoutine = null;
        }

        private static IEnumerator Lerp(Transform t, Vector3 from, Vector3 to, float duration)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.Lerp(from, to, time / duration);
                yield return null;
            }
            t.localScale = to;
        }
    }
}
