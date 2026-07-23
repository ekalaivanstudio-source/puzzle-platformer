using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A lever the player operates with E to control a <see cref="LeverMovingPlatform"/>.
/// Behaves like the laser mover/rotator controllers: pressing E aborts the current
/// execution and resets the player input, then hands control to the lever.
///
/// Flow (mirrors <c>LaserRedirectorMoverInputResetter</c>):
///   1. Player enters the lever's trigger zone → prompt UI appears.
///   2. Player presses E → control mode begins:
///        - The current run is aborted at the player's position and the sequence
///          is cleared (any remaining inputs are dropped) via ResetAtCheckpoint.
///        - Normal gameplay input is locked.
///        - The platform switches ON and starts patrolling (the player rides it).
///   3. Player presses E again → control mode ends:
///        - The platform switches OFF (it finishes the current tile, then stops).
///        - Normal input is restored.
///
/// There is no cooldown — E can be pressed at any time, in any platform motion state.
///
/// Requires a Collider2D (Is Trigger = true) on this GameObject.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlatformLever : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The moving platform this lever controls.")]
    [SerializeField] private LeverMovingPlatform m_TargetPlatform;

    [Header("UI")]
    [Tooltip("Shown when the player is in range ('Press E to control platform').")]
    [SerializeField] private GameObject m_PromptUI;

    [Tooltip("Shown while in control mode ('Platform moving — E to stop').")]
    [SerializeField] private GameObject m_ControlUI;

    [Tooltip("Tag used to identify the player.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("Visuals (optional)")]
    [Tooltip("Shown while the platform is ON / in control mode (e.g. lever pulled). Optional.")]
    [SerializeField] private GameObject m_OnVisual;
    [Tooltip("Shown while the platform is OFF (e.g. lever raised). Optional.")]
    [SerializeField] private GameObject m_OffVisual;

    // ─── Private state ────────────────────────────────────────────────────────────

    private Collider2D m_Collider;
    private InputAction m_InteractAction;
    private bool m_PlayerInRange;
    private bool m_InControlMode;
    private bool m_LastShownOn;
    private bool m_VisualInitialized;

    // Reused so the per-frame overlap check allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_NoFilter;

    // ─── Lifecycle ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        if (m_Collider == null)
            Debug.LogError("[PlatformLever] No Collider2D found.", this);

        m_NoFilter = ContactFilter2D.noFilter;
    }

    private void Start()
    {
        InputActionAsset asset = DeviceInputProvider.Instance?.InputActionAsset;
        if (asset != null)
        {
            InputActionMap map = asset.FindActionMap("Player", throwIfNotFound: false);
            m_InteractAction = map?.FindAction("Interact", throwIfNotFound: false);
        }

        // Guarantee subscription after all Awake calls have run.
        if (m_InteractAction != null)
        {
            m_InteractAction.performed -= OnInteract;
            m_InteractAction.performed += OnInteract;
        }

        RefreshVisual();
    }

    private void OnEnable()
    {
        if (m_InteractAction != null) m_InteractAction.performed += OnInteract;
        GameManager.OnFullReset += OnFullReset;
    }

    private void OnDisable()
    {
        if (m_InteractAction != null) m_InteractAction.performed -= OnInteract;
        if (m_InControlMode) ExitControlMode();
        GameManager.OnFullReset -= OnFullReset;
    }

    private void OnFullReset()
    {
        // The platform resets itself; just leave control mode and re-sync visuals.
        if (m_InControlMode) ExitControlMode();
        RefreshVisual();
    }

    // ─── Update ─────────────────────────────────────────────────────────────────────

    private void Update()
    {
        // While in control mode the prompt is hidden and range no longer matters —
        // E always exits. Skip the range check (matches the laser controllers).
        if (m_InControlMode)
        {
            RefreshVisual();
            return;
        }

        UpdatePlayerInRange();
        RefreshVisual();
    }

    // ─── Overlap detection ────────────────────────────────────────────────────────

    private void UpdatePlayerInRange()
    {
        if (m_Collider == null) return;

        bool inside = false;
        int count = Physics2D.OverlapBox(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f, m_NoFilter, m_OverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D h = m_OverlapResults[i];
            if (h.gameObject == gameObject) continue;
            if (h.CompareTag(m_PlayerTag)) { inside = true; break; }
        }

        if (inside == m_PlayerInRange) return;
        m_PlayerInRange = inside;
        Show(m_PromptUI, m_PlayerInRange);
    }

    // ─── Interact callback ────────────────────────────────────────────────────────

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (m_InControlMode)
        {
            ExitControlMode();
        }
        else if (m_PlayerInRange)
        {
            EnterControlMode();
        }
    }

    // ─── Control mode ─────────────────────────────────────────────────────────────

    private void EnterControlMode()
    {
        if (m_TargetPlatform == null) return;

        m_InControlMode = true;
        AudioManager.Instance?.PlayControlEnter();
        Show(m_PromptUI, false);
        Show(m_ControlUI, true);

        // Abort the run and drop any remaining queued inputs — leaving the player
        // exactly where they are (on the platform), not teleporting to the lever.
        if (PlayerController.Instance != null)
            PlayerController.Instance.ResetAtCheckpoint(PlayerController.Instance.transform.position);

        // Lock normal input while the lever is engaged.
        DeviceInputProvider.Instance?.SetEnabled(false);

        // Start the platform patrolling.
        m_TargetPlatform.SetOn(true);
        RefreshVisual();
    }

    private void ExitControlMode()
    {
        m_InControlMode = false;
        AudioManager.Instance?.PlayControlExit();
        Show(m_ControlUI, false);

        // Stop the platform (it finishes the current tile, then rests).
        m_TargetPlatform?.SetOn(false);

        DeviceInputProvider.Instance?.SetEnabled(true);
        RefreshVisual();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────────

    private void RefreshVisual()
    {
        bool on = m_TargetPlatform != null && m_TargetPlatform.IsOn;
        if (m_VisualInitialized && on == m_LastShownOn) return;

        m_VisualInitialized = true;
        m_LastShownOn = on;
        Show(m_OnVisual, on);
        Show(m_OffVisual, !on);
    }

    private static void Show(GameObject go, bool visible)
    {
        if (go != null) go.SetActive(visible);
    }
}
