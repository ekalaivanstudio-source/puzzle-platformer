using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ModernLevelSelection;
using MainGame.UI.Unified;
using MainGame.UI.Feedback;

namespace LevelSelection
{
    /// <summary>
    /// Generates and manages paginated level selection inside a Grid Layout Group.
    /// Supports multi-arc worlds, configurable grid limits (e.g. 10 levels per page),
    /// previous/next page navigation, smooth pooling or spawn/despawn, and auto-focusing.
    /// </summary>
    [DisallowMultipleComponent]
    public class GridLevelGenerator : MonoBehaviour
    {
        #region Types

        public enum ButtonDisableMode
        {
            InteractableOnly,
            HideGameObject
        }

        public enum LevelDisplayMode
        {
            [Tooltip("Hides or reuses the 10 spawned buttons (zero GC allocation, optimal performance).")]
            HideAndReuse,
            [Tooltip("Destroys existing 10 buttons and spawns fresh 10 buttons on page change.")]
            DeleteAndSpawn
        }

        [Serializable]
        public class ArcConfig
        {
            public string arcName = "ARC 1";
            public Sprite arcTitleSprite;
            [Min(1)] public int totalLevelsInArc = 20;
            [Tooltip("Theme image for this arc, cut into slices for the levels.")]
            public Texture2D arcThemeImage;
            [Tooltip("Optional custom thumbnails for each level in this arc.")]
            public Sprite[] levelThumbnails;
        }

        private class ArcSegment
        {
            public string SegmentName;
            public int StartLevel;
            public int TotalLevels;
            public Texture2D ThemeImage;
            public Sprite[] Thumbnails;
            public List<Sprite> SlicedSprites = new List<Sprite>();
        }

        private class ResolvedArc
        {
            public string ArcName;
            public Sprite TitleSprite;
            public int TotalLevels;
            public int StartLevelNumber;
            public List<ArcSegment> Segments = new List<ArcSegment>();
        }

        #endregion

        #region Inspector Fields

        [Header("Arc Configurations")]
        [Tooltip("Existing ArcData ScriptableObjects from the project (ARC 1, ARC 2, etc.).")]
        [SerializeField] private List<ArcData> m_ArcDataList = new List<ArcData>();

        [Tooltip("Alternative or additional inline custom arc definitions.")]
        [SerializeField] private List<ArcConfig> m_CustomArcs = new List<ArcConfig>();

        [Header("Fallback Arc Config (Used if both lists above are empty)")]
        [SerializeField] private string m_FallbackArcName = "ARC 1";
        [SerializeField] private Sprite m_FallbackArcTitleSprite;
        [SerializeField] private int m_FallbackTotalLevels = 25;
        [SerializeField] private int m_FallbackStartLevel = 1;

        [Header("Grid Layout & Button Spawning")]
        [Tooltip("The parent transform containing the GridLayoutGroup (e.g. 'Grid View').")]
        [SerializeField] private RectTransform m_GridContainer;

        [Tooltip("The New Level Prefab to instantiate if grid doesn't have child buttons.")]
        [SerializeField] private GameObject m_LevelButtonPrefab;

        [Tooltip("Number of levels shown per page in the grid. Defaults to 10.")]
        [Range(1, 30)]
        [SerializeField] private int m_LevelsPerPage = 10;

        [Tooltip("Display strategy: Hide and reuse existing 10 buttons (pooling) or Delete and spawn fresh 10 buttons.")]
        [SerializeField] private LevelDisplayMode m_DisplayMode = LevelDisplayMode.HideAndReuse;

        [Header("Navigation Buttons")]
        [Tooltip("Button to navigate to next 10 levels or next arc.")]
        [SerializeField] private Button m_NextButton;

        [Tooltip("Button to navigate to previous 10 levels or previous arc.")]
        [SerializeField] private Button m_PrevButton;

        [Tooltip("How to treat next/prev buttons when no further page is available.")]
        [SerializeField] private ButtonDisableMode m_ButtonDisableMode = ButtonDisableMode.InteractableOnly;

        [Tooltip("If true, clicking Next on the last page of an arc transitions automatically to the next arc.")]
        [SerializeField] private bool m_AutoAdvanceArc = true;

        [Header("Arc Header Display")]
        [Tooltip("Header image displaying current arc title/banner (e.g. 'IMG Arc').")]
        [SerializeField] private Image m_ArcTitleImage;

        [Tooltip("Optional TMP text displaying the arc title.")]
        [SerializeField] private TextMeshProUGUI m_ArcTitleText;

        [Header("Page Indicators (Optional)")]
        [Tooltip("List of pip GameObjects for pages (Pip_1, Pip_2, etc.).")]
        [SerializeField] private List<GameObject> m_PagePips = new List<GameObject>();

        [Tooltip("Optional text displaying 'Page X / Y'.")]
        [SerializeField] private TextMeshProUGUI m_PageNumberText;

        [SerializeField] private Color m_ActivePipColor = Color.white;
        [SerializeField] private Color m_InactivePipColor = new Color(0.4f, 0.45f, 0.55f, 0.5f);

        [Header("Progression & Initialization")]
        [Tooltip("If true, automatically opens the arc and page containing the player's highest unlocked level.")]
        [SerializeField] private bool m_FocusHighestUnlockedOnStart = true;

        [Tooltip("Generate/populate on Start.")]
        [SerializeField] private bool m_GenerateOnStart = true;

        [Tooltip("Debug override for highest unlocked level (-1 to use SaveManager).")]
        [SerializeField] private int m_DebugHighestUnlocked = -1;

        [Header("Level Thumbnail Images")]
        [Tooltip("Sprite sheet texture containing sliced level thumbnail sprites (e.g. LVL_BG.png).")]
        [SerializeField] private Texture2D m_ThumbnailSpriteSheet;

        [Tooltip("List of level thumbnail sprites sliced from the sprite sheet (LVL_BG_0, LVL_BG_1, ...).")]
        [SerializeField] private List<Sprite> m_LevelThumbnailSprites = new List<Sprite>();

        #endregion

        #region Events

        /// <summary>Invoked when any level button is clicked.</summary>
        public UnityEvent<int> OnLevelClicked = new UnityEvent<int>();

        /// <summary>Invoked when the active page or arc changes.</summary>
        public UnityEvent<int, int> OnPageChanged = new UnityEvent<int, int>(); // (arcIndex, pageIndex)

        #endregion

        #region Private State

        private readonly List<GridLevelButton> m_ButtonPool = new List<GridLevelButton>();
        private List<ResolvedArc> m_ResolvedArcs = new List<ResolvedArc>();

        private int m_CurrentArcIndex = 0;
        private int m_CurrentPageIndex = 0;
        private int m_CurrentHighestUnlocked = 1;
        private int m_FocusedLevelNumber = 1;

        #endregion

        #region Public Properties

        public int CurrentArcIndex => m_CurrentArcIndex;
        public int CurrentPageIndex => m_CurrentPageIndex;
        public int LevelsPerPage => m_LevelsPerPage;
        public int TotalArcs => m_ResolvedArcs != null && m_ResolvedArcs.Count > 0 ? m_ResolvedArcs.Count : 1;
        public List<GridLevelButton> ActiveButtons => m_ButtonPool;

        /// <summary>
        /// Returns the button matching the currently focused level, or the first active unlocked button.
        /// </summary>
        public GridLevelButton GetFocusedOrFirstButton()
        {
            if (m_ButtonPool == null || m_ButtonPool.Count == 0) return null;

            foreach (var btn in m_ButtonPool)
            {
                if (btn != null && btn.gameObject.activeInHierarchy && btn.LevelNumber == m_FocusedLevelNumber)
                {
                    return btn;
                }
            }

            foreach (var btn in m_ButtonPool)
            {
                if (btn != null && btn.gameObject.activeInHierarchy && btn.IsUnlocked)
                {
                    return btn;
                }
            }

            foreach (var btn in m_ButtonPool)
            {
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    return btn;
                }
            }

            return null;
        }

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            AutoWireReferences();
        }

        private void Awake()
        {
            AutoWireReferences();
            BuildResolvedArcs();
            PreloadAllThumbnails();
        }

        private float m_NavCooldown = 0f;
        private bool m_IsPageTransitioning = false;

        private void Start()
        {
            if (m_NextButton != null) m_NextButton.gameObject.SetActive(false);
            if (m_PrevButton != null) m_PrevButton.gameObject.SetActive(false);

            if (m_GenerateOnStart)
            {
                InitializeProgression();
                BuildResolvedArcs();

                if (m_FocusHighestUnlockedOnStart)
                {
                    JumpToLevel(m_CurrentHighestUnlocked, selectInEventSystem: true);
                }
                else
                {
                    ShowPage(0, 0, selectInEventSystem: true);
                }
            }
        }

        private void Update()
        {
            HandleInputNavigation();
        }

        private void HandleButtonFocused(int levelNumber)
        {
            if (levelNumber > 0 && !m_IsPageTransitioning)
            {
                m_FocusedLevelNumber = levelNumber;
            }
        }

        private void HandleInputNavigation()
        {
            if (m_IsPageTransitioning) return;

            if (m_NavCooldown > 0f)
            {
                m_NavCooldown -= Time.unscaledDeltaTime;
            }

            if (!gameObject.activeInHierarchy) return;

            // Navigation strictly uses Left/Right Arrows and A/D keys (no Up/Down arrows, no W/S keys)
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter);

            // Gamepad stick navigation (horizontal only)
            if (m_NavCooldown <= 0f)
            {
                float h = Input.GetAxisRaw("Horizontal");
                if (h > 0.55f) { right = true; m_NavCooldown = 0.20f; }
                else if (h < -0.55f) { left = true; m_NavCooldown = 0.20f; }
            }

            if (right)
            {
                NavigateToLevel(m_FocusedLevelNumber + 1);
            }
            else if (left)
            {
                NavigateToLevel(m_FocusedLevelNumber - 1);
            }
            else if (submit)
            {
                TriggerFocusedLevel();
            }
        }

        /// <summary>
        /// Navigates to a specific level, turning pages and switching arcs automatically if needed.
        /// </summary>
        public void NavigateToLevel(int targetLevel)
        {
            if (m_IsPageTransitioning) return;

            int minLevel = 1;
            int maxLevel = GetTotalLevelsAcrossAllArcs();
            targetLevel = Mathf.Clamp(targetLevel, minLevel, maxLevel);

            if (targetLevel == m_FocusedLevelNumber) return;

            BuildResolvedArcs();
            int targetArcIndex = 0;
            int targetPageIndex = 0;

            for (int a = 0; a < m_ResolvedArcs.Count; a++)
            {
                var arc = m_ResolvedArcs[a];
                int start = arc.StartLevelNumber;
                int end = start + arc.TotalLevels - 1;
                if (targetLevel >= start && targetLevel <= end)
                {
                    targetArcIndex = a;
                    int offset = targetLevel - start;
                    targetPageIndex = offset / m_LevelsPerPage;
                    break;
                }
            }

            m_FocusedLevelNumber = targetLevel;

            if (targetArcIndex != m_CurrentArcIndex || targetPageIndex != m_CurrentPageIndex)
            {
                bool isForward = (targetArcIndex > m_CurrentArcIndex) ||
                                 (targetArcIndex == m_CurrentArcIndex && targetPageIndex >= m_CurrentPageIndex);

                var animator = GetComponent<LevelSelectionScreenAnimator>() ?? GetComponentInParent<LevelSelectionScreenAnimator>();
                if (animator != null && isActiveAndEnabled)
                {
                    m_IsPageTransitioning = true;
                    animator.PlayPageTransition(
                        onPageSwitch: () =>
                        {
                            ShowPage(targetArcIndex, targetPageIndex, selectInEventSystem: false);
                            HighlightFocusedButton();
                        },
                        onComplete: () =>
                        {
                            m_IsPageTransitioning = false;
                            HighlightFocusedButton();
                        },
                        isForward: isForward);
                }
                else
                {
                    ShowPage(targetArcIndex, targetPageIndex, selectInEventSystem: false);
                    HighlightFocusedButton();
                }
            }
            else
            {
                HighlightFocusedButton();
                UIFeedbackAudio.PlaySfx(UISfxType.Navigate, 0.45f);
            }
        }

        public void HighlightFocusedButton()
        {
            foreach (var btn in m_ButtonPool)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                bool isTarget = (btn.LevelNumber == m_FocusedLevelNumber);
                btn.SetSelected(isTarget);
                if (isTarget && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(btn.gameObject);
                }
            }
        }

        private void TriggerFocusedLevel()
        {
            foreach (var btn in m_ButtonPool)
            {
                if (btn != null && btn.gameObject.activeInHierarchy && btn.LevelNumber == m_FocusedLevelNumber)
                {
                    btn.HandleButtonClick();
                    return;
                }
            }
        }

        public int GetTotalLevelsAcrossAllArcs()
        {
            BuildResolvedArcs();
            if (m_ResolvedArcs == null || m_ResolvedArcs.Count == 0) return 20;
            var last = m_ResolvedArcs[m_ResolvedArcs.Count - 1];
            return last.StartLevelNumber + last.TotalLevels - 1;
        }

        private void OnEnable()
        {
            GridLevelButton.OnAnyButtonFocused -= HandleButtonFocused;
            GridLevelButton.OnAnyButtonFocused += HandleButtonFocused;

            if (m_NextButton != null)
            {
                m_NextButton.onClick.RemoveListener(NextPage);
                m_NextButton.onClick.AddListener(NextPage);
            }

            if (m_PrevButton != null)
            {
                m_PrevButton.onClick.RemoveListener(PreviousPage);
                m_PrevButton.onClick.AddListener(PreviousPage);
            }
        }

        private void OnDisable()
        {
            GridLevelButton.OnAnyButtonFocused -= HandleButtonFocused;

            if (m_NextButton != null)
            {
                m_NextButton.onClick.RemoveListener(NextPage);
            }

            if (m_PrevButton != null)
            {
                m_PrevButton.onClick.RemoveListener(PreviousPage);
            }
        }

        #endregion

        #region Auto-Wiring

        /// <summary>
        /// Automatically finds and assigns UI containers, buttons, and headers if unassigned.
        /// </summary>
        [ContextMenu("Auto-Wire References")]
        public void AutoWireReferences()
        {
            Transform searchRoot = transform;
            // If placed on a child under Level Selection, look at the higher root
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                searchRoot = parentCanvas.transform;
            }

            // Grid Container: look for child named "Grid View" or any GridLayoutGroup
            if (m_GridContainer == null)
            {
                var grid = GetComponentInChildren<GridLayoutGroup>(true) ?? searchRoot.GetComponentInChildren<GridLayoutGroup>(true);
                if (grid != null)
                {
                    m_GridContainer = grid.GetComponent<RectTransform>();
                }
            }

            // Next & Previous buttons
            if (m_NextButton == null)
            {
                m_NextButton = FindButtonInHierarchy(searchRoot, "B Next") ?? FindButtonInHierarchy(searchRoot, "NextButton") ?? FindButtonInHierarchy(searchRoot, "Next");
            }

            if (m_PrevButton == null)
            {
                m_PrevButton = FindButtonInHierarchy(searchRoot, "B Perivous") ?? FindButtonInHierarchy(searchRoot, "B Previous") ?? FindButtonInHierarchy(searchRoot, "PrevButton") ?? FindButtonInHierarchy(searchRoot, "Previous");
            }

            // Arc Title Image
            if (m_ArcTitleImage == null)
            {
                var images = searchRoot.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.name.Equals("IMG Arc", StringComparison.OrdinalIgnoreCase) ||
                        img.name.Equals("ArcTitle", StringComparison.OrdinalIgnoreCase))
                    {
                        m_ArcTitleImage = img;
                        break;
                    }
                }
            }

            // Pips
            if (m_PagePips == null || m_PagePips.Count == 0)
            {
                var foundPips = new List<GameObject>();
                for (int i = 1; i <= 10; i++)
                {
                    var pip = FindChildRecursive(searchRoot, $"Pip_{i}");
                    if (pip != null) foundPips.Add(pip.gameObject);
                }
                if (foundPips.Count > 0)
                {
                    m_PagePips = foundPips;
                }
            }

#if UNITY_EDITOR
            // Auto-discover ArcData assets in project if list is empty
            if (m_ArcDataList == null || m_ArcDataList.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArcData");
                if (guids != null && guids.Length > 0)
                {
                    m_ArcDataList = new List<ArcData>();
                    foreach (var guid in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var arc = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcData>(path);
                        if (arc != null && !m_ArcDataList.Contains(arc))
                        {
                            m_ArcDataList.Add(arc);
                        }
                    }
                    m_ArcDataList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Auto-discover New Level Prefab if prefab field is empty
            if (m_LevelButtonPrefab == null)
            {
                string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("New Level Prefab t:Prefab");
                if (prefabGuids != null && prefabGuids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
                    m_LevelButtonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            // Auto-discover Level Thumbnail sprite sheet if empty
            if (m_ThumbnailSpriteSheet == null)
            {
                m_ThumbnailSpriteSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png");
            }

            if (m_LevelThumbnailSprites == null || m_LevelThumbnailSprites.Count == 0)
            {
                LoadThumbnailsFromSpriteSheet();
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Populates the list of thumbnail sprites from the sliced sprite sheet asset (LVL_BG.png).
        /// </summary>
        [ContextMenu("Load Thumbnails From Sprite Sheet")]
        public void LoadThumbnailsFromSpriteSheet()
        {
            string path = m_ThumbnailSpriteSheet != null
                ? UnityEditor.AssetDatabase.GetAssetPath(m_ThumbnailSpriteSheet)
                : "Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png";

            if (string.IsNullOrEmpty(path)) return;

            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            var sprites = new List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    sprites.Add(s);
                }
            }

            // Sort numerically: LVL_BG_0, LVL_BG_1, ... LVL_BG_90
            sprites.Sort((a, b) =>
            {
                int numA = ExtractTrailingNumber(a.name);
                int numB = ExtractTrailingNumber(b.name);
                if (numA != -1 && numB != -1) return numA.CompareTo(numB);
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            m_LevelThumbnailSprites = sprites;
        }

        private static int ExtractTrailingNumber(string name)
        {
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(name.Substring(lastUnderscore + 1), out int n))
            {
                return n;
            }
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            if (i < name.Length - 1 && int.TryParse(name.Substring(i + 1), out int val))
            {
                return val;
            }
            return -1;
        }
#endif

        private Button FindButtonInHierarchy(Transform root, string targetName)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform deeper = FindChildRecursive(child, targetName);
                if (deeper != null) return deeper;
            }
            return null;
        }

        #endregion

        #region Arc Building & Progression

        private void InitializeProgression()
        {
            if (m_DebugHighestUnlocked > 0)
            {
                m_CurrentHighestUnlocked = m_DebugHighestUnlocked;
            }
            else
            {
                m_CurrentHighestUnlocked = SaveManager.GetHighestUnlocked();
            }

            if (m_FocusedLevelNumber <= 0 || (m_FocusedLevelNumber == 1 && m_CurrentHighestUnlocked > 1))
            {
                m_FocusedLevelNumber = m_CurrentHighestUnlocked;
            }
        }

        private void BuildResolvedArcs()
        {
            if (m_ResolvedArcs != null && m_ResolvedArcs.Count > 0)
            {
                return;
            }

            m_ResolvedArcs.Clear();

            int runningStartLevel = 1;

            void AddSegment(string arcName, Sprite titleSprite, int totalLevels, Texture2D themeImg, Sprite[] thumbs)
            {
                if (totalLevels <= 0) totalLevels = 1;
                string cleanName = string.IsNullOrEmpty(arcName) ? "ARC 1" : arcName.Trim();

                // If previous arc has the same arc name, merge as a new segment in that world!
                ResolvedArc targetArc = null;
                if (m_ResolvedArcs.Count > 0)
                {
                    var lastArc = m_ResolvedArcs[m_ResolvedArcs.Count - 1];
                    if (string.Equals(lastArc.ArcName.Trim(), cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetArc = lastArc;
                    }
                }

                if (targetArc == null)
                {
                    targetArc = new ResolvedArc
                    {
                        ArcName = cleanName,
                        TitleSprite = titleSprite,
                        TotalLevels = 0,
                        StartLevelNumber = runningStartLevel,
                        Segments = new List<ArcSegment>()
                    };
                    m_ResolvedArcs.Add(targetArc);
                }

                var seg = new ArcSegment
                {
                    SegmentName = cleanName,
                    StartLevel = runningStartLevel,
                    TotalLevels = totalLevels,
                    ThemeImage = themeImg,
                    Thumbnails = thumbs,
                    SlicedSprites = new List<Sprite>()
                };

                targetArc.Segments.Add(seg);
                targetArc.TotalLevels += totalLevels;
                runningStartLevel += totalLevels;
            }

            // 1. From ScriptableObject ArcData list
            if (m_ArcDataList != null && m_ArcDataList.Count > 0)
            {
                foreach (var data in m_ArcDataList)
                {
                    if (data == null) continue;
                    AddSegment(data.arcName, data.arcTitleSprite, data.totalLevelsInArc, data.arcThemeImage, data.levelThumbnails);
                }
            }
            // 2. From custom inline arc configs (fallback if ArcData list is empty)
            else if (m_CustomArcs != null && m_CustomArcs.Count > 0)
            {
                foreach (var cfg in m_CustomArcs)
                {
                    if (cfg == null) continue;
                    AddSegment(cfg.arcName, cfg.arcTitleSprite, cfg.totalLevelsInArc, cfg.arcThemeImage, cfg.levelThumbnails);
                }
            }

            // 3. Fallback if empty
            if (m_ResolvedArcs.Count == 0)
            {
                AddSegment(m_FallbackArcName, m_FallbackArcTitleSprite, m_FallbackTotalLevels, m_ThumbnailSpriteSheet, null);
            }
        }

        private void PreloadAllThumbnails()
        {
            if (m_ResolvedArcs == null) return;
            foreach (var arc in m_ResolvedArcs)
            {
                if (arc == null || arc.Segments == null) continue;
                foreach (var seg in arc.Segments)
                {
                    if (seg == null) continue;
                    if (seg.SlicedSprites == null || seg.SlicedSprites.Count == 0)
                    {
                        Texture2D theme = seg.ThemeImage;
#if UNITY_EDITOR
                        if (theme == null)
                        {
                            string defaultThemePath = (seg.StartLevel <= 4)
                                ? "Assets/MainGame/Sprites/LEVEL SELECT UI new/TUT_BG.png"
                                : "Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png";
                            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(defaultThemePath);
                        }
#endif
                        if (theme != null)
                        {
                            int cols = 5;
                            int rows = Mathf.CeilToInt((float)seg.TotalLevels / cols);
                            seg.SlicedSprites = SliceTextureIntoGrid(theme, cols, rows, seg.TotalLevels);
                        }
                    }
                }
            }
        }

        private ResolvedArc GetArc(int arcIndex)
        {
            if (m_ResolvedArcs == null || m_ResolvedArcs.Count == 0)
            {
                BuildResolvedArcs();
            }

            int index = Mathf.Clamp(arcIndex, 0, m_ResolvedArcs.Count - 1);
            return m_ResolvedArcs[index];
        }

        private int GetTotalPagesForArc(ResolvedArc arc)
        {
            return Mathf.CeilToInt((float)arc.TotalLevels / Mathf.Max(1, m_LevelsPerPage));
        }

        #endregion

        #region Page Generation & Spawning

        /// <summary>
        /// Displays the specified page of the specified arc.
        /// </summary>
        public void ShowPage(int arcIndex, int pageIndex, bool selectInEventSystem = false)
        {
            InitializeProgression();
            BuildResolvedArcs();

            m_CurrentArcIndex = Mathf.Clamp(arcIndex, 0, m_ResolvedArcs.Count - 1);
            ResolvedArc arc = GetArc(m_CurrentArcIndex);

            int totalPages = GetTotalPagesForArc(arc);
            m_CurrentPageIndex = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, totalPages - 1));

            // Calculate level range for this page
            int pageStartLevel = arc.StartLevelNumber + (m_CurrentPageIndex * m_LevelsPerPage);
            int arcEndLevel = arc.StartLevelNumber + arc.TotalLevels - 1;
            int pageEndLevel = Mathf.Min(pageStartLevel + m_LevelsPerPage - 1, arcEndLevel);
            int levelsOnPage = Mathf.Max(0, pageEndLevel - pageStartLevel + 1);

            if (m_FocusedLevelNumber < pageStartLevel || m_FocusedLevelNumber > pageEndLevel)
            {
                m_FocusedLevelNumber = pageStartLevel;
            }

            // 1. Update Arc Header & Title
            UpdateHeaderDisplay(arc);

            // 2. Update Page Indicators & Buttons
            UpdateNavigationDisplay(totalPages);

            // 3. Populate Level Buttons
            PopulateGrid(pageStartLevel, levelsOnPage, arc, selectInEventSystem);

            // 4. Notify Listeners
            OnPageChanged?.Invoke(m_CurrentArcIndex, m_CurrentPageIndex);
        }

        private void UpdateHeaderDisplay(ResolvedArc arc)
        {
            if (m_ArcTitleImage != null)
            {
                if (arc.TitleSprite != null)
                {
                    m_ArcTitleImage.sprite = arc.TitleSprite;
                    m_ArcTitleImage.gameObject.SetActive(true);
                }
            }

            if (m_ArcTitleText != null)
            {
                m_ArcTitleText.text = arc.ArcName;
            }

            if (m_PageNumberText != null)
            {
                int totalPages = GetTotalPagesForArc(arc);
                m_PageNumberText.text = $"{m_CurrentPageIndex + 1} / {totalPages}";
            }
        }

        private void UpdateNavigationDisplay(int totalPagesInArc)
        {
            // Requirement 1 & reference arrows:
            // Show arrow image for user reference so they understand there are more levels.
            // On page 1: Next arrow is visible if totalPages > 1.
            // On page 2: Prev arrow is visible. Next arrow visible if more pages exist.
            if (m_NextButton != null)
            {
                bool canGoNext = (m_CurrentPageIndex < totalPagesInArc - 1) || (m_AutoAdvanceArc && m_CurrentArcIndex < TotalArcs - 1);
                m_NextButton.gameObject.SetActive(canGoNext);
                m_NextButton.interactable = canGoNext;
            }

            if (m_PrevButton != null)
            {
                bool canGoPrev = (m_CurrentPageIndex > 0) || (m_AutoAdvanceArc && m_CurrentArcIndex > 0);
                m_PrevButton.gameObject.SetActive(canGoPrev);
                m_PrevButton.interactable = canGoPrev;
            }

            // Update Pips if present
            if (m_PagePips != null && m_PagePips.Count > 0)
            {
                for (int i = 0; i < m_PagePips.Count; i++)
                {
                    var pip = m_PagePips[i];
                    if (pip == null) continue;

                    if (i < totalPagesInArc)
                    {
                        pip.SetActive(true);
                        var img = pip.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = (i == m_CurrentPageIndex) ? m_ActivePipColor : m_InactivePipColor;
                        }
                    }
                    else
                    {
                        pip.SetActive(false);
                    }
                }
            }
        }

        private void ApplyButtonState(Button btn, bool enabled)
        {
            if (btn == null) return;

            if (m_ButtonDisableMode == ButtonDisableMode.HideGameObject)
            {
                btn.gameObject.SetActive(enabled);
            }
            else
            {
                btn.gameObject.SetActive(true);
                btn.interactable = enabled;
            }
        }

        private void PopulateGrid(int pageStartLevel, int levelsOnPage, ResolvedArc arc, bool selectInEventSystem)
        {
            if (m_GridContainer == null)
            {
                Debug.LogError("[GridLevelGenerator] GridContainer RectTransform is not assigned.");
                return;
            }

            if (m_DisplayMode == LevelDisplayMode.HideAndReuse)
            {
                PopulateWithPooling(pageStartLevel, levelsOnPage, arc, selectInEventSystem);
            }
            else
            {
                PopulateWithReinstantiation(pageStartLevel, levelsOnPage, arc, selectInEventSystem);
            }
        }

        private void PopulateWithPooling(int pageStartLevel, int levelsOnPage, ResolvedArc arc, bool selectInEventSystem)
        {
            // 1. Gather any existing child buttons under m_GridContainer if pool is empty
            if (m_ButtonPool.Count == 0)
            {
                var existing = m_GridContainer.GetComponentsInChildren<GridLevelButton>(true);
                foreach (var b in existing)
                {
                    if (b.transform.parent == m_GridContainer)
                    {
                        m_ButtonPool.Add(b);
                    }
                }
            }

            // 2. Ensure pool contains at least m_LevelsPerPage buttons
            while (m_ButtonPool.Count < m_LevelsPerPage && m_LevelButtonPrefab != null)
            {
                GameObject obj = Instantiate(m_LevelButtonPrefab, m_GridContainer);
                var btn = obj.GetComponent<GridLevelButton>() ?? obj.AddComponent<GridLevelButton>();
                btn.AutoWireComponents();
                m_ButtonPool.Add(btn);
            }

            GridLevelButton buttonToSelect = null;

            // 3. Configure each button in pool
            for (int i = 0; i < m_ButtonPool.Count; i++)
            {
                var btn = m_ButtonPool[i];
                if (btn == null) continue;

                if (i < levelsOnPage)
                {
                    int levelNum = pageStartLevel + i;
                    btn.gameObject.SetActive(true);
                    btn.gameObject.name = $"Level_{levelNum}";

                    bool isUnlocked = levelNum <= m_CurrentHighestUnlocked;
                    bool isCompleted = SaveManager.IsCompleted(levelNum)
                        || (SaveManager.GetStars(levelNum) > 0)
                        || (levelNum < m_CurrentHighestUnlocked);
                    bool isSelected = levelNum == m_FocusedLevelNumber;

                    Sprite thumb = GetThumbnailForLevel(arc, levelNum);

                    btn.Setup(levelNum, isUnlocked, isCompleted, isSelected, thumb);

                    // Re-bind click event
                    btn.OnLevelClicked.RemoveListener(HandleLevelClicked);
                    btn.OnLevelClicked.AddListener(HandleLevelClicked);

                    if (isSelected || (buttonToSelect == null && isUnlocked))
                    {
                        buttonToSelect = btn;
                    }
                }
                else
                {
                    // Hide remaining buttons beyond levelsOnPage
                    btn.gameObject.SetActive(false);
                }
            }

            // 4. Focus in EventSystem if requested
            if (selectInEventSystem && buttonToSelect != null && EventSystem.current != null)
            {
                StartCoroutine(FocusButtonNextFrame(buttonToSelect.gameObject));
            }
        }

        private void PopulateWithReinstantiation(int pageStartLevel, int levelsOnPage, ResolvedArc arc, bool selectInEventSystem)
        {
            if (m_LevelButtonPrefab == null)
            {
                Debug.LogError("[GridLevelGenerator] LevelButtonPrefab is required when m_PoolButtons is false.");
                return;
            }

            // Clear all existing children
            for (int i = m_GridContainer.childCount - 1; i >= 0; i--)
            {
                var child = m_GridContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            m_ButtonPool.Clear();

            GridLevelButton buttonToSelect = null;

            for (int i = 0; i < levelsOnPage; i++)
            {
                int levelNum = pageStartLevel + i;
                GameObject obj = Instantiate(m_LevelButtonPrefab, m_GridContainer);
                obj.name = $"Level_{levelNum}";

                var btn = obj.GetComponent<GridLevelButton>() ?? obj.AddComponent<GridLevelButton>();
                btn.AutoWireComponents();

                bool isUnlocked = levelNum <= m_CurrentHighestUnlocked;
                bool isCompleted = SaveManager.IsCompleted(levelNum)
                    || (SaveManager.GetStars(levelNum) > 0)
                    || (levelNum < m_CurrentHighestUnlocked && m_CurrentHighestUnlocked > 1);
                bool isSelected = levelNum == m_FocusedLevelNumber;

                Sprite thumb = GetThumbnailForLevel(arc, levelNum);

                btn.Setup(levelNum, isUnlocked, isCompleted, isSelected, thumb);

                btn.OnLevelClicked.RemoveListener(HandleLevelClicked);
                btn.OnLevelClicked.AddListener(HandleLevelClicked);

                m_ButtonPool.Add(btn);

                if (isSelected || (buttonToSelect == null && isUnlocked))
                {
                    buttonToSelect = btn;
                }
            }

            if (selectInEventSystem && buttonToSelect != null && EventSystem.current != null)
            {
                StartCoroutine(FocusButtonNextFrame(buttonToSelect.gameObject));
            }
        }

        private static readonly Dictionary<int, List<Sprite>> s_SlicedSpritesCache = new Dictionary<int, List<Sprite>>();

        /// <summary>
        /// Slices a texture into a grid of sub-sprites (columns x rows).
        /// Supports cutting any theme image into multiple level card slices.
        /// </summary>
        public static List<Sprite> SliceTextureIntoGrid(Texture2D texture, int columns, int rows, int maxSprites = 0)
        {
            var list = new List<Sprite>();
            if (texture == null || columns <= 0 || rows <= 0) return list;

            int cacheKey = (texture.name != null ? texture.name.GetHashCode() : 0) ^ (columns << 16) ^ rows;
            if (s_SlicedSpritesCache.TryGetValue(cacheKey, out var cached) && cached != null && cached.Count > 0)
            {
                return cached;
            }

#if UNITY_EDITOR
            // Try loading pre-cut sprites if texture is configured as Multiple in AssetDatabase
            string path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                var subSprites = new List<Sprite>();
                foreach (var a in assets)
                {
                    if (a is Sprite s) subSprites.Add(s);
                }
                if (subSprites.Count > 0)
                {
                    subSprites.Sort((a, b) =>
                    {
                        int numA = ExtractTrailingNumber(a.name);
                        int numB = ExtractTrailingNumber(b.name);
                        if (numA != -1 && numB != -1) return numA.CompareTo(numB);
                        return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                    });
                    s_SlicedSpritesCache[cacheKey] = subSprites;
                    return subSprites;
                }
            }
#endif

            // Dynamic grid slicing
            float tileWidth = (float)texture.width / columns;
            float tileHeight = (float)texture.height / rows;

            int count = 0;
            for (int r = 0; r < rows; r++)
            {
                // Top to bottom (Unity y=0 is at texture bottom)
                float y = (rows - 1 - r) * tileHeight;
                for (int c = 0; c < columns; c++)
                {
                    float x = c * tileWidth;
                    Rect rect = new Rect(x, y, tileWidth, tileHeight);
                    Sprite slice = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                    slice.name = $"{texture.name}_slice_{count}";
                    list.Add(slice);
                    count++;
                    if (maxSprites > 0 && count >= maxSprites)
                    {
                        s_SlicedSpritesCache[cacheKey] = list;
                        return list;
                    }
                }
            }

            s_SlicedSpritesCache[cacheKey] = list;
            return list;
        }

        private Sprite GetThumbnailForLevel(ResolvedArc arc, int levelNumber)
        {
            if (arc == null || arc.Segments == null || arc.Segments.Count == 0) return null;

            ArcSegment seg = null;
            for (int s = 0; s < arc.Segments.Count; s++)
            {
                var candidate = arc.Segments[s];
                if (levelNumber >= candidate.StartLevel && levelNumber < candidate.StartLevel + candidate.TotalLevels)
                {
                    seg = candidate;
                    break;
                }
            }
            if (seg == null) seg = arc.Segments[0];

            int localIndex = levelNumber - seg.StartLevel;

            // 1. Check custom pre-assigned thumbnails on segment
            if (seg.Thumbnails != null && localIndex >= 0 && localIndex < seg.Thumbnails.Length && seg.Thumbnails[localIndex] != null)
            {
                return seg.Thumbnails[localIndex];
            }

            // 2. Check segment's sliced sprites from its Theme Image
            if (seg.SlicedSprites == null || seg.SlicedSprites.Count == 0)
            {
                Texture2D theme = seg.ThemeImage;

#if UNITY_EDITOR
                if (theme == null)
                {
                    string defaultThemePath = (levelNumber <= 4)
                        ? "Assets/MainGame/Sprites/LEVEL SELECT UI new/TUT_BG.png"
                        : "Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png";
                    theme = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(defaultThemePath);
                    if (theme == null)
                    {
                        theme = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png");
                    }
                }
#endif
                if (theme != null)
                {
                    int cols = 5;
                    int rows = Mathf.CeilToInt((float)seg.TotalLevels / cols);
                    seg.SlicedSprites = SliceTextureIntoGrid(theme, cols, rows, seg.TotalLevels);
                }
            }

            if (seg.SlicedSprites != null && localIndex >= 0 && localIndex < seg.SlicedSprites.Count && seg.SlicedSprites[localIndex] != null)
            {
                return seg.SlicedSprites[localIndex];
            }

            // 3. Fallback to serialized m_LevelThumbnailSprites
            if (m_LevelThumbnailSprites != null && m_LevelThumbnailSprites.Count > 0)
            {
                int spriteIndex = levelNumber - 1;
                if (spriteIndex >= 0 && spriteIndex < m_LevelThumbnailSprites.Count && m_LevelThumbnailSprites[spriteIndex] != null)
                {
                    return m_LevelThumbnailSprites[spriteIndex];
                }
            }

#if UNITY_EDITOR
            // 4. Fallback to individual level sprite
            var individual = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/MainGame/Sprites/LEVEL SELECT UI new/Level {levelNumber}.png");
            if (individual != null) return individual;
#endif

            return null;
        }

        private IEnumerator FocusButtonNextFrame(GameObject target)
        {
            yield return null;
            if (target != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }

        #endregion

        #region Navigation Public API

        /// <summary>
        /// Moves to the next page of 10 levels, or advances to the next arc.
        /// </summary>
        public void NextPage()
        {
            ResolvedArc arc = GetArc(m_CurrentArcIndex);
            int totalPages = GetTotalPagesForArc(arc);

            int nextArc = m_CurrentArcIndex;
            int nextPage = m_CurrentPageIndex;

            if (m_CurrentPageIndex < totalPages - 1)
            {
                nextPage = m_CurrentPageIndex + 1;
            }
            else if (m_AutoAdvanceArc && m_CurrentArcIndex < TotalArcs - 1)
            {
                nextArc = m_CurrentArcIndex + 1;
                nextPage = 0;
            }
            else
            {
                return;
            }

            ResolvedArc targetArc = GetArc(nextArc);
            int newStartLevel = targetArc.StartLevelNumber + (nextPage * m_LevelsPerPage);
            m_FocusedLevelNumber = newStartLevel;

            var animator = GetComponent<LevelSelectionScreenAnimator>() ?? GetComponentInParent<LevelSelectionScreenAnimator>();
            if (animator != null && isActiveAndEnabled)
            {
                animator.PlayPageTransition(
                    onPageSwitch: () =>
                    {
                        ShowPage(nextArc, nextPage, selectInEventSystem: false);
                        HighlightFocusedButton();
                    },
                    onComplete: () =>
                    {
                        HighlightFocusedButton();
                    });
            }
            else
            {
                ShowPage(nextArc, nextPage, selectInEventSystem: true);
                HighlightFocusedButton();
            }
        }

        /// <summary>
        /// Moves to the previous page of 10 levels, or back to the previous arc.
        /// </summary>
        public void PreviousPage()
        {
            int prevArc = m_CurrentArcIndex;
            int prevPage = m_CurrentPageIndex;

            if (m_CurrentPageIndex > 0)
            {
                prevPage = m_CurrentPageIndex - 1;
            }
            else if (m_AutoAdvanceArc && m_CurrentArcIndex > 0)
            {
                prevArc = m_CurrentArcIndex - 1;
                ResolvedArc prevArcObj = GetArc(prevArc);
                prevPage = Mathf.Max(0, GetTotalPagesForArc(prevArcObj) - 1);
            }
            else
            {
                return;
            }

            ResolvedArc targetArc = GetArc(prevArc);
            int newStartLevel = targetArc.StartLevelNumber + (prevPage * m_LevelsPerPage);
            m_FocusedLevelNumber = newStartLevel;

            var animator = GetComponent<LevelSelectionScreenAnimator>() ?? GetComponentInParent<LevelSelectionScreenAnimator>();
            if (animator != null && isActiveAndEnabled)
            {
                animator.PlayPageTransition(
                    onPageSwitch: () =>
                    {
                        ShowPage(prevArc, prevPage, selectInEventSystem: false);
                        HighlightFocusedButton();
                    },
                    onComplete: () =>
                    {
                        HighlightFocusedButton();
                    });
            }
            else
            {
                ShowPage(prevArc, prevPage, selectInEventSystem: true);
                HighlightFocusedButton();
            }
        }

        /// <summary>
        /// Directly jumps to a specific arc and page.
        /// </summary>
        public void GoToArc(int arcIndex, int pageIndex = 0)
        {
            ShowPage(arcIndex, pageIndex, selectInEventSystem: true);
        }

        /// <summary>
        /// Automatically finds which arc and page contains the given level number and displays it.
        /// </summary>
        public void JumpToLevel(int levelNumber, bool selectInEventSystem = true)
        {
            InitializeProgression();
            BuildResolvedArcs();

            int minLevel = 1;
            int maxLevel = GetTotalLevelsAcrossAllArcs();
            levelNumber = Mathf.Clamp(levelNumber, minLevel, maxLevel);

            int targetArc = 0;
            int targetPage = 0;

            for (int a = 0; a < m_ResolvedArcs.Count; a++)
            {
                var arc = m_ResolvedArcs[a];
                int start = arc.StartLevelNumber;
                int end = start + arc.TotalLevels - 1;

                if (levelNumber >= start && levelNumber <= end)
                {
                    targetArc = a;
                    int offset = levelNumber - start;
                    targetPage = offset / m_LevelsPerPage;
                    break;
                }
            }

            m_FocusedLevelNumber = levelNumber;
            ShowPage(targetArc, targetPage, selectInEventSystem);
            HighlightFocusedButton();
        }

        /// <summary>
        /// Refreshes the currently displayed page.
        /// </summary>
        [ContextMenu("Refresh Grid")]
        public void Refresh()
        {
            ShowPage(m_CurrentArcIndex, m_CurrentPageIndex, selectInEventSystem: false);
        }

        #endregion

        #region Level Click Handling

        private void HandleLevelClicked(int levelNumber)
        {
            m_FocusedLevelNumber = levelNumber;
            OnLevelClicked?.Invoke(levelNumber);
        }

        #endregion
    }
}
