using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ModernLevelSelection
{
    /// <summary>
    /// Responsible for generating rows of levels inside a ScrollView content.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelGenerator : MonoBehaviour
    {
        #region Inspector

        [Header("Prefabs & References")]
        [Tooltip("Prefab representing a single line/row containing exactly four LevelButtonUI components.")]
        [SerializeField]
        private GameObject _linePrefab;

        [Tooltip("Content parent (the ScrollRect content) where lines will be instantiated.")]
        [SerializeField]
        private RectTransform _contentParent;

        [Tooltip("ScrollRect used to auto-scroll to current unlocked level.")]
        [SerializeField]
        private ScrollRect _scrollRect;

        [Header("Generation")]
        [Tooltip("Number of lines (rows) to generate. Each line contains 4 level buttons.")]
        [SerializeField]
        [Min(1)]
        private int _numberOfLines = 10;
        [Tooltip("If true, generation runs on Start.")]
        [SerializeField]
        private bool _generateOnStart = true;
        [Header("voidd Main scene")]
        [Tooltip("Number of Scenes to avoid. for show unlocked or locked levels.")]
        [SerializeField]
        private int avoidScenecount = 1;


        [SerializeField]
        private GameObject _continueButton;
        #endregion

        #region Events

        /// <summary>
        /// Event fired when a generated level button is clicked. Parameter = level number.
        /// </summary>
        public event Action<int> OnLevelClicked;

        #endregion

        #region State

        private readonly List<LevelButtonUI> _allButtons = new List<LevelButtonUI>(256);

        #endregion

        #region Unity

        private void Start()
        {
            if (_generateOnStart)
                Generate();
        }

        private void OnValidate()
        {
            if (_numberOfLines < 1) _numberOfLines = 1;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Remove previously generated items.
        /// </summary>
        public void Clear()
        {
            for (int i = _contentParent.childCount - 1; i >= 0; i--)
            {
                var child = _contentParent.GetChild(i).gameObject;
                DestroyImmediate(child);
            }
            _allButtons.Clear();
        }

        /// <summary>
        /// Generate lines and level buttons according to configuration.
        /// </summary>
        public void Generate()
        {
            if (_linePrefab == null) throw new InvalidOperationException("Line Prefab is not assigned.");
            if (_contentParent == null) throw new InvalidOperationException("Content Parent is not assigned.");

            Clear();

            int levelNumber = 1;
            int buttonsPerLine = 4;

            for (int line = 0; line < _numberOfLines; line++)
            {
                var go = Instantiate(_linePrefab, _contentParent, false);
                var buttons = new List<LevelButtonUI>(go.GetComponentsInChildren<LevelButtonUI>(true));
                if (buttons.Count != buttonsPerLine)
                {
                    Debug.LogWarning($"Line prefab expected {buttonsPerLine} LevelButtonUI components but found {buttons.Count}.");
                }

                // Sort by sibling index to ensure left-to-right order if necessary
                buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

                foreach (var btn in buttons)
                {
                    _allButtons.Add(btn);
                    // Attach event
                    btn.OnLevelClicked.RemoveAllListeners();
                    btn.OnLevelClicked.AddListener(HandleButtonClicked);
                    // Setup initial display; actual state will be refreshed by LevelManager
                    btn.Setup(levelNumber, LevelState.ComingSoon, SaveManager.GetStars(levelNumber), false);
                    levelNumber++;
                }
            }

            // After generation, refresh visuals immediately (works in Editor even without LevelManager instance)
            RefreshAll();
            // Auto-scroll to current highest unlocked
            AutoScrollToHighestUnlocked();
        }

        /// <summary>
        /// Refresh visuals for all buttons using save data and build settings.
        /// </summary>
        public void RefreshAll()
        {
            int total = _allButtons.Count;
            int highestUnlocked = SaveManager.GetHighestUnlocked();
            int maxPlayable = LevelManager.GetHighestPlayableLevelFromBuild(avoidScenecount);
            Debug.Log("Highest Unlocked : " + SaveManager.GetHighestUnlocked());
            Debug.Log("Highest Playable : " + LevelManager.GetHighestPlayableLevelFromBuild(avoidScenecount));
            // Show Continue button only if more than one level is unlocked.
            if (_continueButton != null)
            {
                if (SaveManager.GetHighestUnlocked() > 1)
                    _continueButton.SetActive(true);
                else
                    _continueButton.SetActive(false);
            }

            for (int i = 0; i < total; i++)
            {
                int levelNumber = i + 1;
                LevelState state;
                if (levelNumber > maxPlayable) state = LevelState.ComingSoon;
                else if (levelNumber <= highestUnlocked) state = LevelState.Unlocked;
                else state = LevelState.Locked;

                var btn = _allButtons[i];
                btn.Setup(levelNumber, state, SaveManager.GetStars(levelNumber), state == LevelState.Unlocked);
                btn.SetHighlight(levelNumber == highestUnlocked);
            }
        }

        /// <summary>
        /// Attempts to scroll the ScrollRect so the current highest unlocked level is visible and highlighted.
        /// </summary>
        public void AutoScrollToHighestUnlocked()
        {
            if (_scrollRect == null || _contentParent == null) return;
            int highestUnlocked = SaveManager.GetHighestUnlocked();
            int totalButtons = _allButtons.Count;
            if (totalButtons == 0) return;

            int index = Mathf.Clamp(highestUnlocked - 1, 0, totalButtons - 1);
            var button = _allButtons[index];
            // Calculate normalized position (vertical list assumed)
            var contentHeight = _contentParent.rect.height;
            var viewportHeight = _scrollRect.viewport.rect.height;
            if (contentHeight <= viewportHeight) return;

            var targetLocalPos = button.transform as RectTransform;
            if (targetLocalPos == null) return;

            // Convert button local position to content space anchored position
            float buttonCenterY = Mathf.Abs(targetLocalPos.anchoredPosition.y) + (targetLocalPos.rect.height * 0.5f);
            float normalized = Mathf.Clamp01(buttonCenterY / (contentHeight - viewportHeight));
            _scrollRect.verticalNormalizedPosition = 1f - normalized;
        }

        #endregion

        #region Private

        private void HandleButtonClicked(int level)
        {
             OnLevelClicked?.Invoke(level);

            Debug.Log("Level Button Clivked : level " + level);
        }

        #endregion
    }
}
