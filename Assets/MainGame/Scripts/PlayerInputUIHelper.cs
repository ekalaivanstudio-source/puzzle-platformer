using System;
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
    [SerializeField] private GameObject buttonIndication;
    private RectTransform m_ButtonIndicationRT;
    [SerializeField] private AudioClip keyPressClip;
    [SerializeField] private AudioSource audioSource;

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
        RefreshAll();
    }

    // ─── Callbacks ───────────────────────────────────────────────────────────

    private void RefreshSequenceSlots()
    {
        int count = m_SequenceManager != null ? m_SequenceManager.Sequence.Count : 0;
        int slots = m_SequenceManager != null ? m_SequenceManager.MaxLength : 6;

        for (int i = 0; i < slots && i < inputsUI.Length; i++)
        {
            if (inputsUI[i] == null) continue;
            SetAlpha(inputsUI[i], i < count ? m_ActiveAlpha : m_InactiveAlpha);
        }

        // Play sound only when an action was added (count increased), not on undo or clear
        if (count > m_PreviousSequenceCount)
            PlayKeyPress();

        m_PreviousSequenceCount = count;

        // When the sequence is cleared (turn ended), also dim the Enter hint
        if (count == 0)
            SetSubmitAlpha(m_InactiveAlpha);
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
            Destroy(m_ButtonIndicationRT.gameObject);
            m_ButtonIndicationRT = null;
            return;
        }

        int indicatorIndex = Mathf.Clamp(count, 0, inputsUI.Length - 1);
        Image target = inputsUI[indicatorIndex];
        if (target != null)
            m_ButtonIndicationRT.position = target.rectTransform.TransformPoint(target.rectTransform.rect.center);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
