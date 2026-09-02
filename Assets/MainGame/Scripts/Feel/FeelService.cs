using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The engine behind <see cref="FeelPreset"/> — the one place that knows how to turn "a
/// heavy hit happened here" into a camera kick, a buzz, a held frame, a squashed body,
/// a shove through the surrounding scenery and a puff of dust.
///
/// Call sites do not talk to this directly. They serialize a <see cref="FeelPreset"/> and
/// call <c>preset.Play(position, actor, direction)</c>; the preset routes through here.
/// The three public methods below are the individual channels, exposed for the rare caller
/// that wants one on its own — a hit stop with no shake, say.
///
/// Self-provisioning and persistent, like <see cref="CameraController"/>: the first feel
/// moment in the run creates it and it survives scene loads, so nothing has to be dropped
/// into twenty level scenes for the game to have feel.
///
/// ─── How the punches stay out of the way ─────────────────────────────────────
/// Squashing and shoving objects that other scripts are also driving (a brick mid-push, a
/// platform mid-patrol) is the part that normally breaks. Everything here is applied as a
/// DELTA in LateUpdate: each frame the punch adds the difference between the offset it
/// wants now and the offset it applied last frame, and it ends by applying exactly zero.
/// So whatever else moved the object that frame survives untouched, and the object always
/// lands back precisely where its owner left it — no captured "original position" to drift
/// out of date, and no fight over who owns the transform.
/// </summary>
[DisallowMultipleComponent]
public class FeelService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────

    private static FeelService s_Instance;
    private static bool s_Quitting;

    /// <summary>
    /// The live service, created on first use. Null only while the application is shutting
    /// down, so callers keep the null-conditional.
    /// </summary>
    public static FeelService Instance
    {
        get
        {
            if (s_Instance != null) return s_Instance;
            if (s_Quitting || !Application.isPlaying) return null;

            var holder = new GameObject("[FeelService]");
            s_Instance = holder.AddComponent<FeelService>();
            return s_Instance;
        }
    }

    // ─── Tuning ───────────────────────────────────────────────────────────────

    [Tooltip("Master switch. Off, every preset in the game goes quiet — useful for an " +
             "accessibility option, or for isolating whether feel is behind a bug.")]
    [SerializeField] private bool m_Enabled = true;

    [Tooltip("Scales every camera shake the game asks for. An accessibility 'reduce motion' " +
             "setting turns this down without touching a single call site.")]
    [SerializeField, Range(0f, 2f)] private float m_CameraShakeScale = 1f;

    [Tooltip("Scales every squash, shove and world impulse. Same idea as the shake scale.")]
    [SerializeField, Range(0f, 2f)] private float m_MotionScale = 1f;

    [Tooltip("Scales every hit stop. 0 removes all of them, which is the first thing to try " +
             "if the game starts feeling stuttery rather than weighty.")]
    [SerializeField, Range(0f, 2f)] private float m_HitStopScale = 1f;

    [Tooltip("Scales the alpha of every screen flash. Its own control rather than a share " +
             "of the motion scale, because a full-screen colour flash is the one effect " +
             "here that a photosensitive player may need turned down or off on its own.")]
    [SerializeField, Range(0f, 1f)] private float m_FlashScale = 1f;

    // ─── Punch state ──────────────────────────────────────────────────────────

    // One transform being squashed and shoved. Kept as a class, not a struct, because the
    // applied-offset bookkeeping is mutated in place every frame.
    private class Punch
    {
        public Transform Target;
        public Vector2 Axis;            // impact direction; the squash flattens along it
        public Vector3 ReferenceScale;  // the target's own scale, so the squash is proportional
        public float Distance;
        public float Squash;
        public float Duration;
        public float Frequency;
        public float Elapsed;

        // What this punch has already added to the transform. The whole conflict-free
        // scheme rests on these two: every frame applies the difference, never the total.
        public Vector3 AppliedPosition;
        public Vector3 AppliedScale;

        // What the transform read as when this punch last wrote to it. Anything else is
        // proof that another script has written it in between — see LateUpdate.
        public Vector3 LastPosition;
        public Vector3 LastScale;
        public bool HasWritten;
    }

    private readonly List<Punch> m_Punches = new List<Punch>();

    private Coroutine m_HitStopRoutine;
    private float m_DefaultFixedDelta = 0.02f;

    private Image m_FlashImage;
    private Coroutine m_FlashRoutine;

    // Above the game's own UI so a death flash washes the HUD too, and below the auto-play
    // tester's debug canvas (32000) so a designer can still find its button mid-flash.
    private const int k_FlashSortingOrder = 30000;

    // How far a transform may differ from what a punch last wrote before that counts as
    // another script having moved it. A punch stores exactly the value it wrote, so an
    // untouched transform matches bit for bit and this only has to absorb float noise.
    private const float k_DriftEpsilon = 1e-8f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this) { Destroy(gameObject); return; }
        s_Instance = this;
        DontDestroyOnLoad(gameObject);

        // Read rather than hardcoded, so a project that runs physics at something other
        // than 50Hz gets its own rate back after a hit stop.
        m_DefaultFixedDelta = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        if (s_Instance != this) return;
        s_Instance = null;

        // A hit stop that was still running would otherwise leave the whole game in slow
        // motion for good.
        RestoreTimeScale();
    }

    private void OnApplicationQuit() { s_Quitting = true; }

    // ─── Presets ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays every channel <paramref name="preset"/> has switched on. Called by
    /// <see cref="FeelPreset.Play"/>, which owns the chance roll and the cooldown — this
    /// runs the result.
    /// </summary>
    public void Play(FeelPreset preset, Vector3 position, Transform actor, Vector2 direction)
    {
        if (preset == null || !m_Enabled) return;

        if (preset.Delay > 0f)
        {
            StartCoroutine(DelayedPlayRoutine(preset, position, actor, direction));
            return;
        }

        PlayNow(preset, position, actor, direction);
    }

    // Unscaled, so a delayed reaction still lands on time when it was queued during a hit
    // stop — which is exactly when a delayed reaction is most often queued.
    private IEnumerator DelayedPlayRoutine(
        FeelPreset preset, Vector3 position, Transform actor, Vector2 direction)
    {
        yield return new WaitForSecondsRealtime(preset.Delay);
        PlayNow(preset, position, actor, direction);
    }

    private void PlayNow(FeelPreset preset, Vector3 position, Transform actor, Vector2 direction)
    {
        if (preset.CameraShake.IsActive && m_CameraShakeScale > 0f)
        {
            CameraController.Instance?.Shake(
                preset.CameraShake.Magnitude * m_CameraShakeScale,
                preset.CameraShake.Duration);
        }

        HapticService.Instance?.Play(preset.Haptic);

        if (preset.FreezeDuration > 0f && m_HitStopScale > 0f)
            HitStop(preset.FreezeDuration * m_HitStopScale, preset.FreezeTimeScale);

        if (actor != null && (preset.SquashAmount > 0f || preset.PunchDistance > 0f))
        {
            AddPunch(actor, direction,
                     preset.PunchDistance * m_MotionScale,
                     preset.SquashAmount * m_MotionScale,
                     preset.PunchDuration, preset.PunchFrequency);
        }

        if (preset.ImpulseStrength > 0f && preset.ImpulseRadius > 0f)
            Impulse(position, preset.ImpulseStrength * m_MotionScale, preset.ImpulseRadius);

        if (preset.FlashColor.a > 0f)
            Flash(preset.FlashColor, preset.FlashHold, preset.FlashFade, preset.FlashCount);

        if (preset.Particle != null)
            ParticleEffectSpawner.Spawn(preset.Particle, position, preset.ParticleScale);
    }

    // ─── Hit stop ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Holds the game clock down for <paramref name="duration"/> REAL seconds, then puts it
    /// back. The cheapest weight in the toolbox: a couple of frames held on the moment of
    /// contact does more for how a hit lands than any amount of shake.
    ///
    /// A second call replaces the first rather than nesting, so a flurry of impacts cannot
    /// hold the game down for their combined length.
    /// </summary>
    public void HitStop(float duration, float timeScale)
    {
        if (!m_Enabled || duration <= 0f) return;

        if (m_HitStopRoutine != null) StopCoroutine(m_HitStopRoutine);
        m_HitStopRoutine = StartCoroutine(HitStopRoutine(duration, Mathf.Clamp01(timeScale)));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        Time.timeScale = timeScale;

        // Stepped down with the clock so physics keeps the same number of steps per game
        // second. Floored above zero because a fixedDeltaTime of 0 is an error, even when
        // the freeze is a dead stop and no step will actually run.
        Time.fixedDeltaTime = m_DefaultFixedDelta * Mathf.Max(timeScale, 0.01f);

        yield return new WaitForSecondsRealtime(duration);

        m_HitStopRoutine = null;
        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_DefaultFixedDelta;
    }

    // ─── Screen flash ─────────────────────────────────────────────────────────

    /// <summary>
    /// Washes the whole screen with <paramref name="color"/>, holds it, then fades it out —
    /// <paramref name="count"/> times over. A red one on death is the classic use: it reads
    /// before the player has even worked out what killed them.
    ///
    /// The overlay it draws into is built on first use and lives on this service, so no
    /// scene has to carry a flash canvas and nothing has to be wired up. It never takes
    /// input: there is no GraphicRaycaster on it, so it cannot swallow a button press even
    /// while it is at full strength.
    ///
    /// A second flash replaces the first rather than queueing behind it.
    /// </summary>
    public void Flash(Color color, float hold, float fade, int count = 1)
    {
        if (!m_Enabled || m_FlashScale <= 0f) return;
        if (color.a <= 0f || count < 1) return;
        if (hold <= 0f && fade <= 0f) return;

        if (m_FlashRoutine != null) StopCoroutine(m_FlashRoutine);
        m_FlashRoutine = StartCoroutine(FlashRoutine(color, hold, fade, count));
    }

    // Real time throughout: a death flash fires on the same frame as a hit stop, and a
    // flash frozen by the freeze it is announcing would sit on screen as a solid pane of
    // colour until the clock came back.
    private IEnumerator FlashRoutine(Color color, float hold, float fade, int count)
    {
        Image image = ResolveFlashImage();
        if (image == null) { m_FlashRoutine = null; yield break; }

        float peak = Mathf.Clamp01(color.a * m_FlashScale);
        image.enabled = true;

        for (int pulse = 0; pulse < count; pulse++)
        {
            color.a = peak;
            image.color = color;

            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            float elapsed = 0f;
            while (elapsed < fade)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(peak, 0f, elapsed / fade);
                image.color = color;
                yield return null;
            }

            color.a = 0f;
            image.color = color;
        }

        // Switched off rather than left transparent, so a full-screen quad is not submitted
        // every frame for the rest of the level.
        image.enabled = false;
        m_FlashRoutine = null;
    }

    private Image ResolveFlashImage()
    {
        if (m_FlashImage != null) return m_FlashImage;

        var canvasObject = new GameObject("Feel Screen Flash");
        canvasObject.transform.SetParent(transform, worldPositionStays: false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = k_FlashSortingOrder;

        // Deliberately no GraphicRaycaster and no CanvasScaler: this canvas is one stretched
        // quad that must never be hit-tested, and a stretched quad needs no scaling rules.
        var imageObject = new GameObject("Flash");
        imageObject.transform.SetParent(canvasObject.transform, worldPositionStays: false);

        m_FlashImage = imageObject.AddComponent<Image>();
        m_FlashImage.raycastTarget = false;
        m_FlashImage.color = Color.clear;
        m_FlashImage.enabled = false;

        RectTransform rect = m_FlashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return m_FlashImage;
    }

    // ─── World impulse ────────────────────────────────────────────────────────

    /// <summary>
    /// Shoves every <see cref="FeelImpactReceiver"/> within <paramref name="radius"/> of
    /// <paramref name="position"/>, hardest at the centre and fading to nothing at the
    /// edge. This is the scenery noticing: bricks jolt when something lands next to them,
    /// spikes rattle when a body hits the floor.
    ///
    /// Only objects carrying a receiver answer. That is on purpose — a blanket physics
    /// query would also find the level's tilemap collider and shake the whole world.
    /// </summary>
    public void Impulse(Vector3 position, float strength, float radius)
    {
        if (!m_Enabled || strength <= 0f || radius <= 0f) return;

        IReadOnlyList<FeelImpactReceiver> receivers = FeelImpactReceiver.All;
        float radiusSquared = radius * radius;

        for (int i = 0; i < receivers.Count; i++)
        {
            FeelImpactReceiver receiver = receivers[i];
            if (receiver == null) continue;

            Vector3 offset = receiver.transform.position - position;
            offset.z = 0f;   // 2D: depth must not count toward the distance

            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > radiusSquared) continue;

            // Linear falloff on distance, not on the square, so the middle of the radius
            // gets half the strength rather than a quarter — a square falloff makes an
            // impulse read as "only the thing I hit moved".
            float falloff = 1f - Mathf.Sqrt(distanceSquared) / radius;

            // Straight up when the receiver is exactly on the impact point: a zero vector
            // would normalize to nothing and the prop would only squash, never jump.
            Vector2 direction = distanceSquared > 0.0001f
                ? ((Vector2)offset).normalized
                : Vector2.up;

            receiver.TakeImpact(direction, strength * falloff);
        }
    }

    // ─── Punches ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Squashes and shoves one transform. <paramref name="direction"/> is the way the
    /// impact pushed: the target flattens along that axis and swells across it, so a
    /// landing spreads a body sideways and a side-on shove stretches it upward. A zero
    /// direction squashes vertically, which is what a landing wants.
    /// </summary>
    public void AddPunch(
        Transform target, Vector2 direction, float distance, float squash,
        float duration, float frequency)
    {
        if (!m_Enabled || target == null) return;
        if (duration <= 0f || (distance <= 0f && squash <= 0f)) return;

        // A target already being punched is restarted, not layered: two overlapping punches
        // would add up to double the strength the caller asked for.
        CancelPunch(target);

        m_Punches.Add(new Punch
        {
            Target = target,
            Axis = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up,
            // Absolute, because the player's facing is a SIGN FLIP on localScale.x — read
            // raw, a left-facing body would squash inside out.
            ReferenceScale = new Vector3(
                Mathf.Abs(target.localScale.x), Mathf.Abs(target.localScale.y), 0f),
            Distance = distance,
            Squash = squash,
            Duration = duration,
            Frequency = Mathf.Max(frequency, 0.01f),
        });
    }

    /// <summary>
    /// Ends any punch on <paramref name="target"/> immediately, putting it back exactly
    /// where its owner had it. Call it before deactivating, teleporting or resetting
    /// something that might be mid-punch — otherwise the punch's outstanding offset is
    /// still on the transform when the object is put away, and it comes back shifted.
    /// </summary>
    public void CancelPunch(Transform target)
    {
        if (target == null) return;

        for (int i = m_Punches.Count - 1; i >= 0; i--)
        {
            if (m_Punches[i].Target != target) continue;
            ClearPunch(m_Punches[i]);
            m_Punches.RemoveAt(i);
        }
    }

    /// <summary>
    /// Ends every punch immediately, putting each target back exactly where it was. Call it
    /// before teleporting something that might be mid-punch.
    /// </summary>
    public void ClearPunches()
    {
        foreach (Punch punch in m_Punches) ClearPunch(punch);
        m_Punches.Clear();
    }

    // LateUpdate, so this runs after every script that drives a transform in Update and the
    // punch is the last word on the frame. Unscaled time throughout: a punch is normally
    // kicked off by an impact that also started a hit stop, and freezing the reaction along
    // with the game would hide the very thing the freeze is there to sell.
    private void LateUpdate()
    {
        for (int i = m_Punches.Count - 1; i >= 0; i--)
        {
            Punch punch = m_Punches[i];

            if (punch.Target == null) { m_Punches.RemoveAt(i); continue; }

            punch.Elapsed += Time.unscaledDeltaTime;
            bool finished = punch.Elapsed >= punch.Duration;

            // Re-baseline when someone else has written the transform since the last frame
            // — the player's facing flip is a sign change on localScale, a turn end assigns
            // the spawn scale outright, and the portal animation rewrites both every frame.
            // Subtracting an offset from a value that no longer contains it is what would
            // make the body drift a little smaller with every landing. Taking their value
            // as the new baseline instead means the punch adds on top of whatever they did
            // and only ever removes what it actually put there.
            if (punch.HasWritten)
            {
                if ((punch.Target.position - punch.LastPosition).sqrMagnitude > k_DriftEpsilon)
                    punch.AppliedPosition = Vector3.zero;

                if ((punch.Target.localScale - punch.LastScale).sqrMagnitude > k_DriftEpsilon)
                    punch.AppliedScale = Vector3.zero;
            }

            Vector3 position = finished ? Vector3.zero : PositionOffset(punch);
            Vector3 scale = finished ? Vector3.zero : ScaleOffset(punch);

            // The delta, never the total — see the class comment.
            punch.Target.position += position - punch.AppliedPosition;
            punch.Target.localScale += scale - punch.AppliedScale;

            punch.AppliedPosition = position;
            punch.AppliedScale = scale;
            punch.LastPosition = punch.Target.position;
            punch.LastScale = punch.Target.localScale;
            punch.HasWritten = true;

            if (finished) m_Punches.RemoveAt(i);
        }
    }

    // A cosine, not a sine: the punch has to be at full strength on the frame of impact and
    // decay from there. A sine starts at zero, which reads as the object winding up first.
    private static float Wave(Punch punch)
    {
        float t = Mathf.Clamp01(punch.Elapsed / punch.Duration);
        return Mathf.Cos(t * punch.Frequency * 2f * Mathf.PI) * (1f - t);
    }

    private static Vector3 PositionOffset(Punch punch)
    {
        if (punch.Distance <= 0f) return Vector3.zero;
        return (Vector3)(punch.Axis * (punch.Distance * Wave(punch)));
    }

    private static Vector3 ScaleOffset(Punch punch)
    {
        if (punch.Squash <= 0f) return Vector3.zero;

        float amount = punch.Squash * Wave(punch);

        // Flatten along the impact axis, swell across it. For a straight-down landing
        // (axis 0,-1) that is x:+1 / y:-1 — wider and shorter, the classic squash.
        float ax = Mathf.Abs(punch.Axis.x);
        float ay = Mathf.Abs(punch.Axis.y);

        return new Vector3(
            punch.ReferenceScale.x * (ay - ax) * amount,
            punch.ReferenceScale.y * (ax - ay) * amount,
            0f);
    }

    // Removes a punch's contribution without waiting for it to run out, leaving the target
    // exactly as its owner had it.
    private static void ClearPunch(Punch punch)
    {
        if (punch.Target == null || !punch.HasWritten) return;

        // Same rule as LateUpdate: only take back what is still there to take. A transform
        // another script has since rewritten no longer carries this punch's offset, and
        // subtracting it anyway would move the object away from where its owner put it.
        if ((punch.Target.position - punch.LastPosition).sqrMagnitude <= k_DriftEpsilon)
            punch.Target.position -= punch.AppliedPosition;

        if ((punch.Target.localScale - punch.LastScale).sqrMagnitude <= k_DriftEpsilon)
            punch.Target.localScale -= punch.AppliedScale;

        punch.AppliedPosition = Vector3.zero;
        punch.AppliedScale = Vector3.zero;
    }
}
