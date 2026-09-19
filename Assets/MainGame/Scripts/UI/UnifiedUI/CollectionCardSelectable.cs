using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MainGame.UI.Animation;
using Collectables;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Component on each robot station in the Collection Screen providing controller-first navigation,
    /// locked/unlocked hangar status, platform lighting coordination, and selection feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectionCardSelectable : Selectable, ISubmitHandler
    {
        #region Inspector Fields

        [Header("Visual References")]
        [Tooltip("Transform of the station visual that shifts and scales. Defaults to self if not set.")]
        [SerializeField] private RectTransform m_CardVisual;

        [Tooltip("Lock indicator badge for this robot when chassis is offline/locked.")]
        [SerializeField] private GameObject m_LockIndicator;

        [Header("Platform Reference")]
        [SerializeField] private RectTransform m_PlatformRect;
        [SerializeField] private Image m_PlatformGlow;

        [Header("Station Telemetry (Assigned dynamically)")]
        [SerializeField] private Image[] m_StationPips;

        #endregion

        #region Events

        public event Action<CollectionCardSelectable> OnCardSelected;
        public event Action<CollectionCardSelectable> OnCardDeselected;
        public event Action<CollectionCardSelectable> OnCardSubmitted;

        #endregion

        #region Properties

        public RectTransform CardVisual => m_CardVisual != null ? m_CardVisual : (RectTransform)transform;
        public GameObject LockIndicator { get => m_LockIndicator; set => m_LockIndicator = value; }
        public RectTransform PlatformRect { get => m_PlatformRect; set => m_PlatformRect = value; }
        public Image PlatformGlow { get => m_PlatformGlow; set => m_PlatformGlow = value; }
        public Image[] StationPips { get => m_StationPips; set => m_StationPips = value; }
        public RobotCollectionSlot Slot => GetComponent<RobotCollectionSlot>();
        public CanvasGroup CanvasGroup => m_CanvasGroup;
        public bool IsUnlocked { get; private set; } = true;
        public Vector2 RestPosition => m_RestPosition;
        public Vector3 RestScale => m_RestScale;

        public RobotId Robot
        {
            get
            {
                if (Slot != null && Slot.Definition != null) return Slot.Definition.robot;
                string objName = gameObject.name.ToLowerInvariant();
                if (objName.Contains("nova")) return RobotId.Nova;
                if (objName.Contains("patch")) return RobotId.Patch;
                if (objName.Contains("pixel")) return RobotId.Pixel;
                return RobotId.Echo;
            }
        }

        #endregion

        #region Private Fields

        private Vector2 m_RestPosition;
        private Vector3 m_RestScale = Vector3.one;
        private CanvasGroup m_CanvasGroup;
        private Coroutine m_LockPulseCoroutine;
        private bool m_HasCapturedRest = false;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            if (m_CardVisual == null)
            {
                m_CardVisual = GetComponent<RectTransform>();
            }

            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Ensure a transparent graphic exists for controller focus
            if (targetGraphic == null)
            {
                Graphic graphic = GetComponent<Graphic>();
                if (graphic == null)
                {
                    Image img = gameObject.AddComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0f);
                    img.raycastTarget = true;
                    targetGraphic = img;
                }
                else
                {
                    targetGraphic = graphic;
                }
            }

            CaptureRestState();
            UpdateUnlockState();
            EnsureLockBadge();
        }

        protected override void Start()
        {
            base.Start();
            CaptureRestState();
            UpdateUnlockState();
            EnsureLockBadge();
            RefreshStationProgress();
        }

        #endregion

        #region State Management

        public void CaptureRestState()
        {
            if (m_HasCapturedRest || m_CardVisual == null) return;

            m_RestPosition = m_CardVisual.anchoredPosition;
            m_RestScale = m_CardVisual.localScale;
            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            if (!m_HasCapturedRest) return;

            if (m_LockPulseCoroutine != null)
            {
                StopCoroutine(m_LockPulseCoroutine);
                m_LockPulseCoroutine = null;
            }

            if (m_CardVisual != null)
            {
                m_CardVisual.anchoredPosition = m_RestPosition;
                m_CardVisual.localScale = m_RestScale;
            }

            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 1f;
            }
        }

        public void UpdateUnlockState()
        {
            RobotId id = Robot;
            int count = RobotCollectionService.CollectedCount(id);
            int highestLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();

            // Echo (Levels 1-5): always unlocked
            // Nova (Levels 6-10): unlocked if level >= 6 or has parts
            // Patch (Levels 11-15): unlocked if level >= 11 or has parts
            // Pixel (Levels 16-20): unlocked if level >= 16 or has parts
            switch (id)
            {
                case RobotId.Echo:
                    IsUnlocked = true;
                    break;
                case RobotId.Nova:
                    IsUnlocked = (highestLevel >= 6) || (count > 0);
                    break;
                case RobotId.Patch:
                    IsUnlocked = (highestLevel >= 11) || (count > 0);
                    break;
                case RobotId.Pixel:
                    IsUnlocked = (highestLevel >= 16) || (count > 0);
                    break;
                default:
                    IsUnlocked = true;
                    break;
            }

            EnsureLockBadge();
            if (m_LockIndicator != null)
            {
                m_LockIndicator.SetActive(!IsUnlocked);
            }
        }

        public void EnsureLockBadge()
        {
            if (m_LockIndicator != null) return;

            Transform portraitTrans = transform.Find("Portrait");
            if (portraitTrans == null) return;

            Transform existingBadge = portraitTrans.Find("ChassisLockBadge");
            if (existingBadge != null)
            {
                m_LockIndicator = existingBadge.gameObject;
                return;
            }

            // Procedurally create a crisp pixel-art padlock badge
            GameObject badgeGo = new GameObject("ChassisLockBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(portraitTrans, false);
            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.sizeDelta = new Vector2(70f, 70f);
            badgeRt.anchoredPosition = new Vector2(0f, 0f);

            // Dark backing plate
            GameObject plateGo = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plateGo.transform.SetParent(badgeGo.transform, false);
            RectTransform plateRt = plateGo.GetComponent<RectTransform>();
            plateRt.anchorMin = Vector2.zero;
            plateRt.anchorMax = Vector2.one;
            plateRt.sizeDelta = Vector2.zero;
            Image plateImg = plateGo.GetComponent<Image>();
            plateImg.color = new Color(0.08f, 0.04f, 0.04f, 0.88f);
            plateImg.raycastTarget = false;

            // Warning border line
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(badgeGo.transform, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(-4f, -4f);
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.color = new Color(1f, 0.28f, 0.20f, 0.80f);
            borderImg.raycastTarget = false;

            // Inner dark inset
            GameObject insetGo = new GameObject("Inset", typeof(RectTransform), typeof(Image));
            insetGo.transform.SetParent(borderGo.transform, false);
            RectTransform insetRt = insetGo.GetComponent<RectTransform>();
            insetRt.anchorMin = Vector2.zero;
            insetRt.anchorMax = Vector2.one;
            insetRt.sizeDelta = new Vector2(-4f, -4f);
            Image insetImg = insetGo.GetComponent<Image>();
            insetImg.color = new Color(0.12f, 0.05f, 0.05f, 0.95f);
            insetImg.raycastTarget = false;

            // Lock icon label
            GameObject textGo = new GameObject("LockText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(insetGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "<color=#FF4433><size=24>🔒</size></color>\n<size=11><b>LOCKED</b></size>";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            m_LockIndicator = badgeGo;
            badgeGo.SetActive(!IsUnlocked);
        }

        public void RefreshStationProgress()
        {
            if (m_StationPips == null || m_StationPips.Length == 0) return;

            int collected = RobotCollectionService.CollectedCount(Robot);
            Color accent = GetRobotAccentColor();

            for (int i = 0; i < m_StationPips.Length; i++)
            {
                if (m_StationPips[i] == null) continue;
                bool has = i < collected;
                m_StationPips[i].color = has ? accent : new Color(0.12f, 0.18f, 0.28f, 0.65f);
            }
        }

        private Color GetRobotAccentColor()
        {
            switch (Robot)
            {
                case RobotId.Echo: return new Color(0.35f, 0.78f, 1f);
                case RobotId.Nova: return new Color(1f, 0.60f, 0.20f);
                case RobotId.Patch: return new Color(0.35f, 1f, 0.55f);
                case RobotId.Pixel: return new Color(1f, 0.35f, 0.65f);
                default: return Color.cyan;
            }
        }

        public void SetVisualState(bool isSelected, float alphaMultiplier = 1f)
        {
            if (m_CanvasGroup == null) return;

            if (isSelected)
            {
                m_CanvasGroup.alpha = (IsUnlocked ? 1.0f : 0.45f) * alphaMultiplier;
                if (m_PlatformGlow != null)
                {
                    Color c = m_PlatformGlow.color;
                    m_PlatformGlow.color = new Color(c.r, c.g, c.b, (IsUnlocked ? 0.85f : 0.15f) * alphaMultiplier);
                }
            }
            else
            {
                m_CanvasGroup.alpha = (IsUnlocked ? 0.72f : 0.40f) * alphaMultiplier;
                if (m_PlatformGlow != null)
                {
                    Color c = m_PlatformGlow.color;
                    m_PlatformGlow.color = new Color(c.r, c.g, c.b, (IsUnlocked ? 0.20f : 0.08f) * alphaMultiplier);
                }
            }

            RefreshStationProgress();
        }

        #endregion

        #region EventSystem Handling (Controller Only)

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            var animator = GetComponentInParent<CollectionScreenAnimator>();
            if (animator != null && animator.IsAnimating) return;

            OnCardSelected?.Invoke(this);

            if (animator != null)
            {
                animator.HandleCardFocusChanged(this);
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            OnCardDeselected?.Invoke(this);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            // Controller-first: Mouse hover does NOT trigger selection
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            // Controller-first: Mouse hover exit does NOT change selection
        }

        public void OnSubmit(BaseEventData eventData)
        {
            var animator = GetComponentInParent<CollectionScreenAnimator>();
            if (animator != null && animator.IsAnimating) return;

            OnCardSubmitted?.Invoke(this);

            if (animator != null)
            {
                animator.PlayConfirmInspection(this);
            }
        }

        #endregion

        #region Mechanical Pulse Routines

        public void TriggerLockPulse()
        {
            if (m_LockPulseCoroutine != null) StopCoroutine(m_LockPulseCoroutine);
            if (isActiveAndEnabled)
            {
                m_LockPulseCoroutine = StartCoroutine(LockPulseRoutine());
            }
        }

        private IEnumerator LockPulseRoutine()
        {
            if (m_LockIndicator == null) yield break;

            Transform lockT = m_LockIndicator.transform;
            Vector3 baseScale = Vector3.one;
            float duration = 0.24f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Quick bounce scale
                float punch = Mathf.Sin(t * Mathf.PI);
                float s = Mathf.Lerp(1f, 1.28f, punch);
                lockT.localScale = baseScale * s;

                // Subtle mechanical rotation rattle
                float rot = Mathf.Sin(elapsed * 50f) * 7f * (1f - t);
                lockT.localEulerAngles = new Vector3(0f, 0f, rot);

                yield return null;
            }

            lockT.localScale = baseScale;
            lockT.localEulerAngles = Vector3.zero;
            m_LockPulseCoroutine = null;
        }

        #endregion
    }
}
