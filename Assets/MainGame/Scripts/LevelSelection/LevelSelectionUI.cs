using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LevelSelectionSystem
{
    /// <summary>
    /// The level-select screen controller. It is the "wiring" layer that brings the pieces
    /// together: it reads the <see cref="LevelDatabase"/>, asks <see cref="LevelManager"/>
    /// for unlock state, pulls saved progress from <see cref="SaveManager"/>, and spawns one
    /// <see cref="LevelButtonUI"/> per level into a scrollable grid.
    ///
    /// It never hard-codes a level count and never needs buttons placed by hand — the grid is
    /// generated entirely from data, so the same screen serves 10 or 500+ levels unchanged.
    ///
    /// Drop this on the screen root, assign the references, and it does the rest.
    /// </summary>
    public class LevelSelectionUI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("The database of every level. The single source of truth for the grid.")]
        [SerializeField] private LevelDatabase m_Database;

        [Header("Spawning")]
        [Tooltip("Prefab with a LevelButtonUI on it. One instance is created per level.")]
        [SerializeField] private LevelButtonUI m_LevelButtonPrefab;

        [Tooltip("The Scroll View's Content object (must have a GridLayoutGroup). Buttons spawn here.")]
        [SerializeField] private RectTransform m_Content;

        [Tooltip("The ScrollRect, used for auto-scrolling to the latest unlocked level.")]
        [SerializeField] private ScrollRect m_ScrollRect;

        [Header("Grid Layout (applied to Content's GridLayoutGroup)")]
        [Tooltip("Number of columns in the grid.")]
        [SerializeField] private int m_Columns = 3;
        [SerializeField] private Vector2 m_CellSize = new Vector2(180f, 180f);
        [SerializeField] private Vector2 m_Spacing = new Vector2(20f, 20f);

        // NOTE: RectOffset is a class — it must NOT be created in a field initializer
        // (that runs in the constructor, which Unity forbids). Defaulted in Reset() instead.
        [Tooltip("Inner padding of the grid (left, right, top, bottom).")]
        [SerializeField] private RectOffset m_Padding;

        [Header("Enhancements")]
        [Tooltip("Tint and emphasise the highest unlocked level so the player sees where to go next.")]
        [SerializeField] private bool m_HighlightLatestLevel = true;

        [Tooltip("On open, scroll the view so the latest unlocked level is in view.")]
        [SerializeField] private bool m_ScrollToLatestLevel = true;

        private readonly List<LevelButtonUI> m_SpawnedButtons = new List<LevelButtonUI>();

        // Editor-only: seeds a sensible default padding when the component is first added.
        // Safe here (Reset is not a constructor), unlike a field initializer.
        private void Reset()
        {
            m_Padding = new RectOffset(20, 20, 20, 20);
        }

        private void Awake()
        {
            // Register the database for the whole system and seed first-run state
            // (only the first level unlocked).
            LevelManager.Configure(m_Database);
            ApplyGridSettings();
        }

        private void OnEnable()
        {
            // Auto-refresh whenever progress is written anywhere (e.g. returning from a level).
            SaveManager.OnProgressChanged += Refresh;
        }

        private void OnDisable()
        {
            SaveManager.OnProgressChanged -= Refresh;
        }

        private void Start()
        {
            Build();

            if (m_ScrollToLatestLevel)
                StartCoroutine(ScrollToLatestLevelNextFrame());
        }

        /// <summary>Pushes the Inspector grid settings onto the Content's GridLayoutGroup.</summary>
        private void ApplyGridSettings()
        {
            if (m_Content == null) return;

            var grid = m_Content.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                Debug.LogError("[LevelSelectionUI] Content needs a GridLayoutGroup component.", this);
                return;
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, m_Columns);
            grid.cellSize = m_CellSize;
            grid.spacing = m_Spacing;
            // m_Padding may be null on a component added purely via script; fall back safely.
            grid.padding = m_Padding ?? new RectOffset(20, 20, 20, 20);
        }

        /// <summary>
        /// Creates one button per level. Called once on open; for refreshes after progress
        /// changes we reuse the existing buttons via <see cref="Refresh"/> instead of rebuilding.
        /// </summary>
        private void Build()
        {
            if (!ValidateReferences()) return;

            ClearButtons();

            for (int i = 0; i < m_Database.Count; i++)
            {
                LevelData data = m_Database.GetByIndex(i);
                if (data == null) continue;

                LevelButtonUI button = Instantiate(m_LevelButtonPrefab, m_Content);
                button.name = $"LevelButton_{data.LevelId}";
                m_SpawnedButtons.Add(button);
            }

            Refresh();
        }

        /// <summary>
        /// Re-paints every spawned button from current save state. Cheap (no instantiation),
        /// so it is safe to call on every progress change.
        /// </summary>
        public void Refresh()
        {
            if (m_Database == null) return;

            int highestUnlockedId = SaveManager.HighestUnlockedLevelId;

            for (int i = 0; i < m_SpawnedButtons.Count; i++)
            {
                LevelData data = m_Database.GetByIndex(i);
                LevelButtonUI button = m_SpawnedButtons[i];
                if (data == null || button == null) continue;

                bool unlocked = LevelManager.IsLevelUnlocked(m_Database, data.LevelId);
                LevelProgress progress = SaveManager.GetProgress(data.LevelId);

                button.Setup(data, progress, unlocked, OnLevelClicked);

                if (m_HighlightLatestLevel)
                    button.SetHighlighted(unlocked && data.LevelId == highestUnlockedId);
            }
        }

        /// <summary>
        /// Click handler for an unlocked tile. Loads the scene named in the level's data.
        /// Override <see cref="LoadLevelScene"/> to swap in Addressables / async loading.
        /// </summary>
        private void OnLevelClicked(LevelData data)
        {
            if (data == null) return;

            // The button itself blocks clicks on locked levels, so reaching here means unlocked.
            AudioManager.Instance?.PlayButton();
            LoadLevelScene(data);
        }

        /// <summary>
        /// Single, overridable scene-load seam. Default uses the built-in SceneManager.
        /// For Addressables, subclass this controller and load <paramref name="data"/>.SceneName
        /// (or an Addressable key you add to LevelData) here instead.
        /// </summary>
        protected virtual void LoadLevelScene(LevelData data)
        {
            if (string.IsNullOrEmpty(data.SceneName))
            {
                Debug.LogError($"[LevelSelectionUI] Level {data.LevelId} has no SceneName set.", this);
                return;
            }
            SceneManager.LoadScene(data.SceneName);
        }

        private bool ValidateReferences()
        {
            if (m_Database == null) { Debug.LogError("[LevelSelectionUI] Database not assigned.", this); return false; }
            if (m_LevelButtonPrefab == null) { Debug.LogError("[LevelSelectionUI] Button prefab not assigned.", this); return false; }
            if (m_Content == null) { Debug.LogError("[LevelSelectionUI] Content not assigned.", this); return false; }
            return true;
        }

        private void ClearButtons()
        {
            for (int i = 0; i < m_SpawnedButtons.Count; i++)
            {
                if (m_SpawnedButtons[i] != null) Destroy(m_SpawnedButtons[i].gameObject);
            }
            m_SpawnedButtons.Clear();
        }

        // ─── Auto-scroll ──────────────────────────────────────────────────────────

        /// <summary>
        /// Waits one frame so the GridLayoutGroup has positioned every cell, then scrolls the
        /// view so the highest unlocked level is centred vertically.
        /// </summary>
        private IEnumerator ScrollToLatestLevelNextFrame()
        {
            yield return null; // let layout settle
            Canvas.ForceUpdateCanvases();

            int index = m_Database.IndexOf(SaveManager.HighestUnlockedLevelId);
            if (index < 0 || index >= m_SpawnedButtons.Count || m_ScrollRect == null) yield break;

            ScrollToButton(m_SpawnedButtons[index]);
        }

        /// <summary>Scrolls the ScrollRect so the given button's row is roughly centred.</summary>
        private void ScrollToButton(LevelButtonUI button)
        {
            if (button == null || m_ScrollRect == null || m_Content == null) return;

            RectTransform viewport = m_ScrollRect.viewport != null ? m_ScrollRect.viewport : m_ScrollRect.GetComponent<RectTransform>();
            float contentHeight = m_Content.rect.height;
            float viewportHeight = viewport.rect.height;

            // No scrolling possible if everything already fits.
            if (contentHeight <= viewportHeight) return;

            // Distance of the target from the top of the content (anchored Y is negative going down).
            float targetY = -((RectTransform)button.transform).anchoredPosition.y;

            // Centre the target in the viewport, then convert to a 0..1 normalized position.
            float scrollableHeight = contentHeight - viewportHeight;
            float desiredTop = Mathf.Clamp(targetY - viewportHeight * 0.5f, 0f, scrollableHeight);

            // verticalNormalizedPosition: 1 = top, 0 = bottom.
            m_ScrollRect.verticalNormalizedPosition = 1f - (desiredTop / scrollableHeight);
        }
    }
}
