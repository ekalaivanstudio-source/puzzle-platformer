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
        public const int FocusCurrentLevel = -1;

        /// <summary>Passed to <see cref="RefreshArcDisplay"/> to focus the first node of the arc.</summary>
        public const int FocusFirstNode = 0;

        /// <summary>Passed to <see cref="RefreshArcDisplay"/> to focus the last node of the arc.</summary>
        public const int FocusLastNode = int.MaxValue;

        #endregion

        #region Inspector Fields

        [Header("Arc Generator")]
        [SerializeField] private ArcLevelGenerator arcGenerator;

        [Header("Animator Reference")]
        [SerializeField] private LevelSelectionScreenAnimator animator;

        [Header("Animation Settings")]
        [SerializeField] private float fillDuration = 0.5f;

        [Header("Arc Navigation Buttons")]
        [SerializeField] private Button nextArcButton;
        [SerializeField] private Button prevArcButton;
        [SerializeField] private Image arcTitleImage;

        [Header("Global Input Settings")]
        [Tooltip("Reference to the input actions asset to listen to PageLeft / PageRight events.")]
        [SerializeField] private InputActionAsset m_UIInputActionAsset;

        [Header("Pointer Reference")]
        [SerializeField] private LevelSelectionPointer m_Pointer;
        [SerializeField] private Sprite pointerSprite;

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
        public bool IsTransitioning => m_IsTransitioning;
        public int CurrentArcIndex => currentArcIndex;
        public LevelSelectionPointer Pointer => m_Pointer;

        #endregion

        #region Private Fields

        private List<LevelNodeUI> levelNodes = new List<LevelNodeUI>();
        private List<UIPathSegment> pathSegments = new List<UIPathSegment>();
        private int currentArcIndex = 0;
        private InputAction m_PageLeftAction;
        private InputAction m_PageRightAction;
        private bool m_HasGeneratedArc;
        private bool m_IsTransitioning;
        private int m_CurrentGenerationId = 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<LevelSelectionScreenAnimator>();
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<LevelSelectionScreenAnimator>(true);
            }

            ResolveOrCreatePointer();

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
            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

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
        /// Resets the manager to point to the current unlocked level and its corresponding arc.
        /// Called when the level selection screen is opened.
        /// </summary>
        public void ResetToCurrentUnlockedLevel()
        {
            CancelActiveTransition();

            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
            int unlockedArc = (arcGenerator != null) ? arcGenerator.GetArcIndexForLevel(highestUnlockedLevel) : 0;

            if (currentArcIndex != unlockedArc)
            {
                currentArcIndex = unlockedArc;
                m_HasGeneratedArc = false;
            }

            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }
        }

        /// <summary>
        /// Requests arc data. If already generated and valid, notifies listeners immediately without recreating GameObjects.
        /// Otherwise, generates the arc and fires OnArcReady.
        /// </summary>
        public void RequestArcData(int targetArcIndex = -1, bool forceRegenerate = false)
        {
            if (arcGenerator == null) return;

            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
            int unlockedArc = arcGenerator.GetArcIndexForLevel(highestUnlockedLevel);

            if (targetArcIndex >= 0)
            {
                currentArcIndex = Mathf.Clamp(targetArcIndex, 0, arcGenerator.ArcCount - 1);
            }
            else
            {
                // When targetArcIndex is -1 (default entrance), always ensure we are on the unlocked level's arc
                if (currentArcIndex != unlockedArc)
                {
                    currentArcIndex = unlockedArc;
                    forceRegenerate = true;
                }
            }

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
        /// Overload for backwards compatibility.
        /// </summary>
        public void RequestArcData(bool forceRegenerate)
        {
            RequestArcData(-1, forceRegenerate);
        }

        /// <summary>
        /// Returns the header sprite of the active arc.
        /// </summary>
        public Sprite GetCurrentArcSprite()
        {
            return arcGenerator != null ? arcGenerator.GetArcSprite(currentArcIndex) : null;
        }

        /// <summary>
        /// Cancels any active transition and stops the animator.
        /// </summary>
        public void CancelActiveTransition()
        {
            if (m_IsTransitioning)
            {
                m_IsTransitioning = false;
                m_CurrentGenerationId++;
                if (m_Pointer != null)
                {
                    m_Pointer.ResetPointerState();
                }
                if (animator != null)
                {
                    animator.StopActiveAnimation();
                }
            }
        }

        /// <summary>
        /// Generates the arc nodes and sets selection focus directly on the player's highest unlocked level.
        /// </summary>
        public void InitializeAndFocusCurrentLevel()
        {
            RequestArcData(-1, forceRegenerate: false);
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
            return !m_IsTransitioning && arcGenerator != null && currentArcIndex < arcGenerator.ArcCount - 1;
        }

        public bool CanGoToPrevArc()
        {
            return !m_IsTransitioning && arcGenerator != null && currentArcIndex > 0;
        }

        public void GoToNextArc()
        {
            OnNextArcClicked();
        }

        public void GoToPrevArc()
        {
            OnPrevArcClicked();
        }

        /// <summary>
        /// Smoothly transitions to a different arc page, locking inputs, cancelling ongoing animations,
        /// generating new nodes/paths, and playing the coordinated arc transition.
        /// </summary>
        public void SwitchToArc(int newArcIndex, int focusTargetNodeIndex)
        {
            if (arcGenerator == null) return;
            if (m_IsTransitioning) return;
            if (newArcIndex < 0 || newArcIndex >= arcGenerator.ArcCount) return;
            if (newArcIndex == currentArcIndex && m_HasGeneratedArc) return;

            m_IsTransitioning = true;
            int generationId = ++m_CurrentGenerationId;

            // 1. Immediately disable paging buttons and input navigation to prevent re-entrancy
            if (prevArcButton != null) prevArcButton.interactable = false;
            if (nextArcButton != null) nextArcButton.interactable = false;

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = false;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            // 2. Cancel any running animation on the old arc
            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }
            if (animator != null)
            {
                animator.StopActiveAnimation();
            }

            // 3. Update current arc index and title sprite immediately
            currentArcIndex = newArcIndex;
            Sprite arcSprite = arcGenerator.GetArcSprite(currentArcIndex);
            if (arcTitleImage != null)
            {
                arcTitleImage.sprite = arcSprite;
            }

            // 4. Generate the new arc nodes and paths
            int highestUnlockedLevel = ModernLevelSelection.SaveManager.GetHighestUnlocked();
            arcGenerator.GenerateArc(currentArcIndex, highestUnlockedLevel, highestUnlockedLevel);
            m_HasGeneratedArc = true;

            levelNodes = arcGenerator.SpawnedNodes;
            pathSegments = arcGenerator.GeneratedSegments;

            // 5. Fire OnArcReady for any external listeners
            OnArcReady?.Invoke(levelNodes, pathSegments, highestUnlockedLevel);

            // 6. Play coordinated Arc Transition
            if (animator != null)
            {
                animator.PlayArcTransition(
                    currentArcIndex,
                    arcSprite,
                    levelNodes,
                    pathSegments,
                    highestUnlockedLevel,
                    () => OnArcTransitionComplete(generationId, focusTargetNodeIndex),
                    focusTargetNodeIndex);
            }
            else
            {
                OnArcTransitionComplete(generationId, focusTargetNodeIndex);
            }
        }

        private void OnArcTransitionComplete(int generationId, int focusTargetNodeIndex)
        {
            // If another generation started while this was animating, ignore this completion
            if (generationId != m_CurrentGenerationId) return;

            m_IsTransitioning = false;

            // Update navigation button states
            if (prevArcButton != null) prevArcButton.interactable = CanGoToPrevArc();
            if (nextArcButton != null) nextArcButton.interactable = CanGoToNextArc();

            // Re-enable navigation events
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents = true;
            }

            // Restore selection focus
            if (UINavigationManager.Instance != null && levelNodes.Count > 0)
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

            if (m_Pointer != null)
            {
                m_Pointer.ResetPointerState();
            }

            int generationId = ++m_CurrentGenerationId;
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
            if (m_IsTransitioning || !CanGoToNextArc()) return;
            SwitchToArc(currentArcIndex + 1, FocusFirstNode);
        }

        private void OnPrevArcClicked()
        {
            if (m_IsTransitioning || !CanGoToPrevArc()) return;
            SwitchToArc(currentArcIndex - 1, FocusLastNode);
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

        /// <summary>
        /// Called by a LevelNodeUI when selected via controller navigation or pointer hover.
        /// Directs the single authoritative pointer to fly to the newly selected node.
        /// </summary>
        public void OnNodeSelected(LevelNodeUI node)
        {
            if (m_IsTransitioning) return;

            if (m_Pointer != null)
            {
                m_Pointer.MoveToNode(node, 0.10f);
            }
        }

        /// <summary>
        /// Ensures exactly one LevelSelectionPointer instance exists and is properly configured.
        /// </summary>
        public void ResolveOrCreatePointer()
        {
            if (m_Pointer == null)
            {
                m_Pointer = GetComponentInChildren<LevelSelectionPointer>(true);
            }

            if (m_Pointer == null && transform.parent != null)
            {
                m_Pointer = transform.parent.GetComponentInChildren<LevelSelectionPointer>(true);
            }

            if (m_Pointer == null)
            {
                Transform parentTarget = null;
                if (arcGenerator != null && arcGenerator.NodesContainer != null)
                {
                    parentTarget = arcGenerator.NodesContainer.parent != null ? arcGenerator.NodesContainer.parent : arcGenerator.NodesContainer;
                }
                else
                {
                    parentTarget = transform;
                }

                if (parentTarget != null)
                {
                    Transform existing = parentTarget.Find("LevelSelectionPointer");
                    if (existing != null)
                    {
                        m_Pointer = existing.GetComponent<LevelSelectionPointer>();
                    }
                    else
                    {
                        GameObject pointerObj = new GameObject("LevelSelectionPointer", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LevelSelectionPointer));
                        pointerObj.transform.SetParent(parentTarget, false);
                        m_Pointer = pointerObj.GetComponent<LevelSelectionPointer>();
                    }
                }
            }

            if (m_Pointer != null)
            {
                m_Pointer.EnsureComponents();
                if (pointerSprite == null && arcGenerator != null && arcGenerator.LevelNodePrefab != null)
                {
                    var nodeImages = arcGenerator.LevelNodePrefab.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < nodeImages.Length; i++)
                    {
                        if (nodeImages[i].gameObject.name == "Arrow" && nodeImages[i].sprite != null)
                        {
                            pointerSprite = nodeImages[i].sprite;
                            break;
                        }
                    }
                }

                if (pointerSprite != null)
                {
                    m_Pointer.SetSprite(pointerSprite);
                }
                m_Pointer.ResetPointerState();
            }
        }

        #endregion
    }
}
