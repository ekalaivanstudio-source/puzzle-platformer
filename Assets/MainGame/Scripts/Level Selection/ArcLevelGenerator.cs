using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Spawns level nodes automatically in S-curve winding grids across multiple arcs/worlds,
    /// connects them, and ensures correct rendering order. Supports generating a single active arc page.
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

        public int ArcCount => (arcs != null && arcs.Count > 0) ? arcs.Count : 1;

        #endregion

        #region Public Methods

        /// <summary>
        /// Instantiates the level nodes and path lines for a specific arc index.
        /// </summary>
        public void GenerateArc(int arcIndex, int highestUnlockedLevel, int currentSelectedLevelIndex, Button prevArcButton, Button nextArcButton)
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

            // 1. Get the target arc config
            ArcConfig config = new ArcConfig();
            bool hasPreviousArc = false;
            bool hasNextArc = false;

            if (arcs != null && arcs.Count > 0)
            {
                if (arcIndex < 0 || arcIndex >= arcs.Count) arcIndex = 0;
                
                // Calculate start level number by summing up previous arcs
                int startLevel = 1;
                for (int i = 0; i < arcIndex; i++)
                {
                    startLevel += arcs[i].totalLevelsInArc;
                }

                var arcData = arcs[arcIndex];
                config.arcName = arcData.arcName;
                config.totalLevelsInArc = arcData.totalLevelsInArc;
                config.startLevelNumber = startLevel;
                config.columns = arcData.columns;
                config.horizontalSpacing = arcData.horizontalSpacing;
                config.verticalSpacing = arcData.verticalSpacing;
                config.startOffset = arcData.startOffset;

                hasPreviousArc = arcIndex > 0;
                hasNextArc = arcIndex < arcs.Count - 1;
            }
            else
            {
                // Fallback to inspector fields
                config.arcName = arcName;
                config.totalLevelsInArc = totalLevelsInArc;
                config.startLevelNumber = startLevelNumber;
                config.columns = columns;
                config.horizontalSpacing = horizontalSpacing;
                config.verticalSpacing = verticalSpacing;
                config.startOffset = startOffset;

                hasPreviousArc = config.startLevelNumber > 1;
                hasNextArc = false; // single arc fallback has no next arc
            }

            // 2. Spawn Level Nodes in S-curve layout
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
            }

            // 3. Generate Paths between sequential nodes
            for (int i = 0; i < spawnedNodes.Count - 1; i++)
            {
                RectTransform startNode = spawnedNodes[i].GetComponent<RectTransform>();
                RectTransform endNode = spawnedNodes[i + 1].GetComponent<RectTransform>();

                if (startNode != null && endNode != null)
                {
                    CreatePathSegment(startNode, endNode, spawnedNodes[i + 1].levelNumber, highestUnlockedLevel);
                }
            }

            // 4. Connect entrance line if there is a previous arc
            if (hasPreviousArc && spawnedNodes.Count > 0)
            {
                RectTransform firstNode = spawnedNodes[0].GetComponent<RectTransform>();
                if (firstNode != null)
                {
                    CreateEntranceLine(firstNode, highestUnlockedLevel, config.startLevelNumber);
                }
            }

            // 5. Create exit line if there is a next arc
            if (hasNextArc && spawnedNodes.Count > 0)
            {
                RectTransform lastNode = spawnedNodes[spawnedNodes.Count - 1].GetComponent<RectTransform>();
                if (lastNode != null)
                {
                    CreateExitLine(lastNode, highestUnlockedLevel, config.startLevelNumber, config.totalLevelsInArc);
                }
            }

            // 6. Build Winding S-Curve Explicit Button Navigation links
            BuildLevelButtonNavigation(config.columns, prevArcButton, nextArcButton);
        }

        /// <summary>
        /// Explicitly binds the UI buttons in a winding snake navigation mesh (S-curve path).
        /// </summary>
        private void BuildLevelButtonNavigation(int columnsCount, Button prevArcButton, Button nextArcButton)
        {
            int total = spawnedNodes.Count;
            if (total <= 1) return;

            for (int i = 0; i < total; i++)
            {
                Button btn = spawnedNodes[i].GetComponent<Button>();
                if (btn == null)
                {
                    btn = spawnedNodes[i].GetComponentInChildren<Button>();
                }
                if (btn == null) continue;

                Navigation nav = btn.navigation;
                nav.mode = Navigation.Mode.Explicit;

                int row = i / columnsCount;
                int col = i % columnsCount;
                bool isOddRow = (row % 2 != 0);

                // --- HORIZONTAL (LEFT / RIGHT) MOVEMENT ---
                // Right Arrow always progresses to the next level (index i + 1)
                // Left Arrow always reverts to the previous level (index i - 1)
                Button prevSequential = (i > 0) ? GetButton(spawnedNodes[i - 1]) : null;
                Button nextSequential = (i < total - 1) ? GetButton(spawnedNodes[i + 1]) : null;

                nav.selectOnLeft = prevSequential;
                nav.selectOnRight = nextSequential;

                // --- VERTICAL (UP / DOWN) MOVEMENT ---
                // Removed all vertical bridges so player can only navigate horizontally.
                nav.selectOnUp = null;
                nav.selectOnDown = null;

                // --- BOUNDARY ARC CONNECTIONS ---
                // Left on first node and Right on last node will stay on the node (selectOnLeft/Right are set to null/prev/next sequential).

                btn.navigation = nav;
            }

            Debug.Log($"[ArcLevelGenerator] Winding navigation built for {total} level nodes.");
        }

        private Button GetButton(LevelNodeUI node)
        {
            if (node == null) return null;
            Button b = node.GetComponent<Button>();
            return b != null ? b : node.GetComponentInChildren<Button>();
        }

        /// <summary>
        /// Returns which arc index contains the given level number.
        /// </summary>
        public int GetArcIndexForLevel(int levelNumber)
        {
            if (arcs == null || arcs.Count == 0) return 0;
            int startLevel = 1;
            for (int i = 0; i < arcs.Count; i++)
            {
                if (levelNumber >= startLevel && levelNumber < startLevel + arcs[i].totalLevelsInArc)
                {
                    return i;
                }
                startLevel += arcs[i].totalLevelsInArc;
            }
            return arcs.Count - 1; // Default to last arc if beyond
        }

        /// <summary>
        /// Returns the name of the arc at the given index.
        /// </summary>
        public string GetArcName(int arcIndex)
        {
            if (arcs != null && arcIndex >= 0 && arcIndex < arcs.Count)
            {
                return arcs[arcIndex].arcName;
            }
            return arcName;
        }

        /// <summary>
        /// Returns the custom header sprite of the arc at the given index.
        /// </summary>
        public Sprite GetArcSprite(int arcIndex)
        {
            if (arcs != null && arcIndex >= 0 && arcIndex < arcs.Count)
            {
                return arcs[arcIndex].arcTitleSprite;
            }
            return null;
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
                lineRect.pivot = new Vector2(0f, 0.5f); // Pivot at start to draw outwards
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = firstNode.anchoredPosition;
                lineRect.sizeDelta = new Vector2(200f, lineThickness);
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
                lineRect.pivot = new Vector2(0f, 0.5f); // Pivot at start to draw outwards
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = lastNode.anchoredPosition;
                lineRect.sizeDelta = new Vector2(200f, lineThickness);
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
