using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Level Selection Screen controller with procedural Map Network Activation entrance
    /// synchronized with the runtime level generator.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectionScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("References")]
        [Tooltip("Manager that generates the arc pages. Resolved from this object or its children when left empty.")]
        [SerializeField] private LevelSelection.LevelSelectionManager m_LevelSelectionManager;

        [Header("Animator Reference")]
        [SerializeField] private LevelSelectionScreenAnimator m_Animator;

        protected override void Awake()
        {
            base.Awake();

            if (m_LevelSelectionManager == null)
            {
                m_LevelSelectionManager = GetComponent<LevelSelection.LevelSelectionManager>();
            }
            if (m_LevelSelectionManager == null)
            {
                m_LevelSelectionManager = GetComponentInChildren<LevelSelection.LevelSelectionManager>(true);
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponent<LevelSelectionScreenAnimator>();
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<LevelSelectionScreenAnimator>(true);
            }
        }

        public override GameObject DefaultSelectedObject
        {
            get
            {
                GameObject selectTarget = m_LevelSelectionManager != null
                    ? m_LevelSelectionManager.GetCurrentUnlockedLevelNodeObject()
                    : null;

                return selectTarget != null ? selectTarget : base.DefaultSelectedObject;
            }
        }

        public override void Open()
        {
            base.Open();
        }

        public override void PlayEnterTransition(Action onComplete)
        {
            base.Open();

            if (m_LevelSelectionManager != null && m_Animator != null)
            {
                // 1. Synchronously set initial hidden state at frame 0 to prevent 1-frame glitches
                m_Animator.PrepareEntranceState();

                // 2. Synchronize entrance with runtime level generation completion
                Action<List<LevelSelection.LevelNodeUI>, List<LevelSelection.UIPathSegment>, int> onReadyHandler = null;
                onReadyHandler = (nodes, segments, highestUnlockedLevel) =>
                {
                    m_LevelSelectionManager.OnArcReady -= onReadyHandler;

                    // 3. Play the coordinated Map Network Activation cinematic
                    m_Animator.PlayMapEntrance(nodes, segments, highestUnlockedLevel, () =>
                    {
                        // 4. Restore EventSystem focus and unlock controller navigation only after marker settles
                        m_LevelSelectionManager.FocusCurrentLevelNode();
                        onComplete?.Invoke();
                    });
                };

                m_LevelSelectionManager.OnArcReady += onReadyHandler;
                m_LevelSelectionManager.RequestArcData(forceRegenerate: false);
            }
            else if (m_Animator != null)
            {
                m_Animator.PlayEntrance(onComplete);
            }
            else
            {
                if (m_LevelSelectionManager != null)
                {
                    m_LevelSelectionManager.InitializeAndFocusCurrentLevel();
                }
                onComplete?.Invoke();
            }
        }

        public override void PlayExitTransition(Action onComplete)
        {
            if (m_Animator != null)
            {
                var nodes = m_LevelSelectionManager != null ? m_LevelSelectionManager.LevelNodes : null;
                var segments = m_LevelSelectionManager != null ? m_LevelSelectionManager.PathSegments : null;

                m_Animator.PlayMapExit(nodes, segments, () =>
                {
                    Close();
                    onComplete?.Invoke();
                });
            }
            else
            {
                Close();
                onComplete?.Invoke();
            }
        }

        private void OnEnable()
        {
            if (m_BackButton != null) m_BackButton.onClick.AddListener(HandleBackClicked);
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        private void HandleBackClicked()
        {
            if (UINavigationManager.Instance != null && UINavigationManager.Instance.IsTransitioning)
            {
                return;
            }

            if (m_BackButton != null)
            {
                UIAnimatedButton animBtn = m_BackButton.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(() =>
                    {
                        if (UINavigationManager.Instance != null)
                        {
                            UINavigationManager.Instance.PopScreen();
                        }
                    });
                    return;
                }
            }

            AudioManager.Instance?.PlayButton();
            if (UINavigationManager.Instance != null)
            {
                UINavigationManager.Instance.PopScreen();
            }
        }
    }
}
