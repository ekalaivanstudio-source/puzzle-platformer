using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Lays the run of pipe that wires a level's battery socket to its exit door, and hands the
/// finished run to <see cref="KeySlot"/> so the charge travelling it is what opens the door.
///
/// Every level whose door waits on a battery is supposed to show the player where that socket
/// leads, but only one level was ever piped by hand — twenty-odd routes drawn a tile at a time
/// is not work worth doing twice, and re-doing it after a level's geometry moves is worse. So
/// the route is found rather than drawn: the pipe leaves the top of the socket, climbs to the
/// lowest clear height it can run across at, crosses to the door and drops onto it, testing
/// each leg against the Ground tilemap so it never buries itself in the level's walls.
///
/// It builds the same objects the hand-authored run in Level3 is made of — a "Pipe_Connection"
/// root over a "Pipes" group of one-cell pieces, each carrying the glow children that light up
/// — so a generated run and a drawn one are the same thing to the game and to the next person
/// who opens the scene. A level that already has a run keeps it: the menu item only wires it
/// up, and rebuilding one from scratch is a separate, deliberate item.
///
/// Re-running is safe. Where the route it picks reads badly the run is plain GameObjects and
/// can be dragged about freely afterwards; only "Rebuild" throws that away.
/// </summary>
public static class PipeConnectionBuilder
{
    // ─── Assets ──────────────────────────────────────────────────────────────────

    private const string k_PipeFolder = "Assets/MainGame/Sprites/Tiles_pipe";
    private const string k_GlowFolder = k_PipeFolder + "/Glow";

    // The glow rides the same additive material the hand-authored run in Level3 uses, so a
    // generated pipe lights exactly as bright as a drawn one. It comes from the effects pack
    // rather than this project's own folder because that is where the run that set the look
    // took it from — changing it here changes every pipe at once.
    private const string k_GlowMaterial = "Assets/ErbGameArt/Fantasy effects pack/Materials/Bat.mat";

    // URP's unlit sprite material, addressed by GUID because it lives inside the render
    // pipeline package rather than at any path this project owns. A SpriteRenderer created in
    // code gets the LIT one instead, and the Fg1 sorting layer these pipes sit on is not in any
    // of the levels' 2D light target layers — so a lit pipe renders as a black silhouette. The
    // hand-authored run picked the unlit material for the same reason.
    private const string k_PipeMaterialGuid = "9dfc825aed78fcd4ba02077103263b40";

    // Straights and the four corners, named by the two sides they open onto.
    private const string k_PipeCornerUpRight   = "pipes (1)";
    private const string k_PipeCornerUpLeft    = "pipes (2)";
    private const string k_PipeCornerDownRight = "pipes (3)";
    private const string k_PipeCornerDownLeft  = "pipes (4)";
    private const string k_PipeVertical        = "pipes (5)";
    private const string k_PipeHorizontal      = "pipes (10)";

    // The glow is drawn as a line down the middle of the piece. A straight gets the whole
    // line; a corner is lit in two halves, the leg the charge arrives on and the leg it leaves
    // on, so the light visibly turns the corner instead of the whole elbow popping on at once.
    private const string k_GlowHorizontal = "Glow (1)";
    private const string k_GlowVertical   = "Glow (2)";
    private const string k_GlowHalfDown   = "Glow (3)";
    private const string k_GlowHalfUp     = "Glow (4)";
    private const string k_GlowHalfLeft   = "Glow (5)";
    private const string k_GlowHalfRight  = "Glow (6)";

    // ─── Look ────────────────────────────────────────────────────────────────────

    // Pipes are plumbing bolted to the level, so they belong behind everything the player can
    // touch and in front of everything they cannot.
    //
    // That is Default, just under zero. Everything gameplay sits on Default at zero or above —
    // bricks and the socket at 0, the ground and the doorway at 1 and 3, the player and the
    // battery at 4, collectables at 5 — so a negative order is behind all of it. And Default
    // is above every background layer, so a run on it clears Bg2, Bg1, Mg2 and Mg1 outright,
    // whatever sorting orders the backdrops use among themselves; some of them run into the
    // hundreds, which is exactly the trap a "high order on Mg1" answer would walk into.
    //
    // -3 and -2 rather than -2 and -1 because Default already has decoration sitting at -1,
    // and two renderers on the same layer and order draw in an order nothing here controls.
    private const string k_SortingLayer = "Default";
    private const int k_PipeSortingOrder = -3;
    private const int k_GlowSortingOrder = -2;

    private static readonly Color k_GlowColor = new Color(0f, 0.81421626f, 1f, 1f);

    // ─── Layout ──────────────────────────────────────────────────────────────────

    private const string k_RootName   = "Pipe_Connection";
    private const string k_PiecesName = "Pipes";

    // The socket is a cell tall, so the pipe leaves from the cell above it. The door is not:
    // it is two and a bit cells of doorway, and the run has to come down onto its lintel
    // rather than into the gap the player walks through.
    private const float k_SocketClearance = 1f;
    private const float k_DoorClearance   = 2.5f;

    // A route that has to bend costs about four straight cells' worth of preference. Cheaper
    // than that and the fallback search wanders in staircases; dearer and it will run halfway
    // across the level to save a single elbow.
    private const int k_TurnCost = 4;

    // What one cell of crossing spent under a ceiling is worth against one cell of extra pipe
    // to get up there. Two means a run will climb most of the way across a room to reach a
    // ceiling, but will not chase a ledge only a few cells wide across the whole level.
    private const int k_CeilingBonus = 2;

    // Cells of level shown around the run when the Scene window is pulled back to it, and the
    // width-to-height the window is assumed to be. Neither changes anything that ships; they
    // only decide how much context the view gives while routes are being eyeballed.
    private const float k_FrameMargin = 8f;
    private const float k_FrameAspect = 1.6f;

    private static readonly Vector3Int k_Up    = new Vector3Int(0, 1, 0);
    private static readonly Vector3Int k_Down  = new Vector3Int(0, -1, 0);
    private static readonly Vector3Int k_Left  = new Vector3Int(-1, 0, 0);
    private static readonly Vector3Int k_Right = new Vector3Int(1, 0, 0);

    // ─── Menu ────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Pipes/Build Pipe Connection (Open Scene)")]
    private static void BuildOpenScene() => Run(rebuild: false);

    [MenuItem("Tools/Pipes/Rebuild Pipe Connection (Open Scene)")]
    private static void RebuildOpenScene() => Run(rebuild: true);

    // Runs the charge along the open scene's pipe without having to carry the battery to the
    // socket first, which is the only other way to see the one thing about a run that a still
    // Scene window cannot show: whether it lights from the socket end.
    [MenuItem("Tools/Pipes/Preview Pipe Glow (Play Mode)")]
    private static void PreviewGlow()
    {
        PipeConnection connection =
            Object.FindFirstObjectByType<PipeConnection>(FindObjectsInactive.Include);

        if (connection == null)
        {
            Debug.LogWarning("[Pipes] no pipe connection in the open scene to preview.");
            return;
        }

        // The callback is the door: this is where KeySlot opens it, so logging it says the run
        // finished and handed over rather than stalling somewhere along the pipe. Timed off
        // the clock rather than reported from the setting, so a run that overshoots its
        // duration says so instead of claiming the number it was asked for.
        float started = Time.time;

        connection.Power(() => Debug.Log(
            $"[Pipes] charge reached the door end in {Time.time - started:0.00}s " +
            $"(asked for {connection.PowerDuration:0.00}s)."));
    }

    // Greyed out outside play mode: the glow travels on a coroutine, which only runs then.
    [MenuItem("Tools/Pipes/Preview Pipe Glow (Play Mode)", isValidateFunction: true)]
    private static bool ValidatePreviewGlow() => Application.isPlaying;

    /// <summary>
    /// Pipes the open scene: finds its socket and door, lays a run between them if there is
    /// not one already, and points the socket at it. Logs one line saying what it did — the
    /// menu item is meant to be run scene after scene, and a silent no-op is indistinguishable
    /// from a failure.
    /// </summary>
    private static void Run(bool rebuild)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        string level = scene.name;

        KeySlot slot = Object.FindObjectsByType<KeySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();

        if (slot == null)
        {
            Debug.Log($"[Pipes] {level}: no socket in this scene — nothing to wire.");
            return;
        }

        SerializedObject slotObject = new SerializedObject(slot);
        LevelExitDoor door = slotObject.FindProperty("m_ExitDoor").objectReferenceValue as LevelExitDoor;

        if (door == null)
        {
            Debug.LogWarning($"[Pipes] {level}: the socket has no exit door wired — skipped.");
            return;
        }

        // A level converted to open without a battery switches its socket off at Awake; a run
        // of pipe leading to a door that was never locked would be a lie about the puzzle.
        if (door.OpensWithoutKey)
        {
            Debug.Log($"[Pipes] {level}: door opens without a battery — no pipe needed.");
            return;
        }

        Transform existing = FindRoot(scene);

        if (existing != null && rebuild)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            existing = null;
        }

        List<GameObject> glows;

        if (existing != null)
        {
            // Someone drew this one by hand. Its route is better than anything found here, so
            // only the wiring is missing — and the sorting, which a run drawn a piece at a
            // time tends to end up inconsistent about.
            glows = CollectGlows(existing);
            int resorted = ApplySorting(existing);
            Debug.Log($"[Pipes] {level}: kept the existing run, wired {glows.Count} glow " +
                      $"segments, re-sorted {resorted} renderers.");
        }
        else
        {
            Tilemap ground = FindGround(scene);

            if (ground == null)
            {
                Debug.LogWarning($"[Pipes] {level}: no Ground tilemap to route around — skipped.");
                return;
            }

            Vector3Int start = ground.WorldToCell(slot.transform.position + Vector3.up * k_SocketClearance);
            Vector3Int end   = ground.WorldToCell(door.transform.position + Vector3.up * k_DoorClearance);

            start = NearestFree(ground, start);
            end   = NearestFree(ground, end);

            List<Vector3Int> route = Route(ground, start, end);

            if (route == null)
            {
                Debug.LogWarning(
                    $"[Pipes] {level}: could not find a clear route from the socket at {start} " +
                    $"to the door at {end} — this one needs piping by hand.");
                return;
            }

            existing = BuildRun(ground, route);
            glows = CollectGlows(existing);
            Debug.Log($"[Pipes] {level}: laid {route.Count} pipe pieces, {glows.Count} glow segments.");
        }

        PipeConnection connection = existing.GetComponent<PipeConnection>();

        if (connection == null)
            connection = Undo.AddComponent<PipeConnection>(existing.gameObject);

        SerializedObject connectionObject = new SerializedObject(connection);
        SerializedProperty segments = connectionObject.FindProperty("m_GlowSegments");
        segments.arraySize = glows.Count;

        for (int i = 0; i < glows.Count; i++)
            segments.GetArrayElementAtIndex(i).objectReferenceValue = glows[i];

        connectionObject.ApplyModifiedProperties();

        // Set through the SerializedObject rather than the field so the change is recorded as
        // an override on the Door&KeySystem prefab instance the socket lives on — assigning it
        // any other way is silently thrown away the next time the prefab is applied.
        Undo.RecordObject(slot, "Wire Pipe Connection");
        slotObject.FindProperty("m_PipeConnection").objectReferenceValue = connection;
        slotObject.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(slot);

        EditorSceneManager.MarkSceneDirty(scene);
        FrameLevel(existing);
    }

    // ─── Scene look-ups ──────────────────────────────────────────────────────────

    private static Transform FindRoot(UnityEngine.SceneManagement.Scene scene) =>
        scene.GetRootGameObjects().FirstOrDefault(root => root.name == k_RootName)?.transform;

    // The tilemap that carries the level's collision is the one the pipe has to route around;
    // the decorative layers can be run over freely.
    private static Tilemap FindGround(UnityEngine.SceneManagement.Scene scene) =>
        scene.GetRootGameObjects()
             .SelectMany(root => root.GetComponentsInChildren<TilemapCollider2D>(includeInactive: true))
             .Select(collider => collider.GetComponent<Tilemap>())
             .FirstOrDefault(map => map != null);

    // ─── Routing ─────────────────────────────────────────────────────────────────

    private static bool Blocked(Tilemap ground, Vector3Int cell) => ground.HasTile(cell);

    // The ideal end of a leg can land inside a wall when a socket is tucked under a ledge or a
    // door is set into one. Rather than give up, step up a cell at a time until there is air.
    private static Vector3Int NearestFree(Tilemap ground, Vector3Int cell)
    {
        for (int i = 0; i < 4; i++)
        {
            if (!Blocked(ground, cell))
                return cell;

            cell += k_Up;
        }

        return cell;
    }

    /// <summary>
    /// A route from the socket end to the door end, as a list of cells one step apart.
    ///
    /// The shape wanted is the one a real pipe would take and the one Level3 was drawn as: up
    /// out of the socket, along at some height, down onto the door. Every height the run could
    /// cross at is tried and the best scoring one kept; only when none of them clears the
    /// geometry does it fall back to searching the open cells generally.
    ///
    /// Crossing above both ends is the whole shape, so those heights are searched first and on
    /// their own. Allowed lower, the run crosses under the door and then rises into it, which
    /// draws as a length of pipe stuck out of the doorway's roof going nowhere.
    /// </summary>
    private static List<Vector3Int> Route(Tilemap ground, Vector3Int start, Vector3Int end)
    {
        BoundsInt bounds = ground.cellBounds;
        int yMax = Mathf.Max(bounds.yMax, Mathf.Max(start.y, end.y));
        int yMin = Mathf.Min(bounds.yMin, Mathf.Min(start.y, end.y));
        int over = Mathf.Max(start.y, end.y);

        List<Vector3Int> route = BestStaple(ground, start, end, Enumerable.Range(over, yMax - over + 1));

        // Nothing overhead is clear — a socket and door in the same low corridor with a solid
        // ceiling, say. Falling back to below them at least connects the two.
        route ??= BestStaple(ground, start, end, Enumerable.Range(yMin, over - yMin).Reverse());

        return route ?? Search(ground, start, end);
    }

    // Heights are handed over in preference order, so where two of them score the same the one
    // offered first — the one nearest the socket and door, and so the shortest — is kept.
    private static List<Vector3Int> BestStaple(
        Tilemap ground, Vector3Int start, Vector3Int end, IEnumerable<int> heights)
    {
        List<Vector3Int> best = null;
        int bestScore = int.MinValue;

        foreach (int runY in heights)
        {
            List<Vector3Int> staple = TryStaple(ground, start, end, runY);

            if (staple == null)
                continue;

            int score = Score(ground, staple, runY, start, end);

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = staple;
        }

        return best;
    }

    /// <summary>
    /// How good a crossing height is. Pipes belong on ceilings: a run pinned under solid tiles
    /// reads as plumbing bolted to the level, while the same run through open air a cell above
    /// the floor reads as a rail the player walks through. So each cell of the crossing with
    /// something solid over it earns more than the cells the detour up there costs — and with
    /// no ceiling to reach for, nothing earns anything and the shortest run wins by default.
    /// </summary>
    private static int Score(Tilemap ground, List<Vector3Int> staple, int runY, Vector3Int start, Vector3Int end)
    {
        int hugged = 0;

        for (int x = Mathf.Min(start.x, end.x); x <= Mathf.Max(start.x, end.x); x++)
        {
            if (Blocked(ground, new Vector3Int(x, runY + 1, 0)))
                hugged++;
        }

        return hugged * k_CeilingBonus - staple.Count;
    }

    // Up the socket's column to runY, across, then down the door's column. Null the moment any
    // cell of any leg is solid — a pipe that passes through a wall is worse than no pipe.
    private static List<Vector3Int> TryStaple(Tilemap ground, Vector3Int start, Vector3Int end, int runY)
    {
        List<Vector3Int> route = new List<Vector3Int>();

        if (!Append(ground, route, start, new Vector3Int(start.x, runY, 0))) return null;
        if (!Append(ground, route, new Vector3Int(start.x, runY, 0), new Vector3Int(end.x, runY, 0))) return null;
        if (!Append(ground, route, new Vector3Int(end.x, runY, 0), end)) return null;

        return route;
    }

    // Walks a straight leg cell by cell, skipping the corner already added by the leg before.
    private static bool Append(Tilemap ground, List<Vector3Int> route, Vector3Int from, Vector3Int to)
    {
        Vector3Int step = new Vector3Int(
            System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y), 0);

        Vector3Int cell = from;

        while (true)
        {
            if (Blocked(ground, cell))
                return false;

            if (route.Count == 0 || route[route.Count - 1] != cell)
                route.Add(cell);

            if (cell == to)
                return true;

            cell += step;
        }
    }

    /// <summary>
    /// The fallback: a cheapest-route search over the level's open cells, where turning costs
    /// <see cref="k_TurnCost"/> straight cells. The cost is what keeps the result looking like
    /// plumbing — without it the cheapest route through open air is a staircase.
    /// </summary>
    private static List<Vector3Int> Search(Tilemap ground, Vector3Int start, Vector3Int end)
    {
        if (Blocked(ground, start) || Blocked(ground, end))
            return null;

        BoundsInt bounds = ground.cellBounds;
        Vector3Int[] directions = { k_Up, k_Down, k_Left, k_Right };

        // Keyed by the cell AND the direction arrived from: reaching a cell going up is a
        // different thing from reaching it going left, because of what turning next will cost.
        Dictionary<(Vector3Int cell, Vector3Int from), int> best =
            new Dictionary<(Vector3Int, Vector3Int), int>();
        Dictionary<(Vector3Int cell, Vector3Int from), (Vector3Int cell, Vector3Int from)> previous =
            new Dictionary<(Vector3Int, Vector3Int), (Vector3Int, Vector3Int)>();

        List<((Vector3Int cell, Vector3Int from) state, int cost)> frontier =
            new List<((Vector3Int, Vector3Int), int)>();

        foreach (Vector3Int direction in directions)
        {
            best[(start, direction)] = 0;
            frontier.Add(((start, direction), 0));
        }

        while (frontier.Count > 0)
        {
            int cheapest = 0;

            for (int i = 1; i < frontier.Count; i++)
            {
                if (frontier[i].cost < frontier[cheapest].cost)
                    cheapest = i;
            }

            var current = frontier[cheapest];
            frontier.RemoveAt(cheapest);

            if (best.TryGetValue(current.state, out int known) && known < current.cost)
                continue;

            if (current.state.cell == end)
                return Unwind(previous, current.state, start);

            foreach (Vector3Int direction in directions)
            {
                Vector3Int next = current.state.cell + direction;

                if (next.x < bounds.xMin - 1 || next.x > bounds.xMax ||
                    next.y < bounds.yMin - 1 || next.y > bounds.yMax)
                    continue;

                if (Blocked(ground, next))
                    continue;

                int cost = current.cost + 1 + (direction == current.state.from ? 0 : k_TurnCost);
                var state = (next, direction);

                if (best.TryGetValue(state, out int settled) && settled <= cost)
                    continue;

                best[state] = cost;
                previous[state] = current.state;
                frontier.Add((state, cost));
            }
        }

        return null;
    }

    private static List<Vector3Int> Unwind(
        Dictionary<(Vector3Int cell, Vector3Int from), (Vector3Int cell, Vector3Int from)> previous,
        (Vector3Int cell, Vector3Int from) state,
        Vector3Int start)
    {
        List<Vector3Int> route = new List<Vector3Int> { state.cell };

        while (previous.TryGetValue(state, out var earlier))
        {
            state = earlier;
            route.Add(state.cell);

            if (state.cell == start)
                break;
        }

        route.Reverse();
        return route;
    }

    // ─── Building ────────────────────────────────────────────────────────────────

    private static Transform BuildRun(Tilemap ground, List<Vector3Int> route)
    {
        GameObject root = new GameObject(k_RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Pipe Connection");

        GameObject pieces = new GameObject(k_PiecesName);
        pieces.transform.SetParent(root.transform, worldPositionStays: false);

        Material glowMaterial = LoadMaterial(k_GlowMaterial);
        Material pipeMaterial = LoadMaterial(AssetDatabase.GUIDToAssetPath(k_PipeMaterialGuid));

        for (int i = 0; i < route.Count; i++)
        {
            // The side the charge leaves by and the side it came in on. The two ends have only
            // one of those, and the missing one points down — at the socket the run stands on,
            // and at the door it drops onto, both of which sit a cell below the route. Drawn
            // straight through instead, a run that leaves its socket sideways is a length of
            // horizontal pipe hanging in the air above it, joined to nothing.
            Vector3Int outgoing = i < route.Count - 1 ? route[i + 1] - route[i] : k_Down;
            Vector3Int incoming = i > 0 ? route[i - 1] - route[i] : k_Down;

            // Both sides pointing the same way is the degenerate end piece — a run one cell
            // long, or one whose start had to be nudged clear of a wall. There is no elbow to
            // draw, so it goes down the way it came.
            bool corner = incoming != -outgoing && incoming != outgoing;

            GameObject piece = new GameObject($"Pipe_{i:00}");
            piece.transform.SetParent(pieces.transform, worldPositionStays: false);
            piece.transform.localPosition = ground.GetCellCenterWorld(route[i]) - root.transform.position;

            AddRenderer(piece, PipeSprite(incoming, outgoing, corner), k_PipeSortingOrder, Color.white, pipeMaterial);

            if (corner)
            {
                AddGlow(piece, "Glow_In", HalfGlow(incoming), glowMaterial);
                AddGlow(piece, "Glow_Out", HalfGlow(outgoing), glowMaterial);
            }
            else
            {
                bool vertical = outgoing.x == 0;
                AddGlow(piece, "Glow", vertical ? k_GlowVertical : k_GlowHorizontal, glowMaterial);
            }
        }

        return root.transform;
    }

    private static void AddGlow(GameObject piece, string name, string sprite, Material material)
    {
        GameObject glow = new GameObject(name);
        glow.transform.SetParent(piece.transform, worldPositionStays: false);
        AddRenderer(glow, sprite, k_GlowSortingOrder, k_GlowColor, material);
        glow.SetActive(false);
    }

    private static void AddRenderer(GameObject target, string sprite, int order, Color color, Material material)
    {
        SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite(sprite);
        renderer.color = color;
        renderer.sortingLayerName = k_SortingLayer;
        renderer.sortingOrder = order;

        if (material != null)
            renderer.sharedMaterial = material;
    }

    private static Material LoadMaterial(string path)
    {
        Material material = string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
            Debug.LogError($"[Pipes] missing material {path}");

        return material;
    }

    private static Sprite LoadSprite(string name)
    {
        string folder = name.StartsWith("Glow") ? k_GlowFolder : k_PipeFolder;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");

        if (sprite == null)
            Debug.LogError($"[Pipes] missing sprite {folder}/{name}.png");

        return sprite;
    }

    // Straights are named for the axis they run along; corners for the two sides they open
    // onto, which are exactly the sides the charge arrives from and leaves by.
    private static string PipeSprite(Vector3Int incoming, Vector3Int outgoing, bool corner)
    {
        if (!corner)
            return outgoing.x == 0 ? k_PipeVertical : k_PipeHorizontal;

        bool up   = incoming == k_Up   || outgoing == k_Up;
        bool left = incoming == k_Left || outgoing == k_Left;

        if (up)
            return left ? k_PipeCornerUpLeft : k_PipeCornerUpRight;

        return left ? k_PipeCornerDownLeft : k_PipeCornerDownRight;
    }

    private static string HalfGlow(Vector3Int side)
    {
        if (side == k_Up)   return k_GlowHalfUp;
        if (side == k_Down) return k_GlowHalfDown;
        if (side == k_Left) return k_GlowHalfLeft;

        return k_GlowHalfRight;
    }

    // ─── Wiring ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Puts every renderer in a run onto the layer and orders described up top, and reports
    /// how many it had to change. Runs the builder lays down are born correct; this is for the
    /// ones drawn by hand, which pick up whatever sorting the Scene window happened to be
    /// showing at the time — the run in Level3 had one piece of fourteen on a different layer
    /// from the rest of it.
    /// </summary>
    private static int ApplySorting(Transform root)
    {
        int changed = 0;

        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
        {
            // A glow rides just in front of the piece it lights; everything else in the run is
            // the pipe itself.
            int order = renderer.name.StartsWith("Glow") ? k_GlowSortingOrder : k_PipeSortingOrder;

            if (renderer.sortingLayerName == k_SortingLayer && renderer.sortingOrder == order)
                continue;

            Undo.RecordObject(renderer, "Sort Pipe Connection");
            renderer.sortingLayerName = k_SortingLayer;
            renderer.sortingOrder = order;
            EditorUtility.SetDirty(renderer);
            changed++;
        }

        return changed;
    }

    // Depth-first, which for "piece, then the glows on it" is the order the charge travels —
    // the same order a run drawn by hand in the Scene window already sits in.
    private static List<GameObject> CollectGlows(Transform root) =>
        root.GetComponentsInChildren<Transform>(includeInactive: true)
            .Where(child => child != root && child.name.StartsWith("Glow"))
            .Select(child => child.gameObject)
            .ToList();

    // Pulls the Scene window back to the whole level so the route can be judged at a glance
    // without hunting for it — the point of running this scene after scene is looking at it.
    // Squared on to the level and orthographic, because a level laid out on a flat grid tells
    // you nothing about a pipe seen down a perspective camera at an angle.
    private static void FrameLevel(Transform root)
    {
        SceneView view = SceneView.lastActiveSceneView;

        if (view == null)
            return;

        // The run itself plus a few cells of the level either side of it. Framing on the whole
        // ground tilemap instead is useless: its bounds run far past the lit playable area
        // into the black filler around it, and the pipe ends up too small to judge.
        Bounds bounds = new Bounds(root.position, Vector3.one);

        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
            bounds.Encapsulate(renderer.bounds);

        bounds.Expand(k_FrameMargin);

        view.in2DMode = true;
        view.orthographic = true;
        view.LookAt(
            bounds.center, Quaternion.identity,
            Mathf.Max(bounds.extents.y, bounds.extents.x / k_FrameAspect),
            ortho: true, instant: true);
    }
}
