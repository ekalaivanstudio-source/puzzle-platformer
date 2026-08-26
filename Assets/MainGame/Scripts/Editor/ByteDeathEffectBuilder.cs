using System.Collections.Generic;
using System.IO;
using System.Linq;
using CartoonFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the two prefabs Byte's death is made of, and wires them onto the player prefab.
///
///  • ByteDeathExplosion — a variant of the pack's CFXR3 Fire Explosion B, shrunk for this
///    one-unit grid, sorted in front of the level art, and with the pack's own camera shake
///    switched off so it cannot fight <see cref="CameraController"/>'s. Both write the
///    camera's localPosition, and the loser leaves it permanently off centre.
///  • ByteDeathDebris — one particle system per body piece in the Destroy folder, each firing
///    a single piece outward under gravity and bouncing it off the ground through the
///    Collision module in 2D mode.
///
/// This is a generator rather than two hand-built prefabs because the debris is a dozen
/// particle modules times five systems: re-running the menu item is how you re-tune the feel
/// (edit the constants below), and it is safe to run repeatedly — it overwrites in place.
/// Nudging a single value in the Inspector afterwards is still fine; just know that a rebuild
/// throws those nudges away.
/// </summary>
public static class ByteDeathEffectBuilder
{
    // ─── Paths ───────────────────────────────────────────────────────────────────

    private const string k_PiecesFolder   = "Assets/MainGame/Sprites/Byte/Destroy";
    private const string k_EffectsFolder  = "Assets/MainGame/Prefabs/Effects";
    private const string k_MaterialFolder = "Assets/MainGame/Materials/Effects";
    private const string k_PlayerPrefab   = "Assets/MainGame/Prefabs/Player.prefab";
    private const string k_SourceExplosion =
        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab";

    private const string k_ExplosionPrefab = k_EffectsFolder + "/ByteDeathExplosion.prefab";
    private const string k_DebrisPrefab    = k_EffectsFolder + "/ByteDeathDebris.prefab";

    // ─── Feel ────────────────────────────────────────────────────────────────────

    // The CFXR packs are authored for a metre-scale world; this grid's cell is one unit and
    // Byte is about one cell tall, so the stock explosion still has to come down from 1. At
    // 1.2 the fireball is a little wider than Byte is tall — the blast reads as the thing
    // that killed him rather than as a spark coming off him.
    private const float k_ExplosionScale = 1.2f;

    // 24px pieces imported at 100 pixels-per-unit would be 0.24 units — a quarter of Byte's
    // ~0.9-unit width, too small to recognise as body parts once they are moving. Twice that
    // reads as five chunks of the robot that was standing there.
    private const float k_PieceSize = 0.48f;

    private const float k_SpeedMin = 3f;
    private const float k_SpeedMax = 6.5f;

    // Long enough to arc, bounce and settle, and no longer: a piece that has come to rest
    // still has to be got rid of, and the shrink below only has the tail of this to do it in.
    private const float k_LifetimeMin = 1.1f;
    private const float k_LifetimeMax = 1.6f;

    // A multiplier on Physics.gravity, so 1 is a real 9.81. Heavier than that pinned the
    // pieces to the floor within half a metre of where Byte was standing — on a one-unit grid
    // there is no room for a fast fall, and the throw has to stay in the air to be seen.
    private const float k_Gravity = 1f;

    private const float k_Bounce = 0.35f;   // "little bounce" — a third of the impact speed back
    private const float k_Dampen = 0.3f;    // speed shed along the surface on every hit
    private const float k_SpinDegrees = 400f;
    private const float k_EmitRadius = 0.2f;

    // What the pieces shrink to by the end of their life. They are gone before they reach it —
    // the alpha fade below finishes first — but the shrink is what stops a settled piece from
    // reading as a prop lying on the floor while it waits to be removed.
    private const float k_EndSizeScale = 0.2f;

    // In front of the level art and of Byte himself (his renderer is Default / 4).
    private const string k_SortingLayer = "Default";
    private const int k_SortingOrder = 20;

    // Only real ground stops a piece. Anything it misses simply falls out of shot and dies with
    // its lifetime, which is why an unlisted layer is a cosmetic issue and not a bug.
    private static readonly string[] k_CollisionLayers = { "Ground" };

    [MenuItem("Tools/Byte/Rebuild Death Effect Prefabs")]
    private static void Rebuild()
    {
        Directory.CreateDirectory(k_EffectsFolder);
        Directory.CreateDirectory(k_MaterialFolder);
        AssetDatabase.Refresh();

        GameObject explosion = BuildExplosion();
        GameObject debris = BuildDebris();
        if (explosion == null || debris == null) return;

        WirePlayer(explosion, debris);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ByteDeathEffect] Rebuilt {k_ExplosionPrefab} and {k_DebrisPrefab}, and wired " +
                  $"both onto {k_PlayerPrefab}.");
    }

    // ─── Explosion ───────────────────────────────────────────────────────────────

    private static GameObject BuildExplosion()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(k_SourceExplosion);
        if (source == null)
        {
            Debug.LogError($"[ByteDeathEffect] Missing source explosion at {k_SourceExplosion}.");
            return null;
        }

        // Instantiated as a Prefab instance (not a plain clone) so saving it below produces a
        // Variant: a re-import of the FX pack still flows through to Byte's explosion, and the
        // overrides made here stay readable as overrides.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "ByteDeathExplosion";
        instance.transform.localScale = Vector3.one * k_ExplosionScale;

        var effect = instance.GetComponent<CFXR_Effect>();
        if (effect != null && effect.cameraShake != null)
            effect.cameraShake.enabled = false;

        foreach (ParticleSystemRenderer renderer in
                 instance.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true))
        {
            renderer.sortingLayerName = k_SortingLayer;
            renderer.sortingOrder = k_SortingOrder;
        }

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, k_ExplosionPrefab);
        Object.DestroyImmediate(instance);
        return asset;
    }

    // ─── Debris ──────────────────────────────────────────────────────────────────

    private static GameObject BuildDebris()
    {
        List<Sprite> pieces = LoadPieces();
        if (pieces.Count == 0)
        {
            Debug.LogError($"[ByteDeathEffect] No piece sprites found under {k_PiecesFolder}.");
            return null;
        }

        int collisionMask = 0;
        foreach (string layer in k_CollisionLayers)
        {
            int index = LayerMask.NameToLayer(layer);
            if (index >= 0) collisionMask |= 1 << index;
            else Debug.LogWarning($"[ByteDeathEffect] No layer named '{layer}' — debris will not " +
                                  "bounce off it.");
        }

        var root = new GameObject("ByteDeathDebris");

        // One system per piece rather than one system with a sprite sheet: the pieces are five
        // separate 24px textures, and the Texture Sheet Animation module can only pick between
        // sprites that share a single atlas.
        foreach (Sprite piece in pieces)
            BuildPieceSystem(root.transform, piece, collisionMask);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, k_DebrisPrefab);
        Object.DestroyImmediate(root);
        return asset;
    }

    private static void BuildPieceSystem(Transform parent, Sprite piece, int collisionMask)
    {
        var go = new GameObject(piece.name);
        go.transform.SetParent(parent, worldPositionStays: false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(k_LifetimeMin, k_LifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(k_SpeedMin, k_SpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(k_PieceSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(k_Gravity);
        // World space is not a preference here: the Collision module refuses to run against
        // world colliders from a locally-simulated system.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4;
        // Cleanup belongs to ParticleEffectSpawner, which already sizes its delay off these
        // modules. A Destroy stop action here would only take this one child with it.
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        // Circle lies in the local XY plane, so every piece is thrown along the screen rather
        // than toward or away from the camera.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = k_EmitRadius;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.randomDirectionAmount = 0f;
        shape.alignToDirection = false;

        // Tumble, and in either direction — five pieces all spinning the same way reads as a
        // sheet of them rather than as wreckage. Two curves rather than two constants so the
        // spin bleeds off towards zero: at a fixed rate a piece that had already come to rest
        // kept turning on the spot, which read as an object stuck mid-animation rather than as
        // wreckage that had landed. Each piece picks its own point between the two, so some
        // barely turn at all.
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = false;
        spin.z = new ParticleSystem.MinMaxCurve(
            k_SpinDegrees * Mathf.Deg2Rad,
            AnimationCurve.EaseInOut(0f, -1f, 1f, 0f),
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        // High is the only quality that raycasts per particle; the cheaper ones approximate the
        // world with a handful of cached planes, which a 2D tile floor does not survive.
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.bounce = new ParticleSystem.MinMaxCurve(k_Bounce);
        collision.dampen = new ParticleSystem.MinMaxCurve(k_Dampen);
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0f);
        collision.minKillSpeed = 0f;
        collision.radiusScale = 0.4f;
        collision.collidesWith = collisionMask;
        collision.enableDynamicColliders = true;
        collision.sendCollisionMessages = false;

        // Full size until the piece has landed, then pulled down into nothing over the back
        // half. Paired with the alpha below on the same timing, so a piece leaves by shrinking
        // away rather than by blinking off while still lying there at full size.
        ParticleSystem.SizeOverLifetimeModule shrink = ps.sizeOverLifetime;
        shrink.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 1f), new Keyframe(1f, k_EndSizeScale));
        shrink.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Held at full alpha until the piece has come to rest, then taken out over the back
        // half alongside the shrink, so nothing pops out of existence mid-bounce.
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = GetPieceMaterial(piece);
        renderer.sortingLayerName = k_SortingLayer;
        renderer.sortingOrder = k_SortingOrder;
    }

    // ─── Assets ──────────────────────────────────────────────────────────────────

    private static List<Sprite> LoadPieces()
    {
        if (!AssetDatabase.IsValidFolder(k_PiecesFolder)) return new List<Sprite>();

        return AssetDatabase.FindAssets("t:Sprite", new[] { k_PiecesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(sprite => sprite != null)
            .OrderBy(sprite => sprite.name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Reuses the material when the menu item is re-run, so a rebuild doesn't strand the old one
    // in the project and doesn't break anything already pointing at it.
    private static Material GetPieceMaterial(Sprite piece)
    {
        string path = $"{k_MaterialFolder}/{piece.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        // URP's own 2D sprite shader where the pipeline provides it; the built-in one is the
        // fallback so this still produces something usable in a non-URP project.
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                        ?? Shader.Find("Sprites/Default");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.mainTexture = piece.texture;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ─── Player wiring ───────────────────────────────────────────────────────────

    private static void WirePlayer(GameObject explosion, GameObject debris)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(k_PlayerPrefab);
        if (contents == null)
        {
            Debug.LogError($"[ByteDeathEffect] Could not open {k_PlayerPrefab}.");
            return;
        }

        try
        {
            var controller = contents.GetComponent<PlayerController>();
            if (controller == null)
            {
                Debug.LogError("[ByteDeathEffect] Player prefab root has no PlayerController.");
                return;
            }

            var serialized = new SerializedObject(controller);
            Set(serialized, "m_DeathExplosion", explosion);
            Set(serialized, "m_DeathDebris", debris);
            Set(serialized, "m_SpriteRenderer", contents.GetComponent<SpriteRenderer>());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, k_PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Set(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[ByteDeathEffect] PlayerController has no field '{field}'.");
            return;
        }

        property.objectReferenceValue = value;
    }
}
