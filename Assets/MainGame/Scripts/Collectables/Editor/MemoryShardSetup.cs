using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Collectables.EditorTools
{
    /// <summary>
    /// One-button setup for the memory-shard system, under Tools ▸ Memory Shards.
    ///
    /// Everything here is idempotent — re-running never duplicates assets or scene objects —
    /// so this is also the repair tool when the art or the prefabs change.
    ///
    /// <b>Run Full Setup</b> does, in order:
    ///   1. builds the <see cref="MemoryShardDatabase"/> in Resources from the shard sprites,
    ///      seeding placeholder story thresholds only when there are none;
    ///   2. builds the pickup, HUD and story-presenter prefabs;
    ///   3. per level scene: replaces the HUD and presenter, and drops in a pickup if missing.
    ///
    /// It deliberately does <b>not</b> decide which levels hide a shard. That is a design
    /// choice per level (<c>LevelConfig ▸ Collectables ▸ Place Shard</c>), so the bulk helpers
    /// for it sit on their own menu entries and are never run as part of the full setup.
    /// </summary>
    public static class MemoryShardSetup
    {
        // ─── Paths ────────────────────────────────────────────────────────────────

        private const string ShardSpriteFolder = "Assets/MainGame/Sprites/Collectibles/memory shards";
        private const string ShardSpriteFile = "MEMORY SHARD.png";
        private const string CollectEffectSpriteFolder = "Assets/MainGame/Sprites/Effects/item_collection_effect";
        private const string ResourcesFolder = "Assets/Resources";
        internal const string DatabasePath = ResourcesFolder + "/MemoryShardDatabase.asset";
        private const string PrefabFolder = "Assets/MainGame/Prefabs/Collectables";
        private const string PickupPrefabPath = PrefabFolder + "/MemoryShardPickup.prefab";
        private const string CollectEffectPrefabPath = PrefabFolder + "/MemoryShardCollectEffect.prefab";
        private const string HudPrefabPath = PrefabFolder + "/MemoryShardHUD.prefab";
        internal const string PresenterPrefabPath = PrefabFolder + "/MemoryStoryPresenter.prefab";
        private const string LevelConfigFolder = "Assets/MainGame/ScriptableObjects/LevelConfigs";

        private const string PickupObjectName = "MemoryShardPickup";
        private const string HudObjectName = "MemoryShardHUD";
        private const string PresenterObjectName = "MemoryStoryPresenter";

        /// <summary>
        /// The thresholds written when the database has no stories yet — one story every
        /// 9 shards, matching the three Memory clips. Only ever used to seed an empty list;
        /// re-running never overwrites authored entries.
        /// </summary>
        private static readonly (string id, int shards, string title)[] SeedStories =
        {
            ("memory_01", 9,  "Memory I"),
            ("memory_02", 18, "Memory II"),
            ("memory_03", 27, "Memory III"),
        };

        // ─── Menu entries ─────────────────────────────────────────────────────────

        [MenuItem("Tools/Memory Shards/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MemoryShards] Exit play mode before running setup.");
                return;
            }

            BuildDatabase();
            BuildPrefabs();
            SetupAllScenes();

            Debug.Log("[MemoryShards] Full setup complete.");
        }

        [MenuItem("Tools/Memory Shards/1. Build Database", priority = 20)]
        public static void BuildDatabase()
        {
            EnsureFolder(ResourcesFolder);

            var database = AssetDatabase.LoadAssetAtPath<MemoryShardDatabase>(DatabasePath);
            bool created = database == null;

            if (created)
            {
                database = ScriptableObject.CreateInstance<MemoryShardDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            // Art is rebuilt every run — it is generated from what is on disk.
            database.shardSprites = LoadShardSprites();

            // Stories are NOT. They hold the thresholds and the clips, which are authored by
            // hand; overwriting them on every setup run would throw the design away. Seeded
            // only when there is nothing there at all.
            if (database.stories == null || database.stories.Length == 0)
            {
                database.stories = SeedStories
                    .Select(seed => new MemoryStoryEntry
                    {
                        id = seed.id,
                        requiredShards = seed.shards,
                        title = seed.title,
                        clip = null,
                    })
                    .ToArray();

                Debug.Log($"[MemoryShards] Seeded {database.stories.Length} placeholder stories " +
                          "(9/18/27 shards, no clips). Re-tune them on the database asset.");
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            MemoryShardService.InvalidateDatabase();

            Debug.Log($"[MemoryShards] {(created ? "Created" : "Updated")} database → {DatabasePath} " +
                      $"({database.shardSprites.Length} sprites, {database.StoryCount} stories).");
        }

        [MenuItem("Tools/Memory Shards/2. Build Prefabs", priority = 21)]
        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);

            // Before the pickup — the pickup references this prefab, so it has to exist first.
            BuildCollectEffectPrefab();

            BuildPickupPrefab();
            BuildHudPrefab();
            BuildPresenterPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Memory Shards/3. Setup All Level Scenes", priority = 22)]
        public static void SetupAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MemoryShards] Exit play mode before running scene setup.");
                return;
            }

            var scenePaths = FindLevelScenes();
            if (scenePaths.Count == 0)
            {
                Debug.LogWarning("[MemoryShards] No level scenes found.");
                return;
            }

            string reopen = EditorSceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[MemoryShards] Scene setup cancelled — unsaved changes were kept.");
                return;
            }

            int touched = 0;
            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string path = scenePaths[i];
                    EditorUtility.DisplayProgressBar("Memory Shards",
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
                    Debug.Log($"[MemoryShards] {Path.GetFileNameWithoutExtension(path)}: " +
                              (changed ? "updated" : "already up to date"));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen))
                    EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
            }

            Debug.Log($"[MemoryShards] Scene setup done — {touched}/{scenePaths.Count} scenes changed.");
        }

        // ─── Assignment helpers (never part of Run Full Setup) ────────────────────

        /// <summary>
        /// Ticks Place Shard on every level config. A starting point for "every level has one";
        /// untick the levels that should not from their own config afterwards.
        /// </summary>
        [MenuItem("Tools/Memory Shards/Assignment/Place A Shard In Every Level", priority = 30)]
        public static void PlaceShardInEveryLevel() => SetShardOnAllLevels(true);

        /// <summary>Unticks Place Shard on every level config, leaving no shards in the game.</summary>
        [MenuItem("Tools/Memory Shards/Assignment/Remove Shards From Every Level", priority = 31)]
        public static void RemoveShardFromEveryLevel() => SetShardOnAllLevels(false);

        private static void SetShardOnAllLevels(bool place)
        {
            var configs = LoadLevelConfigs();
            if (configs.Count == 0)
            {
                Debug.LogWarning($"[MemoryShards] No LevelConfig assets found under {LevelConfigFolder}.");
                return;
            }

            foreach (var config in configs)
            {
                if (config.memoryShard == null) config.memoryShard = new MemoryShardAssignment();
                config.memoryShard.placeShard = place;
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MemoryShards] {(place ? "Placed" : "Removed")} shards across {configs.Count} level configs.");
        }

        [MenuItem("Tools/Memory Shards/Log Shard Assignments", priority = 40)]
        public static void LogAssignments()
        {
            var configs = LoadLevelConfigs();
            if (configs.Count == 0)
            {
                Debug.LogWarning($"[MemoryShards] No LevelConfig assets found under {LevelConfigFolder}.");
                return;
            }

            var lines = new List<string>();
            int withShard = 0;

            foreach (var config in configs)
            {
                bool has = config.memoryShard != null && config.memoryShard.placeShard;
                if (has) withShard++;

                lines.Add($"  level {config.levelNumber,2} ({config.name}): " +
                          (has ? $"shard, variant {config.memoryShard.VariantIndex(config.levelNumber) + 1}" : "—"));
            }

            var database = AssetDatabase.LoadAssetAtPath<MemoryShardDatabase>(DatabasePath);
            string thresholds = database == null
                ? "no database"
                : string.Join(", ", database.StoriesByThreshold().Select(s => $"{s.SafeId}@{s.requiredShards}"));

            Debug.Log($"[MemoryShards] {withShard} of {configs.Count} levels hide a shard.\n" +
                      string.Join("\n", lines) +
                      $"\n  story thresholds: {thresholds}");
        }

        [MenuItem("Tools/Memory Shards/Reset Progress", priority = 41)]
        public static void ResetProgress()
        {
            MemoryShardService.ResetAll();
            Debug.Log($"[MemoryShards] Progress reset — {MemoryShardSaveSystem.SavePath} deleted.");
        }

        // ─── Scene wiring ─────────────────────────────────────────────────────────

        internal static List<string> FindLevelScenes()
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

        private static bool SetupScene(Scene scene)
        {
            bool changed = false;

            // HUD and presenter are replaced every run so prefab changes reach every level.
            changed |= EnsureReplaced(scene, HudPrefabPath, HudObjectName);
            changed |= EnsureReplaced(scene, PresenterPrefabPath, PresenterObjectName);

            // The pickup is only added when missing — where a shard sits in a level is
            // hand-placed work, and re-running setup must not throw it away.
            changed |= EnsurePickup(scene);

            return changed;
        }

        private static bool EnsureReplaced(Scene scene, string prefabPath, string objectName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MemoryShards] Prefab missing at {prefabPath}; run Build Prefabs.");
                return false;
            }

            var existing = FindInScene(scene, objectName);
            if (existing != null) Object.DestroyImmediate(existing);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = objectName;
            return true;
        }

        private static bool EnsurePickup(Scene scene)
        {
            if (FindInScene(scene, PickupObjectName) != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MemoryShards] Pickup prefab missing at {PickupPrefabPath}; run Build Prefabs.");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = PickupObjectName;

            // Park it near the player's spawn so a designer only has to drag it somewhere
            // interesting, rather than hunt for it at the world origin. Offset away from the
            // robot part, which the other tool parks at +2, +1.5.
            var player = GameObject.FindGameObjectWithTag("Player");
            instance.transform.position = player != null
                ? player.transform.position + new Vector3(-2f, 1.5f, 0f)
                : Vector3.zero;

            return true;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        // ─── Prefab builders ──────────────────────────────────────────────────────

        /// <summary>
        /// The burst played where a shard was picked up. Reuses <see cref="OneShotEffect"/>, which
        /// runs the frames once on unscaled time and then destroys its own GameObject — so the
        /// pickup only has to instantiate it and forget about it.
        /// </summary>
        private static void BuildCollectEffectPrefab()
        {
            var root = new GameObject("MemoryShardCollectEffect");

            var renderer = root.AddComponent<SpriteRenderer>();

            // Above the pickup's own sorting order (10) so the burst reads over the shard rather
            // than behind it.
            renderer.sortingOrder = 20;

            var frames = LoadFrameSprites(CollectEffectSpriteFolder);
            if (frames.Length > 0) renderer.sprite = frames[0];

            var effect = root.AddComponent<OneShotEffect>();
            SetPrivateSpriteArray(effect, "m_Frames", frames);

            // 5 frames at 20fps is a quarter-second pop — long enough to register as a reward,
            // short enough not to trail behind a player who is already moving on.
            SetPrivateFloat(effect, "m_FramesPerSecond", 20f);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, CollectEffectPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[MemoryShards] Built collect effect prefab ({frames.Length} frames) → {CollectEffectPrefabPath}");
        }

        private static void BuildPickupPrefab()
        {
            var root = new GameObject(PickupObjectName);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            // The shard still. The pickup re-applies it from the database at runtime.
            var sprites = LoadShardSprites();
            if (sprites.Length > 0) renderer.sprite = sprites[0];

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            var pickup = root.AddComponent<MemoryShardPickup>();

            // The burst spawned at the shard's position on collect. Built just above, so it is
            // always present by the time this runs.
            var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectEffectPrefabPath);
            if (effectPrefab != null) SetPrivateField(pickup, "m_CollectEffectPrefab", effectPrefab);
            else Debug.LogWarning($"[MemoryShards] No collect effect at {CollectEffectPrefabPath}; " +
                                  "the pickup will collect silently.");

            // Shine sweep, glow, god rays and the breathing pulse.
            MemoryShardShineBuilder.Build(root);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[MemoryShards] Built pickup prefab → {PickupPrefabPath}");
        }

        private static void BuildHudPrefab()
        {
            var root = NewCanvasRoot(HudObjectName, sortingOrder: 90);
            var hud = root.AddComponent<MemoryShardCounterHUD>();

            // Counter, pinned top-left. The robot HUD owns the right-hand side.
            var counter = NewUiObject("Counter", root.transform);
            var counterRect = counter.GetComponent<RectTransform>();
            counterRect.anchorMin = new Vector2(0f, 1f);
            counterRect.anchorMax = new Vector2(0f, 1f);
            counterRect.pivot = new Vector2(0f, 1f);
            counterRect.anchoredPosition = new Vector2(28f, -28f);
            counterRect.sizeDelta = new Vector2(180f, 64f);

            var layout = counter.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var iconObject = NewUiObject("Icon", counter.transform);
            var icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            iconObject.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);

            var sprites = LoadShardSprites();
            if (sprites.Length > 0) icon.sprite = sprites[0];

            var countLabel = BuildLabel(counter.transform, "Count", "0", 34f, TextAlignmentOptions.Left);
            countLabel.rectTransform.sizeDelta = new Vector2(110f, 48f);

            var progressToast = BuildProgressToast(root.transform, sprites.Length > 0 ? sprites[0] : null);

            SetPrivateField(hud, "m_CountLabel", countLabel);
            SetPrivateField(hud, "m_Icon", icon);
            SetPrivateField(hud, "m_ProgressToast", progressToast);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[MemoryShards] Built HUD prefab → {HudPrefabPath}");
        }

        /// <summary>
        /// The card shown after every pickup — "Memory Shards 4 / 9, collect 5 more to unlock
        /// memory clip" — centred near the top and off by default. Its animation, shine and
        /// wording all live on <see cref="MemoryShardProgressToast"/>.
        /// </summary>
        private static MemoryShardProgressToast BuildProgressToast(Transform parent, Sprite shardSprite)
        {
            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var toast = NewUiObject("ProgressToast", parent);
            var toastRect = toast.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 1f);
            toastRect.anchorMax = new Vector2(0.5f, 1f);
            toastRect.pivot = new Vector2(0.5f, 1f);
            toastRect.anchoredPosition = new Vector2(0f, -110f);
            toastRect.sizeDelta = new Vector2(800f, 170f);
            toast.AddComponent<CanvasGroup>();
            var component = toast.AddComponent<MemoryShardProgressToast>();

            // The card is what pops and slides, so the root keeps its layout position.
            var card = NewUiObject("Card", toast.transform);
            var cardRect = card.GetComponent<RectTransform>();
            StretchToParent(cardRect);
            var cardImage = card.AddComponent<Image>();
            cardImage.sprite = rounded;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.06f, 0.08f, 0.17f, 0.92f);
            cardImage.raycastTarget = false;
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0.35f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            // The shine band sits under its own mask, so only the band is clipped and the icon
            // can still punch past the card edge.
            var mask = NewUiObject("ShineMask", card.transform);
            StretchToParent(mask.GetComponent<RectTransform>());
            mask.AddComponent<RectMask2D>();

            var band = NewUiObject("ShineBand", mask.transform);
            var bandRect = band.GetComponent<RectTransform>();
            bandRect.sizeDelta = new Vector2(140f, 320f);
            bandRect.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var bandImage = band.AddComponent<Image>();
            bandImage.color = new Color(1f, 1f, 1f, 0.35f);
            bandImage.raycastTarget = false;
            band.SetActive(false);

            var iconObject = NewUiObject("Icon", card.transform);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(85f, 0f);
            iconRect.sizeDelta = new Vector2(104f, 104f);
            var icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = shardSprite;

            var title = (TextMeshProUGUI)BuildLabel(card.transform, "Title", "Memory Shards  1 / 9", 40f,
                TextAlignmentOptions.Left);
            PlaceTopLeft(title.rectTransform, new Vector2(165f, -20f), new Vector2(605f, 52f));
            title.color = new Color(1f, 0.86f, 0.4f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f;
            title.fontSizeMax = 40f;

            var hint = (TextMeshProUGUI)BuildLabel(card.transform, "Hint",
                "Collect 8 more to unlock memory clip — Memory I", 26f, TextAlignmentOptions.Left);
            PlaceTopLeft(hint.rectTransform, new Vector2(165f, -76f), new Vector2(605f, 40f));
            hint.color = new Color(1f, 1f, 1f, 0.92f);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 16f;
            hint.fontSizeMax = 26f;

            var bar = NewUiObject("Bar", card.transform);
            PlaceTopLeft(bar.GetComponent<RectTransform>(), new Vector2(165f, -128f), new Vector2(600f, 18f));
            var barImage = bar.AddComponent<Image>();
            barImage.sprite = rounded;
            barImage.type = Image.Type.Sliced;
            barImage.color = new Color(1f, 1f, 1f, 0.15f);
            barImage.raycastTarget = false;

            // Filled needs a sprite — a spriteless Image ignores fillAmount and draws full.
            var fill = NewUiObject("Fill", bar.transform);
            StretchToParent(fill.GetComponent<RectTransform>());
            var fillImage = fill.AddComponent<Image>();
            fillImage.sprite = rounded;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0.1f;
            fillImage.color = new Color(0.45f, 0.85f, 1f);
            fillImage.raycastTarget = false;

            SetPrivateField(component, "m_Card", cardRect);
            SetPrivateField(component, "m_TitleLabel", title);
            SetPrivateField(component, "m_HintLabel", hint);
            SetPrivateField(component, "m_Icon", icon);
            SetPrivateField(component, "m_BarFill", fillImage);
            SetPrivateField(component, "m_ShineBand", bandRect);

            toast.SetActive(false);
            return component;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void BuildPresenterPrefab()
        {
            // Above every other canvas in a level — it is a full-screen cutscene.
            var root = NewCanvasRoot(PresenterObjectName, sortingOrder: 500);
            var group = root.AddComponent<CanvasGroup>();
            var presenter = root.AddComponent<MemoryStoryPresenter>();

            // Saved already hidden — the state Awake puts it in anyway. Left visible, its black
            // backdrop covers the whole Game view in edit mode, and the obvious fix from a level
            // designer's chair is to switch the object off. That disables the presenter itself,
            // and every story then silently fails to play (Awake never runs, Instance stays null).
            root.GetComponent<Canvas>().enabled = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;

            // Opaque backdrop, so a letterboxed clip sits on black rather than on the level.
            var backdrop = NewUiObject("Backdrop", root.transform);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = Color.black;
            StretchToParent(backdrop.GetComponent<RectTransform>());

            var surfaceObject = NewUiObject("Surface", root.transform);
            var surface = surfaceObject.AddComponent<RawImage>();
            var fitter = surfaceObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            StretchToParent(surfaceObject.GetComponent<RectTransform>());

            var skipPrompt = BuildLabel(root.transform, "SkipPrompt", "Press Space to skip", 24f,
                TextAlignmentOptions.Right);
            var skipRect = skipPrompt.rectTransform;
            skipRect.anchorMin = new Vector2(1f, 0f);
            skipRect.anchorMax = new Vector2(1f, 0f);
            skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-40f, 40f);
            skipRect.sizeDelta = new Vector2(360f, 40f);
            skipPrompt.color = new Color(1f, 1f, 1f, 0.75f);
            skipPrompt.gameObject.SetActive(false);

            // The VideoPlayer lives on its own child, left inactive: a player on an inactive
            // object never prepares and never decodes, which is how the presenter guarantees no
            // decoder is alive during normal play.
            var playerObject = new GameObject("VideoPlayer");
            playerObject.transform.SetParent(root.transform, worldPositionStays: false);
            var player = playerObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.APIOnly;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.waitForFirstFrame = false;
            playerObject.SetActive(false);

            SetPrivateField(presenter, "m_Player", player);
            SetPrivateField(presenter, "m_Surface", surface);
            SetPrivateField(presenter, "m_SurfaceFitter", fitter);
            SetPrivateField(presenter, "m_SkipPrompt", skipPrompt.gameObject);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PresenterPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[MemoryShards] Built story presenter prefab → {PresenterPrefabPath}");
        }

        // ─── Small builders / helpers ─────────────────────────────────────────────

        private static GameObject NewCanvasRoot(string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform));

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            return root;
        }

        private static TMP_Text BuildLabel(Transform parent, string name, string text, float size,
            TextAlignmentOptions alignment)
        {
            var go = NewUiObject(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

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

        /// <summary>
        /// The shard art — the single still <c>MEMORY SHARD.png</c>. Returns an empty array with
        /// a warning when it is missing, rather than a null hole that would draw as nothing.
        /// </summary>
        private static Sprite[] LoadShardSprites()
        {
            string path = $"{ShardSpriteFolder}/{ShardSpriteFile}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return new[] { sprite };

            Debug.LogWarning($"[MemoryShards] No sprite at {path}. It is either missing or " +
                             "not imported as a Sprite (Texture Type ▸ Sprite (2D and UI)).");
            return new Sprite[0];
        }

        /// <summary>
        /// The frames of an animation folder, in numeric filename order (1.png, 2.png, …).
        ///
        /// Discovered rather than counted to a fixed length, so adding or removing a frame is
        /// just dropping a file in and re-running Build Prefabs. Sorted numerically on purpose —
        /// an alphabetical sort would order a ten-frame effect "1, 10, 2, 3", which plays wrong.
        /// </summary>
        internal static Sprite[] LoadFrameSprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[MemoryShards] No folder at {folder}; the effect will have no frames.");
                return new Sprite[0];
            }

            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(path => new
                {
                    path,
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    order = int.TryParse(Path.GetFileNameWithoutExtension(path), out int n) ? n : int.MaxValue,
                })
                .Where(f => f.sprite != null)
                .OrderBy(f => f.order)
                .ThenBy(f => f.path, System.StringComparer.Ordinal)
                .Select(f => f.sprite)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogWarning($"[MemoryShards] No sprites found in {folder}. The PNGs are most " +
                                 "likely not imported as sprites (Texture Type ▸ Sprite (2D and UI)).");
            }

            return frames;
        }

        private static List<LevelConfig> LoadLevelConfigs()
        {
            // Ordered by levelNumber, not filename, so the walk follows play order
            // (Tutorial1..4, then Level1..20) rather than "Level10" < "Level2".
            return AssetDatabase.FindAssets("t:LevelConfig", new[] { LevelConfigFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
                .Where(c => c != null)
                .OrderBy(c => c.levelNumber)
                .ToList();
        }

        /// <summary>
        /// Writes a [SerializeField] private field. The setup tool authors these components from
        /// scratch, so it needs to reach the same fields the inspector shows.
        /// </summary>
        private static void SetPrivateField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[MemoryShards] {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes a [SerializeField] private Sprite[] field.</summary>
        private static void SetPrivateSpriteArray(Object target, string field, Sprite[] value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[MemoryShards] {target.GetType().Name} has no array field '{field}'.");
                return;
            }

            property.arraySize = value.Length;
            for (int i = 0; i < value.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = value[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes a [SerializeField] private float field.</summary>
        private static void SetPrivateFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[MemoryShards] {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.floatValue = value;
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
