using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fills the open scene in with <see cref="FeelImpactReceiver"/>s so the level's props
/// react to impacts, without anyone having to add fifty components by hand.
///
/// <see cref="FeelImpactReceiver"/> is opt-in for a good reason (see its own comment — a
/// blanket physics query finds the tilemap collider and shakes the whole world), but
/// opt-in is only reasonable if opting in is one click. This is that click.
///
/// It is safe to re-run: an object that already has a receiver is left exactly as it is,
/// including any tuning done to it. Everything goes through Undo, so a single Ctrl+Z puts
/// the scene back.
///
/// What it deliberately skips: anything the player rides. A shove that moves the floor
/// also moves whoever is standing on it, so moving platforms and the level's moving floors
/// are left out and have to be opted in by hand if that is really wanted.
/// </summary>
public static class FeelSceneSetup
{
    // Props worth jolting: the things a player looks at when something lands near them.
    // Named rather than found by collider, so the choice stays readable and reviewable.
    private static readonly string[] k_CandidateTypes =
    {
        "PushBrick",
        "MovableBrick",
        "PlaceableKey",
        "KeySlot",
        "PlatformLever",
        "LaserRedirector",
        "SpikeAnimator",
        "LevelExitDoor",
        "LevelEntryDoor",
        "RobotPartPickup",
        "PipeConnection",
    };

    // Anything the player stands on. A jolt here moves the ground under their feet.
    private static readonly string[] k_ExcludedTypes =
    {
        "MovingPlatform",
        "LeverMovingPlatform",
        "FloorMovement",
        "FloorMovementLvl4",
        "PlayerController",
    };

    [MenuItem("Tools/Feel/Add Impact Receivers To Open Scene")]
    private static void AddReceivers()
    {
        var targets = new HashSet<GameObject>();

        foreach (string typeName in k_CandidateTypes)
        {
            System.Type type = ResolveType(typeName);
            if (type == null) continue;   // a script that does not exist in this project

            foreach (Object found in Object.FindObjectsByType(
                         type, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found is Component component) targets.Add(component.gameObject);
            }
        }

        int added = 0;
        int skipped = 0;

        foreach (GameObject target in targets)
        {
            if (IsExcluded(target)) { skipped++; continue; }
            if (target.GetComponent<FeelImpactReceiver>() != null) { skipped++; continue; }

            Undo.AddComponent<FeelImpactReceiver>(target);
            added++;
        }

        if (added > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[Feel] Added {added} impact receiver(s); left {skipped} object(s) alone " +
                  "(already had one, or ride-on scenery). Undo reverts the whole pass.");
    }

    [MenuItem("Tools/Feel/Remove Impact Receivers From Open Scene")]
    private static void RemoveReceivers()
    {
        FeelImpactReceiver[] receivers = Object.FindObjectsByType<FeelImpactReceiver>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (FeelImpactReceiver receiver in receivers)
            Undo.DestroyObjectImmediate(receiver);

        if (receivers.Length > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[Feel] Removed {receivers.Length} impact receiver(s).");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsExcluded(GameObject target)
    {
        foreach (string typeName in k_ExcludedTypes)
        {
            System.Type type = ResolveType(typeName);
            if (type == null) continue;

            // GetComponentInParent as well as on the object itself: a brick parented to a
            // moving platform rides it just as surely as the platform does.
            if (target.GetComponentInParent(type) != null) return true;
        }

        return false;
    }

    // The project's gameplay scripts live in the global namespace and the default assembly,
    // so a bare name is enough. Returns null for a name that no longer exists rather than
    // throwing, so renaming a script does not break the menu item.
    private static System.Type ResolveType(string typeName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        return typeof(Component).IsAssignableFrom(type) ? type : null;
    }
}
