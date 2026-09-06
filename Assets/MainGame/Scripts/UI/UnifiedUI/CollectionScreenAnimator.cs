using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MainGame.UI.Animation;
using Collectables;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Collection screen entrance, navigation, inspection, and exit as a cinematic
    /// "ROBOT INSPECTION / HANGAR" interface:
    /// 1. Dark navy panel arrives via mechanical terminal deployment with impact micro-shake.
    /// 2. Technical blueprint grid revealed by a horizontal scanline wipe.
    /// 3. Asymmetric mechanical platform deployment for each of the 4 robot bays.
    /// 4. Sequential robot chassis boot-up (Echo -> Nova -> Patch -> Pixel) with lift, overshoot & flash.
    /// 5. Stronger activation sequence on the focused robot: platform illumination, holographic inspection beam sweep, outline flash, micro-lift, and mechanical selection frame.
    /// 6. Mechanical construction of the diagnostics & specs panel (corners pop, lines draw, title, status, description, progress pips).
    /// 7. COLLECT signboard drops with physical pendulum swing & ambient rope sway; Back button slides up from bottom.
    /// 8. Controller-first selection transitions (0.18s) with platform light transfer, micro-lift, sweep beam, and diagnostics update.
    /// 9. Locked robot handling: dim chassis, mechanical lock pulse, restricted warning readouts.
    /// 10. A-button confirmation punch (0.14s) with compression and flash.
    /// 11. Reverse retraction and fold-away on exit.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectionScreenAnimator : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelStartScale = 0.94f;
        [SerializeField] private float m_PanelDuration = 0.28f;

        [Header("Header Elements")]
        [Tooltip("COLLECTION signboard entering from upper-right with physical pendulum decay.")]
        [SerializeField] private RectTransform m_CollectionSign;
        [Tooltip("0/20 total counter entering with slight delay.")]
        [SerializeField] private RectTransform m_TotalCounter;

        [Header("Character Cards (Echo, Nova, Patch, Pixel)")]
        [SerializeField] private RectTransform m_CardEcho;
        [SerializeField] private RectTransform m_CardNova;
        [SerializeField] private RectTransform m_CardPatch;
        [SerializeField] private RectTransform m_CardPixel;

        [Header("Back Button")]
        [SerializeField] private RectTransform m_BackButton;

        [Header("Hangar Elements (Assigned or Auto-Generated)")]
        [SerializeField] private RectTransform m_TechnicalGrid;
        [SerializeField] private RectTransform[] m_Platforms;
        [SerializeField] private Image[] m_PlatformGlows;
        [SerializeField] private RectTransform m_SelectionFrame;
        [SerializeField] private RectTransform m_InspectionBeam;
        [SerializeField] private Image m_InspectionBeamImage;

        [Header("Diagnostics & Specs Panel")]
        [SerializeField] private RectTransform m_DetailsPanel;
        [SerializeField] private RectTransform m_DetailsTopLine;
        [SerializeField] private RectTransform m_DetailsBottomLine;
        [SerializeField] private TMP_Text m_DetailsTitleText;
        [SerializeField] private TMP_Text m_DetailsStatusText;
        [SerializeField] private TMP_Text m_DetailsDescText;
        [SerializeField] private TMP_Text m_DetailsProgressText;
        [SerializeField] private Image[] m_DetailsProgressPips;

        #endregion

        #region Public Properties

        public bool IsAnimating => m_ActiveRoutine != null;
        public CollectionCardSelectable CurrentlySelectedCard => m_CurrentlySelectedCard;

        #endregion

        #region Private Fields

        private Vector2 m_PanelRestPos;
        private Vector3 m_PanelRestScale = Vector3.one;

        private Vector2 m_SignRestPos;
        private Vector3 m_SignRestAngles;

        private Vector2 m_CounterRestPos;
        private Vector2 m_BackRestPos;

        private RectTransform[] m_CardRects;
        private Vector2[] m_CardRestPositions;
        private CollectionCardSelectable[] m_CardSelectables;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_SignBreezeRoutine;
        private Coroutine m_BeamRoutine;
        private Coroutine m_FrameMoveRoutine;
        private Coroutine m_CardLiftRoutine;

        private bool m_HasCapturedRest = false;
        private CollectionCardSelectable m_CurrentlySelectedCard;

        // Robot authoring specs for the diagnostics panel
        private static readonly (RobotId robot, string title, string desc, Color accent)[] RobotAuthoring =
        {
            (RobotId.Echo,  "ECHO // LIGHT SCOUT AUTOMATON",
             "HIGH-MOBILITY SCOUT UNIT EQUIPPED WITH DUAL RECOIL JUMP THRUSTERS FOR ADVANCED VERTICAL RECONNAISSANCE.",
             new Color(0.35f, 0.78f, 1.0f)),
            (RobotId.Nova,  "NOVA // LOGISTICS & POWER RIG",
             "AUXILIARY LOGISTICS AUTOMATON CAPABLE OF RAPID ENERGY RECHARGE, RELAY HARNESSING, AND CIRCUIT ROUTING.",
             new Color(1.0f, 0.60f, 0.20f)),
            (RobotId.Patch, "PATCH // HEAVY REPAIR EXCAVATOR",
             "REINFORCED CHASSIS SPECIALIZED IN HAZARDOUS OBSTACLE DISASSEMBLY AND HEAVY-DUTY SECTOR RESTORATION.",
             new Color(0.35f, 1.0f, 0.55f)),
            (RobotId.Pixel, "PIXEL // TACTICAL CORE RUNNER",
             "COMPACT PROTOTYPE CORE ENGINE POWERED BY MINIATURIZED LOGIC CELLS. BUILT FOR HIGH-PRECISION RUNS.",
             new Color(1.0f, 0.35f, 0.65f)),
        };

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CaptureRestState();
            EnsureHangarElements();
        }

        private void Start()
        {
            CaptureRestState();
            EnsureHangarElements();
        }

        private void OnDisable()
        {
            StopActiveAnimation();
        }

        #endregion

        #region State Capture & Initialization

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            Transform holder = transform.Find("Holder") ?? transform;

            if (m_PanelBackground == null)
            {
                Transform bg = holder.Find("BG") ?? transform.Find("BG");
                if (bg != null) m_PanelBackground = bg as RectTransform;
            }

            if (m_CollectionSign == null)
            {
                Transform sign = holder.Find("Setting IMG") ?? transform.Find("Setting IMG");
                if (sign != null) m_CollectionSign = sign as RectTransform;
            }

            if (m_TotalCounter == null)
            {
                Transform total = holder.Find("RobotCollection/Total") ?? transform.Find("RobotCollection/Total");
                if (total != null) m_TotalCounter = total as RectTransform;
            }

            if (m_CardEcho == null)
            {
                Transform echo = holder.Find("RobotCollection/Robots/Slot_Echo") ?? transform.Find("RobotCollection/Robots/Slot_Echo");
                if (echo != null) m_CardEcho = echo as RectTransform;
            }

            if (m_CardNova == null)
            {
                Transform nova = holder.Find("RobotCollection/Robots/Slot_Nova") ?? transform.Find("RobotCollection/Robots/Slot_Nova");
                if (nova != null) m_CardNova = nova as RectTransform;
            }

            if (m_CardPatch == null)
            {
                Transform patch = holder.Find("RobotCollection/Robots/Slot_Patch") ?? transform.Find("RobotCollection/Robots/Slot_Patch");
                if (patch != null) m_CardPatch = patch as RectTransform;
            }

            if (m_CardPixel == null)
            {
                Transform pixel = holder.Find("RobotCollection/Robots/Slot_Pixel") ?? transform.Find("RobotCollection/Robots/Slot_Pixel");
                if (pixel != null) m_CardPixel = pixel as RectTransform;
            }

            if (m_BackButton == null)
            {
                Transform back = holder.Find("Back B") ?? holder.Find("B Back") ?? transform.Find("Back B") ?? transform.Find("B Back");
                if (back != null) m_BackButton = back as RectTransform;
            }

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
                m_PanelRestScale = m_PanelBackground.localScale;
            }

            if (m_CollectionSign != null)
            {
                m_SignRestPos = m_CollectionSign.anchoredPosition;
                m_SignRestAngles = m_CollectionSign.localEulerAngles;
            }

            if (m_TotalCounter != null)
            {
                m_CounterRestPos = m_TotalCounter.anchoredPosition;
            }

            if (m_BackButton != null)
            {
                m_BackRestPos = m_BackButton.anchoredPosition;
            }

            m_CardRects = new RectTransform[] { m_CardEcho, m_CardNova, m_CardPatch, m_CardPixel };
            m_CardRestPositions = new Vector2[m_CardRects.Length];
            m_CardSelectables = new CollectionCardSelectable[m_CardRects.Length];

            for (int i = 0; i < m_CardRects.Length; i++)
            {
                if (m_CardRects[i] != null)
                {
                    m_CardRestPositions[i] = m_CardRects[i].anchoredPosition;
                    m_CardSelectables[i] = m_CardRects[i].GetComponent<CollectionCardSelectable>()
                        ?? m_CardRects[i].gameObject.AddComponent<CollectionCardSelectable>();
                }
            }

            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_PanelBackground != null)
            {
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = m_PanelRestScale;
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }

            if (m_CollectionSign != null)
            {
                m_CollectionSign.anchoredPosition = m_SignRestPos;
                m_CollectionSign.localEulerAngles = m_SignRestAngles;
            }

            if (m_TotalCounter != null)
            {
                CanvasGroup totalCg = m_TotalCounter.GetComponent<CanvasGroup>();
                if (totalCg != null) totalCg.alpha = 1f;
                m_TotalCounter.localScale = Vector3.one;
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = m_BackRestPos;
                m_BackButton.localScale = Vector3.one;
            }

            if (m_CardRects != null)
            {
                for (int i = 0; i < m_CardRects.Length; i++)
                {
                    if (m_CardRects[i] != null)
                    {
                        m_CardRects[i].anchoredPosition = m_CardRestPositions[i];
                        m_CardRects[i].localScale = Vector3.one;
                    }
                    if (m_CardSelectables != null && i < m_CardSelectables.Length && m_CardSelectables[i] != null)
                    {
                        m_CardSelectables[i].ResetToRestState();
                    }
                }
            }

            if (m_Platforms != null)
            {
                for (int i = 0; i < m_Platforms.Length; i++)
                {
                    if (m_Platforms[i] != null)
                    {
                        m_Platforms[i].localScale = Vector3.one;
                    }
                }
            }

            if (m_SelectionFrame != null) m_SelectionFrame.gameObject.SetActive(false);
            if (m_InspectionBeam != null) m_InspectionBeam.gameObject.SetActive(false);
        }

        #endregion

        #region Hangar Hierarchy Setup & Layout Synchronization

        private void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>
        /// Ensures all mechanical hangar visual objects (technical grid, per-slot inspection stations,
        /// platforms, selection frame, inspection beam, diagnostics console) exist and are linked.
        /// </summary>
        public void EnsureHangarElements()
        {
            Transform holder = transform.Find("Holder") ?? transform;

            // Clean up any obsolete separate floating platform/frame containers from prior setups
            Transform oldP1 = holder.Find("RobotCollection/Robots/PlatformsContainer");
            if (oldP1 != null) SafeDestroy(oldP1.gameObject);
            Transform oldP2 = holder.Find("RobotCollection/PlatformsContainer");
            if (oldP2 != null) SafeDestroy(oldP2.gameObject);
            Transform oldF = holder.Find("RobotCollection/Robots/HangarSelectionFrame");
            if (oldF != null) SafeDestroy(oldF.gameObject);
            Transform oldB = holder.Find("RobotCollection/Robots/HangarInspectionBeam");
            if (oldB != null) SafeDestroy(oldB.gameObject);

            // 1. RobotCollection Layout Container
            Transform robotCollection = holder.Find("RobotCollection");
            if (robotCollection != null)
            {
                RectTransform rcRt = robotCollection as RectTransform;
                if (rcRt != null)
                {
                    rcRt.anchorMin = new Vector2(0.5f, 0.5f);
                    rcRt.anchorMax = new Vector2(0.5f, 0.5f);
                    rcRt.pivot = new Vector2(0.5f, 0.5f);
                    rcRt.anchoredPosition = new Vector2(0f, 60f);
                    rcRt.sizeDelta = new Vector2(1200f, 450f);
                }
            }

            Transform robotsContainer = holder.Find("RobotCollection/Robots") ?? holder.Find("RobotCollection");
            if (robotsContainer != null)
            {
                RectTransform rbRt = robotsContainer as RectTransform;
                if (rbRt != null)
                {
                    rbRt.sizeDelta = new Vector2(1200f, 370f);
                }
                LayoutElement rbLe = robotsContainer.GetComponent<LayoutElement>();
                if (rbLe != null)
                {
                    rbLe.preferredHeight = 370f;
                }
                HorizontalLayoutGroup hlg = robotsContainer.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.spacing = 34f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }
            }

            // 2. Technical Blueprint Grid Backdrop
            if (m_TechnicalGrid == null && holder != null)
            {
                Transform existingGrid = holder.Find("TechnicalGrid");
                if (existingGrid != null)
                {
                    m_TechnicalGrid = existingGrid as RectTransform;
                }
                else
                {
                    GameObject gridGo = new GameObject("TechnicalGrid", typeof(RectTransform));
                    gridGo.transform.SetParent(holder, false);
                    gridGo.transform.SetSiblingIndex(1); // Right in front of BG

                    m_TechnicalGrid = gridGo.GetComponent<RectTransform>();
                    m_TechnicalGrid.anchorMin = new Vector2(0.5f, 0.5f);
                    m_TechnicalGrid.anchorMax = new Vector2(0.5f, 0.5f);
                    m_TechnicalGrid.sizeDelta = new Vector2(1650f, 850f);
                    m_TechnicalGrid.anchoredPosition = new Vector2(0f, -20f);

                    BuildGridLines(m_TechnicalGrid);
                }
            }

            // Style and center Total Counter
            if (m_TotalCounter != null)
            {
                TMP_Text totalText = m_TotalCounter.GetComponent<TMP_Text>();
                if (totalText != null)
                {
                    totalText.alignment = TextAlignmentOptions.Center;
                    totalText.fontSize = 24f;
                    totalText.fontStyle = FontStyles.Bold;
                    totalText.color = new Color(0.85f, 0.94f, 1f, 0.95f);
                }
                LayoutElement totalLe = m_TotalCounter.GetComponent<LayoutElement>();
                if (totalLe != null)
                {
                    totalLe.preferredHeight = 38f;
                }
                CanvasGroup totalCg = m_TotalCounter.GetComponent<CanvasGroup>();
                if (totalCg == null) m_TotalCounter.gameObject.AddComponent<CanvasGroup>();
            }

            // 3. Configure the 4 Robot Stations
            if (m_Platforms == null || m_Platforms.Length != 4)
            {
                m_Platforms = new RectTransform[4];
                m_PlatformGlows = new Image[4];
            }

            string[] slotNames = new string[] { "Slot_Echo", "Slot_Nova", "Slot_Patch", "Slot_Pixel" };
            RectTransform[] cards = new RectTransform[] { m_CardEcho, m_CardNova, m_CardPatch, m_CardPixel };

            for (int i = 0; i < 4; i++)
            {
                RectTransform cardRt = cards[i];
                if (cardRt == null && robotsContainer != null)
                {
                    Transform found = robotsContainer.Find(slotNames[i]);
                    if (found != null) cardRt = found as RectTransform;
                }
                if (cardRt == null) continue;

                // Configure Slot Dimensions (270px width x 370px height)
                cardRt.sizeDelta = new Vector2(270f, 370f);
                LayoutElement cardLe = cardRt.GetComponent<LayoutElement>();
                if (cardLe == null) cardLe = cardRt.gameObject.AddComponent<LayoutElement>();
                cardLe.preferredWidth = 270f;
                cardLe.preferredHeight = 370f;

                // VerticalLayoutGroup for slot contents
                VerticalLayoutGroup vlg = cardRt.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.spacing = 6f;
                    vlg.padding = new RectOffset(0, 0, 4, 4);
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                }

                CollectionCardSelectable selectable = cardRt.GetComponent<CollectionCardSelectable>();
                if (selectable == null) selectable = cardRt.gameObject.AddComponent<CollectionCardSelectable>();

                Color accent = RobotAuthoring[i].accent;

                // A. Dark Chamber Bay Backing Plate
                EnsureChamberBacking(cardRt);

                // B. Robot Portrait (260px x 260px)
                Transform portraitTrans = cardRt.Find("Portrait");
                if (portraitTrans != null)
                {
                    RectTransform portRt = portraitTrans as RectTransform;
                    portRt.sizeDelta = new Vector2(260f, 260f);
                    LayoutElement portLe = portraitTrans.GetComponent<LayoutElement>();
                    if (portLe != null)
                    {
                        portLe.preferredWidth = 260f;
                        portLe.preferredHeight = 260f;
                    }
                }

                // C. Station Platform (260px x 22px) directly beneath Robot Portrait
                EnsureStationPlatform(cardRt, i, accent);

                // D. Name Label
                Transform nameTrans = cardRt.Find("Name");
                if (nameTrans != null)
                {
                    RectTransform nameRt = nameTrans as RectTransform;
                    nameRt.sizeDelta = new Vector2(260f, 24f);
                    LayoutElement nameLe = nameTrans.GetComponent<LayoutElement>();
                    if (nameLe != null) { nameLe.preferredWidth = 260f; nameLe.preferredHeight = 24f; }

                    TMP_Text nameTmp = nameTrans.GetComponent<TMP_Text>();
                    if (nameTmp != null)
                    {
                        nameTmp.alignment = TextAlignmentOptions.Center;
                        nameTmp.fontSize = 20f;
                        nameTmp.fontStyle = FontStyles.Bold;
                    }
                }

                // E. Count Label
                Transform countTrans = cardRt.Find("Count");
                if (countTrans != null)
                {
                    RectTransform countRt = countTrans as RectTransform;
                    countRt.sizeDelta = new Vector2(260f, 18f);
                    LayoutElement countLe = countTrans.GetComponent<LayoutElement>();
                    if (countLe != null) { countLe.preferredWidth = 260f; countLe.preferredHeight = 18f; }

                    TMP_Text countTmp = countTrans.GetComponent<TMP_Text>();
                    if (countTmp != null)
                    {
                        countTmp.alignment = TextAlignmentOptions.Center;
                        countTmp.fontSize = 15f;
                        countTmp.color = new Color(0.65f, 0.75f, 0.88f, 0.90f);
                    }
                }

                // F. Mini Station Progress Pips (5 LED blocks)
                EnsureStationPips(cardRt, selectable, accent);

                // G. Padlock Badge
                selectable.EnsureLockBadge();
                selectable.RefreshStationProgress();
            }

            // 4. Selection Frame (L-Brackets wrapping 260x260 portrait)
            if (m_SelectionFrame == null && holder != null)
            {
                Transform existingF = holder.Find("HangarSelectionFrame");
                if (existingF != null)
                {
                    m_SelectionFrame = existingF as RectTransform;
                }
                else
                {
                    m_SelectionFrame = BuildSelectionFrame(holder);
                }
            }

            // 5. Holographic Inspection Beam
            if (m_InspectionBeam == null && holder != null)
            {
                Transform existingB = holder.Find("HangarInspectionBeam");
                if (existingB != null)
                {
                    m_InspectionBeam = existingB as RectTransform;
                    m_InspectionBeamImage = m_InspectionBeam.GetComponent<Image>();
                }
                else
                {
                    m_InspectionBeam = BuildInspectionBeam(holder, out m_InspectionBeamImage);
                }
            }

            // 6. Diagnostics & Specs Console
            if (m_DetailsPanel == null && holder != null)
            {
                Transform existingD = holder.Find("DiagnosticsDetailsPanel");
                if (existingD != null)
                {
                    m_DetailsPanel = existingD as RectTransform;
                    BindExistingDetailsPanel(m_DetailsPanel);
                }
                else
                {
                    m_DetailsPanel = BuildDiagnosticsPanel(holder);
                }
            }
            if (m_DetailsPanel != null)
            {
                m_DetailsPanel.anchorMin = new Vector2(0.5f, 0.5f);
                m_DetailsPanel.anchorMax = new Vector2(0.5f, 0.5f);
                m_DetailsPanel.pivot = new Vector2(0.5f, 0.5f);
                m_DetailsPanel.sizeDelta = new Vector2(1200f, 106f);
                m_DetailsPanel.anchoredPosition = new Vector2(0f, -235f);
            }
        }

        private void EnsureChamberBacking(RectTransform cardRt)
        {
            Transform existing = cardRt.Find("ChamberBacking");
            if (existing != null) return;

            GameObject backingGo = new GameObject("ChamberBacking", typeof(RectTransform), typeof(Image));
            backingGo.transform.SetParent(cardRt, false);
            backingGo.transform.SetAsFirstSibling(); // Behind all slot elements

            LayoutElement le = backingGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            RectTransform rt = backingGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image img = backingGo.GetComponent<Image>();
            img.color = new Color(0.04f, 0.08f, 0.15f, 0.88f);
            img.raycastTarget = false;

            // Border Line
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(backingGo.transform, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(-4f, -4f);
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.color = new Color(0.12f, 0.24f, 0.40f, 0.65f);
            borderImg.raycastTarget = false;

            // Inner recess
            GameObject recessGo = new GameObject("Recess", typeof(RectTransform), typeof(Image));
            recessGo.transform.SetParent(borderGo.transform, false);
            RectTransform recessRt = recessGo.GetComponent<RectTransform>();
            recessRt.anchorMin = Vector2.zero;
            recessRt.anchorMax = Vector2.one;
            recessRt.sizeDelta = new Vector2(-4f, -4f);
            Image recessImg = recessGo.GetComponent<Image>();
            recessImg.color = new Color(0.03f, 0.06f, 0.11f, 0.94f);
            recessImg.raycastTarget = false;
        }

        private void EnsureStationPlatform(RectTransform cardRt, int slotIndex, Color accent)
        {
            Transform existing = cardRt.Find("StationPlatform");
            GameObject pGo;
            RectTransform pRt;
            Image glow;

            if (existing == null)
            {
                pGo = new GameObject("StationPlatform", typeof(RectTransform));
                pGo.transform.SetParent(cardRt, false);

                // Insert immediately following Portrait (sibling index 1)
                Transform portrait = cardRt.Find("Portrait");
                if (portrait != null)
                {
                    pGo.transform.SetSiblingIndex(portrait.GetSiblingIndex() + 1);
                }

                pRt = pGo.GetComponent<RectTransform>();
                pRt.sizeDelta = new Vector2(260f, 22f);

                LayoutElement le = pGo.AddComponent<LayoutElement>();
                le.preferredWidth = 260f;
                le.preferredHeight = 22f;

                // Base Plate
                GameObject basePlate = new GameObject("BasePlate", typeof(RectTransform), typeof(Image));
                basePlate.transform.SetParent(pGo.transform, false);
                RectTransform baseRt = basePlate.GetComponent<RectTransform>();
                baseRt.anchorMin = new Vector2(0f, 0f);
                baseRt.anchorMax = new Vector2(1f, 1f);
                baseRt.sizeDelta = Vector2.zero;
                Image baseImg = basePlate.GetComponent<Image>();
                baseImg.color = new Color(0.06f, 0.10f, 0.18f, 0.98f);
                baseImg.raycastTarget = false;

                // Neon Top Strip
                GameObject neon = new GameObject("NeonStrip", typeof(RectTransform), typeof(Image));
                neon.transform.SetParent(pGo.transform, false);
                RectTransform neonRt = neon.GetComponent<RectTransform>();
                neonRt.anchorMin = new Vector2(0f, 1f);
                neonRt.anchorMax = new Vector2(1f, 1f);
                neonRt.pivot = new Vector2(0.5f, 1f);
                neonRt.sizeDelta = new Vector2(-8f, 3f);
                neonRt.anchoredPosition = new Vector2(0f, 0f);
                Image neonImg = neon.GetComponent<Image>();
                neonImg.color = accent;
                neonImg.raycastTarget = false;

                // Floor Glow
                GameObject glowObj = new GameObject("Glow", typeof(RectTransform), typeof(Image));
                glowObj.transform.SetParent(pGo.transform, false);
                RectTransform glowRt = glowObj.GetComponent<RectTransform>();
                glowRt.anchorMin = new Vector2(0f, 0f);
                glowRt.anchorMax = new Vector2(1f, 0f);
                glowRt.pivot = new Vector2(0.5f, 0f);
                glowRt.sizeDelta = new Vector2(-16f, 12f);
                glowRt.anchoredPosition = new Vector2(0f, -8f);
                glow = glowObj.GetComponent<Image>();
                glow.color = new Color(accent.r, accent.g, accent.b, 0.25f);
                glow.raycastTarget = false;
            }
            else
            {
                pGo = existing.gameObject;
                pRt = pGo.GetComponent<RectTransform>();
                glow = pGo.transform.Find("Glow")?.GetComponent<Image>();
            }

            m_Platforms[slotIndex] = pRt;
            m_PlatformGlows[slotIndex] = glow;

            CollectionCardSelectable selectable = cardRt.GetComponent<CollectionCardSelectable>();
            if (selectable != null)
            {
                selectable.PlatformRect = pRt;
                selectable.PlatformGlow = glow;
            }
        }

        private void EnsureStationPips(RectTransform cardRt, CollectionCardSelectable selectable, Color accent)
        {
            Transform existing = cardRt.Find("StationPips");
            GameObject pipsGo;
            RectTransform pipsRt;

            if (existing == null)
            {
                pipsGo = new GameObject("StationPips", typeof(RectTransform));
                pipsGo.transform.SetParent(cardRt, false);

                // Position right after Count
                Transform countTrans = cardRt.Find("Count");
                if (countTrans != null)
                {
                    pipsGo.transform.SetSiblingIndex(countTrans.GetSiblingIndex() + 1);
                }

                pipsRt = pipsGo.GetComponent<RectTransform>();
                pipsRt.sizeDelta = new Vector2(130f, 10f);

                LayoutElement le = pipsGo.AddComponent<LayoutElement>();
                le.preferredWidth = 130f;
                le.preferredHeight = 10f;

                HorizontalLayoutGroup hlg = pipsGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 5f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                Image[] pips = new Image[5];
                for (int p = 0; p < 5; p++)
                {
                    GameObject pip = new GameObject($"Pip_{p + 1}", typeof(RectTransform), typeof(Image));
                    pip.transform.SetParent(pipsGo.transform, false);
                    RectTransform pipRt = pip.GetComponent<RectTransform>();
                    pipRt.sizeDelta = new Vector2(20f, 6f);

                    Image img = pip.GetComponent<Image>();
                    img.color = new Color(0.12f, 0.18f, 0.28f, 0.65f);
                    img.raycastTarget = false;
                    pips[p] = img;
                }

                selectable.StationPips = pips;
            }
            else
            {
                selectable.StationPips = existing.GetComponentsInChildren<Image>(true);
            }
        }

        private void BuildGridLines(RectTransform parent)
        {
            Color gridColor = new Color(0.22f, 0.45f, 0.75f, 0.16f);

            // Horizontal hairline grid lines
            float[] yOffsets = new float[] { -220f, -110f, 0f, 110f, 220f, 330f };
            foreach (float y in yOffsets)
            {
                GameObject line = new GameObject("GridH", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(parent, false);
                RectTransform rt = line.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(0f, 1.5f);
                rt.anchoredPosition = new Vector2(0f, y);
                Image img = line.GetComponent<Image>();
                img.color = gridColor;
                img.raycastTarget = false;
            }

            // Vertical hairline grid lines
            float[] xOffsets = new float[] { -600f, -400f, -200f, 0f, 200f, 400f, 600f };
            foreach (float x in xOffsets)
            {
                GameObject line = new GameObject("GridV", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(parent, false);
                RectTransform rt = line.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(1.5f, 0f);
                rt.anchoredPosition = new Vector2(x, 0f);
                Image img = line.GetComponent<Image>();
                img.color = gridColor;
                img.raycastTarget = false;
            }
        }

        private RectTransform BuildSelectionFrame(Transform parent)
        {
            GameObject frameObj = new GameObject("HangarSelectionFrame", typeof(RectTransform));
            frameObj.transform.SetParent(parent, false);

            LayoutElement le = frameObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            RectTransform rt = frameObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(268f, 268f);
            rt.anchoredPosition = Vector2.zero;

            // 4 Sharp L-Bracket Corner Accents
            Color cornerColor = new Color(0.35f, 0.88f, 1f, 0.95f);
            CreateCornerBracket(frameObj.transform, "TL", new Vector2(0f, 1f), new Vector2(6f, -6f), cornerColor);
            CreateCornerBracket(frameObj.transform, "TR", new Vector2(1f, 1f), new Vector2(-6f, -6f), cornerColor);
            CreateCornerBracket(frameObj.transform, "BL", new Vector2(0f, 0f), new Vector2(6f, 6f), cornerColor);
            CreateCornerBracket(frameObj.transform, "BR", new Vector2(1f, 0f), new Vector2(-6f, 6f), cornerColor);

            frameObj.SetActive(false);
            return rt;
        }

        private void CreateCornerBracket(Transform parent, string name, Vector2 anchor, Vector2 offset, Color color)
        {
            GameObject cGo = new GameObject(name, typeof(RectTransform));
            cGo.transform.SetParent(parent, false);
            RectTransform rt = cGo.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(26f, 26f);
            rt.anchoredPosition = offset;

            // Horizontal arm
            GameObject hLine = new GameObject("H", typeof(RectTransform), typeof(Image));
            hLine.transform.SetParent(cGo.transform, false);
            RectTransform hRt = hLine.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(anchor.x == 0f ? 0f : 1f, anchor.y == 0f ? 0f : 1f);
            hRt.anchorMax = hRt.anchorMin;
            hRt.pivot = hRt.anchorMin;
            hRt.sizeDelta = new Vector2(24f, 3.5f);
            hRt.anchoredPosition = new Vector2(anchor.x == 0f ? 0f : -24f, anchor.y == 0f ? 0f : -3.5f);
            Image hImg = hLine.GetComponent<Image>();
            hImg.color = color;
            hImg.raycastTarget = false;

            // Vertical arm
            GameObject vLine = new GameObject("V", typeof(RectTransform), typeof(Image));
            vLine.transform.SetParent(cGo.transform, false);
            RectTransform vRt = vLine.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(anchor.x == 0f ? 0f : 1f, anchor.y == 0f ? 0f : 1f);
            vRt.anchorMax = vRt.anchorMin;
            vRt.pivot = vRt.anchorMin;
            vRt.sizeDelta = new Vector2(3.5f, 24f);
            vRt.anchoredPosition = new Vector2(anchor.x == 0f ? 0f : -3.5f, anchor.y == 0f ? 0f : -24f);
            Image vImg = vLine.GetComponent<Image>();
            vImg.color = color;
            vImg.raycastTarget = false;
        }

        private RectTransform BuildInspectionBeam(Transform parent, out Image beamImage)
        {
            GameObject beamObj = new GameObject("HangarInspectionBeam", typeof(RectTransform), typeof(Image));
            beamObj.transform.SetParent(parent, false);

            LayoutElement le = beamObj.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            RectTransform rt = beamObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 8f);
            rt.anchoredPosition = Vector2.zero;

            beamImage = beamObj.GetComponent<Image>();
            beamImage.color = new Color(0.40f, 0.88f, 1f, 0.65f);
            beamImage.raycastTarget = false;

            beamObj.SetActive(false);
            return rt;
        }

        private RectTransform BuildDiagnosticsPanel(Transform parent)
        {
            GameObject diagObj = new GameObject("DiagnosticsDetailsPanel", typeof(RectTransform), typeof(Image));
            diagObj.transform.SetParent(parent, false);

            RectTransform rt = diagObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1200f, 106f);
            rt.anchoredPosition = new Vector2(0f, -235f);

            Image bgImg = diagObj.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.08f, 0.16f, 0.88f);
            bgImg.raycastTarget = false;

            // Hairline top border line
            GameObject topLine = new GameObject("TopLine", typeof(RectTransform), typeof(Image));
            topLine.transform.SetParent(diagObj.transform, false);
            m_DetailsTopLine = topLine.GetComponent<RectTransform>();
            m_DetailsTopLine.anchorMin = new Vector2(0f, 1f);
            m_DetailsTopLine.anchorMax = new Vector2(1f, 1f);
            m_DetailsTopLine.sizeDelta = new Vector2(0f, 2f);
            m_DetailsTopLine.anchoredPosition = Vector2.zero;
            topLine.GetComponent<Image>().color = new Color(0.35f, 0.75f, 1f, 0.65f);
            topLine.GetComponent<Image>().raycastTarget = false;

            // Hairline bottom border line
            GameObject botLine = new GameObject("BottomLine", typeof(RectTransform), typeof(Image));
            botLine.transform.SetParent(diagObj.transform, false);
            m_DetailsBottomLine = botLine.GetComponent<RectTransform>();
            m_DetailsBottomLine.anchorMin = new Vector2(0f, 0f);
            m_DetailsBottomLine.anchorMax = new Vector2(1f, 0f);
            m_DetailsBottomLine.sizeDelta = new Vector2(0f, 2f);
            m_DetailsBottomLine.anchoredPosition = Vector2.zero;
            botLine.GetComponent<Image>().color = new Color(0.35f, 0.75f, 1f, 0.65f);
            botLine.GetComponent<Image>().raycastTarget = false;

            // Title (Left)
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(diagObj.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0.6f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 30f);
            titleRt.anchoredPosition = new Vector2(24f, -10f);
            m_DetailsTitleText = titleGo.GetComponent<TextMeshProUGUI>();
            m_DetailsTitleText.fontSize = 22f;
            m_DetailsTitleText.fontStyle = FontStyles.Bold;
            m_DetailsTitleText.color = Color.white;
            m_DetailsTitleText.alignment = TextAlignmentOptions.TopLeft;

            // Status (Left-Middle)
            GameObject statusGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(diagObj.transform, false);
            RectTransform statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0.5f);
            statusRt.anchorMax = new Vector2(0.6f, 0.5f);
            statusRt.pivot = new Vector2(0f, 0.5f);
            statusRt.sizeDelta = new Vector2(0f, 22f);
            statusRt.anchoredPosition = new Vector2(24f, -2f);
            m_DetailsStatusText = statusGo.GetComponent<TextMeshProUGUI>();
            m_DetailsStatusText.fontSize = 16f;
            m_DetailsStatusText.color = new Color(0.35f, 1f, 0.55f);
            m_DetailsStatusText.alignment = TextAlignmentOptions.MidlineLeft;

            // Description (Bottom)
            GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(diagObj.transform, false);
            RectTransform descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 0f);
            descRt.pivot = new Vector2(0f, 0f);
            descRt.sizeDelta = new Vector2(-48f, 24f);
            descRt.anchoredPosition = new Vector2(24f, 10f);
            m_DetailsDescText = descGo.GetComponent<TextMeshProUGUI>();
            m_DetailsDescText.fontSize = 13f;
            m_DetailsDescText.color = new Color(0.75f, 0.85f, 0.95f, 0.85f);
            m_DetailsDescText.alignment = TextAlignmentOptions.BottomLeft;

            // Progress Text (Right)
            GameObject progGo = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
            progGo.transform.SetParent(diagObj.transform, false);
            RectTransform progRt = progGo.GetComponent<RectTransform>();
            progRt.anchorMin = new Vector2(0.65f, 1f);
            progRt.anchorMax = new Vector2(1f, 1f);
            progRt.pivot = new Vector2(1f, 1f);
            progRt.sizeDelta = new Vector2(0f, 28f);
            progRt.anchoredPosition = new Vector2(-24f, -10f);
            m_DetailsProgressText = progGo.GetComponent<TextMeshProUGUI>();
            m_DetailsProgressText.fontSize = 18f;
            m_DetailsProgressText.fontStyle = FontStyles.Bold;
            m_DetailsProgressText.color = new Color(0.95f, 0.96f, 1f);
            m_DetailsProgressText.alignment = TextAlignmentOptions.TopRight;

            // Progress Pips (Right-Middle: 5 discrete blocks)
            m_DetailsProgressPips = new Image[5];
            GameObject pipsContainer = new GameObject("Pips", typeof(RectTransform));
            pipsContainer.transform.SetParent(diagObj.transform, false);
            RectTransform pipsRt = pipsContainer.GetComponent<RectTransform>();
            pipsRt.anchorMin = new Vector2(1f, 0.5f);
            pipsRt.anchorMax = new Vector2(1f, 0.5f);
            pipsRt.pivot = new Vector2(1f, 0.5f);
            pipsRt.sizeDelta = new Vector2(140f, 16f);
            pipsRt.anchoredPosition = new Vector2(-24f, -2f);

            for (int p = 0; p < 5; p++)
            {
                GameObject pip = new GameObject($"Pip_{p + 1}", typeof(RectTransform), typeof(Image));
                pip.transform.SetParent(pipsContainer.transform, false);
                RectTransform pipRt = pip.GetComponent<RectTransform>();
                pipRt.anchorMin = new Vector2(0f, 0.5f);
                pipRt.anchorMax = new Vector2(0f, 0.5f);
                pipRt.sizeDelta = new Vector2(22f, 13f);
                pipRt.anchoredPosition = new Vector2(p * 28f + 11f, 0f);
                m_DetailsProgressPips[p] = pip.GetComponent<Image>();
                m_DetailsProgressPips[p].color = new Color(0.35f, 0.78f, 1f);
                m_DetailsProgressPips[p].raycastTarget = false;
            }

            return rt;
        }

        private void BindExistingDetailsPanel(RectTransform panel)
        {
            m_DetailsTopLine = panel.Find("TopLine") as RectTransform;
            m_DetailsBottomLine = panel.Find("BottomLine") as RectTransform;
            m_DetailsTitleText = panel.Find("Title")?.GetComponent<TMP_Text>();
            m_DetailsStatusText = panel.Find("Status")?.GetComponent<TMP_Text>();
            m_DetailsDescText = panel.Find("Desc")?.GetComponent<TMP_Text>();
            m_DetailsProgressText = panel.Find("ProgressText")?.GetComponent<TMP_Text>();

            Transform pipsTrans = panel.Find("Pips");
            if (pipsTrans != null)
            {
                m_DetailsProgressPips = pipsTrans.GetComponentsInChildren<Image>(true);
            }
        }

        #endregion

        #region Pre-Entrance Preparation

        /// <summary>
        /// Synchronously resets every visual element to its initial hidden state at frame 0.
        /// </summary>
        public void PrepareEntranceState()
        {
            CaptureRestState();
            EnsureHangarElements();

            // 1. Panel Background
            if (m_PanelBackground != null)
            {
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                if (cg == null) cg = m_PanelBackground.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                m_PanelBackground.localScale = m_PanelRestScale * m_PanelStartScale;
            }

            // 2. Technical Grid
            if (m_TechnicalGrid != null)
            {
                CanvasGroup gridCg = m_TechnicalGrid.GetComponent<CanvasGroup>();
                if (gridCg == null) gridCg = m_TechnicalGrid.gameObject.AddComponent<CanvasGroup>();
                gridCg.alpha = 0f;
            }

            // 3. Platforms
            if (m_Platforms != null)
            {
                for (int i = 0; i < m_Platforms.Length; i++)
                {
                    if (m_Platforms[i] != null)
                    {
                        m_Platforms[i].localScale = new Vector3(0.85f, 0f, 1f);
                    }
                    if (m_PlatformGlows != null && i < m_PlatformGlows.Length && m_PlatformGlows[i] != null)
                    {
                        Color c = m_PlatformGlows[i].color;
                        m_PlatformGlows[i].color = new Color(c.r, c.g, c.b, 0f);
                    }
                }
            }

            // 4. Robot Cards (lowered and scaled down)
            if (m_CardRects != null)
            {
                for (int i = 0; i < m_CardRects.Length; i++)
                {
                    if (m_CardRects[i] != null)
                    {
                        m_CardRects[i].anchoredPosition = m_CardRestPositions[i] + new Vector2(0f, -25f);
                        m_CardRects[i].localScale = Vector3.zero;
                    }
                    if (m_CardSelectables != null && i < m_CardSelectables.Length && m_CardSelectables[i] != null)
                    {
                        m_CardSelectables[i].UpdateUnlockState();
                        m_CardSelectables[i].SetVisualState(false, 0f);
                    }
                }
            }

            // 5. Selection Frame & Inspection Beam
            if (m_SelectionFrame != null)
            {
                m_SelectionFrame.localScale = Vector3.zero;
                m_SelectionFrame.gameObject.SetActive(false);
            }
            if (m_InspectionBeam != null)
            {
                m_InspectionBeam.gameObject.SetActive(false);
            }

            // 6. Diagnostics Details Panel
            if (m_DetailsPanel != null)
            {
                CanvasGroup dCg = m_DetailsPanel.GetComponent<CanvasGroup>();
                if (dCg == null) dCg = m_DetailsPanel.gameObject.AddComponent<CanvasGroup>();
                dCg.alpha = 0f;

                if (m_DetailsTopLine != null) m_DetailsTopLine.localScale = new Vector3(0f, 1f, 1f);
                if (m_DetailsBottomLine != null) m_DetailsBottomLine.localScale = new Vector3(0f, 1f, 1f);
            }

            // 7. Header Sign & Back Button
            if (m_CollectionSign != null)
            {
                m_CollectionSign.anchoredPosition = new Vector2(m_SignRestPos.x + 250f, m_SignRestPos.y + 180f);
                m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, 7f);
            }

            if (m_TotalCounter != null)
            {
                CanvasGroup totalCg = m_TotalCounter.GetComponent<CanvasGroup>();
                if (totalCg == null) totalCg = m_TotalCounter.gameObject.AddComponent<CanvasGroup>();
                totalCg.alpha = 0f;
                m_TotalCounter.localScale = new Vector3(0.9f, 0.9f, 1f);
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 140f);
            }
        }

        #endregion

        #region Public Animation API

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            EnsureHangarElements();
            m_ActiveRoutine = StartCoroutine(HangarEntranceRoutine(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(HangarExitRoutine(onComplete));
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null) { StopCoroutine(m_ActiveRoutine); m_ActiveRoutine = null; }
            if (m_SignBreezeRoutine != null) { StopCoroutine(m_SignBreezeRoutine); m_SignBreezeRoutine = null; }
            if (m_BeamRoutine != null) { StopCoroutine(m_BeamRoutine); m_BeamRoutine = null; }
            if (m_FrameMoveRoutine != null) { StopCoroutine(m_FrameMoveRoutine); m_FrameMoveRoutine = null; }
            if (m_CardLiftRoutine != null) { StopCoroutine(m_CardLiftRoutine); m_CardLiftRoutine = null; }
        }

        #endregion

        #region Entrance Choreography

        private IEnumerator HangarEntranceRoutine(Action onComplete)
        {
            // 1. Prepare clean initial state synchronously
            PrepareEntranceState();

            // 2. Open main Collection panel with mechanical terminal deployment
            if (m_PanelBackground != null)
            {
                StartCoroutine(PanelMechanicalDeploymentRoutine(m_PanelDuration));
            }

            // 3. Horizontal scanline wipe reveals technical blueprint grid
            StartCoroutine(TechnicalGridScanlineRevealRoutine(0.22f));

            // 4. Heavy mechanical sign drop
            if (m_CollectionSign != null) StartCoroutine(SignEntranceRoutine());

            // 5. Asymmetric deployment of the robot platforms
            StartCoroutine(DeployPlatformsRoutine());

            // 6. Sequential robot chassis boot-up (Echo -> Nova -> Patch -> Pixel)
            yield return StartCoroutine(SequentialRobotBootRoutine());

            // 7. Stronger activation sequence on default selected robot (Echo)
            CollectionCardSelectable defaultCard = (m_CardSelectables != null && m_CardSelectables.Length > 0)
                ? m_CardSelectables[0]
                : null;

            if (defaultCard != null)
            {
                yield return StartCoroutine(ActivateFocusedRobotSequence(defaultCard));
            }

            // 8. Mechanical construction of the diagnostics & specs panel
            yield return StartCoroutine(ConstructDiagnosticsPanelRoutine());

            // 9. Total counter fades in
            if (m_TotalCounter != null) StartCoroutine(CounterEntranceRoutine());

            // 10. Back button slides up from bottom
            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 140f);
                StartCoroutine(AnimateMotion(m_BackButton, startBack, m_BackRestPos, 0f, 0f, 0.28f, 0.06f, EasingType.EaseOutBack, 1.15f));
            }

            // Small settle pause before enabling controller navigation
            float timer = 0f;
            while (timer < 0.10f)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator PanelMechanicalDeploymentRoutine(float duration)
        {
            if (m_PanelBackground == null) yield break;

            CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
            Vector3 startScale = m_PanelRestScale * m_PanelStartScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, t * 2.5f);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.12f);
                m_PanelBackground.localScale = Vector3.LerpUnclamped(startScale, m_PanelRestScale, ease);

                yield return null;
            }

            m_PanelBackground.localScale = m_PanelRestScale;
            if (cg != null) cg.alpha = 1f;

            UIMicroShake.Shake(0.35f, 0.04f);
        }

        private IEnumerator TechnicalGridScanlineRevealRoutine(float duration)
        {
            if (m_TechnicalGrid == null) yield break;

            CanvasGroup cg = m_TechnicalGrid.GetComponent<CanvasGroup>();
            if (cg == null) cg = m_TechnicalGrid.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = Mathf.Lerp(0f, 1f, UIEasing.Evaluate(EasingType.EaseOutQuad, t));
                yield return null;
            }

            cg.alpha = 1f;
        }

        private IEnumerator DeployPlatformsRoutine()
        {
            if (m_Platforms == null) yield break;

            float[] delays = new float[] { 0.04f, 0.08f, 0.12f, 0.16f };
            float duration = 0.28f;

            for (int i = 0; i < m_Platforms.Length; i++)
            {
                if (m_Platforms[i] == null) continue;
                StartCoroutine(SinglePlatformDeploy(m_Platforms[i], delays[i], duration, i));
            }

            yield return null;
        }

        private IEnumerator SinglePlatformDeploy(RectTransform platform, float delay, float duration, int idx)
        {
            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay)
                {
                    d += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.20f);

                if (platform != null)
                {
                    platform.localScale = new Vector3(
                        Mathf.Lerp(0.85f, 1f, ease),
                        Mathf.Lerp(0f, 1f, ease),
                        1f);
                }
                yield return null;
            }

            if (platform != null)
            {
                platform.localScale = Vector3.one;
            }

            // Idle platform glow
            if (m_PlatformGlows != null && idx < m_PlatformGlows.Length && m_PlatformGlows[idx] != null)
            {
                Color c = m_PlatformGlows[idx].color;
                m_PlatformGlows[idx].color = new Color(c.r, c.g, c.b, 0.20f);
            }
        }

        private IEnumerator SequentialRobotBootRoutine()
        {
            if (m_CardRects == null) yield break;

            float stagger = 0.08f;

            for (int i = 0; i < m_CardRects.Length; i++)
            {
                if (m_CardRects[i] == null) continue;

                RectTransform cardRt = m_CardRects[i];
                Vector2 restPos = m_CardRestPositions[i];
                CollectionCardSelectable selectable = m_CardSelectables != null && i < m_CardSelectables.Length
                    ? m_CardSelectables[i]
                    : null;

                StartCoroutine(SingleRobotBootRoutine(cardRt, restPos, selectable));

                float wait = 0f;
                while (wait < stagger)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Wait for last robot to finish boot
            float settleWait = 0.24f;
            float timer = 0f;
            while (timer < settleWait)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator SingleRobotBootRoutine(RectTransform card, Vector2 restPos, CollectionCardSelectable selectable)
        {
            Vector2 startPos = restPos + new Vector2(0f, -25f);
            card.anchoredPosition = startPos;
            card.localScale = new Vector3(0.90f, 0.90f, 1f);

            float duration = 0.24f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f);

                card.anchoredPosition = Vector2.LerpUnclamped(startPos, restPos, ease);
                card.localScale = Vector3.LerpUnclamped(new Vector3(0.90f, 0.90f, 1f), Vector3.one, ease);

                if (selectable != null)
                {
                    selectable.SetVisualState(false, Mathf.Lerp(0f, 1f, t * 2f));
                }

                yield return null;
            }

            card.anchoredPosition = restPos;
            card.localScale = Vector3.one;

            if (selectable != null)
            {
                selectable.SetVisualState(false, 1f);
            }
        }

        private IEnumerator ActivateFocusedRobotSequence(CollectionCardSelectable card)
        {
            m_CurrentlySelectedCard = card;
            RectTransform cardRt = card.CardVisual;
            Transform portraitTrans = cardRt.Find("Portrait");
            RectTransform targetPortrait = portraitTrans != null ? portraitTrans as RectTransform : cardRt;

            // 1. Platform light ON
            if (card.PlatformGlow != null)
            {
                Color c = card.PlatformGlow.color;
                StartCoroutine(AnimateImageAlpha(card.PlatformGlow, c.a, 0.85f, 0.15f));
            }

            // 2. Selection frame activates directly over robot portrait
            if (m_SelectionFrame != null)
            {
                m_SelectionFrame.gameObject.SetActive(true);
                m_SelectionFrame.SetParent(targetPortrait, false);
                LayoutElement le = m_SelectionFrame.GetComponent<LayoutElement>();
                if (le == null) le = m_SelectionFrame.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                m_SelectionFrame.sizeDelta = new Vector2(268f, 268f);
                m_SelectionFrame.anchoredPosition = Vector2.zero;

                StartCoroutine(AnimateScale(m_SelectionFrame, Vector3.zero, Vector3.one, 0.16f, EasingType.EaseOutBack, 1.2f));
            }

            // 3. Holographic inspection beam sweeps over robot portrait
            if (m_InspectionBeam != null && card.IsUnlocked)
            {
                m_InspectionBeam.gameObject.SetActive(true);
                m_InspectionBeam.SetParent(targetPortrait, false);
                LayoutElement ble = m_InspectionBeam.GetComponent<LayoutElement>();
                if (ble == null) ble = m_InspectionBeam.gameObject.AddComponent<LayoutElement>();
                ble.ignoreLayout = true;

                m_InspectionBeam.sizeDelta = new Vector2(260f, 8f);
                float startY = 130f;
                float endY = -130f;
                m_InspectionBeam.anchoredPosition = new Vector2(0f, startY);

                float beamDur = 0.22f;
                float elapsed = 0f;
                while (elapsed < beamDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / beamDur);
                    m_InspectionBeam.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t));
                    yield return null;
                }

                m_InspectionBeam.gameObject.SetActive(false);
            }

            // 4. Robot highlights & micro-lifts (+5px)
            card.SetVisualState(true, 1f);

            Vector2 basePos = card.RestPosition;
            Vector2 liftPos = basePos + new Vector2(0f, 5f);
            float liftDur = 0.15f;
            float lElapsed = 0f;
            while (lElapsed < liftDur)
            {
                lElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(lElapsed / liftDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.25f);
                cardRt.anchoredPosition = Vector2.LerpUnclamped(basePos, liftPos, ease);
                yield return null;
            }
            cardRt.anchoredPosition = liftPos;
        }

        private IEnumerator ConstructDiagnosticsPanelRoutine()
        {
            if (m_DetailsPanel == null) yield break;

            CanvasGroup cg = m_DetailsPanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            // 1. Panel border lines draw outward (0% -> 100%)
            float lineDur = 0.22f;
            float elapsed = 0f;
            while (elapsed < lineDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / lineDur);

                if (m_DetailsTopLine != null) m_DetailsTopLine.localScale = new Vector3(progress, 1f, 1f);
                if (m_DetailsBottomLine != null) m_DetailsBottomLine.localScale = new Vector3(progress, 1f, 1f);

                yield return null;
            }

            if (m_DetailsTopLine != null) m_DetailsTopLine.localScale = Vector3.one;
            if (m_DetailsBottomLine != null) m_DetailsBottomLine.localScale = Vector3.one;

            // 2. Populate and reveal details for current card
            UpdateDiagnosticsContent(m_CurrentlySelectedCard != null ? m_CurrentlySelectedCard : m_CardSelectables[0]);
        }

        private IEnumerator SignEntranceRoutine()
        {
            Vector2 startSign = new Vector2(m_SignRestPos.x + 250f, m_SignRestPos.y + 180f);
            m_CollectionSign.anchoredPosition = startSign;
            m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, 7f);

            float dropDuration = 0.26f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_CollectionSign != null)
                {
                    m_CollectionSign.anchoredPosition = Vector2.Lerp(startSign, m_SignRestPos, ease);
                }
                yield return null;
            }

            if (m_CollectionSign != null) m_CollectionSign.anchoredPosition = m_SignRestPos;

            UIMicroShake.Shake(0.35f, 0.04f);

            // Physical damped pendulum decay
            elapsed = 0f;
            float swingDuration = 0.45f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 5.0f);
                float angle = -decay * Mathf.Sin(elapsed * 16f) * 7.5f;

                if (m_CollectionSign != null)
                {
                    m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_CollectionSign != null) m_CollectionSign.localEulerAngles = m_SignRestAngles;

            m_SignBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine());
        }

        private IEnumerator SignAmbientSwayRoutine()
        {
            while (m_CollectionSign != null)
            {
                float angle = Mathf.Sin(Time.unscaledTime * 1.6f) * 0.60f;
                m_CollectionSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                yield return null;
            }
        }

        private IEnumerator CounterEntranceRoutine()
        {
            if (m_TotalCounter == null) yield break;
            CanvasGroup totalCg = m_TotalCounter.GetComponent<CanvasGroup>();
            if (totalCg == null) totalCg = m_TotalCounter.gameObject.AddComponent<CanvasGroup>();

            float duration = 0.26f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.15f);

                totalCg.alpha = Mathf.Lerp(0f, 1f, t * 2f);
                m_TotalCounter.localScale = Vector3.LerpUnclamped(new Vector3(0.9f, 0.9f, 1f), Vector3.one, ease);
                yield return null;
            }

            totalCg.alpha = 1f;
            m_TotalCounter.localScale = Vector3.one;
        }

        #endregion

        #region Controller Selection Transitions

        /// <summary>
        /// Called when the player moves controller focus to a new robot card.
        /// Executes a rapid 0.18s transition: transfers platform light, sweeps inspection beam,
        /// micro-lifts the new card, moves the selection frame, and updates the diagnostics panel.
        /// </summary>
        public void HandleCardFocusChanged(CollectionCardSelectable newCard)
        {
            if (newCard == null || newCard == m_CurrentlySelectedCard) return;

            CollectionCardSelectable oldCard = m_CurrentlySelectedCard;
            m_CurrentlySelectedCard = newCard;

            // 1. Previous Robot: Dim platform & lower card
            if (oldCard != null)
            {
                oldCard.SetVisualState(false, 1f);
                if (oldCard.PlatformGlow != null)
                {
                    Color c = oldCard.PlatformGlow.color;
                    StartCoroutine(AnimateImageAlpha(oldCard.PlatformGlow, c.a, 0.20f, 0.14f));
                }

                RectTransform oldRt = oldCard.CardVisual;
                if (oldRt != null)
                {
                    StartCoroutine(AnimateMotion(oldRt, oldRt.anchoredPosition, oldCard.RestPosition, 0f, 0f, 0.14f, 0f, EasingType.EaseOutQuad, 1f));
                }
            }

            // 2. New Robot: Power platform light, micro-lift
            newCard.SetVisualState(true, 1f);

            if (newCard.PlatformGlow != null)
            {
                Color c = newCard.PlatformGlow.color;
                StartCoroutine(AnimateImageAlpha(newCard.PlatformGlow, c.a, newCard.IsUnlocked ? 0.85f : 0.15f, 0.14f));
            }

            RectTransform newRt = newCard.CardVisual;
            if (newRt != null)
            {
                Vector2 targetLiftPos = newCard.RestPosition + new Vector2(0f, 5f);
                StartCoroutine(AnimateMotion(newRt, newRt.anchoredPosition, targetLiftPos, 0f, 0f, 0.15f, 0f, EasingType.EaseOutBack, 1.20f));
            }

            // 3. Move Selection Frame to New Slot Portrait
            Transform portraitTrans = newRt.Find("Portrait");
            RectTransform targetPortrait = portraitTrans != null ? portraitTrans as RectTransform : newRt;

            if (m_SelectionFrame != null)
            {
                m_SelectionFrame.gameObject.SetActive(true);
                m_SelectionFrame.SetParent(targetPortrait, false);
                LayoutElement le = m_SelectionFrame.GetComponent<LayoutElement>();
                if (le == null) le = m_SelectionFrame.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                m_SelectionFrame.sizeDelta = new Vector2(268f, 268f);
                m_SelectionFrame.anchoredPosition = Vector2.zero;

                if (m_FrameMoveRoutine != null) StopCoroutine(m_FrameMoveRoutine);
                m_FrameMoveRoutine = StartCoroutine(AnimateScale(m_SelectionFrame, new Vector3(1.15f, 1.15f, 1f), Vector3.one, 0.14f, EasingType.EaseOutBack, 1.25f));
            }

            // 4. Holographic Inspection Beam Sweep (Unlocked) or Lock Pulse (Locked)
            if (newCard.IsUnlocked)
            {
                if (m_InspectionBeam != null)
                {
                    if (m_BeamRoutine != null) StopCoroutine(m_BeamRoutine);
                    m_BeamRoutine = StartCoroutine(InspectionBeamSweepRoutine(targetPortrait));
                }
            }
            else
            {
                newCard.TriggerLockPulse();
            }

            // 5. Update Diagnostics Panel
            UpdateDiagnosticsContent(newCard);
        }

        private IEnumerator InspectionBeamSweepRoutine(RectTransform targetPortrait)
        {
            if (m_InspectionBeam == null || targetPortrait == null) yield break;

            m_InspectionBeam.gameObject.SetActive(true);
            m_InspectionBeam.SetParent(targetPortrait, false);
            LayoutElement ble = m_InspectionBeam.GetComponent<LayoutElement>();
            if (ble == null) ble = m_InspectionBeam.gameObject.AddComponent<LayoutElement>();
            ble.ignoreLayout = true;

            m_InspectionBeam.sizeDelta = new Vector2(260f, 8f);
            float startY = 130f;
            float endY = -130f;
            m_InspectionBeam.anchoredPosition = new Vector2(0f, startY);

            float duration = 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                m_InspectionBeam.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t));
                yield return null;
            }

            m_InspectionBeam.gameObject.SetActive(false);
            m_BeamRoutine = null;
        }

        private void UpdateDiagnosticsContent(CollectionCardSelectable card)
        {
            if (card == null) return;

            RobotId robot = card.Robot;
            int idx = (int)robot;
            if (idx < 0 || idx >= RobotAuthoring.Length) idx = 0;

            var spec = RobotAuthoring[idx];
            int count = RobotCollectionService.CollectedCount(robot);
            int total = RobotIds.PartsPerRobot;
            bool isUnlocked = card.IsUnlocked;

            // Title
            if (m_DetailsTitleText != null)
            {
                m_DetailsTitleText.text = isUnlocked
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(spec.accent)}>{spec.title}</color>"
                    : $"<color=#888888>{spec.title}</color>";
            }

            // Status
            if (m_DetailsStatusText != null)
            {
                m_DetailsStatusText.text = isUnlocked
                    ? "<color=#55FF77>STATUS: CHASSIS OPERATIONAL // CALIBRATED</color>"
                    : "<color=#FF4433>STATUS: CHASSIS OFFLINE // ACCESS RESTRICTED</color>";
            }

            // Description
            if (m_DetailsDescText != null)
            {
                m_DetailsDescText.text = spec.desc;
            }

            // Progress Text
            if (m_DetailsProgressText != null)
            {
                int pct = Mathf.RoundToInt((count / (float)total) * 100f);
                m_DetailsProgressText.text = isUnlocked
                    ? $"PARTS ASSEMBLED: {count} / {total} ({pct}%)"
                    : $"RESTRICTED // REQUIRES PARTS IN SECTOR {idx + 1}";
            }

            // Progress Pips
            if (m_DetailsProgressPips != null)
            {
                for (int p = 0; p < m_DetailsProgressPips.Length; p++)
                {
                    if (m_DetailsProgressPips[p] == null) continue;
                    bool filled = p < count;
                    m_DetailsProgressPips[p].color = filled
                        ? spec.accent
                        : new Color(0.2f, 0.25f, 0.35f, 0.5f);
                }
            }
        }

        #endregion

        #region A / Confirm Punch Feedback

        /// <summary>
        /// Executes a 0.14s mechanical punch when A is pressed on an available robot:
        /// compresses the selection frame and card, flashes the sprite, then restores.
        /// </summary>
        public void PlayConfirmInspection(CollectionCardSelectable card)
        {
            if (card == null) return;

            if (!card.IsUnlocked)
            {
                card.TriggerLockPulse();
                return;
            }

            StartCoroutine(ConfirmInspectionRoutine(card));
        }

        private IEnumerator ConfirmInspectionRoutine(CollectionCardSelectable card)
        {
            RectTransform cardRt = card.CardVisual;
            Vector3 baseScale = card.RestScale;
            Vector3 punchScale = baseScale * 0.94f;

            float p1 = 0.05f;
            float elapsed = 0f;

            // Compress to 0.94x
            while (elapsed < p1)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / p1);
                if (cardRt != null) cardRt.localScale = Vector3.Lerp(baseScale, punchScale, t);
                if (m_SelectionFrame != null) m_SelectionFrame.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.94f, 0.94f, 1f), t);
                yield return null;
            }

            // Rebound to 1.04x
            float p2 = 0.09f;
            elapsed = 0f;
            while (elapsed < p2)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / p2);
                float s = Mathf.Lerp(0.94f, 1.04f, UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.3f));
                if (cardRt != null) cardRt.localScale = baseScale * s;
                if (m_SelectionFrame != null) m_SelectionFrame.localScale = Vector3.one * s;
                yield return null;
            }

            if (cardRt != null) cardRt.localScale = baseScale * 1.04f;
            if (m_SelectionFrame != null) m_SelectionFrame.localScale = Vector3.one;
        }

        #endregion

        #region Exit Retraction Choreography

        private IEnumerator HangarExitRoutine(Action onComplete)
        {
            float duration = 0.24f;

            if (m_SignBreezeRoutine != null) { StopCoroutine(m_SignBreezeRoutine); m_SignBreezeRoutine = null; }
            if (m_BeamRoutine != null) { StopCoroutine(m_BeamRoutine); m_BeamRoutine = null; }

            // 1. Hide inspection beam & selection frame immediately
            if (m_InspectionBeam != null) m_InspectionBeam.gameObject.SetActive(false);
            if (m_SelectionFrame != null) m_SelectionFrame.gameObject.SetActive(false);

            // 2. Dim platform lights
            if (m_PlatformGlows != null)
            {
                for (int i = 0; i < m_PlatformGlows.Length; i++)
                {
                    if (m_PlatformGlows[i] != null)
                    {
                        Color c = m_PlatformGlows[i].color;
                        StartCoroutine(AnimateImageAlpha(m_PlatformGlows[i], c.a, 0f, 0.12f));
                    }
                }
            }

            // 3. Robots lower down and collapse inward
            if (m_CardRects != null)
            {
                for (int i = 0; i < m_CardRects.Length; i++)
                {
                    if (m_CardRects[i] != null)
                    {
                        Vector2 targetCard = new Vector2(m_CardRestPositions[i].x, m_CardRestPositions[i].y - 35f);
                        StartCoroutine(AnimateMotion(m_CardRects[i], m_CardRects[i].anchoredPosition, targetCard, 0f, 0f, 0.12f, 0f, EasingType.EaseInQuad, 1f));
                        StartCoroutine(AnimateScale(m_CardRects[i], m_CardRects[i].localScale, Vector3.zero, 0.12f, EasingType.EaseInQuad, 1f));
                    }
                }
            }

            // 4. Platforms retract downward
            if (m_Platforms != null)
            {
                for (int i = 0; i < m_Platforms.Length; i++)
                {
                    if (m_Platforms[i] != null)
                    {
                        StartCoroutine(AnimateScale(m_Platforms[i], m_Platforms[i].localScale, new Vector3(0.8f, 0f, 1f), 0.14f, EasingType.EaseInQuad, 1f));
                    }
                }
            }

            // 5. Diagnostics details panel lines retract
            if (m_DetailsTopLine != null) StartCoroutine(AnimateScale(m_DetailsTopLine, m_DetailsTopLine.localScale, new Vector3(0f, 1f, 1f), 0.12f, EasingType.EaseInQuad, 1f));
            if (m_DetailsBottomLine != null) StartCoroutine(AnimateScale(m_DetailsBottomLine, m_DetailsBottomLine.localScale, new Vector3(0f, 1f, 1f), 0.12f, EasingType.EaseInQuad, 1f));
            if (m_DetailsPanel != null)
            {
                CanvasGroup dCg = m_DetailsPanel.GetComponent<CanvasGroup>();
                if (dCg != null) StartCoroutine(AnimateCanvasGroupAlpha(dCg, dCg.alpha, 0f, 0.12f));
            }

            // 6. Header sign shoots upper-right (+800, +400)
            if (m_CollectionSign != null)
            {
                Vector2 targetSign = new Vector2(m_SignRestPos.x + 800f, m_SignRestPos.y + 400f);
                StartCoroutine(AnimateMotion(m_CollectionSign, m_CollectionSign.anchoredPosition, targetSign, 0f, 6f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // 7. Counter fades and scales down cleanly
            if (m_TotalCounter != null)
            {
                CanvasGroup totalCg = m_TotalCounter.GetComponent<CanvasGroup>();
                if (totalCg != null) StartCoroutine(AnimateCanvasGroupAlpha(totalCg, totalCg.alpha, 0f, 0.12f));
                StartCoroutine(AnimateScale(m_TotalCounter, m_TotalCounter.localScale, new Vector3(0.9f, 0.9f, 1f), 0.12f, EasingType.EaseInQuad, 1f));
            }

            // 8. Back button drops downward (-350px)
            if (m_BackButton != null)
            {
                Vector2 targetBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 350f);
                StartCoroutine(AnimateMotion(m_BackButton, m_BackButton.anchoredPosition, targetBack, 0f, 0f, duration, 0f, EasingType.EaseInCubic, 1f));
            }

            // 9. Panel folds away (Scale: 1.0 -> 0.90, alpha: 1 -> 0)
            if (m_PanelBackground != null)
            {
                CanvasGroup cg = m_PanelBackground.GetComponent<CanvasGroup>();
                StartCoroutine(AnimateScale(m_PanelBackground, m_PanelBackground.localScale, m_PanelRestScale * 0.90f, duration, EasingType.EaseInCubic, 1f));
                if (cg != null) StartCoroutine(AnimateCanvasGroupAlpha(cg, cg.alpha, 0f, duration));
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        #endregion

        #region Helper Motion Routines

        private IEnumerator AnimateMotion(
            RectTransform target,
            Vector2 startPos, Vector2 targetPos,
            float startRotZ, float targetRotZ,
            float duration, float delay,
            EasingType easing, float overshoot,
            Action onDone = null)
        {
            if (delay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                    if (Mathf.Abs(startRotZ - targetRotZ) > 0.01f)
                    {
                        target.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(startRotZ, targetRotZ, ease));
                    }
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = targetPos;
                target.localEulerAngles = new Vector3(0f, 0f, targetRotZ);
            }

            onDone?.Invoke();
        }

        private IEnumerator AnimateScale(
            RectTransform target,
            Vector3 startScale, Vector3 targetScale,
            float duration, EasingType easing, float overshoot = 1f, Action onDone = null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
                }
                yield return null;
            }

            if (target != null) target.localScale = targetScale;
            onDone?.Invoke();
        }

        private IEnumerator AnimateImageAlpha(Image img, float startAlpha, float targetAlpha, float duration)
        {
            if (img == null) yield break;
            Color c = img.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (img != null)
                {
                    img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startAlpha, targetAlpha, t));
                }
                yield return null;
            }
            if (img != null) img.color = new Color(c.r, c.g, c.b, targetAlpha);
        }

        private IEnumerator AnimateCanvasGroupAlpha(CanvasGroup cg, float startAlpha, float targetAlpha, float duration)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            if (cg != null) cg.alpha = targetAlpha;
        }

        #endregion
    }
}
