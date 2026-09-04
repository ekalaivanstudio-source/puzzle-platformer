/// <summary>
/// The vocabulary of one-shot haptics the game can ask for.
///
/// These are deliberately named after the *meaning* of the buzz rather than after a
/// duration and a strength — a caller says "this was a heavy impact", not "vibrate for 55
/// milliseconds at 100%". That is what lets <see cref="HapticService"/> answer the same
/// request differently on each platform: an iPhone plays its own Taptic impact, an Android
/// phone plays a tuned waveform on its vibration motor, and a gamepad spins its rumble
/// motors, all from the one enum value.
///
/// The set mirrors the system haptic vocabulary both mobile platforms already speak
/// (iOS's UIImpactFeedbackGenerator / UINotificationFeedbackGenerator styles, and the
/// equivalents Android exposes through VibrationEffect), so nothing here has to be
/// translated twice.
/// </summary>
public enum HapticPattern
{
    /// <summary>No haptic at all. The default, so an unfilled inspector field is silent.</summary>
    None = 0,

    /// <summary>The lightest tick there is. For moving between UI choices, queuing a command.</summary>
    Selection,

    /// <summary>A small knock. A footstep, a button press, a light landing.</summary>
    LightImpact,

    /// <summary>A solid knock. Shoving a brick, landing a jump.</summary>
    MediumImpact,

    /// <summary>The full thump. Death, an explosion, a heavy landing.</summary>
    HeavyImpact,

    /// <summary>Short and sharp — a snap rather than a thud. Something locking into place.</summary>
    RigidImpact,

    /// <summary>Long and gentle — a cushioned landing, something settling.</summary>
    SoftImpact,

    /// <summary>Two rising pulses. A level solved, a battery socketed.</summary>
    Success,

    /// <summary>Two equal pulses. Input rejected, a move refused.</summary>
    Warning,

    /// <summary>Three heavy pulses. The attempt failed.</summary>
    Failure,
}
