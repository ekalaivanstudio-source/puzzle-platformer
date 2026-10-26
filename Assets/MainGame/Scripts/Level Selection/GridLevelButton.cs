using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Collectables;
using ModernLevelSelection;
using MainGame.UI.Feedback;

namespace LevelSelection
{
    /// <summary>
    /// UI Controller for a single level card inside a Grid Layout Group.
    /// Handles visual states (locked, unlocked, completed, selected, collectables),
    /// level thumbnail rendering, orange border and banner selection switching,
    /// positioned pointer arrow bobbing, micro-interactions, and scene loading.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class GridLevelButton : MonoBehaviour,
        ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler,
        ISubmitHandler
    {
        #region Inspector Fields

        [Header("State Data")]
        [SerializeField] private int m_LevelNumber = 1;

        [Header("UI Component References")]
        [Tooltip("Primary button component on this card.")]
        [SerializeField] private Button m_Button;

        [Tooltip("Masked thumbnail image displaying level preview art.")]
        [SerializeField] private Image m_LevelImage;

        [Tooltip("TMP text component displaying level title (e.g. 'LEVEL 1').")]
        [SerializeField] private TextMeshProUGUI m_LevelNameText;

        [Tooltip("Background banner image behind the level name text.")]
        [SerializeField] private Image m_LevelNameBannerImage;

        [Tooltip("Root or border image on this card.")]
        [SerializeField] private Image m_BorderImage;

        [Tooltip("Icon or badge displayed when the level has been completed.")]
        [SerializeField] private GameObject m_CompletedTick;

        [Tooltip("Arrow displayed when the button is selected or focused.")]
        [SerializeField] private GameObject m_SelectedArrow;

        [Tooltip("Collectable status icon on this card.")]
        [SerializeField] private GameObject m_CollectableIcon;

        [Tooltip("Highlight/glow border or fill effect shown when selected.")]
        [SerializeField] private GameObject m_SelectorFillEff;

        [Tooltip("Optional locked overlay GameObject (e.g. padlock icon or dark scrim).")]
        [SerializeField] private GameObject m_LockedOverlay;

        [Header("Banner Sprites (Normal vs Selected)")]
        [Tooltip("Normal (blue) sprite for level name background.")]
        [SerializeField] private Sprite m_BannerNormalSprite;

        [Tooltip("Selected (orange) sprite for level name background.")]
        [SerializeField] private Sprite m_BannerSelectedSprite;

        [Header("Border Colors")]
        [Tooltip("Border color when this card is selected.")]
        [SerializeField] private Color m_SelectedBorderColor = new Color(1f, 0.6078f, 0f, 1f); // Orange #FFA800

        [Tooltip("Border color when unlocked and unselected.")]
        [SerializeField] private Color m_NormalBorderColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        [Tooltip("Border color when locked.")]
        [SerializeField] private Color m_LockedBorderColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);

        [Header("Visual Configuration")]
        [Tooltip("Format string for the level name text. Use {0} for the level number.")]
        [SerializeField] private string m_NameFormat = "LEVEL {0}";

        [Tooltip("Tint color applied to thumbnail when level is unlocked.")]
        [SerializeField] private Color m_UnlockedColor = Color.white;

        [Tooltip("Tint color applied to thumbnail when level is locked (kept white to show full sprite art).")]
        [SerializeField] private Color m_LockedColor = Color.white;

        [Header("Collectables Visuals")]
        [Tooltip("Sprite for uncollected collectables (e.g. unknown silhouette).")]
        [SerializeField] private Sprite m_CollectableUncollectedSprite;

        [Tooltip("Sprite for collected collectables (e.g. gear/bolt/pickup art).")]
        [SerializeField] private Sprite m_CollectableCollectedSprite;

        [Tooltip("Tint for collected collectables.")]
        [SerializeField] private Color m_CollectableCollectedColor = Color.white;

        [Tooltip("Tint for uncollected / undiscovered collectables.")]
        [SerializeField] private Color m_CollectableUncollectedColor = Color.white;

        [Header("Animations")]
        [Tooltip("Enable arrow bobbing/pulse when selected.")]
        [SerializeField] private bool m_AnimateArrow = true;
        [SerializeField] private float m_ArrowPulseSpeed = 6f;
        [SerializeField] private float m_ArrowPulseScale = 0.05f;
        [SerializeField] private float m_ArrowBobDistance = 3f;

        [Tooltip("Slight scale lift when highlighted or selected.")]
        [SerializeField] private float m_HoverScaleMultiplier = 1.05f;

        #endregion

        #region Events

        /// <summary>Invoked when this level button is clicked, passing its level number.</summary>
        public UnityEvent<int> OnLevelClicked = new UnityEvent<int>();

        /// <summary>Notifies when any level button gains focus or is selected.</summary>
        public static event Action<int> OnAnyButtonFocused;

        #endregion

        #region Private State

        private bool m_IsUnlocked;
        private bool m_IsCompleted;
        private bool m_IsSelected;
        private bool m_IsLoading;

        private Vector3 m_BaseScale = Vector3.one;
        private Vector3 m_ArrowRestPos;
        private Vector3 m_ArrowRestScale = Vector3.one;

        private Coroutine m_ScaleCoroutine;
        private Coroutine m_ArrowCoroutine;
        private Coroutine m_ShakeCoroutine;

        #endregion

        #region Public Properties

        public int LevelNumber => m_LevelNumber;
        public Vector3 BaseScale => m_BaseScale;
        public bool IsUnlocked => m_IsUnlocked;
        public bool IsCompleted => m_IsCompleted;
        public bool IsSelected => m_IsSelected;
        public Button ButtonComponent => m_Button;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            AutoWireComponents();
        }

        private void Awake()
        {
            if (m_Button == null || m_SelectedArrow == null)
            {
                AutoWireComponents();
            }

            m_BaseScale = transform.localScale;

            if (m_SelectedArrow != null)
            {
                ConfigureArrowTransform();
                m_ArrowRestPos = m_SelectedArrow.transform.localPosition;
                m_ArrowRestScale = m_SelectedArrow.transform.localScale;
                m_SelectedArrow.SetActive(false);
            }

            if (m_SelectorFillEff != null)
            {
                m_SelectorFillEff.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveListener(HandleButtonClick);
                m_Button.onClick.AddListener(HandleButtonClick);
            }

            Collectables.LevelCollectableService.OnProgressChanged -= HandleCollectablesChanged;
            Collectables.LevelCollectableService.OnProgressChanged += HandleCollectablesChanged;
        }

        private void OnDisable()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveListener(HandleButtonClick);
            }

            Collectables.LevelCollectableService.OnProgressChanged -= HandleCollectablesChanged;

            StopAllAnimations();
            transform.localScale = m_BaseScale;
            m_IsLoading = false;
        }

        private void HandleCollectablesChanged()
        {
            if (m_LevelNumber > 0)
            {
                SetupCollectableDisplay(null);
            }
        }

        #endregion

        #region Auto-Wiring

        /// <summary>
        /// Automatically discovers and configures child components matching the New Level Prefab hierarchy.
        /// </summary>
        public void AutoWireComponents()
        {
            if (m_Button == null)
            {
                m_Button = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
            }

            if (m_BorderImage == null)
            {
                m_BorderImage = GetComponent<Image>();
            }

            // Find Level Name TMP Text
            if (m_LevelNameText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.name.Contains("TMP") || t.name.Contains("Text") || t.name.Contains("Level"))
                    {
                        m_LevelNameText = t;
                        break;
                    }
                }
            }
            if (m_LevelNameText != null) m_LevelNameText.raycastTarget = false;

            // Find Level Name Banner Image
            if (m_LevelNameBannerImage == null)
            {
                Transform bannerTrans = FindChildRecursive(transform, "Level Name ") ?? FindChildRecursive(transform, "Level Name");
                if (bannerTrans != null)
                {
                    m_LevelNameBannerImage = bannerTrans.GetComponent<Image>();
                }
            }
            if (m_LevelNameBannerImage != null) m_LevelNameBannerImage.raycastTarget = false;

            // Find Level Image under Mask
            if (m_LevelImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject == gameObject) continue;
                    if (img.name.Equals("Level Image", StringComparison.OrdinalIgnoreCase) ||
                        (img.transform.parent != null && img.transform.parent.name.Equals("Mask", StringComparison.OrdinalIgnoreCase)))
                    {
                        m_LevelImage = img;
                        break;
                    }
                }
            }
            if (m_LevelImage != null) m_LevelImage.raycastTarget = false;

            // Find Mask Image and disable raycast
            Transform maskTrans = FindChildRecursive(transform, "Mask");
            if (maskTrans != null)
            {
                var maskImg = maskTrans.GetComponent<Image>();
                if (maskImg != null) maskImg.raycastTarget = false;
            }

            // Find Completed Tick
            if (m_CompletedTick == null)
            {
                var tick = FindChildRecursive(transform, "Completed Tick");
                if (tick != null) m_CompletedTick = tick.gameObject;
            }
            if (m_CompletedTick != null)
            {
                var tickImg = m_CompletedTick.GetComponent<Image>();
                if (tickImg != null) tickImg.raycastTarget = false;
            }

            // Find Selected Arrow
            if (m_SelectedArrow == null)
            {
                var arrow = FindChildRecursive(transform, "Sellected Arrow") ?? FindChildRecursive(transform, "Selected Arrow");
                if (arrow != null) m_SelectedArrow = arrow.gameObject;
            }
            ConfigureArrowTransform();

            // Find Collectable Icon
            if (m_CollectableIcon == null)
            {
                var collectable = FindChildRecursive(transform, "Collectable Icon");
                if (collectable != null) m_CollectableIcon = collectable.gameObject;
            }
            if (m_CollectableIcon != null)
            {
                var cImg = m_CollectableIcon.GetComponent<Image>();
                if (cImg != null) cImg.raycastTarget = false;
            }

            // Find Selector Fill Effect
            if (m_SelectorFillEff == null)
            {
                var fill = FindChildRecursive(transform, "B Selecter Fill eff") ?? FindChildRecursive(transform, "Selector Fill");
                if (fill != null) m_SelectorFillEff = fill.gameObject;
            }
            if (m_SelectorFillEff != null)
            {
                var fImg = m_SelectorFillEff.GetComponent<Image>();
                if (fImg != null) fImg.raycastTarget = false;
            }

            // Find Locked Overlay
            if (m_LockedOverlay == null)
            {
                var locked = FindChildRecursive(transform, "Locked") ?? FindChildRecursive(transform, "Lock");
                if (locked != null) m_LockedOverlay = locked.gameObject;
            }

#if UNITY_EDITOR
            // Discover banner sprites if not yet assigned
            if (m_BannerNormalSprite == null)
            {
                m_BannerNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL Button.png");
                if (m_BannerNormalSprite == null)
                {
                    m_BannerNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Level 1 button.png");
                }
            }
            if (m_BannerSelectedSprite == null)
            {
                m_BannerSelectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL Button_selected.png");
                if (m_BannerSelectedSprite == null)
                {
                    m_BannerSelectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Level 1 button select.png");
                }
            }

            // Discover collectable sprites if not yet assigned
            if (m_CollectableUncollectedSprite == null)
            {
                m_CollectableUncollectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Collectable unknown.png");
            }
            if (m_CollectableCollectedSprite == null)
            {
                m_CollectableCollectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/Collect sprite/Collect.png")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/00 Marked Asssets for UI/collectables icon.png");
            }
#endif
        }

        /// <summary>
        /// Configures the pointer arrow RectTransform so it floats above the card pointing down.
        /// </summary>
        public void ConfigureArrowTransform()
        {
            if (m_SelectedArrow == null) return;

            RectTransform arrowRt = m_SelectedArrow.GetComponent<RectTransform>();
            if (arrowRt != null)
            {
                arrowRt.anchorMin = new Vector2(0.5f, 1f);
                arrowRt.anchorMax = new Vector2(0.5f, 1f);
                arrowRt.pivot = new Vector2(0.5f, 0f); // Pivot at the bottom tip of the arrow
                arrowRt.anchoredPosition = new Vector2(0f, 2f); // Tightly seated above top edge
                arrowRt.localScale = new Vector3(0.40f, 0.40f, 0.40f); // Compact scale to avoid overlapping row above
            }

            Image arrowImg = m_SelectedArrow.GetComponent<Image>();
            if (arrowImg != null)
            {
                arrowImg.raycastTarget = false;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                    child.name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                Transform deeper = FindChildRecursive(child, targetName);
                if (deeper != null) return deeper;
            }
            return null;
        }

        #endregion

        #region Configuration & Setup

        /// <summary>
        /// Configures this level button with complete progression and visual state.
        /// </summary>
        public void Setup(
            int levelNumber,
            bool isUnlocked,
            bool isCompleted,
            bool isSelected = false,
            Sprite thumbnail = null,
            bool? hasCollectable = null)
        {
            m_LevelNumber = levelNumber;
            m_IsUnlocked = isUnlocked;
            m_IsCompleted = isCompleted;
            m_IsSelected = isSelected;
            m_IsLoading = false;

            if (m_SelectedArrow != null)
            {
                ConfigureArrowTransform();
                m_ArrowRestPos = m_SelectedArrow.transform.localPosition;
                m_ArrowRestScale = m_SelectedArrow.transform.localScale;
            }

            // 1. Text Label: Unlocked shows number (e.g. LEVEL 1), Locked shows X or XX (e.g. LEVEL X, LEVEL XX)
            UpdateNameDisplay();

            // 2. Thumbnail: always show vibrant full color (requirement 2)
            if (m_LevelImage != null)
            {
                if (thumbnail != null)
                {
                    m_LevelImage.sprite = thumbnail;
                }
                m_LevelImage.color = Color.white;
            }

            // 3. Completed Tick: Show tick ONLY if level is completed
            if (m_CompletedTick != null)
            {
                m_CompletedTick.SetActive(isCompleted);
                Image tickImg = m_CompletedTick.GetComponent<Image>();
                if (tickImg != null) tickImg.raycastTarget = false;
            }

            // 4. Locked Overlay
            if (m_LockedOverlay != null)
            {
                m_LockedOverlay.SetActive(!isUnlocked);
            }

            // 5. Button Interactability: Keep interactable and set navigation to None to prevent accidental jumps to B Back
            if (m_Button != null)
            {
                m_Button.interactable = true;
                Navigation nav = m_Button.navigation;
                nav.mode = Navigation.Mode.None;
                m_Button.navigation = nav;
            }

            // 6. Collectable Icon status
            SetupCollectableDisplay(hasCollectable);

            // 7. Selected state visuals
            ApplySelectionVisuals(isSelected, instant: true);
        }

        /// <summary>
        /// Updates the level name text label based on unlock state.
        /// Unlocked levels display their number (e.g. "LEVEL 1", "LEVEL 10").
        /// Locked levels display "X" or "XX" matching digit count (e.g. "LEVEL X", "LEVEL XX").
        /// </summary>
        public void UpdateNameDisplay()
        {
            if (m_LevelNameText == null) return;

            string displayValue = m_LevelNumber.ToString();

            m_LevelNameText.text = string.IsNullOrEmpty(m_NameFormat)
                ? displayValue
                : string.Format(m_NameFormat, displayValue);
        }

        /// <summary>
        /// Updates unlocked state dynamically and refreshes visuals and name text.
        /// </summary>
        public void SetUnlocked(bool unlocked)
        {
            m_IsUnlocked = unlocked;
            if (m_LockedOverlay != null) m_LockedOverlay.SetActive(!unlocked);
            UpdateNameDisplay();
            ApplySelectionVisuals(m_IsSelected, instant: true);
        }

        private void SetupCollectableDisplay(bool? explicitStatus)
        {
            if (m_CollectableIcon == null) return;

            // Requirement 1: Collectable image is strictly and only visible on every 10th level (10, 20, 30, ...)
            if (m_LevelNumber <= 0 || (m_LevelNumber % 10 != 0))
            {
                m_CollectableIcon.SetActive(false);
                return;
            }

            var img = m_CollectableIcon.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;

            bool collected = false;

            if (explicitStatus.HasValue)
            {
                collected = explicitStatus.Value;
            }
            else
            {
                var info = Collectables.LevelCollectableService.For(m_LevelNumber);
                collected = (info.HasRobotPart && info.RobotPartCollected) || (info.HasMemoryShard && info.MemoryShardCollected);
            }

            // Level has collectables -> show icon
            m_CollectableIcon.SetActive(true);

            if (img != null)
            {
                img.color = Color.white;

#if UNITY_EDITOR
                if (m_CollectableUncollectedSprite == null)
                {
                    m_CollectableUncollectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Collectable unknown.png");
                }
                if (m_CollectableCollectedSprite == null)
                {
                    m_CollectableCollectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/Collect sprite/Collect.png")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/00 Marked Asssets for UI/collect.png");
                }
#endif

                // If user collected the collectable, change sprite to collected sprite (or part/shard icon)
                if (collected)
                {
                    Sprite resolvedCollected = m_CollectableCollectedSprite;
                    var info = Collectables.LevelCollectableService.For(m_LevelNumber);
                    if (info.HasRobotPart && info.RobotPartCollected)
                    {
                        Sprite part = Collectables.LevelCollectableService.RobotPartIcon(info.Robot, info.PartIndex);
                        if (part != null) resolvedCollected = part;
                    }
                    else if (info.HasMemoryShard && info.MemoryShardCollected)
                    {
                        Sprite shard = Collectables.LevelCollectableService.MemoryShardIcon();
                        if (shard != null) resolvedCollected = shard;
                    }

                    img.sprite = resolvedCollected != null ? resolvedCollected : m_CollectableCollectedSprite;
                }
                else
                {
                    if (m_CollectableUncollectedSprite != null)
                    {
                        img.sprite = m_CollectableUncollectedSprite;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the selection / focus highlights on this card.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (m_IsSelected == selected) return;
            m_IsSelected = selected;
            ApplySelectionVisuals(selected, instant: false);
        }

        private void ApplySelectionVisuals(bool selected, bool instant)
        {
            // 1. Arrow indicator
            if (m_SelectedArrow != null)
            {
                m_SelectedArrow.SetActive(selected);
                if (selected && m_AnimateArrow && isActiveAndEnabled)
                {
                    StartArrowPulse();
                }
                else
                {
                    StopArrowPulse();
                }
            }

            // 2. Border Color & Selector Fill Effect: Orange when selected!
            Color borderColor = selected ? m_SelectedBorderColor : (m_IsUnlocked ? m_NormalBorderColor : m_LockedBorderColor);
            if (m_BorderImage != null)
            {
                m_BorderImage.color = borderColor;
            }

            if (m_SelectorFillEff != null)
            {
                m_SelectorFillEff.SetActive(selected);
                var fillImg = m_SelectorFillEff.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.color = m_SelectedBorderColor;
                    fillImg.raycastTarget = false;
                }
            }

            // 3. Level Name Banner Sprite: Orange when selected, Blue when normal!
            if (m_LevelNameBannerImage != null)
            {
                if (selected && m_BannerSelectedSprite != null)
                {
                    m_LevelNameBannerImage.sprite = m_BannerSelectedSprite;
                }
                else if (!selected && m_BannerNormalSprite != null)
                {
                    m_LevelNameBannerImage.sprite = m_BannerNormalSprite;
                }
            }

            // 4. Subtle hover lift
            Vector3 targetScale = selected ? (m_BaseScale * m_HoverScaleMultiplier) : m_BaseScale;
            if (instant || !isActiveAndEnabled)
            {
                transform.localScale = targetScale;
            }
            else
            {
                AnimateScaleTo(targetScale, 0.12f);
            }
        }

        #endregion

        #region Event Handlers & Navigation

        private static Vector2 s_LastPointerPosition;

        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(true);
            if (m_LevelNumber > 0)
            {
                OnAnyButtonFocused?.Invoke(m_LevelNumber);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Only take focus if pointer actually moved, avoiding mouse hover re-stealing focus from keyboard navigation
            if (eventData != null && (eventData.position - s_LastPointerPosition).sqrMagnitude < 9f)
            {
                return;
            }
            if (eventData != null) s_LastPointerPosition = eventData.position;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                SetSelected(true);
            }

            if (m_LevelNumber > 0)
            {
                OnAnyButtonFocused?.Invoke(m_LevelNumber);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                SetSelected(false);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            HandleButtonClick();
        }

        public void HandleButtonClick()
        {
            if (!m_IsUnlocked)
            {
                // Play locked reject audio feedback and quick shake
                UIFeedbackAudio.PlaySfx(UISfxType.WarningAlert, 0.6f, 0.05f);
                if (isActiveAndEnabled)
                {
                    if (m_ShakeCoroutine != null) StopCoroutine(m_ShakeCoroutine);
                    m_ShakeCoroutine = StartCoroutine(LockedShakeRoutine());
                }
                return;
            }

            if (m_IsLoading) return;
            StartCoroutine(ClickPunchAndLoadRoutine());
        }

        private IEnumerator LockedShakeRoutine()
        {
            Vector3 basePos = transform.localPosition;
            float elapsed = 0f;
            float duration = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(elapsed / duration);
                float xOffset = Mathf.Sin(elapsed * 45f) * 8f * decay;
                transform.localPosition = new Vector3(basePos.x + xOffset, basePos.y, basePos.z);
                yield return null;
            }
            transform.localPosition = basePos;
            m_ShakeCoroutine = null;
        }

        private IEnumerator ClickPunchAndLoadRoutine()
        {
            m_IsLoading = true;

            // Click juice punch: quick squeeze down then pop out
            float elapsed = 0f;
            float duration = 0.14f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = (t < 0.35f)
                    ? Mathf.Lerp(1f, 0.90f, t / 0.35f)
                    : Mathf.Lerp(0.90f, 1.10f, (t - 0.35f) / 0.65f);

                transform.localScale = m_BaseScale * s;
                yield return null;
            }

            transform.localScale = m_BaseScale;

            // Notify listeners
            OnLevelClicked?.Invoke(m_LevelNumber);

            // Re-enable input and timeScale before loading scene
            Time.timeScale = 1f;

            // Check if LevelManager is available
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.LoadLevel(m_LevelNumber);
                yield break;
            }

            // Fallback direct SceneManager load
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            if (m_LevelNumber >= 0 && m_LevelNumber < sceneCount)
            {
                SceneManager.LoadScene(m_LevelNumber);
            }
            else
            {
                Debug.LogWarning($"[GridLevelButton] Level {m_LevelNumber} scene index is out of Build Settings range (0..{sceneCount - 1}).");
                m_IsLoading = false;
            }
        }

        #endregion

        #region Animation Coroutines

        private void AnimateScaleTo(Vector3 targetScale, float duration)
        {
            if (m_ScaleCoroutine != null) StopCoroutine(m_ScaleCoroutine);
            m_ScaleCoroutine = StartCoroutine(ScaleRoutine(targetScale, duration));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale, float duration)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                transform.localScale = Vector3.LerpUnclamped(start, targetScale, ease);
                yield return null;
            }
            transform.localScale = targetScale;
            m_ScaleCoroutine = null;
        }

        private void StartArrowPulse()
        {
            StopArrowPulse();
            if (m_SelectedArrow != null && isActiveAndEnabled)
            {
                ConfigureArrowTransform();
                m_ArrowRestPos = m_SelectedArrow.transform.localPosition;
                m_ArrowRestScale = m_SelectedArrow.transform.localScale;
                m_ArrowCoroutine = StartCoroutine(ArrowPulseRoutine());
            }
        }

        private void StopArrowPulse()
        {
            if (m_ArrowCoroutine != null)
            {
                StopCoroutine(m_ArrowCoroutine);
                m_ArrowCoroutine = null;
            }
            if (m_SelectedArrow != null)
            {
                m_SelectedArrow.transform.localPosition = m_ArrowRestPos;
                m_SelectedArrow.transform.localScale = m_ArrowRestScale;
            }
        }

        private IEnumerator ArrowPulseRoutine()
        {
            float elapsed = 0f;
            Vector3 basePos = m_ArrowRestPos;
            while (m_SelectedArrow != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(elapsed * m_ArrowPulseSpeed);

                m_SelectedArrow.transform.localScale = m_ArrowRestScale * (1f + wave * m_ArrowPulseScale);
                m_SelectedArrow.transform.localPosition = new Vector3(
                    basePos.x,
                    basePos.y + (wave * m_ArrowBobDistance),
                    basePos.z);

                yield return null;
            }
            m_ArrowCoroutine = null;
        }

        private void StopAllAnimations()
        {
            if (m_ScaleCoroutine != null)
            {
                StopCoroutine(m_ScaleCoroutine);
                m_ScaleCoroutine = null;
            }
            if (m_ShakeCoroutine != null)
            {
                StopCoroutine(m_ShakeCoroutine);
                m_ShakeCoroutine = null;
            }
            StopArrowPulse();
        }

        #endregion
    }
}
