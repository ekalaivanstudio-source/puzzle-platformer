using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the evil doctor's reactions on the gameplay UI and tracks the player's
/// failure streak within the current scene:
///   • Level completed → doctor is Sad (the player beat him); awaited before the next level.
///   • Every Nth failure (a death OR a failed attempt) → doctor is Happy; awaited before restart.
///
/// The failure count is per-scene and intentionally NOT persisted — it starts fresh
/// whenever a scene loads. Reactions route to a <see cref="UIImageAnimator"/> and a
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
    [Tooltip("Failures (deaths + failed attempts) between each doctor gloat — reacts on 3, 6, 9, …")]
    [SerializeField] private int m_FailuresPerReaction = 3;

    private int m_FailureCount;

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

        if (m_Animator == null)
            Debug.LogWarning($"[{nameof(EvilDoctorAnimationController)}] {nameof(m_Animator)} is not assigned.", this);
        if (m_DialogController == null)
            Debug.LogWarning($"[{nameof(EvilDoctorAnimationController)}] {nameof(m_DialogController)} is not assigned.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Game-flow API (await these) ──────────────────────────────────────────

    /// <summary>
    /// Plays the win reaction (doctor sad) and completes only once the full animation
    /// has finished. Also clears the failure streak.
    /// </summary>
    public IEnumerator PlayLevelCompletedRoutine()
    {
        m_FailureCount = 0;
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
