using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Collectables.EditorTools
{
    /// <summary>
    /// One-button setup for the robot-part collection system, under Tools ▸ Robot Collection.
    ///
    /// Everything here is idempotent — re-running never duplicates assets or scene objects —
    /// so this is also the repair tool when art or level assignments change.
    ///
    /// <b>Run Full Setup</b> does, in order:
    ///   1. builds a <see cref="RobotDefinition"/> per robot from the generated sprite folders
    ///      and the <see cref="RobotCollectionDatabase"/> in Resources;
    ///   2. assigns one part per level to the LevelConfig assets (5 levels per robot, in order);
    ///   3. builds the level HUD prefab and the world pickup prefab;
    ///   4. per level scene: strips the legacy collectable objects, then drops in the HUD and
    ///      the pickup;
    ///   5. builds the home screen's Collection tab from the same components.
    /// </summary>
    public static class RobotCollectionSetup
    {
        // ─── Paths ────────────────────────────────────────────────────────────────

        /// <summary>Generated UI art: a silhouette plus one masked layer per part.</summary>
        private const string SpriteRoot = "Assets/MainGame/Sprites/RobotParts";

        /// <summary>The artist's original drops (ECHO/, NOVA/, …), used for the world pickups.</summary>
        private const string SourceSpriteRoot = "Assets/MainGame/Sprites";
        private const string DefinitionFolder = "Assets/MainGame/HomeUIData/RobotCollection";
        private const string ResourcesFolder = "Assets/Resources";
        private const string DatabasePath = ResourcesFolder + "/RobotCollectionDatabase.asset";
        private const string PrefabFolder = "Assets/MainGame/Prefabs/Collectables";
        private const string HudPrefabPath = PrefabFolder + "/RobotCollectionHUD.prefab";
        private const string PickupPrefabPath = PrefabFolder + "/RobotPartPickup.prefab";
        private const string LevelConfigFolder = "Assets/MainGame/ScriptableObjects/LevelConfigs";
        private const string GameSceneFolder = "Assets/MainGame/Scenes";

        private const string HudObjectName = "RobotCollectionHUD";
        private const string PickupObjectName = "RobotPartPickup";

        /// <summary>
        /// Prefabs from the deleted collectable system. Their instances are removed from every
        /// scene by <see cref="PurgeLegacy"/>; the assets themselves are deleted afterwards.
        /// </summary>
        private static readonly string[] LegacyPrefabPaths =
        {
            PrefabFolder + "/CollectableHUD.prefab",
            PrefabFolder + "/MemoryShard.prefab",
            PrefabFolder + "/RobotPart 2.prefab",
            PrefabFolder + "/RobotPart_Bolt.prefab",
            PrefabFolder + "/RobotPart_Bolt 1.prefab",
            PrefabFolder + "/RobotPart_Gear.prefab",
            PrefabFolder + "/RobotPart_Screw.prefab",
            PrefabFolder + "/RobotPart_ScrewG.prefab",
        };

        /// <summary>
        /// Base names of the legacy prefabs. Once the prefab assets are gone, their scene
        /// instances no longer resolve to a path, so leftovers in scenes that were not part of
        /// the main sweep are matched by name plus a missing-asset check.
        /// </summary>
        private static readonly string[] LegacyPrefabNames =
        {
            "CollectableHUD",
            "MemoryShard",
            // Covers every old pickup variant — RobotPart_Gear, RobotPart_Bolt 1, and the
            // instances a designer renamed to a bare "RobotPart".
            "RobotPart",
        };

        /// <summary>Display names and accent colours, in the order the UI lays robots out.</summary>
        private static readonly (RobotId robot, string display, Color accent)[] RobotAuthoring =
        {
            (RobotId.Echo,  "ECHO",  new Color(0.29f, 0.78f, 0.95f)),
            (RobotId.Nova,  "NOVA",  new Color(0.90f, 0.92f, 0.96f)),
            (RobotId.Patch, "PATCH", new Color(0.96f, 0.78f, 0.35f)),
            (RobotId.Pixel, "PIXEL", new Color(0.93f, 0.49f, 0.24f)),
        };

        // ─── Menu entries ─────────────────────────────────────────────────────────

        [MenuItem("Tools/Robot Collection/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RobotCollection] Exit play mode before running setup.");
                return;
            }

            BuildDatabase();
            AssignPartsToLevels();
            BuildPrefabs();
            SetupAllScenes();
            SetupHomeScreen();

            Debug.Log("[RobotCollection] Full setup complete.");
        }

        [MenuItem("Tools/Robot Collection/1. Build Database And Definitions", priority = 20)]
        public static void BuildDatabase()
        {
            EnsureFolder(DefinitionFolder);
            EnsureFolder(ResourcesFolder);

            var definitions = new List<RobotDefinition>();

            foreach (var (robot, display, accent) in RobotAuthoring)
            {
                string name = robot.ToString();
                string path = $"{DefinitionFolder}/{name}.asset";

                var definition = AssetDatabase.LoadAssetAtPath<RobotDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<RobotDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                definition.robot = robot;
                definition.displayName = display;
                definition.accentColor = accent;
                definition.silhouette = LoadSprite($"{SpriteRoot}/{name}/{name}_Silhouette.png");

                definition.partSprites = new Sprite[RobotIds.PartsPerRobot];
                definition.pickupSprites = new Sprite[RobotIds.PartsPerRobot];
                for (int i = 0; i < RobotIds.PartsPerRobot; i++)
                {
                    definition.partSprites[i] = LoadSprite($"{SpriteRoot}/{name}/{name}_Part{i + 1}.png");

                    // World pickups wear the artist's original artwork — the whole robot dark
                    // with this part lit — because several masked layers are only a handful of
                    // pixels and would be invisible lying in a level.
                    definition.pickupSprites[i] = LoadSprite($"{SourceSpriteRoot}/{name.ToUpperInvariant()}/{name} ({i + 1}).png");
                }

                if (definition.partNames == null || definition.partNames.Length != RobotIds.PartsPerRobot)
                    definition.partNames = new string[RobotIds.PartsPerRobot];

                int missingParts = definition.partSprites.Count(s => s == null);
                int missingPickups = definition.pickupSprites.Count(s => s == null);
                if (definition.silhouette == null || missingParts > 0 || missingPickups > 0)
                {
                    Debug.LogWarning($"[RobotCollection] {name}: silhouette " +
                                     $"{(definition.silhouette == null ? "MISSING" : "ok")}, " +
                                     $"{missingParts} part sprite(s) missing under {SpriteRoot}/{name}, " +
                                     $"{missingPickups} pickup sprite(s) missing under " +
                                     $"{SourceSpriteRoot}/{name.ToUpperInvariant()}.");
                }

                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            var database = AssetDatabase.LoadAssetAtPath<RobotCollectionDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<RobotCollectionDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.robots = definitions.ToArray();
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RobotCollectionService.InvalidateDatabase();

            Debug.Log($"[RobotCollection] Database built with {definitions.Count} robots → {DatabasePath}");
        }

        [MenuItem("Tools/Robot Collection/2. Assign Parts To Levels", priority = 21)]
        public static void AssignPartsToLevels()
        {
            // Configs are ordered by their own levelNumber, not by filename, so the walk
            // follows play order (Tutorial1..4, then Level1..20) rather than "Level10" < "Level2".
            var configs = AssetDatabase.FindAssets("t:LevelConfig", new[] { LevelConfigFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
                .Where(c => c != null)
                .OrderBy(c => c.levelNumber)
                .ToList();

            if (configs.Count == 0)
            {
                Debug.LogWarning($"[RobotCollection] No LevelConfig assets found under {LevelConfigFolder}.");
                return;
            }

            int assigned = 0;

            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config.robotPart == null) config.robotPart = new RobotPartAssignment();

                // Sequential per robot: the first five levels complete ECHO, the next five
                // NOVA, and so on. Levels past the last part hold none.
                int robotIndex = i / RobotIds.PartsPerRobot;

                if (robotIndex < RobotIds.All.Length)
                {
                    config.robotPart.placePart = true;
                    config.robotPart.robot = RobotIds.All[robotIndex];
                    config.robotPart.partNumber = (i % RobotIds.PartsPerRobot) + 1;
                    assigned++;
                }
                else
                {
                    config.robotPart.placePart = false;
                }

                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[RobotCollection] Assigned {assigned} parts across {configs.Count} level configs " +
                      $"(levels {configs.First().levelNumber}..{configs.Last().levelNumber}).");
        }

        [MenuItem("Tools/Robot Collection/3. Build Prefabs", priority = 22)]
        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);
            BuildHudPrefab();
            BuildPickupPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Robot Collection/4. Setup All Level Scenes", priority = 23)]
        public static void SetupAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RobotCollection] Exit play mode before running scene setup.");
                return;
            }

            var scenePaths = FindLevelScenes();
            if (scenePaths.Count == 0)
            {
                Debug.LogWarning("[RobotCollection] No level scenes found.");
                return;
            }

            string reopen = EditorSceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[RobotCollection] Scene setup cancelled — unsaved changes were kept.");
                return;
            }

            int touched = 0;
            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string path = scenePaths[i];
                    EditorUtility.DisplayProgressBar("Robot Collection",
                        $"Setting up {Path.GetFileNameWithoutExtension(path)} ({i + 1}/{scenePaths.Count})",
                        (float)i / scenePaths.Count);

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = SetupScene(scene);

                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        touched++;
                    }

                    // Per-scene logging: a long editor job must stay legible if it stalls.
                    Debug.Log($"[RobotCollection] {Path.GetFileNameWithoutExtension(path)}: " +
                              (changed ? "updated" : "already up to date"));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen))
                    EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
            }

            DeleteLegacyPrefabAssets();

            Debug.Log($"[RobotCollection] Scene setup done — {touched}/{scenePaths.Count} scenes changed.");
        }

        [MenuItem("Tools/Robot Collection/Reset Progress", priority = 40)]
        public static void ResetProgress()
        {
            RobotCollectionService.ResetAll();
            Debug.Log($"[RobotCollection] Progress reset ({RobotPartSaveSystem.SavePath}).");
        }

        [MenuItem("Tools/Robot Collection/Log Level Assignments", priority = 41)]
        public static void LogAssignments()
        {
            var configs = AssetDatabase.FindAssets("t:LevelConfig", new[] { LevelConfigFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (path: p, config: AssetDatabase.LoadAssetAtPath<LevelConfig>(p)))
                .Where(c => c.config != null)
                .OrderBy(c => c.config.levelNumber);

            var report = new System.Text.StringBuilder("[RobotCollection] Level → part assignments\n");
            foreach (var (path, config) in configs)
            {
                var part = config.robotPart;
                string what = part != null && part.placePart
                    ? $"{part.robot} part {part.partNumber}  ({part.PartKey})"
                    : "—";
                report.AppendLine($"  {config.levelNumber,3}  {Path.GetFileNameWithoutExtension(path),-18} {what}");
            }
            Debug.Log(report.ToString());
        }

        // ─── Scene work ───────────────────────────────────────────────────────────

        /// <summary>Every level scene that has a build entry, plus the unshipped Abel levels.</summary>
        private static List<string> FindLevelScenes()
        {
            var paths = new List<string>();

            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.path)) continue;
                if (!IsLevelScene(entry.path)) continue;
                paths.Add(entry.path);
            }

            // Levels authored but not yet in the build list still get set up, so adding them
            // to the build later is a one-step change.
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/MainGame/Scenes/Abel" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsLevelScene(path) && !paths.Contains(path)) paths.Add(path);
            }

            return paths;
        }

        private static bool IsLevelScene(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("Level", System.StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Tutorial", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Strips the legacy system from a scene, then ensures the HUD and pickup exist.</summary>
        private static bool SetupScene(Scene scene)
        {
            bool changed = PurgeLegacy(scene, stripMissingScripts: true);
            changed |= EnsureHud(scene);
            changed |= EnsurePickup(scene);
            return changed;
        }

        /// <summary>
        /// Removes objects belonging to the deleted collectable system: instances of the legacy
        /// prefabs, and the missing-script components those scripts left on surviving objects
        /// (the LevelManager keeps its LevelContext, so it is cleaned rather than deleted).
        /// </summary>
        /// <param name="stripMissingScripts">
        /// Also clear null MonoBehaviours left behind by the deleted scripts. Only safe on our
        /// own level scenes: it would otherwise strip unrelated broken components out of any
        /// scene the sweep happens to open.
        /// </param>
        private static bool PurgeLegacy(Scene scene, bool stripMissingScripts)
        {
            bool changed = false;

            var legacy = new HashSet<string>(LegacyPrefabPaths);
            var doomed = new List<GameObject>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    var go = transform.gameObject;

                    string source = PrefabUtility.IsPartOfPrefabInstance(go)
                        ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)
                        : null;

                    bool isLegacy = !string.IsNullOrEmpty(source) && legacy.Contains(source);

                    // Scenes outside the main sweep still hold instances of prefabs that have
                    // since been deleted; those resolve to no path at all. Match them on name
                    // as well, but only when the asset really is missing, so a live prefab that
                    // happens to share a name is never touched.
                    if (!isLegacy && PrefabUtility.IsPrefabAssetMissing(go))
                        isLegacy = HasLegacyName(go.name);

                    if (isLegacy)
                    {
                        var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;
                        if (!doomed.Contains(instanceRoot)) doomed.Add(instanceRoot);
                    }
                }
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
                changed = true;
            }

            // Collectable / CollectableLevelManager / CollectableHUD are gone, so anything that
            // still carried them now holds a null component. Clear those without touching the
            // object, which may still hold live components such as LevelContext.
            if (!stripMissingScripts) return changed;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    if (removed > 0) changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Puts the current HUD prefab in the scene, replacing any older instance. The HUD holds
        /// no per-scene authoring, so rebuilding it wholesale is what makes a layout change in
        /// the prefab reach every level.
        /// </summary>
        private static bool EnsureHud(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RobotCollection] HUD prefab missing at {HudPrefabPath}; run Build Prefabs.");
                return false;
            }

            var existing = FindInScene(scene, HudObjectName);
            if (existing != null) Object.DestroyImmediate(existing);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = HudObjectName;
            return true;
        }

        /// <summary>
        /// Adds the pickup only when the scene has none. Unlike the HUD it is never replaced:
        /// where a part sits in a level is hand-placed work, and re-running setup must not
        /// throw it away.
        /// </summary>
        private static bool EnsurePickup(Scene scene)
        {
            if (FindInScene(scene, PickupObjectName) != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RobotCollection] Pickup prefab missing at {PickupPrefabPath}; run Build Prefabs.");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = PickupObjectName;

            // Park it on the player's spawn so a designer only has to drag it somewhere
            // interesting, rather than hunt for it at the world origin.
            var player = GameObject.FindGameObjectWithTag("Player");
            instance.transform.position = player != null
                ? player.transform.position + new Vector3(2f, 1.5f, 0f)
                : Vector3.zero;

            return true;
        }

        /// <summary>
        /// True when a name matches a legacy prefab, allowing renames and Unity's " (1)"
        /// suffixes. The current pickup is excluded by name because "RobotPartPickup" also
        /// starts with "RobotPart" — it must never be purged, even if its asset goes missing.
        /// </summary>
        private static bool HasLegacyName(string name)
        {
            if (name.StartsWith(PickupObjectName, System.StringComparison.Ordinal)) return false;

            foreach (var legacy in LegacyPrefabNames)
                if (name.StartsWith(legacy, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Clears legacy collectable leftovers out of every other scene in the project — the
        /// unshipped duplicates under Scenes/ and the recovery scenes. They are not part of the
        /// build, but leaving them pointing at deleted prefabs would show as broken instances.
        /// </summary>
        [MenuItem("Tools/Robot Collection/Purge Legacy From Remaining Scenes", priority = 42)]
        public static void PurgeLegacyEverywhereElse()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RobotCollection] Exit play mode before purging.");
                return;
            }

            // Scoped to the game's own scenes. A project-wide sweep would open third-party
            // demo scenes too, dirtying them (and generating lighting assets) for nothing.
            var alreadyDone = new HashSet<string>(FindLevelScenes());
            var remaining = AssetDatabase.FindAssets("t:Scene", new[] { GameSceneFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !alreadyDone.Contains(p))
                .OrderBy(p => p)
                .ToList();

            string reopen = EditorSceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[RobotCollection] Purge cancelled — unsaved changes were kept.");
                return;
            }

            int touched = 0;
            try
            {
                for (int i = 0; i < remaining.Count; i++)
                {
                    string path = remaining[i];
                    EditorUtility.DisplayProgressBar("Robot Collection",
                        $"Purging {Path.GetFileNameWithoutExtension(path)} ({i + 1}/{remaining.Count})",
                        (float)i / remaining.Count);

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (!PurgeLegacy(scene, stripMissingScripts: false)) continue;

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    touched++;
                    Debug.Log($"[RobotCollection] Purged {path}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen))
                    EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
            }

            Debug.Log($"[RobotCollection] Purge done — {touched}/{remaining.Count} remaining scenes cleaned.");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                    if (t.gameObject.name == name) return t.gameObject;
            }
            return null;
        }

        private static void DeleteLegacyPrefabAssets()
        {
            foreach (var path in LegacyPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[RobotCollection] Deleted legacy prefab {path}");
            }
            AssetDatabase.Refresh();
        }

        // ─── Prefab construction ──────────────────────────────────────────────────

        /// <summary>
        /// Builds the level HUD: its own screen-space canvas holding a right-aligned column of
        /// four robot slots. Level scenes have no shared canvas, so the HUD carries its own.
        /// </summary>
        private static void BuildHudPrefab()
        {
            var root = new GameObject(HudObjectName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Bottom-most UI layer: the level's own HUD canvases sit at 0 and the pause menu at
            // 0, so anything higher would leave the collection panel floating over the pause
            // dialog and the brightness overlay.
            canvas.sortingOrder = -1;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Column pinned to the right edge, vertically centred.
            var column = NewUiObject("Robots", root.transform);
            var columnRect = column.GetComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(1f, 0.5f);
            columnRect.anchorMax = new Vector2(1f, 0.5f);
            columnRect.pivot = new Vector2(1f, 0.5f);
            columnRect.anchoredPosition = new Vector2(-28f, 0f);
            columnRect.sizeDelta = new Vector2(104f, 0f);

            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = column.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var backdrop = column.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.30f);
            backdrop.raycastTarget = false;

            var view = column.AddComponent<RobotCollectionView>();
            var slots = new List<RobotCollectionSlot>();

            foreach (var (robot, display, accent) in RobotAuthoring)
            {
                slots.Add(BuildSlot(column.transform, robot, display, accent,
                    portrait: 84f, showName: false, showCount: true));
            }

            SetPrivateField(view, "m_Slots", slots.ToArray());

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[RobotCollection] Built HUD prefab → {HudPrefabPath}");
        }

        /// <summary>
        /// Builds one robot entry: silhouette, five part layers stacked on it, and a count
        /// label. Shared by the level HUD and the home-screen collection grid.
        /// </summary>
        private static RobotCollectionSlot BuildSlot(Transform parent, RobotId robot, string display,
            Color accent, float portrait, bool showName, bool showCount)
        {
            string name = robot.ToString();
            var definition = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{DefinitionFolder}/{name}.asset");

            var slotGo = NewUiObject($"Slot_{name}", parent);
            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(portrait,
                portrait + (showName ? 36f : 0f) + (showCount ? 30f : 0f));

            var slotLayout = slotGo.AddComponent<VerticalLayoutGroup>();
            slotLayout.spacing = 6f;
            slotLayout.childAlignment = TextAnchor.UpperCenter;
            // Width is left alone so the portrait keeps its authored square size; stretching it
            // to the column width would letterbox the robot inside its own rect.
            slotLayout.childControlWidth = false;
            slotLayout.childControlHeight = false;
            slotLayout.childForceExpandWidth = false;
            slotLayout.childForceExpandHeight = false;

            var slotSize = slotGo.AddComponent<LayoutElement>();
            slotSize.preferredWidth = portrait;

            var slot = slotGo.AddComponent<RobotCollectionSlot>();

            // ── portrait: silhouette + one layer per part, all on the same rect ──
            var portraitGo = NewUiObject("Portrait", slotGo.transform);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.sizeDelta = new Vector2(portrait, portrait);
            var portraitSize = portraitGo.AddComponent<LayoutElement>();
            portraitSize.preferredHeight = portrait;
            portraitSize.preferredWidth = portrait;

            var silhouetteGo = NewUiObject("Silhouette", portraitGo.transform);
            StretchToParent(silhouetteGo.GetComponent<RectTransform>());
            var silhouette = silhouetteGo.AddComponent<Image>();
            silhouette.raycastTarget = false;
            silhouette.preserveAspect = true;
            if (definition != null) silhouette.sprite = definition.silhouette;

            var layers = new Image[RobotIds.PartsPerRobot];
            for (int i = 0; i < RobotIds.PartsPerRobot; i++)
            {
                var layerGo = NewUiObject($"Part{i + 1}", portraitGo.transform);
                StretchToParent(layerGo.GetComponent<RectTransform>());
                var image = layerGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                if (definition != null) image.sprite = definition.GetPartSprite(i);
                image.enabled = false;   // starts empty; Refresh turns collected parts on
                layers[i] = image;
            }

            TMP_Text nameLabel = null;
            if (showName)
            {
                nameLabel = BuildLabel(slotGo.transform, "Name", display, 22f, accent, 30f, portrait);
            }

            TMP_Text countLabel = null;
            if (showCount)
            {
                countLabel = BuildLabel(slotGo.transform, "Count", "0/5", 20f,
                    new Color(0.87f, 0.90f, 0.95f), 24f, portrait);
            }

            SetPrivateField(slot, "m_Silhouette", silhouette);
            SetPrivateField(slot, "m_PartLayers", layers);
            SetPrivateField(slot, "m_NameLabel", nameLabel);
            SetPrivateField(slot, "m_CountLabel", countLabel);

            return slot;
        }

        private static TMP_Text BuildLabel(Transform parent, string name, string text, float size,
            Color color, float height, float width = 0f)
        {
            var go = NewUiObject(name, parent);

            // Slot layouts don't control child width, so a label has to carry its own.
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width > 0f ? width : 160f, height);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            if (width > 0f) element.preferredWidth = width;

            return label;
        }

        /// <summary>Builds the world pickup: a trigger with a sprite, dressed by the level's config.</summary>
        private static void BuildPickupPrefab()
        {
            var root = new GameObject(PickupObjectName);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            // A placeholder so the object is visible in the scene view before play; the real
            // sprite is applied at runtime from the level's assignment.
            var echo = AssetDatabase.LoadAssetAtPath<RobotDefinition>($"{DefinitionFolder}/{RobotId.Echo}.asset");
            if (echo != null) renderer.sprite = echo.GetPartSprite(0);

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            root.AddComponent<RobotPartPickup>();

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[RobotCollection] Built pickup prefab → {PickupPrefabPath}");
        }

        // ─── Home screen collection tab ───────────────────────────────────────────

        private const string HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";
        private const string HomePanelName = "Collection Panel";
        private const string HomeGridName = "RobotCollection";

        /// <summary>
        /// Fills the home screen's Collection tab with the same four robots the level HUD
        /// shows, drawn larger and with names. The tab reuses <see cref="RobotCollectionView"/>
        /// and <see cref="RobotCollectionSlot"/>, so both places always agree.
        /// </summary>
        [MenuItem("Tools/Robot Collection/5. Setup Home Screen Tab", priority = 24)]
        public static void SetupHomeScreen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RobotCollection] Exit play mode before running setup.");
                return;
            }

            string reopen = EditorSceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[RobotCollection] Home screen setup cancelled — unsaved changes were kept.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);

            var panel = FindInScene(scene, HomePanelName);
            if (panel == null)
            {
                Debug.LogWarning($"[RobotCollection] No '{HomePanelName}' in {HomeScenePath}.");
                return;
            }

            // The panel's own children (background, title, back button) live under Holder;
            // the grid goes there too so it inherits the tab's framing.
            var holder = panel.transform.Find("Holder");
            Transform parent = holder != null ? holder : panel.transform;

            var existing = parent.Find(HomeGridName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var grid = NewUiObject(HomeGridName, parent);
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -40f);
            gridRect.sizeDelta = new Vector2(1200f, 420f);

            var column = grid.AddComponent<VerticalLayoutGroup>();
            column.spacing = 24f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            // Height must be controlled: otherwise the robot row keeps its zero-height rect,
            // its slots spill out of it, and the total label lands on top of them.
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var view = grid.AddComponent<RobotCollectionView>();

            var totalLabel = BuildLabel(grid.transform, "Total", "0/20", 40f,
                new Color(0.95f, 0.96f, 1f), 54f, 600f);

            var row = NewUiObject("Robots", grid.transform);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(1200f, 300f);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 34f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowSize = row.AddComponent<LayoutElement>();
            rowSize.preferredHeight = 300f;

            var slots = new List<RobotCollectionSlot>();
            foreach (var (robot, display, accent) in RobotAuthoring)
            {
                slots.Add(BuildSlot(row.transform, robot, display, accent,
                    portrait: 210f, showName: true, showCount: true));
            }

            SetPrivateField(view, "m_Slots", slots.ToArray());
            SetPrivateField(view, "m_TotalLabel", totalLabel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[RobotCollection] Home screen Collection tab built with {slots.Count} robots.");

            if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen) && reopen != HomeScenePath)
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        // ─── Small helpers ────────────────────────────────────────────────────────

        /// <summary>Creates a UI GameObject with a RectTransform already attached.</summary>
        private static GameObject NewUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>
        /// Writes a [SerializeField] private field. The setup tool authors these components
        /// from scratch, so it needs to reach the same fields the inspector shows.
        /// </summary>
        private static void SetPrivateField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[RobotCollection] {target.GetType().Name} has no field '{field}'.");
                return;
            }

            if (value is System.Array array && property.isArray)
            {
                property.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = (Object)array.GetValue(i);
            }
            else
            {
                property.objectReferenceValue = (Object)value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Creates an asset folder (and any missing parents) if it doesn't exist.</summary>
        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
