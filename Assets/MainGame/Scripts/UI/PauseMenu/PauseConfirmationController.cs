using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.Unified;

namespace MainGame.UI.PauseMenu
{
    public struct ConfirmationRequest
    {
        public Sprite TitleSprite;
        public Action OnConfirm;
        public Action OnCancel;
    }

    /// <summary>
    /// Universal Confirmation Popup controller for the Pause Menu system.
    /// Manages warning/decision lock animations, opposing button slides,
    /// safe focus defaults (NO button), and clean callback execution.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseConfirmationController : MonoBehaviour
    {
        [Header("Popup Containers")]
        [SerializeField] private CanvasGroup m_PopupCanvasGroup;
        [SerializeField] private RectTransform m_DialogTransform;

        [Header("Controls")]
        [SerializeField] private Button m_YesButton;
        [SerializeField] private Button m_NoButton;
        [SerializeField] private Image m_TitleImage;

        [Header("Animator Reference")]
        [SerializeField] private ConfirmationPopupAnimator m_Animator;

        [Header("Audio Controller")]
        [SerializeField] private PauseAudioController m_Audio;

        private ConfirmationRequest m_CurrentRequest;
        private Coroutine m_ActiveRoutine;
        private bool m_IsOpen;

        public bool IsOpen => m_IsOpen;

        private void Awake()
        {
            ResolveReferences();
            BuildHorizontalNavigation();
        }

        public void ResolveReferences()
        {
            if (m_PopupCanvasGroup == null) m_PopupCanvasGroup = GetComponent<CanvasGroup>();
            if (m_DialogTransform == null)
            {
                Transform d = transform.Find("Dialog");
                if (d != null) m_DialogTransform = d as RectTransform;
            }

            if (m_YesButton == null && m_DialogTransform != null)
            {
                Transform yes = m_DialogTransform.Find("YesButton");
                if (yes != null) m_YesButton = yes.GetComponent<Button>();
            }

            if (m_NoButton == null && m_DialogTransform != null)
            {
                Transform no = m_DialogTransform.Find("NoButton");
                if (no != null) m_NoButton = no.GetComponent<Button>();
            }

            if (m_DialogTransform != null)
            {
                Transform title = m_DialogTransform.Find("Title");
                if (title != null)
                {
                    m_TitleImage = title.GetComponent<Image>();
                }
                else if (m_TitleImage == null)
                {
                    Transform msg = m_DialogTransform.Find("Message");
                    if (msg != null) m_TitleImage = msg.GetComponent<Image>();
                }
            }

            // Deactivate any duplicate Message GameObject (ghost header bug)
            if (m_DialogTransform != null)
            {
                Transform msg = m_DialogTransform.Find("Message");
                if (msg != null && (m_TitleImage == null || msg != m_TitleImage.transform))
                {
                    msg.gameObject.SetActive(false);
                }
            }

            // Ensure dark backdrop scrim Image exists on ConfirmationPopup
            Image scrim = GetComponent<Image>();
            if (scrim == null)
            {
                scrim = gameObject.AddComponent<Image>();
            }
            if (scrim != null)
            {
                scrim.color = new Color(0f, 0f, 0f, 0.85f);
                scrim.raycastTarget = true;
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponent<ConfirmationPopupAnimator>();
                if (m_Animator == null) m_Animator = GetComponentInChildren<ConfirmationPopupAnimator>(true);
            }

            if (m_Audio == null)
            {
                m_Audio = GetComponentInParent<PauseAudioController>();
            }
        }

        private void OnEnable()
        {
            if (m_PopupCanvasGroup != null)
            {
                m_PopupCanvasGroup.alpha = 1f;
                m_PopupCanvasGroup.interactable = true;
                m_PopupCanvasGroup.blocksRaycasts = true;
            }
            EnsureButtonsInteractable();
            BuildHorizontalNavigation();
            if (m_YesButton != null) m_YesButton.onClick.AddListener(HandleYesClicked);
            if (m_NoButton != null) m_NoButton.onClick.AddListener(HandleNoClicked);
        }

        private void OnDisable()
        {
            if (m_YesButton != null) m_YesButton.onClick.RemoveListener(HandleYesClicked);
            if (m_NoButton != null) m_NoButton.onClick.RemoveListener(HandleNoClicked);
        }

        public void EnsureButtonsInteractable()
        {
            if (m_YesButton != null)
            {
                m_YesButton.interactable = true;
                CanvasGroup yesCg = m_YesButton.GetComponent<CanvasGroup>();
                if (yesCg != null)
                {
                    yesCg.alpha = 1f;
                    yesCg.interactable = true;
                    yesCg.blocksRaycasts = true;
                }
            }

            if (m_NoButton != null)
            {
                m_NoButton.interactable = true;
                CanvasGroup noCg = m_NoButton.GetComponent<CanvasGroup>();
                if (noCg != null)
                {
                    noCg.alpha = 1f;
                    noCg.interactable = true;
                    noCg.blocksRaycasts = true;
                }
            }
        }

        public void BuildHorizontalNavigation()
        {
            if (m_YesButton == null || m_NoButton == null) return;

            Navigation yesNav = m_YesButton.navigation;
            yesNav.mode = Navigation.Mode.Explicit;
            yesNav.selectOnRight = m_NoButton;
            yesNav.selectOnLeft = m_NoButton; // Loop
            m_YesButton.navigation = yesNav;

            Navigation noNav = m_NoButton.navigation;
            noNav.mode = Navigation.Mode.Explicit;
            noNav.selectOnLeft = m_YesButton;
            noNav.selectOnRight = m_YesButton; // Loop
            m_NoButton.navigation = noNav;
        }

        public void Show(ConfirmationRequest request, Action onOpened = null)
        {
            m_CurrentRequest = request;
            m_IsOpen = true;

            gameObject.SetActive(true);
            ResolveReferences();

            // Guarantee CanvasGroup and buttons are fully opaque, interactable, and raycast-ready
            if (m_PopupCanvasGroup != null)
            {
                m_PopupCanvasGroup.alpha = 1f;
                m_PopupCanvasGroup.interactable = true;
                m_PopupCanvasGroup.blocksRaycasts = true;
            }
            EnsureButtonsInteractable();

            BuildHorizontalNavigation();

            if (m_TitleImage != null && request.TitleSprite != null)
            {
                m_TitleImage.sprite = request.TitleSprite;
            }

            if (m_Audio != null) m_Audio.PlayWarningPopup();

            // Focus NO button immediately so there is no unselected frame gap
            FocusNoButton();

            if (m_Animator != null)
            {
                m_Animator.PlayEntrance(() =>
                {
                    EnsureButtonsInteractable();
                    FocusNoButton();
                    onOpened?.Invoke();
                });
            }
            else
            {
                PlaySimpleEntrance(() =>
                {
                    EnsureButtonsInteractable();
                    FocusNoButton();
                    onOpened?.Invoke();
                });
            }
        }

        public void Hide(Action onClosed = null)
        {
            m_IsOpen = false;

            if (EventSystem.current != null)
            {
                GameObject cur = EventSystem.current.currentSelectedGameObject;
                if (cur != null && cur.transform.IsChildOf(transform))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            if (m_PopupCanvasGroup != null)
            {
                m_PopupCanvasGroup.interactable = false;
                m_PopupCanvasGroup.blocksRaycasts = false;
            }

            if (m_Animator != null)
            {
                m_Animator.PlayExit(() =>
                {
                    gameObject.SetActive(false);
                    onClosed?.Invoke();
                });
            }
            else
            {
                PlaySimpleExit(() =>
                {
                    gameObject.SetActive(false);
                    onClosed?.Invoke();
                });
            }
        }

        public void FocusNoButton()
        {
            if (EventSystem.current == null) EventSystem.current = FindAnyObjectByType<EventSystem>();
            if (m_NoButton != null && EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject != m_NoButton.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(m_NoButton.gameObject);
                }
            }
        }

        public void FocusYesButton()
        {
            if (EventSystem.current == null) EventSystem.current = FindAnyObjectByType<EventSystem>();
            if (m_YesButton != null && EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject != m_YesButton.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(m_YesButton.gameObject);
                }
            }
        }

        private void Update()
        {
            if (!m_IsOpen) return;

            // Notice: Escape is deliberately excluded. Per requirement:
            // "if the conformation panel is turnd on the esc shouldnt work,
            // it should work if the user is not selected any button conformaation"
            bool cancelPressed = false;

            if (Keyboard.current != null)
            {
                cancelPressed = Keyboard.current.backspaceKey.wasPressedThisFrame;
            }

            if (Gamepad.current != null)
            {
                cancelPressed |= Gamepad.current.bButton.wasPressedThisFrame;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Backspace)) cancelPressed = true;
#endif

            if (cancelPressed)
            {
                HandleCancel();
                return;
            }

            // Selection recovery: if focus is lost from Yes/No buttons, recover to NO button on any key
            GameObject cur = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            bool isYesOrChild = m_YesButton != null && cur != null && (cur == m_YesButton.gameObject || cur.transform.IsChildOf(m_YesButton.transform));
            bool isNoOrChild = m_NoButton != null && cur != null && (cur == m_NoButton.gameObject || cur.transform.IsChildOf(m_NoButton.transform));

            if (cur == null || !cur.activeInHierarchy || (!isYesOrChild && !isNoOrChild))
            {
                bool anyNav = false;
                if (Keyboard.current != null)
                {
                    anyNav = Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                             Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame ||
                             Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame;
                }
                if (Gamepad.current != null)
                {
                    anyNav |= Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.dpad.right.wasPressedThisFrame ||
                              Gamepad.current.leftStick.left.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame ||
                              Gamepad.current.buttonSouth.wasPressedThisFrame;
                }
                if (anyNav)
                {
                    FocusNoButton();
                }
            }
        }

        public void HandleCancel()
        {
            HandleNoClicked();
        }

        private void HandleYesClicked()
        {
            if (!m_IsOpen) return;
            Action confirmCallback = m_CurrentRequest.OnConfirm;
            m_CurrentRequest = default;

            if (m_Audio != null) m_Audio.PlayConfirmYes();

            // Trigger punch animation on Yes button
            TriggerButtonPunch(m_YesButton, () =>
            {
                Hide(() =>
                {
                    confirmCallback?.Invoke();
                });
            });
        }

        private void HandleNoClicked()
        {
            if (!m_IsOpen) return;
            Action cancelCallback = m_CurrentRequest.OnCancel;
            m_CurrentRequest = default;

            if (m_Audio != null) m_Audio.PlayCancelNo();

            // Trigger punch animation on No button
            TriggerButtonPunch(m_NoButton, () =>
            {
                Hide(() =>
                {
                    cancelCallback?.Invoke();
                });
            });
        }

        private void TriggerButtonPunch(Button button, Action callback)
        {
            if (button != null)
            {
                UIAnimatedButton animBtn = button.GetComponent<UIAnimatedButton>();
                if (animBtn != null)
                {
                    animBtn.PlayConfirmPunch(callback);
                    return;
                }

                MainMenuButtonEnergyAnimator energyBtn = button.GetComponent<MainMenuButtonEnergyAnimator>();
                if (energyBtn != null)
                {
                    energyBtn.PlayConfirmPunch(callback);
                    return;
                }
            }

            callback?.Invoke();
        }

        private void PlaySimpleEntrance(Action onComplete)
        {
            if (m_ActiveRoutine != null) StopCoroutine(m_ActiveRoutine);
            m_ActiveRoutine = StartCoroutine(SimpleEntranceRoutine(onComplete));
        }

        private IEnumerator SimpleEntranceRoutine(Action onComplete)
        {
            if (m_PopupCanvasGroup != null)
            {
                m_PopupCanvasGroup.alpha = 0f;
                m_PopupCanvasGroup.interactable = false;
            }

            if (m_DialogTransform != null)
            {
                m_DialogTransform.localScale = new Vector3(0.85f, 0.85f, 1f);
            }

            float duration = 0.16f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutBack, t, 1.2f);

                if (m_PopupCanvasGroup != null) m_PopupCanvasGroup.alpha = Mathf.Clamp01(t * 1.5f);
                if (m_DialogTransform != null) m_DialogTransform.localScale = Vector3.LerpUnclamped(new Vector3(0.85f, 0.85f, 1f), Vector3.one, ease);
                yield return null;
            }

            if (m_PopupCanvasGroup != null)
            {
                m_PopupCanvasGroup.alpha = 1f;
                m_PopupCanvasGroup.interactable = true;
            }
            if (m_DialogTransform != null) m_DialogTransform.localScale = Vector3.one;

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private void PlaySimpleExit(Action onComplete)
        {
            if (m_ActiveRoutine != null) StopCoroutine(m_ActiveRoutine);
            m_ActiveRoutine = StartCoroutine(SimpleExitRoutine(onComplete));
        }

        private IEnumerator SimpleExitRoutine(Action onComplete)
        {
            if (m_PopupCanvasGroup != null) m_PopupCanvasGroup.interactable = false;

            float duration = 0.12f;
            float elapsed = 0f;

            Vector3 startScale = m_DialogTransform != null ? m_DialogTransform.localScale : Vector3.one;
            float startAlpha = m_PopupCanvasGroup != null ? m_PopupCanvasGroup.alpha : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_PopupCanvasGroup != null) m_PopupCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                if (m_DialogTransform != null) m_DialogTransform.localScale = Vector3.LerpUnclamped(startScale, new Vector3(0.9f, 0.9f, 1f), ease);
                yield return null;
            }

            if (m_PopupCanvasGroup != null) m_PopupCanvasGroup.alpha = 0f;

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }
    }
}
