using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MainGame.UI.Unified;

namespace LevelSelection
{
    /// <summary>
    /// Coordinates path generation, node setup, progression unlocking, and multi-arc page switching.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionManager : MonoBehaviour
    {
        #region Constants

        /// <summary>Passed to <see cref="RefreshArcDisplay"/> to focus the player's current level.</summary>
        private const int FocusCurrentLevel = -1;

        /// <summary>Passed to <see cref="RefreshArcDisplay"/> to focus the first node of the arc.</summary>
        private const int FocusFirstNode = 0;

        /// <summary>Passed to <see cref="RefreshArcDisplay"/> to focus the last node of the arc.</summary>
        private const int FocusLastNode = int.MaxValue;

        #endregion

        #region Inspector Fields

        [Header("Arc Generator")]
        [SerializeField] private ArcLevelGenerator arcGenerator;

        [Header("Animation Settings")]
        [SerializeField] private float fillDuration = 0.5f;

        [Header("Arc Navigation Buttons")]
        [SerializeField] private Button nextArcButton;
        [SerializeField] private Button prevArcButton;
        [SerializeField] private Image arcTitleImage;

        [Header("Global Input Settings")]
        [Tooltip("Reference to the input actions asset to listen to PageLeft / PageRight events.")]
        [SerializeField] private InputActionAsset m_UIInputActionAsset;

        #endregion

        #region Events

        /// <summary>
        /// Fired when an arc page has completed generation and is ready for animation: (nodes, pathSegments, highestUnlockedLevel).
        /// </summary>
        public event System.Action<List<LevelNodeUI>, List<UIPathSegment>, int> OnArcReady;

        #endregion

        #region Properties

        public List<LevelNodeUI> LevelNodes => levelNodes;
        public List<UIPathSegment> PathSegments => pathSegments;
        public bool HasGeneratedArc => m_HasGeneratedArc && levelNodes != null && levelNodes.Count > 0;

        #endregion

        #region Private Fields

        private List<LevelNodeUI> levelNodes = new List<LevelNodeUI>();
        private List<UIPathSegment> pathSegments = new List<UIPathSegment>();
        private int currentArcIndex = 0;
        private InputAction m_PageLeftAction;
        private InputAction m_PageRightAction;
        private bool m_HasGeneratedArc;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Resolve the paging actions once; enabling/disabling them follows this component's lifetime
            // so Q/E and the shoulder buttons only page arcs while the level selection screen is open.
            if (m_UIInputActionAsset != null)
            {
                InputActionMap uiMap = m_UIInputActionAsset.FindActionMap("UI", throwIfNotFound: false);
                if (uiMap != null)
                {
                    m_PageLeftAction = uiMap.FindAction("PageLeft", throwIfNotFound: false);
                    m_PageRightAction = uiMap.FindAction("PageRight", throwIfNotFound: false);
                }
            }
        }

        private void OnEnable()
        {
            if (nextArcButton != null) nextArcButton.onClick.AddListener(OnNextArcClicked);
            if (prevArcButton != null) prevArcButton.onClick.AddListener(OnPrevArcClicked);

            if (m_PageLeftAction != null)
            {
                m_PageLeftAction.performed += HandlePageLeft;
                m_PageLeftAction.Enable();
            }
            if (m_PageRightAction != null)
            {
                m_PageRightAction.performed += HandlePageRight;
                m_PageRightAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (nextArcButton != null) nextArcButton.onClick.RemoveListener(OnNextArcClicked);
            if (prevArcButton != null) prevArcButton.onClick.RemoveListener(OnPrevArcClicked);

            if (m_PageLeftAction != null)
            {
                m_PageLeftAction.performed -= HandlePageLeft;
                m_PageLeftAction.Disable();
            }
            if (m_PageRightAction != null)
            {
                m_PageRightAction.performed -= HandlePageRight;
                m_PageRightAction.Disable();
            }
        }

        private void Start()
        {
            // Fallback initial load for scenes that show this manager without going through
            // LevelSelectionScreen. When that screen drove Open() first, the arc already exists
            // and regenerating here would destroy the nodes the EventSystem just focused.
            if (!m_HasGeneratedArc)
            {
                InitializeAndFocusCurrentLevel();
            }
        }

        private void HandlePageLeft(InputAction.CallbackContext context)
        {
            OnPrevArcClicked();
        }

        private void HandlePageRight(InputAction.CallbackContext context)
        {
            OnNextArcClicked();
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

        /// <summary>
        /// Requests arc data. If already generated and valid, notifies listeners immediately without recreating GameObjects.
        /// Otherwise, generates the arc and fires OnArcReady.
        /// </summary>
        public void RequestArcData(bool forceRegenerate = false)
        {
            if (arcGenerator == null) return;

            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
            currentArcIndex = arcGenerator.GetArcIndexForLevel(highestUnlockedLevel);

            if (HasGeneratedArc && !forceRegenerate)
            {
                // Update navigation button states & title
                if (prevArcButton != null) prevArcButton.interactable = CanGoToPrevArc();
                if (nextArcButton != null) nextArcButton.interactable = CanGoToNextArc();
                if (arcTitleImage != null) arcTitleImage.sprite = arcGenerator.GetArcSprite(currentArcIndex);

                OnArcReady?.Invoke(levelNodes, pathSegments, highestUnlockedLevel);
            }
            else
            {
                RefreshArcDisplay(FocusCurrentLevel, autoFocus: false);
            }
        }

        /// <summary>
        /// Generates the arc nodes and sets selection focus directly on the player's highest unlocked level.
        /// </summary>
        public void InitializeAndFocusCurrentLevel()
        {
            RequestArcData(forceRegenerate: false);
        }

        /// <summary>
        /// Restores controller/keyboard selection focus to the current unlocked level node.
        /// Must only be called after the entrance map construction animation has finished!
        /// </summary>
        public void FocusCurrentLevelNode()
        {
            if (UINavigationManager.Instance == null || levelNodes == null || levelNodes.Count == 0) return;

            GameObject selectTarget = GetCurrentUnlockedLevelNodeObject();
            if (selectTarget != null)
            {
                UINavigationManager.Instance.RestoreSelectedElement(selectTarget);
            }
        }

        /// <summary>
        /// Returns the spawned node for the player's highest unlocked level, falling back to the first
        /// node of the current arc. Returns null when no arc has been generated yet.
        /// </summary>
        public GameObject GetCurrentUnlockedLevelNodeObject()
        {
            LevelNodeUI node = FindNodeForLevel(ModernLevelSelection.SaveManager.GetHighestUnlocked());
            if (node != null) return node.gameObject;

            return levelNodes.Count > 0 && levelNodes[0] != null ? levelNodes[0].gameObject : null;
        }

        public bool IsFirstLevelOfCurrentArc(int levelNum)
        {
            if (levelNodes.Count == 0 || levelNodes[0] == null) return false;
            return levelNum == levelNodes[0].levelNumber;
        }

        public bool IsLastLevelOfCurrentArc(int levelNum)
        {
            if (levelNodes.Count == 0) return false;
            LevelNodeUI last = levelNodes[levelNodes.Count - 1];
            return last != null && levelNum == last.levelNumber;
        }

        public bool CanGoToNextArc()
        {
            return arcGenerator != null && currentArcIndex < arcGenerator.ArcCount - 1;
        }

        public bool CanGoToPrevArc()
        {
            return arcGenerator != null && currentArcIndex > 0;
        }

        public void GoToNextArc()
        {
            OnNextArcClicked();
        }

        public void GoToPrevArc()
        {
            OnPrevArcClicked();
        }

        #endregion

        #region Private Methods

        private LevelNodeUI FindNodeForLevel(int levelNumber)
        {
            for (int i = 0; i < levelNodes.Count; i++)
            {
                LevelNodeUI node = levelNodes[i];
                if (node != null && node.levelNumber == levelNumber)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Rebuilds the current arc page and fires OnArcReady. Focus is only set if autoFocus is true.
        /// </summary>
        private void RefreshArcDisplay(int focusTargetNodeIndex = FocusCurrentLevel, bool autoFocus = false)
        {
            if (arcGenerator == null) return;

            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();

            // 1. Generate the specific arc nodes and paths
            arcGenerator.GenerateArc(currentArcIndex, highestUnlockedLevel, highestUnlockedLevel);
            m_HasGeneratedArc = true;

            // 2. Fetch references to spawned UI elements
            levelNodes = arcGenerator.SpawnedNodes;
            pathSegments = arcGenerator.GeneratedSegments;

            // 3. Update navigation buttons
            if (prevArcButton != null)
            {
                prevArcButton.interactable = CanGoToPrevArc();
            }
            if (nextArcButton != null)
            {
                nextArcButton.interactable = CanGoToNextArc();
            }

            // 4. Update Arc Title image
            if (arcTitleImage != null)
            {
                arcTitleImage.sprite = arcGenerator.GetArcSprite(currentArcIndex);
            }

            // 5. Fire completion callback for animation synchronization
            OnArcReady?.Invoke(levelNodes, pathSegments, highestUnlockedLevel);

            // 6. Restore EventSystem focus only when explicitly requested (e.g. manual page change)
            if (autoFocus && UINavigationManager.Instance != null && levelNodes.Count > 0)
            {
                GameObject selectTarget;
                if (focusTargetNodeIndex == FocusCurrentLevel)
                {
                    selectTarget = GetCurrentUnlockedLevelNodeObject();
                }
                else
                {
                    int targetIdx = Mathf.Clamp(focusTargetNodeIndex, 0, levelNodes.Count - 1);
                    LevelNodeUI target = levelNodes[targetIdx];
                    selectTarget = target != null ? target.gameObject : null;
                }

                if (selectTarget != null)
                {
                    UINavigationManager.Instance.RestoreSelectedElement(selectTarget);
                }
            }
        }

        private void OnNextArcClicked()
        {
            if (!CanGoToNextArc()) return;

            currentArcIndex++;
            RefreshArcDisplay(FocusFirstNode, autoFocus: true);
        }

        private void OnPrevArcClicked()
        {
            if (!CanGoToPrevArc()) return;

            currentArcIndex--;
            RefreshArcDisplay(FocusLastNode, autoFocus: true);
        }

        private IEnumerator UnlockSequence(int completedLevelIndex)
        {
            LevelNodeUI currentNode = FindNodeForLevel(completedLevelIndex);
            LevelNodeUI nextNode = FindNodeForLevel(completedLevelIndex + 1);

            if (currentNode != null)
            {
                // 1. Set completed state on current level node
                currentNode.SetupNode(isUnlocked: true, isCompleted: true, isSelected: false);
                ModernLevelSelection.SaveManager.SetCompleted(completedLevelIndex);
            }

            // 2. Find and animate the path leading to the next level
            for (int i = 0; i < pathSegments.Count; i++)
            {
                UIPathSegment segment = pathSegments[i];
                if (segment != null && segment.targetLevelIndex == completedLevelIndex + 1)
                {
                    yield return StartCoroutine(segment.AnimateFill(fillDuration));
                    break;
                }
            }

            // 3. Save progress and highlight the newly unlocked level.
            // Saved unconditionally: the next level may live on the following arc page and have no
            // spawned node here, and progress must persist either way.
            ModernLevelSelection.SaveManager.SetHighestUnlocked(completedLevelIndex + 1);

            if (nextNode != null)
            {
                nextNode.SetupNode(isUnlocked: true, isCompleted: false, isSelected: true);
            }
        }

        #endregion
    }
}
