using System.Collections;
using UnityEngine;

/// <summary>
/// Fades a spawned particle effect to nothing over <see cref="Duration"/> seconds, then
/// destroys it. Added by <see cref="ParticleEffectSpawner"/> when a caller asks for a fade;
/// it is not meant to be placed on a prefab by hand.
///
/// The alpha is written onto the live particles rather than onto the renderer, because the
/// FX packs' materials (Mobile/Particles/Alpha Blended) expose only _MainTex — there is no
/// tint property to drive. Each frame scales the particles' alpha by the ratio between this
/// frame's target and the last one's, so the compounded result follows the fade without this
/// having to track individual particles as they die and the array compacts under them.
/// </summary>
public class ParticleFadeOut : MonoBehaviour
{
    /// <summary>Seconds the fade takes. Set by the spawner straight after AddComponent.</summary>
    public float Duration = 0.5f;

    private ParticleSystem[] m_Systems;
    private ParticleSystem.Particle[] m_Buffer;

    private IEnumerator Start()
    {
        m_Systems = GetComponentsInChildren<ParticleSystem>();

        if (Duration <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        // One frame for the systems' time-0 bursts to emit. Stopping emission before that
        // would leave the effect with nothing to fade.
        yield return null;

        // No new particles from here on, so a late arrival can't pop in at full alpha
        // halfway through the fade. Already-live particles keep going.
        foreach (ParticleSystem system in m_Systems)
        {
            if (system != null)
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        float elapsed = 0f;
        float previous = 1f;

        while (elapsed < Duration)
        {
            // Scaled time, unlike OneShotEffect's sprite playback: these systems simulate on
            // scaled time themselves (Unscaled Time is off in the prefabs), so a fade on
            // unscaled time would outrun the particles it is fading whenever the game slows.
            elapsed += Time.deltaTime;

            float target = Mathf.Clamp01(1f - elapsed / Duration);
            ScaleAlpha(previous > 0.0001f ? target / previous : 0f);
            previous = target;

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ScaleAlpha(float factor)
    {
        foreach (ParticleSystem system in m_Systems)
        {
            if (system == null) continue;

            int capacity = system.main.maxParticles;
            if (m_Buffer == null || m_Buffer.Length < capacity)
                m_Buffer = new ParticleSystem.Particle[capacity];

            int count = system.GetParticles(m_Buffer);
            for (int i = 0; i < count; i++)
            {
                Color32 colour = m_Buffer[i].startColor;
                // Rounded rather than truncated: the factor is applied every frame, and a
                // truncating cast would compound its loss into a visibly early blackout.
                colour.a = (byte)Mathf.RoundToInt(colour.a * factor);
                m_Buffer[i].startColor = colour;
            }

            system.SetParticles(m_Buffer, count);
        }
    }
}
