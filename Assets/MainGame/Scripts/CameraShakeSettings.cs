using UnityEngine;

/// <summary>
/// One reusable camera-shake preset: the magnitude/duration pair every shake in the
/// project is described by. Serialize this instead of two loose floats, so a script
/// that shakes for more than one reason (a brick being shoved vs. shattered) keeps each
/// shake as a single named, tunable inspector field rather than a growing pile of
/// m_SomethingMagnitude / m_SomethingDuration pairs.
///
/// Play it with <see cref="Play"/>, or hand it to
/// <see cref="CameraController.Shake(CameraShakeSettings)"/>. Both no-op when the preset
/// is left at zero, which is how a caller turns its shake off from the inspector.
/// </summary>
[System.Serializable]
public struct CameraShakeSettings
{
    [Tooltip("How far the camera is thrown, in world units, at the start of the shake. " +
             "Fades to nothing across the duration. 0 disables this shake.")]
    public float Magnitude;

    [Tooltip("How long the shake lasts, in unscaled seconds. 0 disables this shake.")]
    public float Duration;

    public CameraShakeSettings(float magnitude, float duration)
    {
        Magnitude = magnitude;
        Duration = duration;
    }

    /// <summary>True when this preset would actually move the camera.</summary>
    public bool IsActive => Magnitude > 0f && Duration > 0f;

    /// <summary>
    /// Runs this shake. Safe to call unconditionally: does nothing when the preset is
    /// switched off, or when no <see cref="CameraController"/> exists in the scene.
    /// </summary>
    public void Play()
    {
        CameraController.Instance?.Shake(this);
    }
}
