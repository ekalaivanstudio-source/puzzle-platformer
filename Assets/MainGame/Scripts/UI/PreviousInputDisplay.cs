using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left recap of the player's LAST attempt.
///
/// A turn that ends without a win — a death, a wrong sequence that simply ran out, a trap that
/// walked the player back to spawn — clears the queue and hands input straight back, leaving the
/// player to re-enter a sequence with no record of the one that just failed.
/// <see cref="SequenceManager"/> keeps that submitted sequence in
/// <see cref="SequenceManager.PreviousSequence"/>; this draws it as a row of action icons in the
/// bottom-left corner of the screen, where it stays for the whole of the next turn's input phase
/// and is replaced only when the next attempt ends.
///
/// Needs no wiring beyond being placed on the HUD canvas: the panel is built at runtime and the
/// action sprites are read from the <see cref="PlayerInputUIHelper"/> that already has them
/// authored, so the recap uses exactly the icons the input hint above it uses.
/// </summary>
public class PreviousInputDisplay : MonoBehaviour
{
    [Header("Availability")]
    [Tooltip("Off: the attempt markers AttemptGhostService leaves in the level replaced this " +
             "corner recap. Hovering a faded marker shows the sequence that ended there, " +
             "drawn with these same slot sprites — so every attempt still on screen can be " +
             "asked about, and it is read where it happened rather than in a corner. Switch " +
             "this back on to have both.")]
    [SerializeField] private bool m_ShowRecap = false;

    [Header("Placement")]
    [Tooltip("Distance (reference pixels) the panel is inset from the bottom-left screen corner.")]
    [SerializeField] private Vector2 m_ScreenMargin = new Vector2(40f, 40f);

    [Header("Icons")]
    [Tooltip("Edge length (reference pixels) of one action icon. The HUD's own slots are 100.")]
    [SerializeField] private float m_IconSize = 64f;

    [Tooltip("Gap (reference pixels) between two icons.")]
    [SerializeField] private float m_IconSpacing = 10f;

    [Tooltip("Opacity of the whole recap. Held below 1 so a record of a past turn never reads " +
             "as louder than the sequence the player is entering right now.")]
    [Range(0f, 1f)]
    [SerializeField] private float m_Opacity = 0.65f;

    [Header("Label")]
    [SerializeField] private bool m_ShowLabel = true;
    [SerializeField] private string m_LabelText = "LAST TRY";
    [SerializeField] private float m_LabelSize = 30f;

    [Header("References")]
    [Tooltip("Where the per-action sprites come from. Left empty, the PlayerInputUIHelper on " +
             "this object is used, then the first one in the scene.")]
    [SerializeField] private PlayerInputUIHelper m_IconSource;

    // Built once in Awake. m_Root is what gets shown and hidden; m_IconRow is the horizontal
    // strip the icon slots are parented to.
    private RectTransform m_Root;
    private RectTransform m_IconRow;

    // Slot pool. Slots are never destroyed, only deactivated — a level can end dozens of turns
    // and each one would otherwise churn a whole row of UI objects.
    private readonly List<Image> m_Slots = new List<Image>();

    // ─── Lifecycle ───────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Nothing is built at all when the recap is off — no panel, no slot pool, and the
        // subscriptions below never fire against a null root.
        if (!m_ShowRecap)
        {
            enabled = false;
            return;
        }

        if (m_IconSource == null) m_IconSource = GetComponent<PlayerInputUIHelper>();
        if (m_IconSource == null) m_IconSource = SceneObjects.FindInActiveScene<PlayerInputUIHelper>();

        if (m_IconSource == null)
            Debug.LogWarning("[PreviousInputDisplay] No PlayerInputUIHelper found — the recap has " +
                             "no action sprites to draw and will stay hidden.", this);

        BuildPanel();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnPreviousSequenceChanged += Refresh;
    }

    private void OnDisable()
    {
        if (SequenceManager.Instance != null)
            SequenceManager.Instance.OnPreviousSequenceChanged -= Refresh;
    }

    // Guarantees the subscription regardless of Awake/OnEnable order: if OnEnable ran before
    // SequenceManager.Awake, Instance was still null and the subscription above was skipped.
    // Same reason PlayerInputUIHelper re-subscribes in its own Start.
    private void Start()
    {
        if (SequenceManager.Instance != null)
        {
            SequenceManager.Instance.OnPreviousSequenceChanged -= Refresh;
            SequenceManager.Instance.OnPreviousSequenceChanged += Refresh;
        }

        Refresh();
    }

    private void OnValidate()
    {
        if (m_IconSize <= 0f) m_IconSize = 64f;
        if (m_IconSpacing < 0f) m_IconSpacing = 0f;
        if (m_LabelSize <= 0f) m_LabelSize = 30f;
    }

    // ─── Refresh ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Redraws the recap from <see cref="SequenceManager.PreviousSequence"/>. Hides the panel
    /// entirely when there is nothing to show — the first turn of a level has no previous
    /// attempt behind it, and an empty frame in the corner would just be noise.
    /// </summary>
    public void Refresh()
    {
        if (m_IconRow == null) return;

        IReadOnlyList<ActionTypeEnum> previous =
            SequenceManager.Instance != null ? SequenceManager.Instance.PreviousSequence : null;

        int shown = 0;

        if (previous != null)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                // Interact is not a queued movement command and has no icon of its own — the
                // HUD's slot row leaves it out for the same reason.
                if (previous[i] == ActionTypeEnum.Interact) continue;

                Sprite icon = IconFor(previous[i]);
                if (icon == null) continue;

                SlotAt(shown).sprite = icon;
                shown++;
            }
        }

        for (int i = shown; i < m_Slots.Count; i++)
            m_Slots[i].transform.parent.gameObject.SetActive(false);

        SetVisible(shown > 0);
    }

    // ─── Panel Construction ──────────────────────────────────────────────────────

    // Builds the corner panel: a bottom-left anchored root holding an optional caption and a
    // left-aligned strip of icon slots. Deliberately built in code rather than authored — the
    // HUD lives in one prefab shared by every level scene, so a runtime panel keeps this
    // drop-in with no per-level wiring to go stale.
    private void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        m_Root = NewRect("PreviousInputPanel", parent);
        m_Root.anchoredPosition = new Vector2(m_ScreenMargin.x, m_ScreenMargin.y);

        m_IconRow = NewRect("Icons", m_Root);

        HorizontalLayoutGroup row = m_IconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.LowerLeft;
        row.spacing = m_IconSpacing;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.childControlWidth = false;
        row.childControlHeight = false;

        ContentSizeFitter fitter = m_IconRow.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (m_ShowLabel) BuildLabel();
    }

    private void BuildLabel()
    {
        RectTransform labelRT = NewRect("Label", m_Root);
        // Sits directly above the icon strip, which grows upward from the root's bottom pivot.
        labelRT.anchoredPosition = new Vector2(0f, m_IconSize + 4f);
        labelRT.sizeDelta = new Vector2(400f, m_LabelSize * 1.4f);

        TextMeshProUGUI label = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = m_LabelText;
        label.fontSize = m_LabelSize;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.raycastTarget = false;
        label.color = new Color(1f, 1f, 1f, m_Opacity);
    }

    // Returns the icon Image of the slot at `index`, creating the slot on first use. A slot is
    // the frame-plus-icon pair the HUD's own InputUI prefab uses: the outer Image draws the box,
    // the inner one the action arrow.
    private Image SlotAt(int index)
    {
        while (m_Slots.Count <= index)
        {
            RectTransform frameRT = NewRect($"Slot{m_Slots.Count}", m_IconRow);
            frameRT.anchorMin = frameRT.anchorMax = frameRT.pivot = new Vector2(0.5f, 0.5f);
            frameRT.anchoredPosition = Vector2.zero;
            frameRT.sizeDelta = new Vector2(m_IconSize, m_IconSize);

            Image frame = frameRT.gameObject.AddComponent<Image>();
            frame.raycastTarget = false;
            frame.preserveAspect = true;
            frame.color = new Color(1f, 1f, 1f, m_Opacity);
            // The "Any" sprite IS the empty slot box the HUD frames every action with.
            frame.sprite = IconFor(ActionTypeEnum.Any);
            frame.enabled = frame.sprite != null;

            RectTransform iconRT = NewRect("Icon", frameRT);
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = Vector2.zero;

            Image icon = iconRT.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.color = new Color(1f, 1f, 1f, m_Opacity);

            m_Slots.Add(icon);
        }

        Image slot = m_Slots[index];
        slot.transform.parent.gameObject.SetActive(true);
        return slot;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;

        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        // Bottom-left by default: the panel hugs that corner, and every strip inside it grows
        // rightward and upward from there.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    private Sprite IconFor(ActionTypeEnum action) =>
        m_IconSource != null ? m_IconSource.GetSpriteForAction(action) : null;

    private void SetVisible(bool visible)
    {
        if (m_Root != null && m_Root.gameObject.activeSelf != visible)
            m_Root.gameObject.SetActive(visible);
    }
}
