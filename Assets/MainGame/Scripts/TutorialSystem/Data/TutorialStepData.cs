using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// Designer-authored data for ONE tutorial beat ("step").
    ///
    /// A step is pure data — it never references scene objects directly. Instead it names a
    /// <b>Target Id</b>, which the <see cref="TutorialManager"/> resolves at runtime through the
    /// <see cref="TutorialTargetRegistry"/>. This is what lets one tutorial asset work across scene
    /// reloads, additive scenes, pooled objects, etc.
    ///
    /// Steps are normally created as sub-assets of a <see cref="TutorialSequenceData"/> via the
    /// Tutorial Creator window (Tools ▸ Tutorial System ▸ Tutorial Creator), but they can also be
    /// made standalone (Create ▸ Tutorial System ▸ Step).
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialStep_", menuName = "Tutorial System/Step", order = 1)]
    public class TutorialStepData : ScriptableObject
    {
        [Header("Editor")]
        [Tooltip("Short label shown in the Tutorial Creator list. Purely cosmetic.")]
        [SerializeField] private string m_StepName = "New Step";

        [Header("Target")]
        [Tooltip("Id of the TutorialTarget this step points at. Leave EMPTY for a full-screen, " +
                 "centered message with no target (e.g. an intro line). Must match the Target Id " +
                 "on a TutorialTarget component somewhere in the loaded scenes.")]
        [SerializeField] private string m_TargetId = "";

        [Header("Behaviour")]
        [Tooltip("What the player must do before the tutorial advances past this step.")]
        [SerializeField] private TutorialActionType m_ActionType = TutorialActionType.PopupOnly;

        [Tooltip("For WaitForObjectInteraction / DragAndDrop / CustomEvent: the event id that " +
                 "completes this step. If left empty, the Target Id is used as the event id.")]
        [SerializeField] private string m_CustomEventId = "";

        [Header("Content")]
        [Tooltip("Instructional text shown in the speech bubble. Supports TextMeshPro rich text.")]
        [TextArea(2, 5)]
        [SerializeField] private string m_Message = "";

        [Tooltip("Character / mascot icon shown to the left of the speech bubble. Optional.")]
        [SerializeField] private Sprite m_CharacterSprite;

        [Header("Presentation")]
        [Tooltip("Dim the rest of the screen and cut a spotlight hole around the target.")]
        [SerializeField] private bool m_DimBackground = true;

        [Tooltip("Draw the pulsing highlight ring around the target.")]
        [SerializeField] private bool m_ShowHighlight = true;

        [Tooltip("Show the bouncing arrow pointing at the target.")]
        [SerializeField] private bool m_ShowArrow = true;

        [Tooltip("Extra padding (in reference pixels) added around the target for the spotlight/ring.")]
        [SerializeField] private float m_HighlightPadding = 24f;

        [Tooltip("Where to place the speech-bubble popup. RelativeToTarget positions it next to this " +
                 "step's target (using the popup's Target Offset) and follows it; Auto keeps it in the " +
                 "screen half opposite the target; Top/Bottom/Center are fixed positions.")]
        [SerializeField] private TutorialPopupAnchor m_PopupAnchor = TutorialPopupAnchor.RelativeToTarget;

        [Header("Advance Rules")]
        [Tooltip("PopupOnly / Highlight only: also advance when the player taps anywhere on the dim, " +
                 "not just the Next button.")]
        [SerializeField] private bool m_AllowTapToContinue = true;

        [Tooltip("PopupOnly / Highlight only: auto-advance after this many seconds. 0 = never " +
                 "(wait for the player).")]
        [SerializeField] private float m_AutoAdvanceDelay = 0f;

        // ─── Read-only accessors ──────────────────────────────────────────────────

        /// <summary>Cosmetic label used in editor lists.</summary>
        public string StepName => m_StepName;

        /// <summary>Id of the target to point at, or empty for a target-less message.</summary>
        public string TargetId => m_TargetId;

        /// <summary>Whether this step targets an object at all.</summary>
        public bool HasTarget => !string.IsNullOrEmpty(m_TargetId);

        /// <summary>The completion condition for this step.</summary>
        public TutorialActionType ActionType => m_ActionType;

        /// <summary>Event id that completes event-driven steps (defaults to the Target Id).</summary>
        public string CompletionEventId =>
            string.IsNullOrEmpty(m_CustomEventId) ? m_TargetId : m_CustomEventId;

        /// <summary>Instructional text for the speech bubble.</summary>
        public string Message => m_Message;

        /// <summary>Mascot/character sprite, or null.</summary>
        public Sprite CharacterSprite => m_CharacterSprite;

        /// <summary>Whether to dim the background and cut a spotlight around the target.</summary>
        public bool DimBackground => m_DimBackground;

        /// <summary>Whether to draw the pulsing highlight ring.</summary>
        public bool ShowHighlight => m_ShowHighlight;

        /// <summary>Whether to draw the bouncing arrow.</summary>
        public bool ShowArrow => m_ShowArrow;

        /// <summary>Extra padding (reference px) around the target for spotlight/ring.</summary>
        public float HighlightPadding => m_HighlightPadding;

        /// <summary>Where the popup is anchored.</summary>
        public TutorialPopupAnchor PopupAnchor => m_PopupAnchor;

        /// <summary>Whether tapping the dim advances PopupOnly/Highlight steps.</summary>
        public bool AllowTapToContinue => m_AllowTapToContinue;

        /// <summary>Auto-advance delay in seconds (0 = wait for player).</summary>
        public float AutoAdvanceDelay => m_AutoAdvanceDelay;

        /// <summary>
        /// True if this step's completion is driven by <see cref="TutorialEventBus"/> rather than by
        /// a tap or a UI button click.
        /// </summary>
        public bool IsEventDriven =>
            m_ActionType == TutorialActionType.WaitForObjectInteraction ||
            m_ActionType == TutorialActionType.DragAndDrop ||
            m_ActionType == TutorialActionType.CustomEvent;
    }
}
