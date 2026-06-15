using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One named, frame-based sprite clip played by <see cref="UIImageAnimator"/>.
/// </summary>
[Serializable]
public class SpriteAnimation
{
    [Tooltip("Reaction this clip plays for.")]
    public EvilDoctorAnimationController.DoctorAnimation AnimationType;

    [Tooltip("Ordered frames, played in a loop.")]
    public List<Sprite> Frames = new();

    [Min(1f)]
    [Tooltip("Playback speed in frames per second.")]
    public float FPS = 12f;

    /// <summary>True when this clip has at least one frame to show.</summary>
    public bool HasFrames => Frames != null && Frames.Count > 0;

    /// <summary>Seconds each frame is held. Guards against a zero/negative FPS.</summary>
    public float FrameDuration => 1f / Mathf.Max(1f, FPS);
}
