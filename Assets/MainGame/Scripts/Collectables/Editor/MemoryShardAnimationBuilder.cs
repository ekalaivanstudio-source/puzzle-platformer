using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Puts the shard's spin on the pickup prefab.
    ///
    /// The five sprites in <c>Sprites/Collectibles/memory shards/</c> are not five shards — they
    /// are one shard turning on the spot: front, three-quarter, edge-on, three-quarter, front.
    /// Played in order they are the animation the art was drawn as, so the pickup carries a
    /// <see cref="SpriteSheetAnimator"/> and the shard turns in the level. Nothing here moves or
    /// tints the shard by hand; the art already does it.
    ///
    /// The frames baked into the prefab are what the scene view shows before play. At runtime
    /// <see cref="MemoryShardPickup"/> replaces them with the database's, so re-drawing the
    /// shard means Build Database and nothing else.
    ///
    /// Re-runnable: <b>Tools ▸ Memory Shards ▸ Rebuild Shard Animation</b> edits the prefab
    /// already on disk rather than re-authoring it, which is what keeps the level scenes that
    /// instance it linked. <see cref="MemoryShardSetup.BuildPrefabs"/> calls <see cref="Build"/>
    /// too, so a from-scratch rebuild gets the same spin.
    /// </summary>
    public static class MemoryShardAnimationBuilder
    {
        // ─── Paths ────────────────────────────────────────────────────────────────

        private const string PickupPrefabPath =
            "Assets/MainGame/Prefabs/Collectables/MemoryShardPickup.prefab";

        private const string ShardSpriteFolder =
            "Assets/MainGame/Sprites/Collectibles/memory shards";

        // ─── Feel ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Five frames at 10fps is a half-second turn — quick enough to catch the eye from
        /// across a room, slow enough that the edge-on frame still reads as a shard seen side
        /// on rather than a flicker. Retune it on the prefab; this is only the value a rebuild
        /// starts from.
        /// </summary>
        private const float FramesPerSecond = 10f;

        // ─── Entry point ──────────────────────────────────────────────────────────

        /// <summary>
        /// Patches the pickup prefab on disk: gives it the spin, points the pickup at the
        /// animator, and clears out anything an earlier version of this tool left behind.
        ///
        /// In-place rather than a fresh <c>SaveAsPrefabAsset</c> of a new hierarchy, because
        /// every level scene holds an instance of this prefab and re-authoring it from nothing
        /// gives each object in it a new file ID.
        /// </summary>
        [MenuItem("Tools/Memory Shards/Rebuild Shard Animation", priority = 23)]
        public static void RebuildOnPickupPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[MemoryShards] No pickup prefab at {PickupPrefabPath}. " +
                               "Run Tools ▸ Memory Shards ▸ 2. Build Prefabs first.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PickupPrefabPath);
            try
            {
                var pickup = contents.GetComponent<MemoryShardPickup>();
                if (!Build(contents.transform, pickup)) return;

                PrefabUtility.SaveAsPrefabAsset(contents, PickupPrefabPath);
                Debug.Log($"[MemoryShards] Rebuilt the shard spin on {PickupPrefabPath} " +
                          $"({FramesPerSecond}fps). Every level instance picks it up.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ─── Build ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gives <paramref name="pickupRoot"/> the spin and wires it into
        /// <paramref name="pickup"/>. Returns false when the frames cannot be loaded, having
        /// logged why.
        /// </summary>
        public static bool Build(Transform pickupRoot, MemoryShardPickup pickup)
        {
            StripLegacyEffects(pickupRoot);

            Sprite[] frames = LoadFrames();
            if (frames == null) return false;

            var renderer = pickupRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[MemoryShards] The pickup has no SpriteRenderer, so there is " +
                                 "nothing for the spin to draw to.");
                return false;
            }

            // The front-facing frame, so the shard reads correctly in the scene view where
            // nothing has driven the animator yet.
            renderer.sprite = frames[0];

            var animator = pickupRoot.GetComponent<SpriteSheetAnimator>();
            if (animator == null) animator = pickupRoot.gameObject.AddComponent<SpriteSheetAnimator>();
            ConfigureAnimator(animator, frames, FramesPerSecond);

            WirePickup(pickup, animator);
            return true;
        }

        // ─── Wiring ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Points the pickup at the animator it hands the database's frames to, and empties the
        /// Shine Effect slot — the spin is the shard's idle now, and the slot would otherwise
        /// hold a reference to an object this rebuild deleted.
        /// </summary>
        private static void WirePickup(MemoryShardPickup pickup, SpriteSheetAnimator animator)
        {
            if (pickup == null)
            {
                Debug.LogWarning("[MemoryShards] The pickup prefab has no MemoryShardPickup " +
                                 "component, so the animator could not be wired.");
                return;
            }

            var so = new SerializedObject(pickup);

            SerializedProperty slot = so.FindProperty("m_SpinAnimator");
            if (slot != null) slot.objectReferenceValue = animator;
            else Debug.LogWarning("[MemoryShards] MemoryShardPickup has no m_SpinAnimator field.");

            SerializedProperty shine = so.FindProperty("m_ShineEffect");
            if (shine != null) shine.objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes <see cref="SpriteSheetAnimator"/>'s serialized fields.</summary>
        private static void ConfigureAnimator(SpriteSheetAnimator animator, Sprite[] frames, float fps)
        {
            var so = new SerializedObject(animator);

            SerializedProperty list = so.FindProperty("m_Frames");
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

            so.FindProperty("m_FramesPerSecond").floatValue = fps;
            so.FindProperty("m_Loop").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears out earlier attempts at a shard idle: the particle children the pickup used to
        /// carry, the "Shine" sparkle child, and any component whose script has since been
        /// deleted — a missing script left on the prefab shows up on every level instance.
        /// </summary>
        private static void StripLegacyEffects(Transform pickupRoot)
        {
            var doomed = new List<GameObject>();

            foreach (Transform child in pickupRoot)
            {
                if (child.name == "Shine" ||
                    child.GetComponentInChildren<ParticleSystem>(includeInactive: true) != null)
                    doomed.Add(child.gameObject);
            }

            foreach (GameObject go in doomed) Object.DestroyImmediate(go);

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(pickupRoot.gameObject);
        }

        // ─── Frames ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The spin frames, in numeric filename order. Shares
        /// <see cref="MemoryShardSetup.LoadFrameSprites"/> with the collect effect, so both
        /// discover their frames the same way — drop a file in the folder and it is picked up.
        ///
        /// Returns null when the folder holds no sprites, having logged why.
        /// </summary>
        private static Sprite[] LoadFrames()
        {
            Sprite[] frames = MemoryShardSetup.LoadFrameSprites(ShardSpriteFolder);
            if (frames.Length != 0) return frames;

            Debug.LogError($"[MemoryShards] No sprites in {ShardSpriteFolder}. The files must be " +
                           "imported as sprites (Texture Type: Sprite (2D and UI)).");
            return null;
        }
    }
}
