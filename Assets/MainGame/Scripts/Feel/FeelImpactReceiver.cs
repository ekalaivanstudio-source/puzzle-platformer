using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a prop as something the world can shove. Drop it on a brick, a spike, a lever, a
/// door — anything that should flinch when a body lands or an explosion goes off nearby —
/// and <see cref="FeelService.Impulse"/> will jolt it, hardest when the impact was close.
///
/// This is the half of "impact on the environment" that the level itself provides. The
/// camera kick and the buzz tell the player something happened; the scenery jumping is what
/// tells them it happened HERE.
///
/// It is opt-in by design. The obvious alternative — an overlap query that shoves whatever
/// it finds — also finds the level's tilemap collider, which is one object covering the
/// whole stage, and jolting that jolts the entire world. A component per prop keeps the
/// decision with whoever built the level. <c>Tools/Feel/Add Impact Receivers To Open
/// Scene</c> fills a scene in with sensible choices in one go.
///
/// Nothing here drives the transform itself: it hands the work to <see cref="FeelService"/>,
/// whose punches are applied as per-frame deltas and so cannot fight a script that is
/// already moving the object. The one thing to keep it OFF is a platform the player rides
/// while it is moving — not because it breaks, but because a shove that moves the floor
/// also moves whoever is standing on it.
/// </summary>
[DisallowMultipleComponent]
public class FeelImpactReceiver : MonoBehaviour
{
    // ─── Registry ─────────────────────────────────────────────────────────────

    private static readonly List<FeelImpactReceiver> s_All = new List<FeelImpactReceiver>();

    /// <summary>
    /// Every enabled receiver in the loaded scenes. Read by
    /// <see cref="FeelService.Impulse"/>; a level has a handful of these, so walking the
    /// list per impulse is cheaper than any spatial structure would be to maintain.
    /// </summary>
    public static IReadOnlyList<FeelImpactReceiver> All => s_All;

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Reaction")]
    [Tooltip("Transform that actually gets jolted. Leave empty to use this object's own. " +
             "Point it at a visual child when the root carries colliders or grid logic that " +
             "should stay put.")]
    [SerializeField] private Transform m_Target;

    [Tooltip("How far this prop is squashed by a full-strength impact, as a fraction of its " +
             "own scale. It flattens along the direction the impact pushed it and swells " +
             "across. 0 for a prop that should only slide, never deform.")]
    [SerializeField, Range(0f, 0.5f)] private float m_Squash = 0.1f;

    [Tooltip("How far a full-strength impact shoves this prop, in world units. Keep it well " +
             "under a cell — a tenth of one already reads clearly on this grid.")]
    [SerializeField, Min(0f)] private float m_ShoveDistance = 0.06f;

    [Tooltip("Seconds the jolt takes to settle back to nothing.")]
    [SerializeField, Min(0f)] private float m_Duration = 0.28f;

    [Tooltip("How many times the prop swings back and forth before it dies out. Around 2 " +
             "reads as something rattling; 1 as something taking a single knock.")]
    [SerializeField, Min(0f)] private float m_Frequency = 2.2f;

    [Header("Variation")]
    [Tooltip("Random spread applied to each jolt, as a fraction. Without it a row of " +
             "identical bricks all rattle in perfect lockstep, which reads as one object " +
             "rather than as several. 0 makes every reaction identical.")]
    [SerializeField, Range(0f, 0.5f)] private float m_Variance = 0.18f;

    [Header("Limits")]
    [Tooltip("Strongest impulse this prop will answer, after distance falloff. Caps how far " +
             "a heavy blast right next to a small prop can throw it.")]
    [SerializeField, Min(0f)] private float m_MaxStrength = 2f;

    [Tooltip("Weakest impulse worth answering. Below this the jolt is too small to see and " +
             "only costs a punch slot.")]
    [SerializeField, Min(0f)] private float m_MinStrength = 0.05f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable() => s_All.Add(this);

    private void OnDisable()
    {
        s_All.Remove(this);

        // PushBrick deactivates a shattered brick outright. Ending the punch here rather
        // than leaving it to expire means the brick is restored to its exact authored
        // transform, so the next full reset brings it back unshifted instead of carrying
        // whatever offset the punch still had applied when the object went away.
        FeelService.Instance?.CancelPunch(ResolveTarget());
    }

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Takes a hit. <paramref name="direction"/> points away from where the impact
    /// happened, and <paramref name="strength"/> has already been faded by distance —
    /// 1 is a full-strength hit at point-blank range.
    /// </summary>
    public void TakeImpact(Vector2 direction, float strength)
    {
        if (strength < m_MinStrength) return;

        Transform target = ResolveTarget();
        if (target == null) return;

        strength = Mathf.Min(strength, m_MaxStrength);

        // Rolled per hit, not per prop: the same brick knocked twice should not answer
        // identically both times.
        float variance = m_Variance > 0f
            ? 1f + Random.Range(-m_Variance, m_Variance)
            : 1f;

        FeelService.Instance?.AddPunch(
            target,
            direction,
            m_ShoveDistance * strength * variance,
            m_Squash * strength * variance,
            m_Duration * variance,
            m_Frequency);
    }

    private Transform ResolveTarget() => m_Target != null ? m_Target : transform;
}
