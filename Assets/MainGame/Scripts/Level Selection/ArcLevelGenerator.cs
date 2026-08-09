using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Spawns level nodes automatically in S-curve winding grids across multiple arcs/worlds,
    /// connects them, and ensures correct rendering order.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArcLevelGenerator : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Arc Configurations")]
        [SerializeField] private List<ArcData> arcs;

        [Header("Fallback Arc Config (If list is empty)")]
        public string arcName = "ARC 1";
        public int totalLevelsInArc = 15;
        public int startLevelNumber = 1;

        [Header("Fallback Grid Layout Settings")]
        [SerializeField] private int columns = 5;
        [SerializeField] private float horizontalSpacing = 200f;
        [SerializeField] private float verticalSpacing = 150f;
        [SerializeField] private Vector2 startOffset = new Vector2(-400f, 200f);

        [Header("Prefabs")]
        [SerializeField] private GameObject levelNodePrefab;
        [SerializeField] private GameObject linePrefab;

        [Header("Containers")]
        [SerializeField] private RectTransform nodesContainer;
        [SerializeField] private RectTransform linesContainer;

        [Header("Line Thickness")]
        [SerializeField] private float lineThickness = 12f;

        #endregion

        #region Private Fields

        private readonly List<LevelNodeUI> spawnedNodes = new List<LevelNodeUI>();
        private readonly List<UIPathSegment> generatedSegments = new List<UIPathSegment>();

        #endregion

        #region Properties

        public List<LevelNodeUI> SpawnedNodes => spawnedNodes;
        public List<UIPathSegment> GeneratedSegments => generatedSegments;

        #endregion

        #region Public Methods

        /// <summary>
        /// Instantiates the level nodes and path lines for all configured arcs.
        /// </summary>
        public void GenerateArc(int highestUnlockedLevel, int currentSelectedLevelIndex)
        {
            // Clear existing elements
            if (nodesContainer != null)
            {
                foreach (Transform child in nodesContainer) Destroy(child.gameObject);
            }
            if (linesContainer != null)
            {
                foreach (Transform child in linesContainer) Destroy(child.gameObject);
            }
            
            spawnedNodes.Clear();
            generatedSegments.Clear();

            if (levelNodePrefab == null || linePrefab == null || nodesContainer == null || linesContainer == null)
            {
                return;
            }

            // Ensure path lines are rendered behind the level nodes (first sibling = back, last sibling = front)
            linesContainer.SetAsFirstSibling();
            nodesContainer.SetAsLastSibling();

            // Prepare the list of arcs to generate
            List<ArcConfig> activeConfigs = new List<ArcConfig>();

            if (arcs != null && arcs.Count > 0)
            {
                int currentStartLevel = 1;
                foreach (var arcData in arcs)
                {
                    if (arcData == null) continue;
                    activeConfigs.Add(new ArcConfig
                    {
                        arcName = arcData.arcName,
                        totalLevelsInArc = arcData.totalLevelsInArc,
                        startLevelNumber = currentStartLevel,
                        columns = arcData.columns,
                        horizontalSpacing = arcData.horizontalSpacing,
                        verticalSpacing = arcData.verticalSpacing,
                        startOffset = arcData.startOffset
                    });
                    currentStartLevel += arcData.totalLevelsInArc;
                }
            }
            else
            {
                // Fallback to inspector fields
                activeConfigs.Add(new ArcConfig
                {
                    arcName = arcName,
                    totalLevelsInArc = totalLevelsInArc,
                    startLevelNumber = startLevelNumber,
                    columns = columns,
                    horizontalSpacing = horizontalSpacing,
                    verticalSpacing = verticalSpacing,
                    startOffset = startOffset
                });
            }

            LevelNodeUI lastNodeOfPreviousArc = null;

            for (int k = 0; k < activeConfigs.Count; k++)
            {
                var config = activeConfigs[k];
                List<LevelNodeUI> arcSpawnedNodes = new List<LevelNodeUI>();

                // 1. Spawn Level Nodes in S-curve layout
                for (int i = 0; i < config.totalLevelsInArc; i++)
                {
                    int levelNum = config.startLevelNumber + i;

                    // Calculate S-curve grid position
                    int row = i / config.columns;
                    int col = i % config.columns;

                    // Reverse direction on odd rows (winding S-curve layout)
                    if (row % 2 != 0)
                    {
                        col = (config.columns - 1) - col;
                    }

                    float posX = config.startOffset.x + (col * config.horizontalSpacing);
                    float posY = config.startOffset.y - (row * config.verticalSpacing);

                    GameObject nodeObj = Instantiate(levelNodePrefab, nodesContainer);
                    nodeObj.name = $"LevelNode_{levelNum}";

                    RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();
                    if (nodeRect != null)
                    {
                        nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                        nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                        nodeRect.pivot = new Vector2(0.5f, 0.5f);
                        nodeRect.anchoredPosition = new Vector2(posX, posY);
                    }

                    LevelNodeUI nodeScript = nodeObj.GetComponent<LevelNodeUI>();
                    if (nodeScript == null)
                    {
                        nodeScript = nodeObj.AddComponent<LevelNodeUI>();
                    }

                    nodeScript.levelNumber = levelNum;

                    // Apply progression state
                    bool isUnlocked = levelNum <= highestUnlockedLevel;
                    bool isCompleted = levelNum < highestUnlockedLevel;
                    bool isSelected = levelNum == currentSelectedLevelIndex;
                    nodeScript.SetupNode(isUnlocked, isCompleted, isSelected);

                    spawnedNodes.Add(nodeScript);
                    arcSpawnedNodes.Add(nodeScript);
                }

                // 2. Generate Paths between sequential nodes
                for (int i = 0; i < arcSpawnedNodes.Count - 1; i++)
                {
                    RectTransform startNode = arcSpawnedNodes[i].GetComponent<RectTransform>();
                    RectTransform endNode = arcSpawnedNodes[i + 1].GetComponent<RectTransform>();

                    if (startNode != null && endNode != null)
                    {
                        CreatePathSegment(startNode, endNode, arcSpawnedNodes[i + 1].levelNumber, highestUnlockedLevel);
                    }
                }

                // 3. Connect to previous Arc directly
                if (lastNodeOfPreviousArc != null && arcSpawnedNodes.Count > 0)
                {
                    RectTransform prevNodeRect = lastNodeOfPreviousArc.GetComponent<RectTransform>();
                    RectTransform currNodeRect = arcSpawnedNodes[0].GetComponent<RectTransform>();
                    if (prevNodeRect != null && currNodeRect != null)
                    {
                        CreatePathSegment(prevNodeRect, currNodeRect, arcSpawnedNodes[0].levelNumber, highestUnlockedLevel);
                    }
                }

                // 4. Connect entrance line only if config starts at level > 1 (e.g. not the absolute start of path)
                if (k == 0 && config.startLevelNumber > 1 && arcSpawnedNodes.Count > 0)
                {
                    RectTransform firstNode = arcSpawnedNodes[0].GetComponent<RectTransform>();
                    if (firstNode != null)
                    {
                        CreateEntranceLine(firstNode, highestUnlockedLevel, config.startLevelNumber);
                    }
                }

                // Cache last node to connect to next arc
                if (arcSpawnedNodes.Count > 0)
                {
                    lastNodeOfPreviousArc = arcSpawnedNodes[arcSpawnedNodes.Count - 1];
                }
            }

            // Create exit line for the last node of the final arc
            if (activeConfigs.Count > 0 && lastNodeOfPreviousArc != null)
            {
                var finalConfig = activeConfigs[activeConfigs.Count - 1];
                RectTransform lastNode = lastNodeOfPreviousArc.GetComponent<RectTransform>();
                if (lastNode != null)
                {
                    CreateExitLine(lastNode, highestUnlockedLevel, finalConfig.startLevelNumber, finalConfig.totalLevelsInArc);
                }
            }
        }

        #endregion

        #region Private Methods

        private void CreatePathSegment(RectTransform startNode, RectTransform endNode, int targetLevelNum, int highestUnlockedLevel)
        {
            GameObject lineObj = Instantiate(linePrefab, linesContainer);
            lineObj.transform.SetAsFirstSibling(); // Force line to render behind level nodes
            RectTransform lineRect = lineObj.GetComponent<RectTransform>();
            UIPathSegment segment = lineObj.GetComponent<UIPathSegment>();

            if (segment != null)
            {
                segment.targetLevelIndex = targetLevelNum;
                generatedSegments.Add(segment);
            }

            if (lineRect != null)
            {
                // Pivot at start node center
                lineRect.pivot = new Vector2(0f, 0.5f);
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = startNode.anchoredPosition;

                Vector2 direction = endNode.anchoredPosition - startNode.anchoredPosition;
                lineRect.sizeDelta = new Vector2(direction.magnitude, lineThickness);
                lineRect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }

            if (segment != null)
            {
                segment.SetFilled(targetLevelNum <= highestUnlockedLevel);
            }
        }

        private void CreateEntranceLine(RectTransform firstNode, int highestUnlockedLevel, int startLevelNum)
        {
            // Vertical entry line coming from top edge of screen down to first node
            GameObject lineObj = Instantiate(linePrefab, linesContainer);
            lineObj.transform.SetAsFirstSibling(); // Force line to render behind level nodes
            RectTransform lineRect = lineObj.GetComponent<RectTransform>();
            
            if (lineRect != null)
            {
                lineRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom-middle to draw upwards
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = firstNode.anchoredPosition;
                lineRect.sizeDelta = new Vector2(lineThickness, 200f);
                lineRect.localRotation = Quaternion.Euler(0, 0, 90f); // Pointing straight up
            }

            UIPathSegment segment = lineObj.GetComponent<UIPathSegment>();
            if (segment != null)
            {
                segment.targetLevelIndex = startLevelNum;
                segment.SetFilled(startLevelNum <= highestUnlockedLevel);
            }
        }

        private void CreateExitLine(RectTransform lastNode, int highestUnlockedLevel, int startLevelNum, int totalLevels)
        {
            // Vertical exit line going from last node down to bottom edge of screen
            GameObject lineObj = Instantiate(linePrefab, linesContainer);
            lineObj.transform.SetAsFirstSibling(); // Force line to render behind level nodes
            RectTransform lineRect = lineObj.GetComponent<RectTransform>();
            
            if (lineRect != null)
            {
                lineRect.pivot = new Vector2(0.5f, 0f); // Pivot at top-middle to draw downwards
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = lastNode.anchoredPosition;
                lineRect.sizeDelta = new Vector2(lineThickness, 200f);
                lineRect.localRotation = Quaternion.Euler(0, 0, -90f); // Pointing straight down
            }

            UIPathSegment segment = lineObj.GetComponent<UIPathSegment>();
            if (segment != null)
            {
                segment.targetLevelIndex = startLevelNum + totalLevels; // Unlocks when entering the next Arc
                segment.SetFilled(segment.targetLevelIndex <= highestUnlockedLevel);
            }
        }

        #endregion

        // Helper struct for holding generator settings per arc
        private struct ArcConfig
        {
            public string arcName;
            public int totalLevelsInArc;
            public int startLevelNumber;
            public int columns;
            public float horizontalSpacing;
            public float verticalSpacing;
            public Vector2 startOffset;
        }
    }
}
