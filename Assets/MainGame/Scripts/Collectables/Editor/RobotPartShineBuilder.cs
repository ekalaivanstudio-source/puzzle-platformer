using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Builds the twinkle that sits on top of an uncollected robot part, and strips whatever
    /// effect was on the pickup before it.
    ///
    /// The pickup used to carry a Hyper Casual FX particle prefab (Area_star_ellow) as a plain
    /// child — always on, never wired to <see cref="RobotPartPickup"/>'s Shine Effect slot, and
    /// a whole particle system for what is a five-frame sparkle. This replaces it with the
    /// hand-drawn <c>item_shine_effect</c> frames, drawn over the part rather than around it.
    ///
    /// Three sparkles rather than one: a single looping star reads as a decal stuck to the
    /// sprite, while three of different sizes flashing on different beats reads as light
    /// catching a surface. The beats are set by <see cref="Sparkle.GapFrames"/> and the frame
    /// rate, and are deliberately non-harmonic (1.00s / 1.40s / 1.14s) so they never settle
    /// into a pattern.
    ///
    /// Re-runnable: <b>Tools ▸ Robot Collection ▸ Rebuild Part Shine</b> edits the existing
    /// prefab in place, which is what keeps the twenty level scenes that instance it linked.
    /// <see cref="RobotCollectionSetup.BuildPrefabs"/> calls <see cref="Build"/> too, so a
    /// from-scratch rebuild of the pickup gets the same shine.
    /// </summary>
    public static class RobotPartShineBuilder
    {
        // ─── Paths ────────────────────────────────────────────────────────────────

        private const string PickupPrefabPath =
            "Assets/MainGame/Prefabs/Collectables/RobotPartPickup.prefab";

        private const string FrameFolder = "Assets/MainGame/Sprites/Effects/item_shine_effect";

        /// <summary>
        /// Unlit, because the level scenes light their sprites with Light2D and a shine that
        /// dims in a dark corner is the one place it is least use. The part itself stays lit.
        /// </summary>
        private const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>The child <see cref="RobotPartPickup"/>'s Shine Effect slot points at.</summary>
        public const string ShineObjectName = "Shine";

        /// <summary>Frame files under <see cref="FrameFolder"/>, in play order.</summary>
        private static readonly string[] FrameNames = { "1", "2", "3", "4", "5" };

        // ─── Feel ─────────────────────────────────────────────────────────────────

        // The part renderer is Default / 10, so one above it puts the sparkle over the part
        // without lifting it above anything else that shares the layer.
        private const int SortingOrder = 11;

        // Part sprites and shine frames are both 72px at 72 pixels-per-unit, so a sparkle at
        // scale 1 would be exactly as big as the part it is meant to be a highlight on. The
        // scales below are fractions of the part: about a half, a third and a quarter.
        private static readonly Sparkle[] Sparkles =
        {
            //          name             offset from part centre      scale  fps  gap  phase
            new Sparkle("Sparkle_Large", new Vector2( 0.28f,  0.30f), 0.50f, 12f,  7,  0),
            new Sparkle("Sparkle_Small", new Vector2(-0.30f, -0.20f), 0.32f, 10f,  9,  5),
            new Sparkle("Sparkle_Tiny",  new Vector2(-0.16f,  0.33f), 0.24f, 14f, 11,  9),
        };

        /// <summary>One flashing star: where it sits on the part, how big, and on what beat.</summary>
        private readonly struct Sparkle
        {
            public readonly string Name;
            public readonly Vector2 Offset;
            public readonly float Scale;
            public readonly float FramesPerSecond;

            /// <summary>
            /// Empty frames appended after the five drawn ones. They are what makes this a
            /// twinkle instead of a permanent star: <see cref="SpriteSheetAnimator"/> assigns
            /// whatever is at the current index, and a null there simply draws nothing.
            /// </summary>
            public readonly int GapFrames;

            /// <summary>
            /// How far into its own cycle this sparkle starts, as a frame count. Rotating the
            /// array is how the three avoid flashing together on the first frame of the level —
            /// the animator always restarts at index 0 when it is enabled.
            /// </summary>
            public readonly int PhaseFrames;

            public Sparkle(string name, Vector2 offset, float scale, float framesPerSecond,
                           int gapFrames, int phaseFrames)
            {
                Name = name;
                Offset = offset;
                Scale = scale;
                FramesPerSecond = framesPerSecond;
                GapFrames = gapFrames;
                PhaseFrames = phaseFrames;
            }
        }

        // ─── Entry point ──────────────────────────────────────────────────────────

        /// <summary>
        /// Patches the pickup prefab that is already on disk: strips the old particle child,
        /// rebuilds the shine, and points the Shine Effect slot at it.
        ///
        /// In-place rather than a fresh <c>SaveAsPrefabAsset</c> of a new hierarchy, because
        /// twenty level scenes hold instances of this prefab and re-authoring it from nothing
        /// gives every object in it a new file ID.
        /// </summary>
        [MenuItem("Tools/Robot Collection/Rebuild Part Shine", priority = 25)]
        public static void RebuildOnPickupPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RobotPartShine] No pickup prefab at {PickupPrefabPath}. " +
                               "Run Tools ▸ Robot Collection ▸ 3. Build Prefabs first.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PickupPrefabPath);
            try
            {
                int stripped = StripLegacyEffects(contents.transform);
                GameObject shine = Build(contents.transform);
                if (shine == null) return;

                WireShineSlot(contents, shine);
                PrefabUtility.SaveAsPrefabAsset(contents, PickupPrefabPath);

                Debug.Log($"[RobotPartShine] Rebuilt the shine on {PickupPrefabPath} " +
                          $"({Sparkles.Length} sparkles, {stripped} old effect object(s) removed).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ─── Build ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the "Shine" child under <paramref name="pickup"/>, replacing any existing
        /// one. Returns null when the frames cannot be loaded, having logged why.
        /// </summary>
        public static GameObject Build(Transform pickup)
        {
            Sprite[] frames = LoadFrames();
            if (frames == null) return null;

            Transform existing = pickup.Find(ShineObjectName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlit == null)
                Debug.LogWarning($"[RobotPartShine] No unlit sprite material at {UnlitMaterialPath}; " +
                                 "the sparkles will be lit and can be dimmed by the scene's 2D lights.");

            var root = new GameObject(ShineObjectName);
            root.transform.SetParent(pickup, worldPositionStays: false);

            foreach (Sparkle sparkle in Sparkles)
                BuildSparkle(root.transform, sparkle, frames, unlit);

            return root;
        }

        private static void BuildSparkle(Transform parent, Sparkle sparkle, Sprite[] frames,
                                         Material unlit)
        {
            var go = new GameObject(sparkle.Name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = sparkle.Offset;
            go.transform.localScale = Vector3.one * sparkle.Scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrder;
            if (unlit != null) renderer.sharedMaterial = unlit;
            // So the sparkle is visible in the scene view before play, where nothing has
            // driven the animator yet.
            renderer.sprite = frames[0];

            var animator = go.AddComponent<SpriteSheetAnimator>();
            Configure(animator, BuildCycle(frames, sparkle), sparkle.FramesPerSecond);
        }

        /// <summary>
        /// The sparkle's full cycle: the drawn frames, then <see cref="Sparkle.GapFrames"/>
        /// empty ones, rotated forward by <see cref="Sparkle.PhaseFrames"/>.
        /// </summary>
        private static Sprite[] BuildCycle(Sprite[] frames, Sparkle sparkle)
        {
            int length = frames.Length + Mathf.Max(0, sparkle.GapFrames);
            var raw = new Sprite[length];
            for (int i = 0; i < frames.Length; i++) raw[i] = frames[i];

            var cycle = new Sprite[length];
            int phase = ((sparkle.PhaseFrames % length) + length) % length;
            for (int i = 0; i < length; i++) cycle[i] = raw[(i + phase) % length];

            return cycle;
        }

        // ─── Wiring ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Points <see cref="RobotPartPickup"/>'s Shine Effect slot at the new object, which is
        /// what makes the shine come and go with the pickup instead of merely sitting on it.
        /// </summary>
        private static void WireShineSlot(GameObject pickup, GameObject shine)
        {
            var component = pickup.GetComponent<RobotPartPickup>();
            if (component == null)
            {
                Debug.LogWarning("[RobotPartShine] The pickup prefab has no RobotPartPickup " +
                                 "component, so the Shine Effect slot could not be wired.");
                return;
            }

            var so = new SerializedObject(component);
            SerializedProperty slot = so.FindProperty("m_ShineEffect");
            if (slot == null)
            {
                Debug.LogWarning("[RobotPartShine] RobotPartPickup has no m_ShineEffect field.");
                return;
            }

            slot.objectReferenceValue = shine;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes <see cref="SpriteSheetAnimator"/>'s serialized fields.</summary>
        private static void Configure(SpriteSheetAnimator animator, Sprite[] cycle, float fps)
        {
            var so = new SerializedObject(animator);

            SerializedProperty frames = so.FindProperty("m_Frames");
            frames.arraySize = cycle.Length;
            for (int i = 0; i < cycle.Length; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = cycle[i];

            so.FindProperty("m_FramesPerSecond").floatValue = fps;
            so.FindProperty("m_Loop").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the pickup's older idle effects — anything with a particle system under it.
        /// Returns how many objects went, so a re-run can say "0" honestly.
        /// </summary>
        private static int StripLegacyEffects(Transform pickup)
        {
            var doomed = new List<GameObject>();

            foreach (Transform child in pickup)
            {
                if (child.name == ShineObjectName) continue;
                if (child.GetComponentInChildren<ParticleSystem>(includeInactive: true) != null)
                    doomed.Add(child.gameObject);
            }

            foreach (GameObject go in doomed) Object.DestroyImmediate(go);
            return doomed.Count;
        }

        // ─── Frames ───────────────────────────────────────────────────────────────

        private static Sprite[] LoadFrames()
        {
            var frames = new Sprite[FrameNames.Length];

            for (int i = 0; i < FrameNames.Length; i++)
            {
                string path = $"{FrameFolder}/{FrameNames[i]}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError($"[RobotPartShine] No sprite at {path}. The file must be " +
                                   "imported as a Sprite (Texture Type: Sprite (2D and UI)).");
                    return null;
                }

                frames[i] = sprite;
            }

            return frames;
        }
    }
}
