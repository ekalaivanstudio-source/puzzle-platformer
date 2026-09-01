using UnityEngine;

/// <summary>
/// Spawns authored particle prefabs (the Hyper Casual FX bursts and friends) as one-shot
/// effects and cleans them up afterwards.
///
/// The cleanup lives here because these prefabs are not the project's <see cref="OneShotEffect"/>
/// sprites: their systems play once but their Stop Action is None, so nothing in the prefab ever
/// removes the spawned object. The delay is read off the systems themselves — the longest duration
/// plus start delay plus particle lifetime any of them can reach — so re-timing an effect in its
/// prefab doesn't cut it short here. Floored at one second, since a curve-driven lifetime reports
/// nothing usable.
/// </summary>
public static class ParticleEffectSpawner
{
    /// <summary>
    /// Instantiates <paramref name="prefab"/> at <paramref name="position"/> and schedules its
    /// destruction for when its particle systems have finished. No-op when the prefab is unassigned.
    ///
    /// <paramref name="scale"/> multiplies the instance's scale. These packs are authored at a
    /// scale of their own — the flashes span a dozen world units, where this project's grid cell
    /// is one — so a caller placing a burst on a single tile needs to shrink it. The systems all
    /// use Hierarchy scaling mode, so scaling the root carries through to every child.
    ///
    /// <paramref name="zRotation"/> turns the instance about Z. These bursts are authored facing
    /// one way — an area effect blooms upward out of its origin — so a caller that wants one
    /// pointing the other way (a portal hanging overhead, dropping something out of itself)
    /// turns it 180. The systems simulate in local space, so the whole burst turns with it.
    ///
    /// <paramref name="fadeOutDuration"/>, when above zero, hands the instance to
    /// <see cref="ParticleFadeOut"/>: the burst fades away over that many seconds and is destroyed,
    /// rather than playing out to the full authored length the delay below would allow.
    /// </summary>
    public static GameObject Spawn(
        GameObject prefab, Vector3 position, float scale = 1f, float zRotation = 0f,
        float fadeOutDuration = 0f)
    {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(
            prefab, position, Quaternion.Euler(0f, 0f, zRotation));

        if (!Mathf.Approximately(scale, 1f))
            instance.transform.localScale = prefab.transform.localScale * scale;

        // The fade owns the cleanup when there is one: it always finishes before the authored
        // length below, so scheduling that second Destroy as well would serve no purpose.
        if (fadeOutDuration > 0f)
        {
            instance.AddComponent<ParticleFadeOut>().Duration = fadeOutDuration;
            return instance;
        }

        float lifetime = 0f;
        foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = system.main;
            lifetime = Mathf.Max(lifetime,
                main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        Object.Destroy(instance, Mathf.Max(lifetime, 1f));
        return instance;
    }
}
