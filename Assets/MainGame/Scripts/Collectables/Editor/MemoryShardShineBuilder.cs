using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Dresses the memory shard pickup so it reads as a collectable rather than a decal:
    ///
    ///   • a shine band sweeping across the shard (<c>MainGame/Sprite Shine</c> material),
    ///   • a soft glow and two counter-rotating god-ray layers behind it,
    ///   • a breathing scale pulse (<see cref="CollectableIdleFx"/>).
    ///
    /// The glow and rays live under a "Shine" child wired to
    /// <see cref="MemoryShardPickup"/>'s Shine Effect slot, so they come and go with the shard.
    /// Everything here is unlit: the levels light their sprites with Light2D, and a collectable
    /// that dims in a dark corner is exactly the one the player misses.
    ///
    /// Re-runnable: <b>Tools ▸ Memory Shards ▸ Rebuild Shard Shine</b> edits the existing
    /// prefab in place, which keeps the level scenes that instance it linked.
    /// <see cref="MemoryShardSetup.BuildPrefabs"/> calls <see cref="Build"/> too.
    /// </summary>
    public static class MemoryShardShineBuilder
    {
        // ─── Paths ────────────────────────────────────────────────────────────────

        private const string PickupPrefabPath =
            "Assets/MainGame/Prefabs/Collectables/MemoryShardPickup.prefab";

        private const string FxFolder = "Assets/MainGame/Sprites/Effects/memory_shard_fx";
        private const string GodRaysPath = FxFolder + "/godrays.png";
        private const string GlowPath = FxFolder + "/glow.png";

        private const string ShineShaderName = "MainGame/Sprite Shine";
        private const string ShineMaterialPath = "Assets/MainGame/Materials/MemoryShardShine.mat";

        private const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>The child <see cref="MemoryShardPickup"/>'s Shine Effect slot points at.</summary>
        public const string ShineObjectName = "Shine";

        // ─── Feel ─────────────────────────────────────────────────────────────────

        // The shard renderer is Default / 10; everything here sits just behind it.
        private const int GlowSortingOrder = 7;
        private const int RaysBackSortingOrder = 8;
        private const int RaysFrontSortingOrder = 9;

        // The FX textures are 256px imported at 100 PPU (2.56 units); the shard is 1 unit.
        private const float FxPixelsPerUnit = 100f;
        private const float GlowScale = 0.9f;
        private const float RaysBackScale = 1.25f;
        private const float RaysFrontScale = 0.9f;

        // Pale cyan to match the shard and the level's portal light.
        private static readonly Color GlowColor = new Color(0.45f, 0.85f, 1f, 0.6f);
        private static readonly Color RaysBackColor = new Color(0.6f, 0.92f, 1f, 0.35f);
        private static readonly Color RaysFrontColor = new Color(1f, 1f, 1f, 0.45f);

        // ─── Entry point ──────────────────────────────────────────────────────────

        [MenuItem("Tools/Memory Shards/Rebuild Shard Shine", priority = 25)]
        public static void RebuildOnPickupPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath) == null)
            {
                Debug.LogError($"[MemoryShardShine] No pickup prefab at {PickupPrefabPath}. " +
                               "Run Tools ▸ Memory Shards ▸ 2. Build Prefabs first.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PickupPrefabPath);
            try
            {
                if (!Build(contents)) return;
                PrefabUtility.SaveAsPrefabAsset(contents, PickupPrefabPath);
                Debug.Log($"[MemoryShardShine] Rebuilt the shine on {PickupPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ─── Build ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the shine material, (re)creates the glow + god rays child, and adds and
        /// wires <see cref="CollectableIdleFx"/>. Returns false when an asset is missing,
        /// having logged why.
        /// </summary>
        public static bool Build(GameObject pickup)
        {
            Sprite rays = LoadFxSprite(GodRaysPath);
            Sprite glow = LoadFxSprite(GlowPath);
            Material shine = LoadOrCreateShineMaterial();
            if (rays == null || glow == null || shine == null) return false;

            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlit == null)
                Debug.LogWarning($"[MemoryShardShine] No unlit sprite material at {UnlitMaterialPath}; " +
                                 "the rays will be lit and can be dimmed by the scene's 2D lights.");

            var renderer = pickup.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sharedMaterial = shine;

            Transform existing = pickup.transform.Find(ShineObjectName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(ShineObjectName);
            root.transform.SetParent(pickup.transform, worldPositionStays: false);

            SpriteRenderer glowRenderer = BuildLayer(root.transform, "Glow", glow, unlit,
                                                     GlowScale, GlowColor, GlowSortingOrder);
            SpriteRenderer back = BuildLayer(root.transform, "GodRays_Back", rays, unlit,
                                             RaysBackScale, RaysBackColor, RaysBackSortingOrder);
            SpriteRenderer front = BuildLayer(root.transform, "GodRays_Front", rays, unlit,
                                              RaysFrontScale, RaysFrontColor, RaysFrontSortingOrder);
            // Start the two layers off-axis so their beams interleave from the first frame.
            front.transform.localRotation = Quaternion.Euler(0f, 0f, 22.5f);

            var fx = pickup.GetComponent<CollectableIdleFx>();
            if (fx == null) fx = pickup.AddComponent<CollectableIdleFx>();

            var fxSo = new SerializedObject(fx);
            fxSo.FindProperty("m_ScaleTarget").objectReferenceValue = pickup.transform;
            SerializedProperty layers = fxSo.FindProperty("m_Rays");
            layers.arraySize = 2;
            layers.GetArrayElementAtIndex(0).objectReferenceValue = back.transform;
            layers.GetArrayElementAtIndex(1).objectReferenceValue = front.transform;
            fxSo.FindProperty("m_Glow").objectReferenceValue = glowRenderer;
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            WireShineSlot(pickup, root);
            return true;
        }

        private static SpriteRenderer BuildLayer(Transform parent, string name, Sprite sprite,
                                                 Material unlit, float scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localScale = Vector3.one * scale;

            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            if (unlit != null) r.sharedMaterial = unlit;
            return r;
        }

        // ─── Wiring ───────────────────────────────────────────────────────────────

        private static void WireShineSlot(GameObject pickup, GameObject shine)
        {
            var component = pickup.GetComponent<MemoryShardPickup>();
            if (component == null)
            {
                Debug.LogWarning("[MemoryShardShine] The pickup prefab has no MemoryShardPickup " +
                                 "component, so the Shine Effect slot could not be wired.");
                return;
            }

            var so = new SerializedObject(component);
            so.FindProperty("m_ShineEffect").objectReferenceValue = shine;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ─── Assets ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads one of the generated FX textures as a sprite, fixing its import settings first
        /// if it came in as a plain texture.
        /// </summary>
        private static Sprite LoadFxSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[MemoryShardShine] No texture at {path}.");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, FxPixelsPerUnit) ||
                !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = FxPixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material LoadOrCreateShineMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ShineMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find(ShineShaderName);
            if (shader == null)
            {
                Debug.LogError($"[MemoryShardShine] Shader '{ShineShaderName}' not found.");
                return null;
            }

            material = new Material(shader);
            material.SetColor("_ShineColor", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_ShineIntensity", 1.1f);
            material.SetFloat("_ShineWidth", 0.2f);
            material.SetFloat("_ShineSoftness", 0.5f);
            material.SetFloat("_ShineAngle", 35f);
            material.SetFloat("_ShineSize", 1.3f);
            material.SetFloat("_ShineDuration", 0.5f);
            material.SetFloat("_ShineInterval", 1.6f);
            AssetDatabase.CreateAsset(material, ShineMaterialPath);
            return material;
        }
    }
}
