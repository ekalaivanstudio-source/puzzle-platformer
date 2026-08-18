using UnityEngine;

/// <summary>
/// Drives the retract / extend loop of a floor spike.
///
/// The spike cycles: held up → sinks into its base → held down → rises again.
///
/// The proximity hold only applies DURING AN EXECUTION RUN. Once the player has
/// entered their sequence and pressed play, any spike within
/// <see cref="m_AlertRadius"/> of them suspends its cycle and rises to — and stays
/// at — fully extended, so a spike the player is actually walking into is always up.
/// While the sequence is still being composed the player is parked and the run has
/// not begun, so every spike just keeps breathing regardless of how close they are
/// standing; freezing the ones next to spawn would leave half the level looking dead.
///
/// Run boundaries come from <see cref="GameManager.OnExecutionStarted"/> and
/// <see cref="GameManager.OnTurnReset"/>. Note that AbortExecution deliberately fires
/// neither, so the mid-run brick/waypoint transports that abort and then resume keep
/// the spikes correctly flagged as running.
///
/// None of this touches the collider, which is what keeps it purely cosmetic: a
/// retracted spike is still lethal on paper. The kill check in
/// <see cref="PlayerController"/> is cell-based (the player has to share the spike's
/// grid cell to die) and the alert radius is several cells wide, so during a run the
/// spike is fully extended long before the player is close enough for it to matter.
/// Puzzles stay deterministic — a sequence that died yesterday dies today, whatever
/// the animation happened to be doing.
///
/// Setup: put this on the GameObject carrying the spike's SpriteRenderer and order
/// <see cref="m_Frames"/> fully-extended → fully-retracted.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpikeAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Ordered fully-UP → fully-DOWN. Element 0 is the extended spike, the " +
             "last element is the spike sunk into its base.")]
    [SerializeField] private Sprite[] m_Frames;

    [Header("Idle Cycle (seconds)")]
    [Tooltip("How long the spike stays fully extended before sinking.")]
    [SerializeField] private float m_UpHold = 1.2f;

    [Tooltip("Time taken to sink from fully up to fully down.")]
    [SerializeField] private float m_RetractTime = 0.25f;

    [Tooltip("How long the spike stays sunk before rising again.")]
    [SerializeField] private float m_DownHold = 0.9f;

    [Tooltip("Time taken to rise from fully down back to fully up.")]
    [SerializeField] private float m_ExtendTime = 0.2f;

    [Tooltip("Seconds to advance this spike's starting point in the cycle. Leave at 0 " +
             "to keep every spike in a level beating in unison; dial it in per instance " +
             "to stagger a row.")]
    [SerializeField] private float m_PhaseOffset = 0f;

    [Header("Player Proximity")]
    [Tooltip("Once a run is under way, the spike holds fully extended while the player " +
             "is within this many world units. Ignored before the player presses play. " +
             "Keep it comfortably wider than one grid cell so a spike the player is " +
             "walking into is always visibly up.")]
    [SerializeField] private float m_AlertRadius = 3f;

    [Tooltip("How fast the spike snaps back up when the player enters the alert " +
             "radius, in extension per second (1 = fully down to fully up in a second).")]
    [SerializeField] private float m_AlertExtendSpeed = 8f;

    // ─── State ────────────────────────────────────────────────────────────────

    private SpriteRenderer m_Renderer;

    // Position within the idle cycle, in seconds.
    private float m_CycleTime;

    // How far out the spike currently is: 1 = fully extended, 0 = fully retracted.
    private float m_Extension = 1f;

    // Last sprite pushed to the renderer, so an unchanged frame costs nothing.
    private int m_FrameIndex = -1;

    // True between the player pressing play and the turn ending. The proximity hold
    // is gated on this — see the class summary.
    private bool m_IsRunning;

    private float CycleLength => m_UpHold + m_RetractTime + m_DownHold + m_ExtendTime;

    // Cap on how fast m_Extension may move. It is at least as fast as the idle
    // curve's own retract/extend rates, so during the idle loop MoveTowards tracks
    // that curve exactly; it only lags — and therefore only eases — when the spike
    // has to catch up after the player leaves the alert radius mid-rise.
    private float TrackingRate => Mathf.Max(
        m_AlertExtendSpeed,
        1f / Mathf.Max(m_RetractTime, 0.01f),
        1f / Mathf.Max(m_ExtendTime, 0.01f));

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        m_Renderer = GetComponent<SpriteRenderer>();
        m_CycleTime = CycleLength > 0f ? Mathf.Repeat(m_PhaseOffset, CycleLength) : 0f;
    }

    private void OnEnable()
    {
        m_Extension = 1f;
        m_FrameIndex = -1;
        m_IsRunning = false;
        ApplyFrame();

        GameManager.OnExecutionStarted += OnRunStarted;
        GameManager.OnTurnReset += OnRunEnded;
        GameManager.OnFullReset += OnRunEnded;
    }

    private void OnDisable()
    {
        GameManager.OnExecutionStarted -= OnRunStarted;
        GameManager.OnTurnReset -= OnRunEnded;
        GameManager.OnFullReset -= OnRunEnded;
    }

    private void OnRunStarted() => m_IsRunning = true;

    private void OnRunEnded() => m_IsRunning = false;

    private void Update()
    {
        if (m_Frames == null || m_Frames.Length <= 1) return;

        float length = CycleLength;
        if (length <= 0f) return; // Misconfigured cycle — leave the spike as authored.

        bool alert = IsPlayerNear();

        if (alert)
            m_CycleTime = 0f; // Park at the top so the loop resumes on the up-hold.
        else
            m_CycleTime = Mathf.Repeat(m_CycleTime + Time.deltaTime, length);

        float target = alert ? 1f : EvaluateCycle(m_CycleTime);
        m_Extension = Mathf.MoveTowards(m_Extension, target, TrackingRate * Time.deltaTime);

        ApplyFrame();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Extension the idle cycle calls for at <paramref name="time"/> seconds in.</summary>
    private float EvaluateCycle(float time)
    {
        if (time < m_UpHold) return 1f;
        time -= m_UpHold;

        if (time < m_RetractTime) return 1f - time / Mathf.Max(m_RetractTime, 0.0001f);
        time -= m_RetractTime;

        if (time < m_DownHold) return 0f;
        time -= m_DownHold;

        return Mathf.Clamp01(time / Mathf.Max(m_ExtendTime, 0.0001f));
    }

    private bool IsPlayerNear()
    {
        // Before the player commits their sequence there is no run to protect, so the
        // spike keeps cycling however close they happen to be standing.
        if (!m_IsRunning) return false;

        PlayerController player = PlayerController.Instance;
        if (player == null) return false;

        Vector2 offset = (Vector2)player.transform.position - (Vector2)transform.position;
        return offset.sqrMagnitude <= m_AlertRadius * m_AlertRadius;
    }

    private void ApplyFrame()
    {
        if (m_Renderer == null || m_Frames == null || m_Frames.Length == 0) return;

        // Frames run extended → retracted, so frame 0 is extension 1.
        int last = m_Frames.Length - 1;
        int index = Mathf.Clamp(Mathf.RoundToInt((1f - m_Extension) * last), 0, last);

        if (index == m_FrameIndex) return;

        m_FrameIndex = index;
        if (m_Frames[index] != null) m_Renderer.sprite = m_Frames[index];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, m_AlertRadius);
    }
}
