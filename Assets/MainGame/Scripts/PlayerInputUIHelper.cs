using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the input hint UI:
///   • First <see cref="SequenceManager.MaxLength"/> images light up one-by-one as the player
///     queues actions (index 0 = first action added, etc.).
///   • The remaining images (the Enter/Submit hint) light up when the player presses Submit.
/// All images dim back when the sequence is cleared at turn end.
/// </summary>
public class PlayerInputUIHelper : MonoBehaviour
{
    [SerializeField] private Image[] inputsUI;

    [SerializeField] private SequenceManager m_SequenceManager;
    [SerializeField] private InputActionAsset m_InputActionAsset;

    [SerializeField] private float m_ActiveAlpha = 1f;
    [SerializeField] private float m_InactiveAlpha = 0.3f;

    private InputAction m_SubmitAction;
    private int m_PreviousSequenceCount;
    private bool m_IsRejecting;

    // Blink state — tracked so a mid-blink slot can be force-restored
    private Coroutine m_BlinkCoroutine;
    private Image m_BlinkingSlot;
    private Color m_BlinkOriginalColor;
    [SerializeField] private GameObject buttonIndication;
    private RectTransform m_ButtonIndicationRT;
    [SerializeField] private AudioClip keyPressClip;
    [SerializeField] private AudioSource audioSource;

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
    [SerializeField] private AudioClip m_WrongKeyClip;
    [SerializeField] private Color m_WrongColor = Color.red;
    [SerializeField] private float m_BlinkDuration = 0.12f;
    [SerializeField] private int m_BlinkCount = 2;
    [SerializeField] private float m_ShakeMagnitude = 0.08f;
    [SerializeField] private float m_ShakeDuration = 0.25f;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

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
    }

    private void OnEnable()
    {
        if (m_SequenceManager != null)
            m_SequenceManager.OnSequenceChanged += RefreshSequenceSlots;

        if (m_SubmitAction != null)
            m_SubmitAction.performed += OnSubmit;
    }

    private void OnDisable()
    {
        if (m_SequenceManager != null)
            m_SequenceManager.OnSequenceChanged -= RefreshSequenceSlots;

        if (m_SubmitAction != null)
            m_SubmitAction.performed -= OnSubmit;
    }

    private void Start()
    {
        // Drive MaxLength and correct-sequence validation from the configured sequence
        if (m_SequenceManager != null && m_CorrectSequence != null && m_CorrectSequence.Length > 0)
        {
            m_SequenceManager.SetMaxLength(m_CorrectSequence.Length);
            m_SequenceManager.SetCorrectSequence(m_CorrectSequence);
        }

        AssignSlotSprites();
        RefreshAll();
    }

    // ─── Callbacks ───────────────────────────────────────────────────────────

    private void RefreshSequenceSlots()
    {
        // Force-stop any running blink UNLESS we're mid-rejection — the rejection
        // itself just started the blink and the RemoveLastAction callback must not kill it.
        if (!m_IsRejecting)
            StopBlinkImmediate();
        // Count only non-Interact actions for UI display
        int slots = m_SequenceManager != null ? m_SequenceManager.MaxLength : 6;
        // Build a filtered list of non-Interact actions
        var filteredActions = new System.Collections.Generic.List<ActionTypeEnum>();
        if (m_SequenceManager != null)
        {
            foreach (var act in m_SequenceManager.Sequence)
                if (act != ActionTypeEnum.Interact)
                    filteredActions.Add(act);
        }
        int count = filteredActions.Count;

        // Validate the newly added action against the correct sequence.
        // Slots marked ActionTypeEnum.Any are wildcards — any input is accepted.
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
                    m_BlinkCoroutine = StartCoroutine(BlinkSlot(inputsUI[slotIndex]));
                }
                if (audioSource != null && m_WrongKeyClip != null)
                    audioSource.PlayOneShot(m_WrongKeyClip);
                StartCoroutine(ShakeCamera());

                m_IsRejecting = true;
                m_SequenceManager.RemoveLastAction();
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
                        inputsUI[newIndex].sprite = actualSprite;
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
            }
        }

        // Play sound only when an action was added (count increased), not on undo or clear
        if (count > m_PreviousSequenceCount)
            PlayKeyPress();

        m_PreviousSequenceCount = count;

        // When the sequence is cleared (turn ended), also dim the Enter hint
        if (count == 0)
        {
            AssignSlotSprites(); // restore Any slots back to the Any sprite
            SetSubmitAlpha(m_InactiveAlpha);
        }
        else if (count >= slots)
            SetSubmitAlpha(m_ActiveAlpha);

        UpdateButtonIndication(count);
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        SetSubmitAlpha(m_ActiveAlpha);
        PlayKeyPress();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void AssignSlotSprites()
    {
        if (m_CorrectSequence == null || inputsUI == null) return;
        for (int i = 0; i < m_CorrectSequence.Length && i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            Sprite s = GetSpriteForAction(m_CorrectSequence[i]);
            if (s != null)
                inputsUI[i].sprite = s;
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
        int slots = m_SequenceManager != null ? m_SequenceManager.MaxLength : 6;
        for (int i = slots; i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            SetAlpha(inputsUI[i], alpha);
        }
    }

    private void PlayKeyPress()
    {
        if (audioSource != null && keyPressClip != null)
            audioSource.PlayOneShot(keyPressClip);
    }

    // Moves the buttonIndication to sit on top of the next slot to be filled.
    // index == count means "pointing at the slot the player will fill next".
    // When all sequence slots are filled (count == maxLength) it advances to the
    // first Enter/Submit hint image, telling the player to press Enter.
    private void UpdateButtonIndication(int count)
    {
        if (m_ButtonIndicationRT == null || inputsUI == null) return;

        int slots = m_SequenceManager != null ? m_SequenceManager.MaxLength : 6;
        if (count >= slots)
        {
            m_ButtonIndicationRT.gameObject.SetActive(false);
            return;
        }

        m_ButtonIndicationRT.gameObject.SetActive(true);
        int indicatorIndex = Mathf.Clamp(count, 0, inputsUI.Length - 1);
        Image target = inputsUI[indicatorIndex];
        if (target != null)
            m_ButtonIndicationRT.position = target.rectTransform.TransformPoint(target.rectTransform.rect.center);
    }

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

    private IEnumerator ShakeCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 origin = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < m_ShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - (elapsed / m_ShakeDuration); // fade out shake
            cam.transform.localPosition = origin + (Vector3)UnityEngine.Random.insideUnitCircle * m_ShakeMagnitude * t;
            yield return null;
        }
        cam.transform.localPosition = origin;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
