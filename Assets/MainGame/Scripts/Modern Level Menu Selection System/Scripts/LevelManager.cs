using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ModernLevelSelection
{
    /// <summary>
    /// Core manager responsible for level loading, completion, unlocking and UI refresh.
    /// Attach one instance to a scene object (no heavy singleton abuse).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour
    {
        #region Inspector

        [Header("References")]
        [Tooltip("Reference to the LevelGenerator in the scene.")]
        [SerializeField]
        private LevelGenerator _generator;

        private static int _index = 0;
        #endregion

        #region Events

        /// <summary>Invoked when a level is unlocked. Parameter = level number.</summary>
        public UnityEvent<int> OnLevelUnlocked = new UnityEvent<int>();

        /// <summary>Invoked when a level is completed. Parameter = level number.</summary>
        public UnityEvent<int> OnLevelCompleted = new UnityEvent<int>();

        /// <summary>Invoked when an unlocked level is clicked. Parameter = level number.</summary>
        public UnityEvent<int> OnLevelClicked = new UnityEvent<int>();

        #endregion

        #region Singleton

        private static LevelManager _instance;

        /// <summary>
        /// A safe global accessor for convenience. Avoid heavy reliance; prefer serialised references.
        /// </summary>
        public static LevelManager Instance => _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning("Multiple LevelManager instances detected. Keeping the first instance.");
                Destroy(gameObject);
                return;
            }
            // Auto-link generator if not assigned
            if (_generator == null)
            {
                _generator = GetComponent<LevelGenerator>();
            }

            if (_generator != null)
            {
                _generator.OnLevelClicked -= LoadLevel;
                _generator.OnLevelClicked += LoadLevel;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Loads the requested level scene by convention: "Level_{n}".
        /// </summary>
        public void LoadLevel(int levelNumber)
        {
            // Build Index 0 = Launcher splash
            // Build Index 1..N = the levels, in build order: Tutorial1-4 are levels 1-4,
            // then Level1-9 are levels 5-13.
            // The home screen sits at the end of the build list, so level number == build index.
            int buildIndex = levelNumber;

            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"Build Index {buildIndex} is not in Build Settings.");
                return;
            }

            Debug.Log($"Loading Level {levelNumber} (Build Index {buildIndex})");

            OnLevelClicked?.Invoke(levelNumber);
            SceneManager.LoadScene(buildIndex);
        }

        /// <summary>
        /// Complete the given level and assign stars. This will unlock the next playable level if applicable.
        /// </summary>
        public void CompleteLevel(int levelNumber, int stars)
        {
            SaveManager.SetStars(levelNumber, stars);
            SaveManager.SetCompleted(levelNumber);
            OnLevelCompleted?.Invoke(levelNumber);

            int highestPlayable = GetHighestPlayableLevelFromBuild(_index);
            if (levelNumber < highestPlayable)
            {
                int next = levelNumber + 1;
                if (SaveManager.GetHighestUnlocked() < next)
                {
                    SaveManager.SetHighestUnlocked(next);
                    OnLevelUnlocked?.Invoke(next);
                }
            }

            RefreshUI();
        }

        /// <summary>
        /// Unlocks the next playable level manually.
        /// </summary>
        public void UnlockNextLevel()
        {
            int highestPlayable = GetHighestPlayableLevelFromBuild(_index);
            int current = SaveManager.GetHighestUnlocked();
            if (current < highestPlayable)
            {
                int next = current + 1;
                SaveManager.SetHighestUnlocked(next);
                OnLevelUnlocked?.Invoke(next);
                RefreshUI();
            }
        }

        /// <summary>
        /// Resets progress via SaveManager.
        /// </summary>
        public void ResetProgress()
        {
            SaveManager.ResetProgress();
            RefreshUI();
        }

        /// <summary>
        /// Refresh UI state on the generator.
        /// </summary>
        public void RefreshUI()
        {
            _generator?.RefreshAll();
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Determine the highest playable level present in build settings with the "Level_{n}" naming convention.
        /// </summary>
        public static int GetHighestPlayableLevelFromBuild(int index)
        {
            // Build index 0 is the Launcher splash, which is not a level. Discount it so this
            // keeps returning the same count it did before the Launcher scene was added.
            const int launcherSceneCount = 1;
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings
                             - launcherSceneCount;

            // Everything from build index 1 up to the home screen at the end is a playable level.
            _index = index;
            return Mathf.Max(sceneCount - index, 0);
        }

        private static bool IsSceneInBuild(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        #endregion
    }
}
