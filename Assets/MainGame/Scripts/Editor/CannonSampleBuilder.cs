using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds everything a <see cref="CannonLauncher"/> needs to be looked at: placeholder art,
/// a cannon prefab wired to it, and a sample level that fires the player both ways.
///
/// The art is DELIBERATELY generated rather than imported. Two flat sprites drawn in code
/// are enough to see whether a shot arcs, lands on the right cell and leaves the player
/// facing the right way, and generating them means the system can be tried out before any
/// real art exists — nothing here reads the sprites, only the barrel's pivot and length,
/// so dropping finished art in later is a matter of swapping two SpriteRenderers.
///
/// The sample scene is copied from Level1 rather than assembled from nothing, because a
/// working level is a large amount of wiring — managers, the input UI, the camera follow,
/// the 2D lighting, the tilemap's composite collider — none of which is what a cannon test
/// is about. The copy keeps all of it and replaces only the parts that are: the ground is
/// repainted into two platforms with a gap between them that no Jump command can cross,
/// the level's door and battery are removed, and two cannons are placed — one firing left
/// to right, one firing right to left, so the facing rule can be seen in both directions.
///
/// Re-running is safe and idempotent: every step overwrites its own output and touches
/// nothing else in the project.
/// </summary>
public static class CannonSampleBuilder
{
    // ─── Asset paths ─────────────────────────────────────────────────────────────

    private const string k_SpriteFolder = "Assets/MainGame/Sprites/Cannon";
    private const string k_BasePng = k_SpriteFolder + "/CannonBase.png";
    private const string k_BarrelPng = k_SpriteFolder + "/CannonBarrel.png";

    private const string k_PrefabFolder = "Assets/MainGame/Prefabs/Cannon";
    private const string k_PrefabPath = k_PrefabFolder + "/Cannon.prefab";

    // The flight streak rides the player, so it is the PLAYER prefab this tool edits.
    private const string k_PlayerPrefab = "Assets/MainGame/Prefabs/Player.prefab";
    private const string k_TrailChild = "CannonTrail";

    // URP's Sprite-Unlit-Default, which lives in the package cache rather than in Assets.
    private const string k_UnlitSpriteMaterialGuid = "9dfc825aed78fcd4ba02077103263b40";

    private const string k_ConfigFolder = "Assets/MainGame/ScriptableObjects";
    private const string k_ConfigPath = k_ConfigFolder + "/CannonSampleConfig.asset";

    private const string k_SourceScene = "Assets/MainGame/Scenes/Level1.unity";
    private const string k_SampleScene = "Assets/MainGame/Scenes/CannonSample.unity";

    // Pixels per unit for the generated art. 64 makes the base exactly one cell wide and
    // the barrel exactly k_BarrelLength long, so the numbers in the component match what
    // is on screen without anyone having to measure it.
    private const int k_PixelsPerUnit = 64;

    // ─── Cannon geometry ─────────────────────────────────────────────────────────
    // The prefab's ROOT sits on the floor surface — the half-integer boundary a standing
    // body rests its feet on — so a cannon is placed by dropping it onto a platform rather
    // than by working out where its middle should be.

    // The line the player's body passes along, above the root's feet. What the catch
    // radius is measured from — unrelated to where the barrel happens to be bolted on.
    private const float k_WalkLineHeight = 0.5f;

    // Where the barrel is bolted to the base, and what its art measures. All three are
    // READ OFF THE SPRITES rather than chosen: the mount sits just inside the top of the
    // base so the breech stays buried in it at every aim, the length is breech to mouth,
    // and the art angle is the tilt the barrel is drawn at. Re-measure all three if the
    // art is redrawn — see CannonLauncher's own notes on the same three numbers.
    private static readonly Vector2 k_BarrelMount = new Vector2(-0.06f, 0.44f);
    private const float k_BarrelLength = 1.109f;
    private const float k_BarrelArtAngle = 40.19f;

    // ─── Sample level layout ─────────────────────────────────────────────────────
    // World cell centres. The grid puts those on integers, so every number here is a cell
    // the player can actually come to rest on.

    private const int k_LeftPlatformFrom = -16;
    private const int k_LeftPlatformTo = 2;
    private const int k_LeftPlatformY = -1;      // floor surface at y = -0.5, walk line y = 0

    private const int k_RightPlatformFrom = 14;
    private const int k_RightPlatformTo = 32;
    private const int k_RightPlatformY = 3;      // floor surface at y = 3.5, walk line y = 4

    // How many rows of tile are painted under each platform's surface. Purely so the
    // platforms read as ground rather than as a line.
    private const int k_PlatformDepth = 4;

    private static readonly Vector2 k_PlayerStart = new Vector2(-10f, 0f);

    // Cannon A throws the player over the gap and up onto the right platform; cannon B
    // throws them all the way back. Between them they cover both facings.
    private static readonly Vector2 k_CannonAFloor = new Vector2(0f, -0.5f);
    private static readonly Vector2 k_CannonALanding = new Vector2(16f, 4f);

    private static readonly Vector2 k_CannonBFloor = new Vector2(28f, 3.5f);
    private static readonly Vector2 k_CannonBLanding = new Vector2(-2f, 0f);

    // The solution the level is authored around, and what Auto Play (F8) plays back:
    //
    //   Right  -10 → -5
    //   Right   -5 → 0    caught by cannon A part-way, fired to (16, 4) facing RIGHT
    //   Right   16 → 21
    //   Right   21 → 26
    //   Right   26 → 31   caught by cannon B part-way, fired to (-2, 0) facing LEFT
    //   Left    -2 → -7
    //
    // The two commands after each flight are the point of the test: they prove the beat
    // the shot cost was exactly one, and that the rest of the sequence still runs from
    // wherever the cannon put the player down.
    private static readonly ActionTypeEnum[] k_Solution =
    {
        ActionTypeEnum.Right,
        ActionTypeEnum.Right,
        ActionTypeEnum.Right,
        ActionTypeEnum.Right,
        ActionTypeEnum.Right,
        ActionTypeEnum.Left,
    };

    // Level1's own furniture, which the sample has no use for. The door in particular has
    // to go: it ends the level on contact, and the sample's whole right platform is inside
    // the arc of a cannon that would otherwise fire the player straight into it.
    private static readonly string[] k_RootsToRemove =
    {
        "Door&KeySystem", "Door Text (2)", "Battery Text", "Battery Text (1)",
        "Text (TMP)", "Level_1_BG",
    };

    // ─── Menu ────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Cannon/Build Everything", priority = 0)]
    public static void BuildEverything()
    {
        BuildPlaceholderArt();
        BuildCannonPrefab();
        AttachPlayerFlightTrail();
        BuildSampleScene();
    }

    /// <summary>
    /// Puts the flight streak on the player prefab and wires it to
    /// <c>PlayerController.m_CannonTrail</c>.
    ///
    /// It lives on the PLAYER rather than on a cannon because it is the player who is
    /// travelling — a trail belongs to the thing moving, and the same one has to serve
    /// however many cannons a level has. It ships switched OFF and not emitting, so every
    /// level that never fires anybody carries an inert component and draws nothing; only
    /// the flight turns it on.
    /// </summary>
    [MenuItem("Tools/Cannon/Attach Player Flight Trail", priority = 23)]
    public static void AttachPlayerFlightTrail()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(k_PlayerPrefab) == null)
        {
            Debug.LogError($"[Cannon] {k_PlayerPrefab} is missing — the flight trail lives on " +
                           "the player, so there is nothing to attach it to.");
            return;
        }

        // Edited through a prefab stage rather than on an instance, so the change lands on
        // the asset itself and any nesting inside it survives.
        GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefab);
        try
        {
            Transform existing = root.transform.Find(k_TrailChild);
            GameObject host = existing != null ? existing.gameObject : new GameObject(k_TrailChild);

            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;

            // A child of its own rather than a second renderer on the body, so the streak
            // can be sorted behind the sprite and swapped for authored VFX without
            // disturbing anything the player itself draws.
            if (!host.TryGetComponent(out TrailRenderer trail))
                trail = host.AddComponent<TrailRenderer>();

            ConfigureFlightTrail(trail);

            if (root.TryGetComponent(out PlayerController player))
            {
                var so = new SerializedObject(player);
                so.FindProperty("m_CannonTrail").objectReferenceValue = trail;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[Cannon] Flight trail attached to {k_PlayerPrefab} (child '{k_TrailChild}').");
    }

    // Placeholder look, same standing as the cannon sprites: a short warm streak that fades
    // to nothing. Everything here is a plain serialized value, so replacing it with authored
    // VFX is a matter of restyling this renderer or pointing the field at another one.
    private static void ConfigureFlightTrail(TrailRenderer trail)
    {
        // Short. The streak is meant to read as speed, and a long one on a flight that now
        // lasts under half a second would still be catching up when the player lands.
        trail.time = 0.22f;

        trail.startWidth = 0.75f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 2;
        trail.autodestruct = false;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;

        // Off in the asset: the flight is the only thing allowed to switch it on, so a level
        // with no cannon in it never draws a streak behind an ordinary walk.
        trail.emitting = false;
        trail.enabled = false;

        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.lightProbeUsage = LightProbeUsage.Off;

        // A TrailRenderer builds into a mesh renderer, which has no 2D sorting of its own and
        // would otherwise land at an arbitrary depth. Behind the player's own sprite (order
        // 4), so the body stays on top of its own streak.
        trail.sortingLayerName = "Default";
        trail.sortingOrder = 3;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.12f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        trail.colorGradient = gradient;

        // URP's unlit sprite material, addressed by GUID because it lives inside the render
        // pipeline package rather than at any path this project owns. Left to itself a
        // TrailRenderer created in code has no material at all and renders magenta.
        string materialPath = AssetDatabase.GUIDToAssetPath(k_UnlitSpriteMaterialGuid);
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material != null) trail.sharedMaterial = material;
        else Debug.LogWarning("[Cannon] URP's Sprite-Unlit-Default was not found; the flight " +
                              "trail will render with no material until one is assigned.");
    }

    [MenuItem("Tools/Cannon/Build Placeholder Art", priority = 20)]
    public static void BuildPlaceholderArt()
    {
        EnsureFolder(k_SpriteFolder);

        // The cannon is no longer on placeholder art, and this menu item writes straight
        // over the two PNGs by path. Running it out of habit would destroy hand-drawn art
        // that is not recoverable from anything in this file, so it asks first — and the
        // pivots and angles below only describe the GENERATED art, not whatever is there.
        if ((File.Exists(k_BasePng) || File.Exists(k_BarrelPng)) &&
            !EditorUtility.DisplayDialog(
                "Overwrite cannon art?",
                $"{k_SpriteFolder} already holds cannon sprites. Rebuilding the " +
                "placeholders overwrites them, and the real art cannot be regenerated " +
                "from this script. Overwrite?",
                "Overwrite", "Cancel"))
            return;

        WriteSprite(k_BasePng, DrawBase(96, 64), SpriteAlignment.BottomCenter, Vector2.zero);
        WriteSprite(k_BarrelPng, DrawBarrel(96, 48), SpriteAlignment.Custom, new Vector2(0f, 0.5f));

        // Placeholders are drawn flat along +X with the pivot on the breech, so a cannon
        // built on them needs the art angle zeroed — the constant above describes the real
        // barrel, which is drawn tilted.
        Debug.LogWarning($"[Cannon] Placeholder art written to {k_SpriteFolder}. Set " +
                         "CannonLauncher's Barrel Art Angle to 0 on any cannon using it.");
    }

    [MenuItem("Tools/Cannon/Build Cannon Prefab", priority = 21)]
    public static void BuildCannonPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(k_BasePng) == null) BuildPlaceholderArt();

        EnsureFolder(k_PrefabFolder);

        var root = new GameObject("Cannon");
        try
        {
            // Base and barrel are separate renderers because only the barrel turns. Drawn
            // ahead of the base so the barrel reads as sitting in front of the mount.
            Transform mount = NewSpritePart(
                "Base", root.transform, Vector3.zero, k_BasePng, sortingOrder: 2);
            Transform barrel = NewSpritePart(
                "Barrel", root.transform, new Vector3(k_BarrelMount.x, k_BarrelMount.y, 0f),
                k_BarrelPng, sortingOrder: 3);

            CannonLauncher cannon = root.AddComponent<CannonLauncher>();

            var so = new SerializedObject(cannon);
            so.FindProperty("m_Barrel").objectReferenceValue = barrel;
            so.FindProperty("m_Base").objectReferenceValue = mount;
            so.FindProperty("m_BarrelPivotOffset").vector2Value = k_BarrelMount;
            so.FindProperty("m_BarrelLength").floatValue = k_BarrelLength;
            so.FindProperty("m_BarrelArtAngle").floatValue = k_BarrelArtAngle;

            // The catch point sits on the walk line rather than at the root's feet, so the
            // radius is measured from where the player's body actually passes.
            so.FindProperty("m_CatchOffset").vector2Value = new Vector2(0f, k_WalkLineHeight);
            so.FindProperty("m_CatchRadius").floatValue = 1.2f;

            // A default shot a designer can see straight away when they drop one into a
            // scene: twelve cells to the right, on a lob that clears a fair-sized wall.
            so.FindProperty("m_LandingOffset").vector2Value = new Vector2(12f, 0.5f);
            so.FindProperty("m_ArcLift").floatValue = 5f;

            // Units per second, not seconds: the flight time is derived from this and the
            // arc's length, so a cannon dragged out to twice the reach still fires just as
            // fast rather than doubling into a slow float. Well above walking pace — the
            // shot has to read as a launch.
            so.FindProperty("m_LaunchSpeed").floatValue = 25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[Cannon] Prefab written to {k_PrefabPath}.");
    }

    [MenuItem("Tools/Cannon/Build Sample Scene", priority = 22)]
    public static void BuildSampleScene()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath) == null) BuildCannonPrefab();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_SourceScene) == null)
        {
            Debug.LogError($"[Cannon] {k_SourceScene} is missing — the sample scene is built " +
                           "from a copy of it, so that a cannon can be tried in a level that " +
                           "is already wired up.");
            return;
        }

        // Copied fresh every time rather than opened and patched, so a re-run always starts
        // from a known level instead of stacking edits on top of the last attempt.
        AssetDatabase.DeleteAsset(k_SampleScene);
        if (!AssetDatabase.CopyAsset(k_SourceScene, k_SampleScene))
        {
            Debug.LogError($"[Cannon] Could not copy {k_SourceScene} to {k_SampleScene}.");
            return;
        }

        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(k_SampleScene, OpenSceneMode.Single);

        StripLevelFurniture(scene);
        PaintPlatforms(scene);
        PlacePlayer(scene);
        PlaceCannons(scene);
        ApplySampleConfig(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[Cannon] Sample scene built at {k_SampleScene}. Press Play, then F8 " +
                  "(or Tools ▸ Auto Play ▸ Run Level Solution) to run the authored solution.");
    }

    // ─── Sample scene steps ──────────────────────────────────────────────────────

    private static void StripLevelFurniture(Scene scene)
    {
        var doomed = new HashSet<string>(k_RootsToRemove);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (doomed.Contains(root.name))
                Object.DestroyImmediate(root);
    }

    // Replaces the level's ground with two platforms and a gap.
    //
    // The gap is deliberately wider than the player's Jump Forward Distance: if a jump
    // could cross it the cannon would be decoration, and a test that passes whether or not
    // the flight worked is not a test.
    private static void PaintPlatforms(Scene scene)
    {
        Tilemap ground = FindGroundTilemap(scene);
        if (ground == null)
        {
            Debug.LogError("[Cannon] No Ground tilemap found in the copied scene.");
            return;
        }

        // Reuses whatever tile the source level was painted with, so the sample looks like
        // the rest of the game without this having to know a tile asset by name.
        TileBase tile = MostUsedTile(ground);
        if (tile == null)
        {
            Debug.LogError("[Cannon] The Ground tilemap has no tiles to copy a look from.");
            return;
        }

        ground.ClearAllTiles();

        PaintPlatform(ground, tile, k_LeftPlatformFrom, k_LeftPlatformTo, k_LeftPlatformY);
        PaintPlatform(ground, tile, k_RightPlatformFrom, k_RightPlatformTo, k_RightPlatformY);

        ground.CompressBounds();
        ground.RefreshAllTiles();

        // The composite is what the player's ground probe actually reads; it does not
        // regenerate on its own after a scripted repaint.
        if (ground.TryGetComponent(out TilemapCollider2D tilemapCollider))
            tilemapCollider.ProcessTilemapChanges();

        if (ground.TryGetComponent(out CompositeCollider2D composite))
            composite.GenerateGeometry();
    }

    private static void PaintPlatform(Tilemap ground, TileBase tile, int fromX, int toX, int surfaceY)
    {
        for (int x = fromX; x <= toX; x++)
            for (int depth = 0; depth < k_PlatformDepth; depth++)
                ground.SetTile(WorldCellToTile(x, surfaceY - depth), tile);
    }

    private static void PlacePlayer(Scene scene)
    {
        PlayerController player = FindInScene<PlayerController>(scene);
        if (player == null)
        {
            Debug.LogError("[Cannon] No player in the copied scene.");
            return;
        }

        Transform t = player.transform;
        t.position = new Vector3(k_PlayerStart.x, k_PlayerStart.y, t.position.z);

        // Facing right, so the first Right command does not begin with a turn.
        t.localScale = new Vector3(1f, 1f, 1f);
    }

    private static void PlaceCannons(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);

        SpawnCannon(prefab, scene, "Cannon A (fires right)", k_CannonAFloor, k_CannonALanding);
        SpawnCannon(prefab, scene, "Cannon B (fires left)", k_CannonBFloor, k_CannonBLanding);
    }

    private static void SpawnCannon(
        GameObject prefab, Scene scene, string name, Vector2 floorPosition,
        Vector2 landingPoint)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = name;
        instance.transform.position = floorPosition;

        var so = new SerializedObject(instance.GetComponent<CannonLauncher>());

        // Stored as an offset from the cannon, so moving the cannon carries its shot with
        // it — the landing point is expressed relative to the thing doing the firing.
        so.FindProperty("m_LandingOffset").vector2Value = landingPoint - floorPosition;

        // No per-cannon speed override: both run at the prefab's launch speed, which is the
        // point of timing a shot by speed rather than by duration — the long shot and the
        // short one leave the barrel equally fast.

        // On in the sample so a run leaves a readable trail in the console: which cannon
        // caught the player, where it fired from, and which cell they came down on.
        so.FindProperty("m_LogPhases").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Points the level's systems at a config of the sample's own, and teaches the input UI
    // the solution above. Between them these give the level its six input slots, a camera
    // that can follow the player across a much wider level than Level1, and a solution for
    // Auto Play to run.
    private static void ApplySampleConfig(Scene scene)
    {
        EnsureFolder(k_ConfigFolder);

        var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(k_ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LevelConfig>();
            AssetDatabase.CreateAsset(config, k_ConfigPath);
        }

        config.levelNumber = 0;
        config.sequence.maxSequenceLength = k_Solution.Length;
        config.sequence.requireFullSequence = false;
        config.sequence.autoPlaySequence = (ActionTypeEnum[])k_Solution.Clone();
        config.tutorial.showTutorial = false;

        // Wide enough to hold both platforms and the flight between them. The dead zone is
        // small so the camera stays with a player who has just been thrown thirty cells.
        config.cameraDeadZone.deadZoneX = 0.5f;
        config.cameraDeadZone.deadZoneY = 0.5f;
        config.cameraDeadZone.smoothTime = 0.25f;
        config.cameraDeadZone.followX = true;
        config.cameraDeadZone.followY = true;
        config.cameraDeadZone.minX = k_LeftPlatformFrom + 4f;
        config.cameraDeadZone.maxX = k_RightPlatformTo - 4f;
        config.cameraDeadZone.minY = k_LeftPlatformY;
        config.cameraDeadZone.maxY = k_RightPlatformY + 6f;

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        LevelContext context = FindInScene<LevelContext>(scene);
        if (context != null)
        {
            var so = new SerializedObject(context);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(context);
        }

        PlayerInputUIHelper input = FindInScene<PlayerInputUIHelper>(scene);
        if (input != null)
        {
            var so = new SerializedObject(input);
            SerializedProperty correct = so.FindProperty("m_CorrectSequence");
            correct.arraySize = k_Solution.Length;
            for (int i = 0; i < k_Solution.Length; i++)
                correct.GetArrayElementAtIndex(i).enumValueIndex = (int)k_Solution[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(input);
        }

        // Starts looking at the player rather than at wherever Level1 left it, so the scene
        // opens on something worth seeing.
        Camera camera = Camera.main;
        if (camera != null)
            camera.transform.position =
                new Vector3(k_PlayerStart.x, k_PlayerStart.y + 1f, camera.transform.position.z);

        WireCameraFollow(scene, camera);
    }

    // Points the camera follow at the player IN THE SCENE, rather than leaving it to find one
    // for itself at runtime.
    //
    // Its own search runs in Awake and goes through SceneObjects.FindInActiveScene, which bails
    // out while the active scene still reports isLoaded == false — which is exactly where a
    // scene's own Awake runs. The result is a null target and a camera that never moves. The
    // field is serialized precisely so a scene can answer this itself, so the sample does: this
    // level is forty-odd cells wide and a cannon throws the player thirty of them, so a static
    // camera would put the whole point of the scene off screen.
    //
    // Deliberately scoped to this scene. The same gap affects every level that relies on the
    // runtime search, but that lives in the shared Managers prefab and is not this tool's to
    // change.
    private static void WireCameraFollow(Scene scene, Camera camera)
    {
        CameraFollowDeadZone follow = FindInScene<CameraFollowDeadZone>(scene);
        PlayerController player = FindInScene<PlayerController>(scene);
        if (follow == null || player == null) return;

        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.FindProperty("targetCamera").objectReferenceValue = camera;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(follow);
    }

    // ─── Scene helpers ───────────────────────────────────────────────────────────

    // Tilemap cells are indexed from the Grid's origin, which every level places at
    // (0.5, 0.5) so that CELL CENTRES land on integers (see GridWorld). Cell (0,0)
    // therefore spans world x 0.5..1.5 and is centred on world x = 1 — one less than the
    // world cell it draws. Everything above is written in world cells, the unit a level is
    // actually designed in, and converted here rather than in each caller.
    private static Vector3Int WorldCellToTile(int worldX, int worldY) =>
        new Vector3Int(worldX - 1, worldY - 1, 0);

    private static Tilemap FindGroundTilemap(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                if (map.gameObject.name == "Ground")
                    return map;
        }

        return null;
    }

    // The tile the level is mostly made of — its solid body — rather than whichever one
    // happens to sit at some sampled cell, which could easily be an edge or a corner piece.
    private static TileBase MostUsedTile(Tilemap map)
    {
        var counts = new Dictionary<TileBase, int>();

        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(cell);
            if (tile == null) continue;

            counts.TryGetValue(tile, out int seen);
            counts[tile] = seen + 1;
        }

        TileBase best = null;
        int bestCount = 0;

        foreach (KeyValuePair<TileBase, int> entry in counts)
            if (entry.Value > bestCount) { best = entry.Key; bestCount = entry.Value; }

        return best;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }

    private static Transform NewSpritePart(
        string name, Transform parent, Vector3 localPosition, string spritePath, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        renderer.sortingOrder = sortingOrder;

        return go.transform;
    }

    // ─── Placeholder art ─────────────────────────────────────────────────────────

    // Steel greys with a warm rim, so the cannon reads against both the dark tiles and the
    // player without anyone having to pick a palette for art that is going to be replaced.
    private static readonly Color32 k_Shadow = new Color32(0x22, 0x28, 0x33, 0xFF);
    private static readonly Color32 k_Metal = new Color32(0x4A, 0x57, 0x6B, 0xFF);
    private static readonly Color32 k_MetalLight = new Color32(0x74, 0x86, 0xA1, 0xFF);
    private static readonly Color32 k_Rim = new Color32(0xE8, 0xA0, 0x3C, 0xFF);

    // The mount: a squat wheeled block, one cell wide and a cell high with its pivot on
    // the floor, so dropping one onto a platform puts it exactly on the surface.
    private static Texture2D DrawBase(int width, int height)
    {
        Texture2D tex = NewTexture(width, height);

        FillCircle(tex, 24, 16, 15, k_Shadow);
        FillCircle(tex, 72, 16, 15, k_Shadow);
        FillCircle(tex, 24, 16, 6, k_MetalLight);
        FillCircle(tex, 72, 16, 6, k_MetalLight);

        FillRect(tex, 8, 18, width - 8, height - 8, k_Metal);
        FillRect(tex, 8, height - 12, width - 8, height - 8, k_MetalLight);
        FillRect(tex, 8, 18, width - 8, 22, k_Shadow);

        // Bolts, purely so the block does not read as a flat rectangle at a glance.
        FillCircle(tex, 18, 32, 3, k_Rim);
        FillCircle(tex, width - 18, 32, 3, k_Rim);

        tex.Apply();
        return tex;
    }

    // The barrel: authored pointing RIGHT with its pivot on the LEFT edge, which is what
    // lets CannonLauncher rotate it about its mount and treat its far end as the muzzle.
    private static Texture2D DrawBarrel(int width, int height)
    {
        Texture2D tex = NewTexture(width, height);

        int midY = height / 2;

        // Tube, tapering into a heavier ring at the mouth.
        FillRect(tex, 6, midY - 13, width - 14, midY + 13, k_Metal);
        FillRect(tex, 6, midY + 5, width - 14, midY + 13, k_MetalLight);
        FillRect(tex, 6, midY - 13, width - 14, midY - 7, k_Shadow);

        // Muzzle ring, wider than the tube so the mouth is obvious at any rotation.
        FillRect(tex, width - 16, midY - 17, width - 2, midY + 17, k_MetalLight);
        FillRect(tex, width - 8, midY - 13, width - 2, midY + 13, k_Rim);

        // Breech cap over the pivot end.
        FillCircle(tex, 12, midY, 16, k_Metal);
        FillCircle(tex, 12, midY, 7, k_Shadow);

        tex.Apply();
        return tex;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        var blank = new Color32[width * height];   // Color32 default is fully transparent
        tex.SetPixels32(blank);

        return tex;
    }

    private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color32 color)
    {
        x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0);
        x1 = Mathf.Min(tex.width, x1); y1 = Mathf.Min(tex.height, y1);

        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                tex.SetPixel(x, y, color);
    }

    private static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color32 color)
    {
        int r2 = radius * radius;

        for (int y = Mathf.Max(0, cy - radius); y < Mathf.Min(tex.height, cy + radius + 1); y++)
            for (int x = Mathf.Max(0, cx - radius); x < Mathf.Min(tex.width, cx + radius + 1); x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) tex.SetPixel(x, y, color);
            }
    }

    // Writes the texture out as a PNG and imports it as a sprite. Written to disk rather
    // than kept as an in-memory asset so the result is an ordinary file an artist can open,
    // overwrite in place, and keep the prefab's reference to.
    private static void WriteSprite(
        string path, Texture2D texture, SpriteAlignment alignment, Vector2 customPivot)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = k_PixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)alignment;
        settings.spritePivot = customPivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
