using System;
using System.Collections.Generic;
using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// Global, scene-independent lookup from a string id to the live <see cref="TutorialTarget"/>.
    ///
    /// Targets register themselves on enable and unregister on disable, so the registry always
    /// reflects what is currently in the loaded scene(s). The <see cref="TutorialManager"/> uses
    /// this to resolve a step's Target Id into an actual object — and to *wait* for a target that
    /// hasn't spawned yet (via <see cref="OnTargetRegistered"/>).
    ///
    /// This is a static class, not a MonoBehaviour: there is nothing to place in a scene.
    /// </summary>
    public static class TutorialTargetRegistry
    {
        private static readonly Dictionary<string, TutorialTarget> s_Targets =
            new Dictionary<string, TutorialTarget>();

        /// <summary>Raised whenever a target registers. Argument is the target's id.</summary>
        public static event Action<string> OnTargetRegistered;

        /// <summary>All currently-registered target ids (snapshot copy, safe to iterate).</summary>
        public static IEnumerable<string> RegisteredIds => new List<string>(s_Targets.Keys);

        /// <summary>Registers (or replaces) a target under its id. No-op for empty ids.</summary>
        public static void Register(TutorialTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.TargetId)) return;

            if (s_Targets.TryGetValue(target.TargetId, out TutorialTarget existing) &&
                existing != null && existing != target)
            {
                Debug.LogWarning(
                    $"[TutorialTargetRegistry] Duplicate target id '{target.TargetId}'. " +
                    $"'{target.name}' is replacing '{existing.name}'. Ids must be unique.",
                    target);
            }

            s_Targets[target.TargetId] = target;
            OnTargetRegistered?.Invoke(target.TargetId);
        }

        /// <summary>Removes a target, but only if it is the one currently stored under its id.</summary>
        public static void Unregister(TutorialTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.TargetId)) return;
            if (s_Targets.TryGetValue(target.TargetId, out TutorialTarget existing) &&
                existing == target)
            {
                s_Targets.Remove(target.TargetId);
            }
        }

        /// <summary>Returns the target for <paramref name="id"/>, or null if not registered.</summary>
        public static TutorialTarget Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            s_Targets.TryGetValue(id, out TutorialTarget t);
            // A target can be destroyed without OnDisable in some teardown paths; guard against it.
            return t != null ? t : null;
        }

        /// <summary>True if a live target is registered under <paramref name="id"/>.</summary>
        public static bool Has(string id) => Get(id) != null;

        /// <summary>
        /// Clears the whole registry. Useful from a play-mode-exit hook in the editor so stale
        /// references don't survive domain-reload-disabled sessions. Not needed in builds.
        /// </summary>
        public static void Clear() => s_Targets.Clear();

        // When entering play mode with "Reload Domain" disabled, the static dictionary keeps stale
        // entries from the previous session. Clearing here guarantees a clean registry each run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Targets.Clear();
            OnTargetRegistered = null;
        }
    }
}
