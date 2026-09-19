using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UI.Animation
{
    /// <summary>
    /// Coordinates entrance and exit transitions across a group of UIAnimatedElement instances
    /// with customizable stagger delays and direction profiles.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAnimationGroup : MonoBehaviour
    {
        [Header("Elements")]
        [Tooltip("Animated elements in sequential order. If empty, collected from children on Awake.")]
        [SerializeField] private UIAnimatedElement[] m_Elements;

        [Header("Default Profiles")]
        [SerializeField] private UITransitionProfile m_EnterProfile = UITransitionProfile.DefaultEnter;
        [SerializeField] private UITransitionProfile m_ExitProfile = UITransitionProfile.DefaultExit;

        private Coroutine m_GroupRoutine;

        public UIAnimatedElement[] Elements => m_Elements;

        private void Awake()
        {
            EnsureElements();
        }

        public void EnsureElements()
        {
            if (m_Elements == null || m_Elements.Length == 0)
            {
                m_Elements = GetComponentsInChildren<UIAnimatedElement>(true);
            }
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            EnsureElements();

            if (m_Elements == null) return;
            for (int i = 0; i < m_Elements.Length; i++)
            {
                if (m_Elements[i] != null)
                {
                    m_Elements[i].ResetToRestState();
                }
            }
        }

        public Coroutine PlayEnter(UITransitionProfile? customProfile = null, float staggerOverride = -1f, Action onComplete = null)
        {
            StopActiveAnimation();
            EnsureElements();

            UITransitionProfile profile = customProfile ?? m_EnterProfile;
            float stagger = staggerOverride >= 0f ? staggerOverride : profile.Stagger;

            m_GroupRoutine = StartCoroutine(GroupRoutine(profile, stagger, isEnter: true, onComplete));
            return m_GroupRoutine;
        }

        public Coroutine PlayExit(UITransitionProfile? customProfile = null, float staggerOverride = -1f, Action onComplete = null)
        {
            StopActiveAnimation();
            EnsureElements();

            UITransitionProfile profile = customProfile ?? m_ExitProfile;
            float stagger = staggerOverride >= 0f ? staggerOverride : profile.Stagger;

            m_GroupRoutine = StartCoroutine(GroupRoutine(profile, stagger, isEnter: false, onComplete));
            return m_GroupRoutine;
        }

        public void StopActiveAnimation()
        {
            if (m_GroupRoutine != null)
            {
                StopCoroutine(m_GroupRoutine);
                m_GroupRoutine = null;
            }

            if (m_Elements != null)
            {
                for (int i = 0; i < m_Elements.Length; i++)
                {
                    if (m_Elements[i] != null)
                    {
                        m_Elements[i].StopActiveAnimation();
                    }
                }
            }
        }

        private IEnumerator GroupRoutine(UITransitionProfile profile, float stagger, bool isEnter, Action onComplete)
        {
            if (m_Elements == null || m_Elements.Length == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            int completedCount = 0;
            int total = m_Elements.Length;

            for (int i = 0; i < total; i++)
            {
                UIAnimatedElement element = m_Elements[i];
                if (element == null)
                {
                    completedCount++;
                    continue;
                }

                float extraDelay = i * stagger;
                if (isEnter)
                {
                    element.PlayEnter(profile, extraDelay, () => completedCount++);
                }
                else
                {
                    element.PlayExit(profile, extraDelay, () => completedCount++);
                }
            }

            // Wait until all elements finish their individual motions
            while (completedCount < total)
            {
                yield return null;
            }

            m_GroupRoutine = null;
            onComplete?.Invoke();
        }

        private void OnDisable()
        {
            StopActiveAnimation();
        }
    }
}
