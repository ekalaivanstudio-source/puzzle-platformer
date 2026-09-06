using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Setting.Menu;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Unified Options Screen controller with alternating row entrance transitions and explicit controller navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionsScreen : UIScreen
    {
        [Header("Controls")]
        [SerializeField] private Button m_BackButton;

        [Header("Settings Rows (Order Top to Bottom)")]
        [SerializeField] private SettingStepControl[] m_SettingsRows;

        [Header("Animator Reference")]
        [SerializeField] private SettingsScreenAnimator m_Animator;

        private readonly List<Selectable> m_NavigationChain = new List<Selectable>();
        private Coroutine m_BuildNavigationCoroutine;

        public override GameObject DefaultSelectedObject
        {
            get
            {
                SettingStepControl firstRow = GetFirstUsableRow();
                return firstRow != null ? firstRow.gameObject : base.DefaultSelectedObject;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (m_Animator == null)
            {
                m_Animator = GetComponent<SettingsScreenAnimator>();
            }
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<SettingsScreenAnimator>(true);
            }
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
            m_BuildNavigationCoroutine = StartCoroutine(BuildExplicitNavigationNextFrame());
        }

        private void OnDisable()
        {
            if (m_BackButton != null) m_BackButton.onClick.RemoveListener(HandleBackClicked);

            if (m_BuildNavigationCoroutine != null)
            {
                StopCoroutine(m_BuildNavigationCoroutine);
                m_BuildNavigationCoroutine = null;
            }
        }

        private SettingStepControl GetFirstUsableRow()
        {
            if (m_SettingsRows == null) return null;

            for (int i = 0; i < m_SettingsRows.Length; i++)
            {
                if (m_SettingsRows[i] != null && m_SettingsRows[i].gameObject.activeInHierarchy) return m_SettingsRows[i];
            }
            return null;
        }

        private IEnumerator BuildExplicitNavigationNextFrame()
        {
            yield return null;
            BuildExplicitNavigation();
            m_BuildNavigationCoroutine = null;
        }

        private void BuildExplicitNavigation()
        {
            m_NavigationChain.Clear();

            if (m_SettingsRows != null)
            {
                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    if (m_SettingsRows[i] != null && m_SettingsRows[i].gameObject.activeInHierarchy)
                    {
                        m_NavigationChain.Add(m_SettingsRows[i]);
                    }
                }
            }

            if (m_BackButton != null) m_NavigationChain.Add(m_BackButton);

            int count = m_NavigationChain.Count;
            if (count <= 1) return;

            for (int i = 0; i < count; i++)
            {
                Selectable current = m_NavigationChain[i];

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = m_NavigationChain[(i - 1 + count) % count];
                nav.selectOnDown = m_NavigationChain[(i + 1) % count];
                current.navigation = nav;
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
