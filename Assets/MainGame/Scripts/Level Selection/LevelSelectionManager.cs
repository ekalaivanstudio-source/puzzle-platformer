using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LevelSelection
{
    /// <summary>
    /// Coordinates path generation, node setup, and progression unlocking.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Arc Generator")]
        [SerializeField] private ArcLevelGenerator arcGenerator;

        [Header("Animation Settings")]
        [SerializeField] private float fillDuration = 0.5f;

        #endregion

        #region Private Fields

        private List<LevelNodeUI> levelNodes = new List<LevelNodeUI>();
        private List<UIPathSegment> pathSegments = new List<UIPathSegment>();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (arcGenerator != null)
            {
                int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
                
                // 1. Generate the arc nodes and paths dynamically
                arcGenerator.GenerateArc(highestUnlockedLevel, highestUnlockedLevel);
                
                // 2. Fetch references to spawned UI elements
                levelNodes = arcGenerator.SpawnedNodes;
                pathSegments = arcGenerator.GeneratedSegments;
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
