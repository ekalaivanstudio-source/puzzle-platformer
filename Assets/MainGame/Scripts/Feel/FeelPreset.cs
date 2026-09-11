using UnityEngine;

/// <summary>
/// One authored moment of game feel: everything that should happen when the game wants a
/// hit to land — the camera kick, the buzz in the player's hands, the freeze on the frame
/// of impact, the squash on whatever caused it, the shove the surrounding props take, and
/// the burst of dust at the point of contact.
///
/// Serialize it like <see cref="CameraShakeSettings"/> — one named, tunable inspector
/// field per moment — and play it with <see cref="Play"/>:
///
/// <code>
/// [SerializeField] private FeelPreset m_LandFeel = FeelPreset.Medium();
/// ...
/// m_LandFeel.Play(transform.position, transform, Vector2.down);
/// </code>
///
/// Every channel is off at zero, so a preset left blank in the inspector costs nothing and
/// a caller never needs a guard of its own. The static presets below (<see cref="Soft"/>,
/// <see cref="Light"/>, <see cref="Medium"/>, <see cref="Heavy"/>, <see cref="Ui"/>) are
/// tuned starting points for a one-unit grid, so a new call site is one word from feeling
/// right and can then be nudged in the inspector.
///
/// Deliberately not an asset: <see cref="CameraShakeSettings"/> established the inline
/// pattern here, and an inline field means a new feel moment works the moment the code
/// runs, with nothing to wire up across twenty level scenes and prefabs.
///
/// Audio is deliberately absent. Every call site in this project already plays its own
/// sound through <see cref="AudioManager"/>, and a second sound channel here would only
/// double them up.
/// </summary>
[System.Serializable]
public class FeelPreset
{
    // ─── When ─────────────────────────────────────────────────────────────────

    [Header("When")]
    [Tooltip("Seconds to wait before any of this plays, in unscaled time. Use it to land " +
             "a reaction a beat after its cause — dust settling after the thud.")]
    [Min(0f)] public float Delay = 0f;

    [Tooltip("Odds this preset plays at all, 0..1. Below 1 it fires only sometimes, which " +
             "is how a repeated action (a footstep) stops feeling mechanical.")]
    [Range(0f, 1f)] public float Chance = 1f;

    [Tooltip("Shortest gap between two plays of this preset, in unscaled seconds. Guards " +
             "against a per-frame caller — an overlap test, a collision that re-fires — " +
             "turning one impact into a machine-gun burst.")]
    [Min(0f)] public float Cooldown = 0f;

    // ─── Camera ───────────────────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("The camera kick. Runs through the project's existing CameraController, so it " +
             "shares the same fade-out and the same cancel-and-restore rules as every " +
             "other shake in the game.")]
    public CameraShakeSettings CameraShake;

    // ─── Haptics ──────────────────────────────────────────────────────────────

    [Header("Haptics")]
    [Tooltip("The buzz in the player's hands. Phone vibration, gamepad rumble, or an iOS " +
             "Taptic tap, whichever the player is holding. None is silent.")]
    public HapticPattern Haptic = HapticPattern.None;

    // ─── Hit stop ─────────────────────────────────────────────────────────────

    [Header("Hit Stop")]
    [Tooltip("Seconds the game clock is held down on the frame of impact, in REAL time. " +
             "The single cheapest way to make a hit feel like it landed: two or three " +
             "frames is usually enough, and anything past ~0.12s starts reading as a " +
             "stutter. 0 disables it.")]
    [Min(0f)] public float FreezeDuration = 0f;

    [Tooltip("What the game clock is held at during the freeze. 0 is a dead stop; a small " +
             "value like 0.05 keeps everything crawling, which reads as weight rather " +
             "than as a dropped frame.")]
    [Range(0f, 1f)] public float FreezeTimeScale = 0.05f;

    // ─── Actor punch ──────────────────────────────────────────────────────────

    [Header("Actor Punch (squash & stretch on whatever caused this)")]
    [Tooltip("How far the actor is squashed at the moment of impact, as a fraction of its " +
             "own scale. It flattens ALONG the impact direction and swells across it, so a " +
             "landing spreads the body sideways. 0.2 is a lot; 0.08 is a nudge. 0 disables.")]
    [Range(0f, 0.5f)] public float SquashAmount = 0f;

    [Tooltip("How far the actor is shoved along the impact direction, in world units. " +
             "Small — a tenth of a cell reads clearly on a one-unit grid. 0 disables.")]
    [Min(0f)] public float PunchDistance = 0f;

    [Tooltip("Seconds the squash and shove take to settle back to nothing.")]
    [Min(0f)] public float PunchDuration = 0.22f;

    [Tooltip("How many times the punch swings back and forth before it dies out. 1 is a " +
             "single squash-and-return; higher values wobble.")]
    [Min(0f)] public float PunchFrequency = 1.6f;

    // ─── World impulse ────────────────────────────────────────────────────────

    [Header("World Impulse (the scenery reacting)")]
    [Tooltip("How hard the surrounding props are shoved, as a multiplier on their own " +
             "reaction strength. Only objects carrying a FeelImpactReceiver answer, and " +
             "each one is scaled by how close it is. 0 disables.")]
    [Min(0f)] public float ImpulseStrength = 0f;

    [Tooltip("How far the impulse reaches, in world units. Props at the edge barely move; " +
             "props at the centre take the full strength.")]
    [Min(0f)] public float ImpulseRadius = 2.5f;

    // ─── Screen flash ─────────────────────────────────────────────────────────

    [Header("Screen Flash")]
    [Tooltip("Colour the whole screen is washed with. THE ALPHA IS THE SWITCH: at 0 there " +
             "is no flash at all, which is why a preset left alone never flashes. Around " +
             "0.5 reads as a hard hit without blanking out what is behind it.")]
    public Color FlashColor = new Color(1f, 1f, 1f, 0f);

    [Tooltip("Seconds the flash sits at full strength before it starts fading, in real " +
             "time. Keep it tiny — a flash that holds stops being a flash.")]
    [Min(0f)] public float FlashHold = 0.04f;

    [Tooltip("Seconds the flash takes to fade away, in real time.")]
    [Min(0f)] public float FlashFade = 0.18f;

    [Tooltip("How many times it flashes. 1 is a single wash; 2 is the double blink that " +
             "reads as an alarm rather than as an impact.")]
    [Min(1)] public int FlashCount = 1;

    // ─── Particles ────────────────────────────────────────────────────────────

    [Header("Particles")]
    [Tooltip("Optional burst spawned at the point of impact. Goes through " +
             "ParticleEffectSpawner, so it cleans itself up.")]
    public GameObject Particle;

    [Tooltip("Uniform scale for the burst. The stock FX packs are authored for a far " +
             "larger world than this one-unit grid, so most of them need shrinking here.")]
    [Min(0f)] public float ParticleScale = 1f;

    // ─── Runtime state ────────────────────────────────────────────────────────

    // Not serialized: this is per-run bookkeeping for Cooldown, not authored data.
    [System.NonSerialized] private float m_LastPlayTime = float.NegativeInfinity;

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when this preset would actually do something. Lets a caller skip work it would
    /// otherwise do just to feed a preset that is switched off.
    /// </summary>
    public bool IsActive =>
        CameraShake.IsActive || Haptic != HapticPattern.None || FreezeDuration > 0f ||
        SquashAmount > 0f || PunchDistance > 0f || ImpulseStrength > 0f ||
        FlashColor.a > 0f || Particle != null;

    /// <summary>
    /// Plays every channel this preset has switched on.
    ///
    /// <paramref name="position"/> is where the impact happened — the particle burst goes
    /// there and the world impulse radiates from there.
    /// <paramref name="actor"/> is the thing that caused it, and is what the squash and
    /// the shove are applied to. Pass null for a feel moment with no body behind it (a UI
    /// rejection, a level ending).
    /// <paramref name="direction"/> is which way the impact pushed — down for a landing,
    /// sideways for a shove. Left at zero the punch squashes vertically, which is what a
    /// landing wants and a sane default for everything else.
    ///
    /// Safe to call unconditionally and as often as you like: a blank preset, a failed
    /// chance roll and a call inside the cooldown all return here quietly.
    /// </summary>
    public void Play(Vector3 position, Transform actor = null, Vector2 direction = default)
    {
        if (Cooldown > 0f && Time.unscaledTime - m_LastPlayTime < Cooldown) return;
        if (Chance < 1f && Random.value > Chance) return;

        m_LastPlayTime = Time.unscaledTime;
        FeelService.Instance?.Play(this, position, actor, direction);
    }

    // ─── Tuned starting points ────────────────────────────────────────────────

    /// <summary>A cushioned touch — a footstep, a light contact. Barely there on purpose.</summary>
    public static FeelPreset Soft() => new FeelPreset
    {
        Haptic = HapticPattern.SoftImpact,
        SquashAmount = 0.05f,
        PunchDuration = 0.18f,
        Chance = 1f,
    };

    /// <summary>A small knock — a jump taking off, a queued command, a pickup.</summary>
    public static FeelPreset Light() => new FeelPreset
    {
        CameraShake = new CameraShakeSettings(0.035f, 0.12f),
        Haptic = HapticPattern.LightImpact,
        SquashAmount = 0.07f,
        PunchDuration = 0.18f,
    };

    /// <summary>A solid hit — landing a jump, shoving a brick. Where hit stop starts.</summary>
    public static FeelPreset Medium() => new FeelPreset
    {
        CameraShake = new CameraShakeSettings(0.07f, 0.18f),
        Haptic = HapticPattern.MediumImpact,
        FreezeDuration = 0.04f,
        FreezeTimeScale = 0.05f,
        SquashAmount = 0.12f,
        PunchDistance = 0.05f,
        PunchDuration = 0.22f,
        ImpulseStrength = 0.6f,
        ImpulseRadius = 2f,
    };

    /// <summary>The full thump — a death, a brick shattering, an explosion.</summary>
    public static FeelPreset Heavy() => new FeelPreset
    {
        CameraShake = new CameraShakeSettings(0.16f, 0.35f),
        Haptic = HapticPattern.HeavyImpact,
        FreezeDuration = 0.09f,
        FreezeTimeScale = 0.02f,
        SquashAmount = 0.18f,
        PunchDistance = 0.09f,
        PunchDuration = 0.3f,
        ImpulseStrength = 1.4f,
        ImpulseRadius = 4f,
    };

    /// <summary>
    /// Screen furniture — a button, a rejected input. Haptics and a camera tick only,
    /// because most screen moments have nothing of their own to move.
    ///
    /// A UI element passed as the actor DOES squash — it has a transform like anything else,
    /// and <see cref="PlayerInputUIHelper"/> punches the input row that way. It is the WORLD
    /// channels that have no world to act in on a canvas: the impulse, the particle burst,
    /// and <see cref="PunchDistance"/>, which is measured in world units and vanishes against
    /// a canvas measured in pixels.
    /// </summary>
    public static FeelPreset Ui(HapticPattern haptic = HapticPattern.Selection) => new FeelPreset
    {
        Haptic = haptic,
        CameraShake = new CameraShakeSettings(0.03f, 0.1f),
    };
}
