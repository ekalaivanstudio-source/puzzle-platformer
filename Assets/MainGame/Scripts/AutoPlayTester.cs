using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Designer test aid: plays a level's authored solution for the player, so a level can be
/// verified end-to-end without typing the sequence by hand every run.
///
/// It does NOT drive <see cref="PlayerController"/> directly. It queues the solution into
/// <see cref="SequenceManager"/> one action at a time — exactly as
/// <see cref="DeviceInputProvider"/> does for a real key press — and then submits through
/// <see cref="GameManager.OnPlayClicked"/>. That means the run goes through the same input
/// validation, the same slot UI, and the same execution loop as a human playthrough, so what
/// it proves about the level is real.
///
/// The solution is read from the level's <see cref="LevelConfig"/>
/// (<c>sequence.autoPlaySequence</c>). When that is empty it falls back to the correct
/// sequence registered on <see cref="SequenceManager"/>, which is usable only if that
/// sequence has no <see cref="ActionTypeEnum.Any"/> wildcards — a wildcard says "any input
/// is accepted here", which is not an instruction anything can play back.
/// </summary>
public class AutoPlayTester : MonoBehaviour
{
    public static AutoPlayTester Instance { get; private set; }

    [Header("Availability")]
    [Tooltip("Off by default: this is a test tool, so it stays out of player builds. " +
             "It always runs in the editor and in development builds.")]
    [SerializeField] private bool m_EnableInReleaseBuilds = false;

    [Header("Button")]
    [Tooltip("Optional. A button in your own UI whose OnClick calls RunAutoPlay(). " +
             "Leave empty to let this component spawn its own on-screen test button.")]
    [SerializeField] private Button m_Button;

    [Tooltip("Spawns a plain screen-space button when no button is assigned above, so every " +
             "level has one without per-scene wiring.")]
    [SerializeField] private bool m_CreateButtonIfMissing = true;

    [SerializeField] private string m_ButtonLabel = "AUTO PLAY";

    [Tooltip("Corner offset (pixels) of the spawned button from the top-right of the screen.")]
    [SerializeField] private Vector2 m_ButtonMargin = new Vector2(24f, 24f);

    [SerializeField] private Vector2 m_ButtonSize = new Vector2(190f, 64f);

    [Header("Playback")]
    [Tooltip("Pause between queued actions. Lets the input slot UI fill visibly, the way it " +
             "does when a player types the sequence. 0 queues the whole solution instantly.")]
    [Min(0f)][SerializeField] private float m_QueueStepDelay = 0.12f;

    [Tooltip("Pause after the last action is queued, before Play is submitted.")]
    [Min(0f)] [SerializeField] private float m_SubmitDelay = 0.3f;

    [Header("Hotkey")]
    [Tooltip("Key that triggers auto play without clicking the button. None to disable.")]
    [SerializeField] private KeyCode m_Hotkey = KeyCode.F8;

    // True from the first queued action until Play is submitted. Guards against a second
    // run being started on top of the first while the queue is still filling.
    private bool m_IsRunning;

    /// <summary>True when this tool is allowed to run in the current build.</summary>
    private bool IsAvailable =>
        Application.isEditor || Debug.isDebugBuild || m_EnableInReleaseBuilds;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (!IsAvailable)
        {
            if (m_Button != null) m_Button.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        if (m_Button != null)
            m_Button.onClick.AddListener(RunAutoPlay);
        else if (m_CreateButtonIfMissing)
            BuildTestButton();
    }

    private void OnDestroy()
    {
        if (m_Button != null) m_Button.onClick.RemoveListener(RunAutoPlay);
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (m_Hotkey != KeyCode.None && Input.GetKeyDown(m_Hotkey))
            RunAutoPlay();
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Queues this level's solution and submits it. Wired to the test button's OnClick and to
    /// the hotkey; safe to call from anywhere — every failure is reported and does nothing else.
    /// </summary>
    [ContextMenu("Run Auto Play")]
    public void RunAutoPlay()
    {
        if (!IsAvailable) return;

        if (m_IsRunning)
        {
            Debug.Log("[AutoPlayTester] Already running — ignoring.");
            return;
        }

        if (SequenceManager.Instance == null || GameManager.Instance == null)
        {
            Debug.LogError("[AutoPlayTester] No SequenceManager or GameManager in the scene.", this);
            return;
        }

        // Input is off while a turn executes and while the spawn portal plays. Both are times
        // when a queued sequence would be thrown away, so refuse rather than half-run.
        if (DeviceInputProvider.Instance != null && !DeviceInputProvider.Instance.IsEnabled)
        {
            Debug.Log("[AutoPlayTester] Input is locked (a turn is running, or the player is " +
                      "still arriving) — wait for it to finish.");
            return;
        }

        if (!TryResolveSolution(out ActionTypeEnum[] solution, out string reason))
        {
            Debug.LogError($"[AutoPlayTester] {reason}", this);
            return;
        }

        StartCoroutine(AutoPlayRoutine(solution));
    }

    // ─── Solution Resolution ─────────────────────────────────────────────────

    // Picks the sequence to play: the level config's explicit solution when authored,
    // otherwise the registered correct sequence if it is concrete enough to be played.
    // Returns false with a reason the designer can act on rather than a silent no-op.
    private bool TryResolveSolution(out ActionTypeEnum[] solution, out string reason)
    {
        solution = null;
        reason = null;

        LevelConfig config = LevelContext.Instance != null ? LevelContext.Instance.Config : null;
        ActionTypeEnum[] authored = config != null ? config.sequence.autoPlaySequence : null;

        if (authored != null && authored.Length > 0)
        {
            int wildcard = System.Array.IndexOf(authored, ActionTypeEnum.Any);
            if (wildcard >= 0)
            {
                reason = $"'{config.name}' has Any at index {wildcard} of its Auto Play Sequence. " +
                         "A wildcard is not a playable action — replace it with the action the " +
                         "solution actually uses.";
                return false;
            }

            solution = authored;
            return true;
        }

        // No authored solution — fall back to the level's correct sequence.
        IReadOnlyList<ActionTypeEnum> correct = SequenceManager.Instance.CorrectSequence;
        string configName = config != null ? config.name : "this level's LevelConfig";

        if (correct == null || correct.Count == 0)
        {
            reason = $"No solution to play. Fill in Sequence ▸ Auto Play Sequence on {configName}.";
            return false;
        }

        var resolved = new ActionTypeEnum[correct.Count];
        for (int i = 0; i < correct.Count; i++)
        {
            if (correct[i] == ActionTypeEnum.Any)
            {
                reason = $"The correct sequence has Any at index {i}, so it cannot be played back. " +
                         $"Fill in Sequence ▸ Auto Play Sequence on {configName} instead.";
                return false;
            }
            resolved[i] = correct[i];
        }

        solution = resolved;
        return true;
    }

    // ─── Playback ────────────────────────────────────────────────────────────

    // Feeds the solution into SequenceManager one action at a time, then submits.
    //
    // Unscaled waits throughout: a level being tested may well be sitting in slow motion or
    // paused, and the queue has to fill at the same pace either way.
    private IEnumerator AutoPlayRoutine(ActionTypeEnum[] solution)
    {
        m_IsRunning = true;
        SetButtonInteractable(false);

        // Start from a clean queue, and keep the designer's own key presses from interleaving
        // with the ones being fed in. GameManager disables input again on submit.
        SequenceManager.Instance.ClearSequence();
        DeviceInputProvider.Instance?.SetEnabled(false);

        bool queued = true;

        for (int i = 0; i < solution.Length; i++)
        {
            int before = SequenceManager.Instance.SequenceLength;
            SequenceManager.Instance.AddAction(solution[i]);

            // The queue is unchanged, so the action did not stick. Either it was full
            // (MaxLength is driven by the correct sequence's length) or PlayerInputUIHelper
            // rejected it as wrong for that slot — which means the authored solution and the
            // level's correct sequence disagree. Both are the designer's to fix, so say which.
            if (SequenceManager.Instance.SequenceLength == before)
            {
                Debug.LogError(
                    $"[AutoPlayTester] Action {i} ({solution[i]}) was rejected. The queue holds " +
                    $"{SequenceManager.Instance.MaxLength} actions and the solution is " +
                    $"{solution.Length} long — check that the two match, and that the solution " +
                    "agrees with this level's correct sequence.", this);
                queued = false;
                break;
            }

            if (m_QueueStepDelay > 0f)
                yield return new WaitForSecondsRealtime(m_QueueStepDelay);
        }

        if (!queued)
        {
            SequenceManager.Instance.ClearSequence();
            DeviceInputProvider.Instance?.SetEnabled(true);
            FinishRun();
            yield break;
        }

        if (m_SubmitDelay > 0f)
            yield return new WaitForSecondsRealtime(m_SubmitDelay);

        // Re-enabled purely so the submit path sees the same state a real Enter press would;
        // OnPlayClicked turns it straight back off for the duration of the turn.
        DeviceInputProvider.Instance?.SetEnabled(true);

        if (!SequenceManager.Instance.CanExecute)
        {
            Debug.LogError(
                "[AutoPlayTester] The queued solution was not accepted for execution — it does " +
                "not match this level's correct sequence, or does not fill every required slot.",
                this);
            SequenceManager.Instance.ClearSequence();
            FinishRun();
            yield break;
        }

        GameManager.Instance.OnPlayClicked();
        FinishRun();
    }

    private void FinishRun()
    {
        m_IsRunning = false;
        SetButtonInteractable(true);
    }

    // ─── Test Button ─────────────────────────────────────────────────────────

    private void SetButtonInteractable(bool value)
    {
        if (m_Button != null) m_Button.interactable = value;
    }

    // Builds a self-contained screen-space button so the tool is present in every level
    // without touching 20 scenes. Its canvas sorts above the game UI; the scene's existing
    // EventSystem drives the click, the same one the Play and Clear buttons already use.
    private void BuildTestButton()
    {
        var root = new GameObject("AutoPlayTestButton (runtime)");
        root.transform.SetParent(transform, worldPositionStays: false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // above every authored UI layer
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonGO = new GameObject("Button", typeof(RectTransform));
        buttonGO.transform.SetParent(root.transform, worldPositionStays: false);

        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = m_ButtonSize;
        rect.anchoredPosition = new Vector2(-m_ButtonMargin.x, -m_ButtonMargin.y);

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.11f, 0.60f, 0.95f, 0.92f);

        m_Button = buttonGO.AddComponent<Button>();
        m_Button.targetGraphic = image;
        m_Button.onClick.AddListener(RunAutoPlay);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(buttonGO.transform, worldPositionStays: false);

        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = m_ButtonLabel;
        label.fontSize = 28f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }
}
