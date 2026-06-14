namespace TutorialSystem
{
    /// <summary>
    /// The completion condition for a single tutorial step — i.e. *what the player must do*
    /// before the tutorial advances to the next step.
    ///
    /// The <see cref="TutorialManager"/> reads this to decide which "wait" logic to run after
    /// it has shown the popup / arrow / highlight for the step.
    /// </summary>
    public enum TutorialActionType
    {
        /// <summary>
        /// Show a message only. Advances when the player taps "Next" (or taps anywhere, if the
        /// step allows it) or after an optional auto-advance delay. No target is required.
        /// </summary>
        PopupOnly = 0,

        /// <summary>
        /// Like <see cref="PopupOnly"/> but the step's target is spotlighted/dimmed and (optionally)
        /// pointed at by the arrow. Still advances on tap / auto-delay. Use for "look here" beats.
        /// </summary>
        Highlight = 1,

        /// <summary>
        /// Wait until the player clicks the target's UI <c>Button</c>. The target MUST be a UI
        /// object that has (or whose children have) a <c>UnityEngine.UI.Button</c>.
        /// </summary>
        WaitForButtonClick = 2,

        /// <summary>
        /// Wait until the target object is "interacted with". Completion is signalled by firing
        /// the step's Custom Event Id (falls back to the Target Id) on <see cref="TutorialEventBus"/>.
        /// Drop a <see cref="TutorialEventTrigger"/> on the world object, or call
        /// <c>TutorialEventBus.Fire(id)</c> from your gameplay code (e.g. inside <c>Interact()</c>).
        /// </summary>
        WaitForObjectInteraction = 3,

        /// <summary>
        /// Wait until a drag-and-drop is completed. Completion is signalled the same way as
        /// <see cref="WaitForObjectInteraction"/> (fire the event when the drop succeeds —
        /// see the provided <c>TutorialDragHandle</c> helper).
        /// </summary>
        DragAndDrop = 4,

        /// <summary>
        /// Wait for an arbitrary, code-driven event. Completion is signalled by firing the step's
        /// Custom Event Id on <see cref="TutorialEventBus"/>. Use this for anything bespoke
        /// ("matched 3 jellies", "reached the door", "opened inventory", ...).
        /// </summary>
        CustomEvent = 5,
    }

    /// <summary>Where the instructional popup is anchored on screen for a step.</summary>
    public enum TutorialPopupAnchor
    {
        /// <summary>Place the popup in the screen half opposite the target so it never covers it.</summary>
        Auto = 0,
        /// <summary>Pin the popup to the top of the screen.</summary>
        Top = 1,
        /// <summary>Pin the popup to the bottom of the screen.</summary>
        Bottom = 2,
        /// <summary>Pin the popup to the vertical center of the screen.</summary>
        Center = 3,
        /// <summary>
        /// Place the popup at the target's on-screen position plus a configurable offset, and follow
        /// the target. The position therefore changes for every step / target.
        /// </summary>
        RelativeToTarget = 4,
    }
}
