using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Coordinates path generation, node setup, progression unlocking, and multi-arc page switching.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Arc Generator")]
        [SerializeField] private ArcLevelGenerator arcGenerator;

        [Header("Animation Settings")]
        [SerializeField] private float fillDuration = 0.5f;

        [Header("Arc Navigation Buttons")]
        [SerializeField] private Button nextArcButton;
        [SerializeField] private Button prevArcButton;
        [SerializeField] private Image arcTitleImage;

        #endregion

        #region Private Fields

        private List<LevelNodeUI> levelNodes = new List<LevelNodeUI>();
        private List<UIPathSegment> pathSegments = new List<UIPathSegment>();
        private int currentArcIndex = 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Set up navigation listeners
            if (nextArcButton != null)
            {
                nextArcButton.onClick.AddListener(OnNextArcClicked);
            }
            if (prevArcButton != null)
            {
                prevArcButton.onClick.AddListener(OnPrevArcClicked);
            }

            if (arcGenerator != null)
            {
                int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
                
                // Initialize at the arc containing the highest unlocked level
                currentArcIndex = arcGenerator.GetArcIndexForLevel(highestUnlockedLevel);
                
                RefreshArcDisplay();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Call this function when the player successfully beats a level to animate the new path unlock.
        /// </summary>
        /// <param name="completedLevelIndex">1-based index of the completed level (e.g. 1 for Level 1).</param>
        public void UnlockNextLevel(int completedLevelIndex)
        {
            StartCoroutine(UnlockSequence(completedLevelIndex));
        }

        #endregion

        #region Private Methods

        private void RefreshArcDisplay()
        {
            if (arcGenerator == null) return;

            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();

            // 1. Generate the specific arc nodes and paths
            arcGenerator.GenerateArc(currentArcIndex, highestUnlockedLevel, highestUnlockedLevel);
            
            // 2. Fetch references to spawned UI elements
            levelNodes = arcGenerator.SpawnedNodes;
            pathSegments = arcGenerator.GeneratedSegments;

            // 3. Update navigation buttons
            if (prevArcButton != null)
            {
                prevArcButton.interactable = currentArcIndex > 0;
            }
            if (nextArcButton != null)
            {
                nextArcButton.interactable = currentArcIndex < arcGenerator.ArcCount - 1;
            }

            // 4. Update Arc Title image
            if (arcTitleImage != null)
            {
                arcTitleImage.sprite = arcGenerator.GetArcSprite(currentArcIndex);
            }
        }

        private void OnNextArcClicked()
        {
            if (arcGenerator != null && currentArcIndex < arcGenerator.ArcCount - 1)
            {
                currentArcIndex++;
                RefreshArcDisplay();
            }
        }

        private void OnPrevArcClicked()
        {
            if (arcGenerator != null && currentArcIndex > 0)
            {
                currentArcIndex--;
                RefreshArcDisplay();
            }
        }

        private IEnumerator UnlockSequence(int completedLevelIndex)
        {
            LevelNodeUI currentNode = null;
            LevelNodeUI nextNode = null;
            
            foreach (var node in levelNodes)
            {
                if (node != null)
                {
                    if (node.levelNumber == completedLevelIndex) currentNode = node;
                    if (node.levelNumber == completedLevelIndex + 1) nextNode = node;
                }
            }

            if (currentNode != null)
            {
                // 1. Set completed state on current level node
                currentNode.SetupNode(isUnlocked: true, isCompleted: true, isSelected: false);
                ModernLevelSelection.SaveManager.SetCompleted(completedLevelIndex);
            }

            // 2. Find and animate the path leading to the next level
            foreach (var segment in pathSegments)
            {
                if (segment != null && segment.targetLevelIndex == completedLevelIndex + 1)
                {
                    yield return StartCoroutine(segment.AnimateFill(fillDuration));
                    break;
                }
            }

            // 3. Set unlocked state on next level node and highlight it
            if (nextNode != null)
            {
                nextNode.SetupNode(isUnlocked: true, isCompleted: false, isSelected: true);
                
                // Save progress
                ModernLevelSelection.SaveManager.SetHighestUnlocked(completedLevelIndex + 1);
            }
        }

        #endregion
    }
}
