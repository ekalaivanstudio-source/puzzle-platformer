using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the evil doctor's reactions on the gameplay UI and tracks the player's
/// failure streak within the current scene:
///   • Every Nth level completed → doctor is Sad (the player beat him); awaited before the next level.
///   • Every Nth failure (a death OR a failed attempt) → doctor is Happy; awaited before restart.
///
/// The failure count is per-scene and intentionally NOT persisted — it starts fresh
/// whenever a scene loads. The completed-level count is the opposite: every level is its
/// own scene, so it lives in a static that survives the load and is cleared once per play
/// session. Reactions route to a <see cref="UIImageAnimator"/> and a
/// <see cref="DoctorDialogController"/>; a missing reference is logged once and skipped.
/// </summary>
public class EvilDoctorAnimationController : MonoBehaviour
{
    public enum DoctorAnimation
    {
        Happy,
        Sad
    }

    public static EvilDoctorAnimationController Instance { get; private set; }

    [Header("References")]
    [Tooltip("Plays the doctor's sprite reaction.")]
    [SerializeField] private UIImageAnimator m_Animator;

    [Tooltip("Shows the doctor's dialog/speech bubble.")]
    [SerializeField] private DoctorDialogController m_DialogController;
    
    [Header("Settings")]
    [Tooltip("Failures (deaths + failed attempts) between each doctor gloat — reacts on 7, 14, 21, …")]
    [SerializeField] private int m_FailuresPerReaction = 7;

    [Tooltip("Level completions between each doctor reaction — reacts on 3, 6, 9, …, not on every level.")]
    [SerializeField] private int m_LevelsPerReaction = 3;

    private int m_FailureCount;

    // Static because the controller dies with the level scene: each completion loads the next
    // level, so a per-instance field would be back at zero before the count could ever reach
    // m_LevelsPerReaction. Cleared per play session by ResetSessionState below.
    private static int s_CompletedLevelCount;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (m_FailuresPerReaction < 1) m_FailuresPerReaction = 1;
        if (m_LevelsPerReaction < 1) m_LevelsPerReaction = 1;

        if (m_Animator == null)
            Debug.LogWarning($"[{nameof(EvilDoctorAnimationController)}] {nameof(m_Animator)} is not assigned.", this);
        if (m_DialogController == null)
            Debug.LogWarning($"[{nameof(EvilDoctorAnimationController)}] {nameof(m_DialogController)} is not assigned.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Statics survive a play-mode restart when domain reload is disabled, which would carry a
    // half-finished streak into the next session and shift every reaction off the 3rd level.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState() => s_CompletedLevelCount = 0;

    // ─── Game-flow API (await these) ──────────────────────────────────────────

    /// <summary>
    /// Registers one completed level and, on every <see cref="m_LevelsPerReaction"/>th one,
    /// plays the win reaction (doctor sad), completing only once the full animation has
    /// finished. On the levels in between it returns immediately so the outro isn't delayed.
    /// Always clears the failure streak.
    /// </summary>
    public IEnumerator PlayLevelCompletedRoutine()
    {
        m_FailureCount = 0;
        s_CompletedLevelCount++;
        if (s_CompletedLevelCount % m_LevelsPerReaction == 0)
            yield return PlayReactionRoutine(DoctorAnimation.Sad);
    }

    /// <summary>
    /// Registers one failure. On every <see cref="m_FailuresPerReaction"/>th failure the
    /// doctor gloats (happy) and the routine waits for the full animation; otherwise it
    /// returns immediately so the restart isn't delayed.
    /// </summary>
    public IEnumerator RegisterFailureRoutine()
    {
        m_FailureCount++;
        if (m_FailureCount % m_FailuresPerReaction == 0)
            yield return PlayReactionRoutine(DoctorAnimation.Happy);
    }

    /// <summary>Failures accumulated this scene.</summary>
    public int DeathCount() => m_FailureCount;

    /// <summary>Levels completed this play session.</summary>
    public int CompletedLevelCount() => s_CompletedLevelCount;

    // ─── Inspector / ContextMenu helpers (fire-and-forget) ─────────────────────

    [ContextMenu("OnLevelCompleted")]
    public void OnLevelCompleted()
    {
        if (isActiveAndEnabled) StartCoroutine(PlayLevelCompletedRoutine());
    }

    [ContextMenu("OnLevelFailed")]
    public void OnLevelFailed()
    {
        if (isActiveAndEnabled) StartCoroutine(RegisterFailureRoutine());
    }

    public void OnPlayerdead()
    {
        if (isActiveAndEnabled) StartCoroutine(PlayReactionRoutine(DoctorAnimation.Happy));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Shows the dialog and reaction sprite, then waits for the full float cycle to end.
    // Each presenter is null-checked so one missing reference never suppresses the other,
    // and a missing animator simply skips the wait rather than blocking forever.
    private IEnumerator PlayReactionRoutine(DoctorAnimation animation)
    {
        if (m_DialogController != null)
            m_DialogController.ShowDialog(animation);

        if (m_Animator == null)
        {
            Debug.LogWarning($"[{nameof(EvilDoctorAnimationController)}] Cannot play reaction — {nameof(m_Animator)} is missing.", this);
            yield break;
        }

        m_Animator.ShowReaction(animation);
        yield return new WaitWhile(() => m_Animator.IsPlaying);
    }
}
