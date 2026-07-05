using System;
using System.Collections;
using TutorialSystem;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the input hint UI:
///   â€¢ First <see cref="SequenceManager.MaxLength"/> images light up one-by-one as the player
///     queues actions (index 0 = first action added, etc.).
///   â€¢ The remaining images (the Enter/Submit hint) light up when the player presses Submit.
/// All images dim back when the sequence is cleared at turn end.
/// </summary>
public class PlayerInputUIHelper : MonoBehaviour
{
    // Built at runtime by instantiating inputUI_GO into inputContainer,
    // one slot per entry in m_CorrectSequence.
    private Image[] inputsUI;

    [SerializeField] private GameObject inputUI_GO;
    [SerializeField] private Transform inputContainer;

    [SerializeField] private InputActionAsset m_InputActionAsset;

    [SerializeField] private float m_ActiveAlpha = 1f;
    [SerializeField] private float m_InactiveAlpha = 0.3f;

    private InputAction m_SubmitAction;
    private int m_PreviousSequenceCount;
    private bool m_IsRejecting;

    // Blink state â€” tracked so a mid-blink slot can be force-restored
    private Coroutine m_BlinkCoroutine;
    private Image m_BlinkingSlot;
    private Color m_BlinkOriginalColor;
    [SerializeField] private GameObject buttonIndication;
    private RectTransform m_ButtonIndicationRT;

    [Header("Correct Sequence")]
    [Tooltip("The exact sequence the player must enter. Leave empty to allow any input.")]
    [SerializeField] private ActionTypeEnum[] m_CorrectSequence;

    [Header("Action Sprites")]
    [Tooltip("Sprites assigned to inputsUI slots based on the correct sequence.")]
    [SerializeField] private Sprite m_LeftSprite;
    [SerializeField] private Sprite m_RightSprite;
    [SerializeField] private Sprite m_JumpSprite;
    [SerializeField] private Sprite m_JumpRightSprite;
    [SerializeField] private Sprite m_JumpLeftSprite;
    [SerializeField] private Sprite m_InteractSprite;
    [SerializeField] private Sprite m_AnySprite;

    [Header("Wrong Input Feedback")]
    [SerializeField] private Color m_WrongColor = Color.red;
    [SerializeField] private float m_BlinkDuration = 0.12f;
    [SerializeField] private int m_BlinkCount = 2;
    [SerializeField] private float m_ShakeMagnitude = 0.08f;
    [SerializeField] private float m_ShakeDuration = 0.25f;

    // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Awake()
    {
        if (m_InputActionAsset != null)
        {
            InputActionMap map = m_InputActionAsset.FindActionMap("Player", throwIfNotFound: false);
            m_SubmitAction = map?.FindAction("Submit", throwIfNotFound: false);
        }

        if (buttonIndication != null)
        {
            // Instantiate the prefab into this object's canvas hierarchy
            GameObject instance = Instantiate(buttonIndication, transform);
            m_ButtonIndicationRT = instance.GetComponent<RectTransform>();
        }

        BuildInputSlots();
    }

    // Instantiates one inputUI_GO prefab per entry in m_CorrectSequence inside
    // inputContainer, replacing the previously hand-assigned inputsUI array.
    private void BuildInputSlots()
    {
        int count = m_CorrectSequence != null ? m_CorrectSequence.Length : 0;
        inputsUI = new Image[count];

        if (inputUI_GO == null || inputContainer == null || count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(inputUI_GO, inputContainer);
            instance.SetActive(true);
            Image img = instance.GetComponent<Image>();
            if (img == null) img = instance.GetComponentInChildren<Image>(true);
            inputsUI[i] = img;
        }
    }

    private void OnEnable()
    {
        if (SequenceManager.Instance != null) SequenceManager.Instance.OnSequenceChanged += RefreshSequenceSlots;

        SubscribeTutorialEvents();

        if (m_SubmitAction != null)
            m_SubmitAction.performed += OnSubmit;
    }

    private void OnDisable()
    {
        if (SequenceManager.Instance != null) SequenceManager.Instance.OnSequenceChanged -= RefreshSequenceSlots;

        UnsubscribeTutorialEvents();

        if (m_SubmitAction != null)
            m_SubmitAction.performed -= OnSubmit;
    }

    private void Start()
    {
        // Guarantee the subscription regardless of OnEnable/Awake execution order.
        // If OnEnable ran before SequenceManager.Awake, Instance was null and the
        // subscription was skipped — this ensures it is always in place by Start().
        if (SequenceManager.Instance != null)
        {
            SequenceManager.Instance.OnSequenceChanged -= RefreshSequenceSlots;
            SequenceManager.Instance.OnSequenceChanged += RefreshSequenceSlots;
        }

        // TutorialManager.Instance may have been null during OnEnable (Awake order) — ensure it now.
        SubscribeTutorialEvents();

        // Drive MaxLength and correct-sequence validation from the configured sequence
        if (SequenceManager.Instance != null && m_CorrectSequence != null && m_CorrectSequence.Length > 0)
        {
            SequenceManager.Instance.SetMaxLength(m_CorrectSequence.Length);
            SequenceManager.Instance.SetCorrectSequence(m_CorrectSequence);
        }

        AssignSlotSprites();
        RefreshAll();
    }

    // â”€â”€â”€ Callbacks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void RefreshSequenceSlots()
    {
        // Force-stop any running blink UNLESS we're mid-rejection â€” the rejection
        // itself just started the blink and the RemoveLastAction callback must not kill it.
        if (!m_IsRejecting)
            StopBlinkImmediate();
        // Count only non-Interact actions for UI display
        int slots = SequenceManager.Instance != null ? SequenceManager.Instance.MaxLength : 6;
        // Build a filtered list of non-Interact actions
        var filteredActions = new System.Collections.Generic.List<ActionTypeEnum>();
        if (SequenceManager.Instance != null)
        {
            foreach (var act in SequenceManager.Instance.Sequence)
                if (act != ActionTypeEnum.Interact)
                    filteredActions.Add(act);
        }
        int count = filteredActions.Count;

        // Validate the newly added action against the correct sequence.
        // Slots marked ActionTypeEnum.Any are wildcards â€” any input is accepted.
        if (!m_IsRejecting && m_CorrectSequence != null && m_CorrectSequence.Length > 0
            && count > m_PreviousSequenceCount)
        {
            int newIndex = count - 1;
            if (newIndex < m_CorrectSequence.Length
                && m_CorrectSequence[newIndex] != ActionTypeEnum.Any
                && filteredActions[newIndex] != m_CorrectSequence[newIndex])
            {
                // Visual feedback before removing
                int slotIndex = Mathf.Clamp(newIndex, 0, inputsUI.Length - 1);
                if (inputsUI[slotIndex] != null)
                {
                    m_BlinkCoroutine = StartCoroutine(BlinkSlot(inputsUI[slotIndex].transform.GetChild(0).GetComponent<Image>()));
                }
                CameraController.Instance?.Shake(m_ShakeMagnitude, m_ShakeDuration);

                m_IsRejecting = true;
                SequenceManager.Instance.RemoveLastAction();
                m_IsRejecting = false;
                return;
            }
        }

        for (int i = 0; i < slots && i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            SetAlpha(inputsUI[i], i < count ? m_ActiveAlpha : m_InactiveAlpha);
        }

        // When a wildcard (Any) slot is filled, show the sprite of the actual input chosen, but not for Interact
        if (count > m_PreviousSequenceCount)
        {
            int newIndex = count - 1;
            if (m_CorrectSequence != null && newIndex < m_CorrectSequence.Length
                && m_CorrectSequence[newIndex] == ActionTypeEnum.Any
                && newIndex < inputsUI.Length && inputsUI[newIndex] != null)
            {
                var actualAction = filteredActions[newIndex];
                if (actualAction != ActionTypeEnum.Interact)
                {
                    Sprite actualSprite = GetSpriteForAction(actualAction);
                    if (actualSprite != null)
                    {
                        inputsUI[newIndex].sprite = m_AnySprite;
                        Image inputChild = inputsUI[newIndex].transform.GetChild(0).GetComponent<Image>();
                        inputChild.sprite = actualSprite;
                    }
                        
                }
            }
        }

        // When a wildcard slot is un-filled (undo), restore the Any sprite.
        if (count < m_PreviousSequenceCount)
        {
            int removedIndex = count;
            if (m_CorrectSequence != null && removedIndex < m_CorrectSequence.Length
                && m_CorrectSequence[removedIndex] == ActionTypeEnum.Any
                && removedIndex < inputsUI.Length && inputsUI[removedIndex] != null
                && m_AnySprite != null)
            {
                inputsUI[removedIndex].sprite = m_AnySprite;
                Image inputChild = inputsUI[removedIndex].transform.GetChild(0).GetComponent<Image>();
                inputChild.sprite = m_AnySprite;
            }
        }

        // When the sequence is cleared (turn ended), also dim the Enter hint
        if (count == 0)
        {
            AssignSlotSprites(); // restore Any slots back to the Any sprite
            SetSubmitAlpha(m_InactiveAlpha);
        }
        else if (count >= slots)
            SetSubmitAlpha(m_ActiveAlpha);

        UpdateButtonIndication(count);
        m_PreviousSequenceCount = count;
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        SetSubmitAlpha(m_ActiveAlpha);
    }

    // â”€â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void AssignSlotSprites()
    {
        if (m_CorrectSequence == null || inputsUI == null) return;
        for (int i = 0; i < m_CorrectSequence.Length && i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            Sprite s = GetSpriteForAction(m_CorrectSequence[i]);
            if (s != null)
            {
                inputsUI[i].sprite = s;
                Image inputChild = inputsUI[i].transform.GetChild(0).GetComponent<Image>();
                inputChild.sprite = s;
            }

        }
    }

    private Sprite GetSpriteForAction(ActionTypeEnum action)
    {
        return action switch
        {
            ActionTypeEnum.Left => m_LeftSprite,
            ActionTypeEnum.Right => m_RightSprite,
            ActionTypeEnum.Jump => m_JumpSprite,
            ActionTypeEnum.JumpRight => m_JumpRightSprite != null ? m_JumpRightSprite : m_JumpSprite,
            ActionTypeEnum.JumpLeft => m_JumpLeftSprite != null ? m_JumpLeftSprite : m_JumpSprite,
            ActionTypeEnum.Interact => m_InteractSprite,
            ActionTypeEnum.Any => m_AnySprite,
            _ => null
        };
    }

    private void RefreshAll()
    {
        RefreshSequenceSlots();
        SetSubmitAlpha(m_InactiveAlpha);
        UpdateButtonIndication(0);
    }

    private void SetSubmitAlpha(float alpha)
    {
        int slots = SequenceManager.Instance != null ? SequenceManager.Instance.MaxLength : 6;
        for (int i = slots; i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            SetAlpha(inputsUI[i], alpha);
        }
    }

    // Moves the buttonIndication to sit on top of the next slot to be filled.
    // index == count means "pointing at the slot the player will fill next".
    // When all sequence slots are filled (count == maxLength) it advances to the
    // first Enter/Submit hint image, telling the player to press Enter.
    private void UpdateButtonIndication(int count)
    {
        if (m_ButtonIndicationRT == null || inputsUI == null) return;

        // Keep the input hint hidden until the tutorial has finished — the tutorial
        // teaches the controls itself, so the indicator would otherwise compete with it.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying)
        {
            m_ButtonIndicationRT.gameObject.SetActive(false);
            return;
        }

        int slots = SequenceManager.Instance != null ? SequenceManager.Instance.MaxLength : 6;
        if (count >= slots)
        {
            m_ButtonIndicationRT.gameObject.SetActive(false);
            return;
        }

        m_ButtonIndicationRT.gameObject.SetActive(true);
        int indicatorIndex = Mathf.Clamp(count, 0, inputsUI.Length - 1);
        Image target = inputsUI[indicatorIndex];
        if (target != null)
        {
            // The slots are instantiated into a layout group, which doesn't position
            // them until the next layout pass (after Start). Force the rebuild now so
            // the very first placement reads each slot's final position, not its
            // pre-layout origin — otherwise the indicator starts in the wrong spot.
            if (inputContainer is RectTransform containerRT)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRT);

            m_ButtonIndicationRT.position = target.rectTransform.TransformPoint(target.rectTransform.rect.center);
        }
    }

    // ─── Tutorial gating ────────────────────────────────────────────────────────
    // The button indicator stays hidden while a tutorial is on screen and re-appears
    // (if appropriate) the moment the tutorial ends.

    private void SubscribeTutorialEvents()
    {
        if (TutorialManager.Instance == null) return;
        TutorialManager.Instance.OnSequenceStarted -= OnTutorialChanged;
        TutorialManager.Instance.OnSequenceStarted += OnTutorialChanged;
        TutorialManager.Instance.OnSequenceEnded -= OnTutorialEnded;
        TutorialManager.Instance.OnSequenceEnded += OnTutorialEnded;
    }

    private void UnsubscribeTutorialEvents()
    {
        if (TutorialManager.Instance == null) return;
        TutorialManager.Instance.OnSequenceStarted -= OnTutorialChanged;
        TutorialManager.Instance.OnSequenceEnded -= OnTutorialEnded;
    }

    private void OnTutorialChanged(TutorialSequenceData sequence) => UpdateButtonIndication(m_PreviousSequenceCount);

    private void OnTutorialEnded(TutorialSequenceData sequence, bool completed) => UpdateButtonIndication(m_PreviousSequenceCount);

    private void StopBlinkImmediate()
    {
        if (m_BlinkCoroutine == null) return;
        StopCoroutine(m_BlinkCoroutine);
        m_BlinkCoroutine = null;
        if (m_BlinkingSlot != null)
        {
            m_BlinkingSlot.color = m_BlinkOriginalColor;
            m_BlinkingSlot = null;
        }
    }

    private IEnumerator BlinkSlot(Image slot)
    {
        m_BlinkingSlot = slot;
        m_BlinkOriginalColor = slot.color;
        Color wrong = m_WrongColor;
        wrong.a = m_BlinkOriginalColor.a;
        for (int i = 0; i < m_BlinkCount; i++)
        {
            slot.color = wrong;
            yield return new WaitForSecondsRealtime(m_BlinkDuration);
            slot.color = m_BlinkOriginalColor;
            yield return new WaitForSecondsRealtime(m_BlinkDuration);
        }
        m_BlinkCoroutine = null;
        m_BlinkingSlot = null;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
