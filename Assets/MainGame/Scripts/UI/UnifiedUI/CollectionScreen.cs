using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Collection Screen controller with physical card cascade and controller navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectionScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("Selectable Cards (In Left-to-Right Order)")]
        [SerializeField] private Selectable[] m_CharacterCards;

        [Header("Animator Reference")]
        [SerializeField] private CollectionScreenAnimator m_Animator;

        public override GameObject DefaultSelectedObject
        {
            get
            {
                if (m_CharacterCards != null && m_CharacterCards.Length > 0 && m_CharacterCards[0] != null && m_CharacterCards[0].gameObject.activeInHierarchy)
                {
                    return m_CharacterCards[0].gameObject;
                }
                return m_BackButton != null ? m_BackButton.gameObject : base.DefaultSelectedObject;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (m_Animator == null)
            {
                m_Animator = GetComponent<CollectionScreenAnimator>();
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<CollectionScreenAnimator>(true);
            }

            AutoFindCardsIfEmpty();
        }

        private void AutoFindCardsIfEmpty()
        {
            if (m_CharacterCards == null || m_CharacterCards.Length == 0)
            {
                m_CharacterCards = GetComponentsInChildren<Selectable>(true);
                // Exclude back button from cards array
                List<Selectable> cards = new List<Selectable>();
                for (int i = 0; i < m_CharacterCards.Length; i++)
                {
                    if (m_CharacterCards[i] != null && m_CharacterCards[i] != m_BackButton)
                    {
                        cards.Add(m_CharacterCards[i]);
                    }
                }
                m_CharacterCards = cards.ToArray();
            }
        }

        public override void Open()
        {
            base.Open();
            BuildNavigationLinks();
        }

        public override void PlayEnterTransition(Action onComplete)
        {
            Open();

            if (m_Animator != null)
            {
                m_Animator.PlayEntrance(onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public override void PlayExitTransition(Action onComplete)
        {
            if (m_Animator != null)
            {
                m_Animator.PlayExit(() =>
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
            StartCoroutine(DelayedBuildNavigation());
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);
        }

        private IEnumerator DelayedBuildNavigation()
        {
            yield return null;
            BuildNavigationLinks();
        }

        private void BuildNavigationLinks()
        {
            AutoFindCardsIfEmpty();

            if (m_CharacterCards == null || m_CharacterCards.Length == 0) return;

            int count = m_CharacterCards.Length;
            for (int i = 0; i < count; i++)
            {
                Selectable card = m_CharacterCards[i];
                if (card == null) continue;

                Navigation nav = card.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // Horizontal loop across the 4 cards
                nav.selectOnLeft = m_CharacterCards[(i - 1 + count) % count];
                nav.selectOnRight = m_CharacterCards[(i + 1) % count];

                // Down moves to Back button
                nav.selectOnDown = m_BackButton;
                nav.selectOnUp = null;

                card.navigation = nav;
            }

            if (m_BackButton != null)
            {
                Navigation backNav = m_BackButton.navigation;
                backNav.mode = Navigation.Mode.Explicit;
                // Up from Back button returns to the middle/first card
                backNav.selectOnUp = m_CharacterCards[0];
                backNav.selectOnDown = null;
                backNav.selectOnLeft = null;
                backNav.selectOnRight = null;
                m_BackButton.navigation = backNav;
            }
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
