using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LevelSelection
{
    /// <summary>
    /// Generates connecting lines between RectTransforms representing level nodes.
    /// Positions and scales them dynamically, preparing them for horizontal filling.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPathGenerator : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [Tooltip("The sequential list of level nodes in the S-curve path.")]
        [SerializeField] private RectTransform[] levelNodes;
        
        [Tooltip("Parent transform to hold the generated lines. Should be placed behind the level nodes in rendering order.")]
        [SerializeField] private RectTransform lineContainer;

        [Header("Line Settings")]
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private float lineThickness = 12f;

        #endregion

        #region Private Fields

        private readonly List<UIPathSegment> generatedSegments = new List<UIPathSegment>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates paths dynamically between the configured level nodes and returns the created segments.
        /// </summary>
        public List<UIPathSegment> GeneratePath()
        {
            // Clear any old generated lines
            if (lineContainer != null)
            {
                foreach (Transform child in lineContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            generatedSegments.Clear();

            if (levelNodes == null || levelNodes.Length < 2 || lineContainer == null || linePrefab == null)
            {
                return generatedSegments;
            }

            // Ensure lineContainer renders behind the level nodes
            if (levelNodes[0] != null && lineContainer.parent == levelNodes[0].parent)
            {
                int firstNodeIndex = levelNodes[0].GetSiblingIndex();
                int containerIndex = lineContainer.GetSiblingIndex();
                if (containerIndex > firstNodeIndex)
                {
                    lineContainer.SetSiblingIndex(firstNodeIndex);
                }
            }

            for (int i = 0; i < levelNodes.Length - 1; i++)
            {
                RectTransform startNode = levelNodes[i];
                RectTransform endNode = levelNodes[i + 1];

                if (startNode == null || endNode == null) continue;

                // 1. Instantiate the line prefab
                GameObject lineObj = Instantiate(linePrefab, lineContainer);
                lineObj.name = $"Line_Level_{i + 1}_to_{i + 2}";

                RectTransform lineRect = lineObj.GetComponent<RectTransform>();
                UIPathSegment segment = lineObj.GetComponent<UIPathSegment>();
                
                if (segment != null)
                {
                    segment.targetLevelIndex = i + 2; // Path leading to Level (i+2)
                    generatedSegments.Add(segment);
                }

                if (lineRect != null)
                {
                    // 2. Set Pivot to Left-Middle (0, 0.5) so it scales and rotates from the start node
                    lineRect.pivot = new Vector2(0f, 0.5f);
                    lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                    lineRect.anchorMax = new Vector2(0.5f, 0.5f);

                    // 3. Position the line at the start node's position
                    lineRect.anchoredPosition = startNode.anchoredPosition;

                    // 4. Calculate direction and distance between the nodes
                    Vector2 direction = endNode.anchoredPosition - startNode.anchoredPosition;
                    float distance = direction.magnitude;

                    // 5. Adjust the size of the line
                    lineRect.sizeDelta = new Vector2(distance, lineThickness);

                    // 6. Rotate the line to point at the end node
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    lineRect.localRotation = Quaternion.Euler(0, 0, angle);
                }

                // 7. Ensure Image is configured for horizontal filling from left-to-right
                Image img = lineObj.GetComponent<Image>();
                if (img != null)
                {
                    img.type = Image.Type.Filled;
                    img.fillMethod = Image.FillMethod.Horizontal;
                    img.fillOrigin = (int)Image.OriginHorizontal.Left;
                }
            }

            return generatedSegments;
        }

        #endregion
    }
}
