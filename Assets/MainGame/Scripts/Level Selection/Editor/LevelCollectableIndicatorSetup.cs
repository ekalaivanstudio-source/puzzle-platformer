using System.Collections.Generic;
using System.Linq;
using System.Text;
using Collectables;
using LevelSelection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LevelSelectionEditor
{
    /// <summary>
    /// <c>Tools ▸ Level Collectables</c> — everything needed to make the level selection map show
    /// what each level hides, and to put the level number inside the marker.
    ///
    /// Two jobs, both idempotent, so <c>Run Full Setup</c> is safe to re-run at any time:
    ///
    /// 1. <b>The database.</b> LevelConfig assets do not live in <c>Resources/</c>, so nothing
    ///    outside a level's own scene can reach them. The database is a list of references to
    ///    them that does, and it is rebuilt from the folder rather than maintained by hand.
    /// 2. <b>The node.</b> Builds the collectable icons and the level-number label into
    ///    <c>Level Node.prefab</c> and wires them to <see cref="LevelNodeUI"/>. Patched in place
    ///    rather than re-authored, so the nodes already sitting in HomeScreen keep their links.
    ///
    /// Everything the second job writes is a serialized field, so tuning is an inspector edit on
    /// the prefab — re-running step 2 is what resets it.
    /// </summary>
    public static class LevelCollectableIndicatorSetup
    {
        #region Constants

        internal const string LevelConfigFolder = "Assets/MainGame/ScriptableObjects/LevelConfigs";
        internal const string DatabasePath = "Assets/Resources/LevelConfigDatabase.asset";
        private const string NodePrefabPath = "Assets/MainGame/Prefabs/UI/Menu Level Creation/Level Node.prefab";

        /// <summary>
        /// The light PIXEL silhouette used in place of the part's own artwork. See
        /// <c>LevelNodeCollectables.RobotIconOverride</c> for why — the short version is that every
        /// part sprite is near-black and an Image tint can only darken.
        /// </summary>
        private const string PixelSilhouettePath =
            "Assets/MainGame/Sprites/UI buttons/Collect sprite/Pixel-Shilloute.png";

        /// <summary>The game's UI font, as used by the rest of the home screen.</summary>
        private const string FontPath = "Assets/MainGame/Font/pixel_noir/Pixel-Noir Caps SDF.asset";

        private const string IconsObjectName = "Collectables";
        private const string PartIconName = "PartIcon";
        private const string ShardIconName = "ShardIcon";
        private const string LevelNumberName = "LevelNumber";

        // ─── Icon layout ──────────────────────────────────────────────────────────
        // The icons sit on the path line, so they are sized against the level marker next to
        // them (a 50-unit circle inside a 100-unit node) rather than against any panel.

        private const float IconSize = 44f;
        private const float IconGap = 50f;

        /// <summary>Only a fallback: the generator overwrites this per node with the real segment midpoint.</summary>
        private static readonly Vector2 LineAnchorFallback = new Vector2(175f, 0f);

        private static readonly Color CollectedTint = Color.white;

        /// <summary>
        /// Mid-toned on purpose. An unlit icon has to read both against the dark map background
        /// and against the white or yellow route line it is sitting on, so it can be neither dark
        /// navy nor near-white.
        /// </summary>
        private static readonly Color UncollectedTint = new Color(0.38f, 0.45f, 0.56f, 1f);

        // ─── Level number ─────────────────────────────────────────────────────────

        /// <summary>Matches the marker circle: the node is 100 units and the circle inset by 50.</summary>
        private static readonly Vector2 LevelNumberInset = new Vector2(-58f, -58f);

        private const float LevelNumberMinSize = 14f;
        private const float LevelNumberMaxSize = 28f;

        /// <summary>Dark, because it sits on the marker's white face — and on the yellow one a completed level wears.</summary>
        private static readonly Color LevelNumberColor = new Color(0.05f, 0.11f, 0.22f, 1f);

        #endregion

        #region Entry Points

        [MenuItem("Tools/Level Collectables/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            RebuildDatabase();
            BuildNodeIndicator();
            Debug.Log("[LevelCollectables] Full setup done. Open the level selection screen to see " +
                      "the icons and the level numbers.");
        }

        /// <summary>
        /// Rewrites <c>Resources/LevelConfigDatabase.asset</c> from every LevelConfig in the
        /// configs folder, ordered by level number. Creates the asset when it is missing.
        /// </summary>
        [MenuItem("Tools/Level Collectables/1. Rebuild Level Config Database", priority = 20)]
        public static void RebuildDatabase()
        {
            List<LevelConfig> configs = LoadLevelConfigs();

            LevelConfigDatabase database = AssetDatabase.LoadAssetAtPath<LevelConfigDatabase>(DatabasePath);
            if (database == null)
            {
                EnsureFolder("Assets/Resources");
                database = ScriptableObject.CreateInstance<LevelConfigDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.SetConfigs(configs);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            LevelCollectableService.InvalidateDatabase();
            WarnAboutDuplicateLevelNumbers(configs);

            Debug.Log($"[LevelCollectables] Database rebuilt with {configs.Count} level configs " +
                      $"({DatabasePath}).");
        }

        /// <summary>
        /// Puts the collectable icons and the level-number label on the level node prefab, or
        /// refreshes the ones already there. Patches the prefab in place so the node instances in
        /// HomeScreen stay linked.
        /// </summary>
        [MenuItem("Tools/Level Collectables/2. Build Node Indicator", priority = 21)]
        public static void BuildNodeIndicator()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath) == null)
            {
                Debug.LogError($"[LevelCollectables] No level node prefab at {NodePrefabPath}.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(NodePrefabPath);
            try
            {
                var node = contents.GetComponent<LevelNodeUI>();
                if (node == null)
                {
                    Debug.LogError($"[LevelCollectables] {NodePrefabPath} has no LevelNodeUI, so it " +
                                   "is not the level node prefab this tool expects.");
                    return;
                }

                RectTransform iconRoot = BuildIconRoot(contents.transform);
                Image partIcon = BuildIcon(iconRoot, PartIconName);
                Image shardIcon = BuildIcon(iconRoot, ShardIconName);
                PruneStaleIcons(iconRoot);

                var indicator = contents.GetComponent<LevelNodeCollectables>();
                if (indicator == null) indicator = contents.AddComponent<LevelNodeCollectables>();
                WireIndicator(indicator, iconRoot, partIcon, shardIcon);

                TMP_Text label = BuildLevelNumber(contents.transform);

                SetPrivateField(node, "collectables", indicator);
                SetPrivateField(node, "levelNumberLabel", label);

                PrefabUtility.SaveAsPrefabAsset(contents, NodePrefabPath);
                Debug.Log($"[LevelCollectables] Icons and level number built on {NodePrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Prints the level → collectables table the map will draw, so an assignment can be
        /// checked without opening twenty assets.
        /// </summary>
        [MenuItem("Tools/Level Collectables/Log Level Collectables", priority = 40)]
        public static void LogLevelCollectables()
        {
            List<LevelConfig> configs = LoadLevelConfigs();
            var report = new StringBuilder("[LevelCollectables] Level → collectables\n");

            foreach (LevelConfig config in configs)
            {
                string part = config.robotPart != null && config.robotPart.placePart
                    ? config.robotPart.PartKey
                    : "—";
                string shard = config.memoryShard != null && config.memoryShard.placeShard
                    ? $"shard_{config.levelNumber}"
                    : "—";

                report.AppendLine($"  {config.levelNumber,3}  {config.name,-18}  part: {part,-10}  shard: {shard}");
            }

            int withAny = configs.Count(c =>
                (c.robotPart != null && c.robotPart.placePart) ||
                (c.memoryShard != null && c.memoryShard.placeShard));
            report.AppendLine($"  {withAny} of {configs.Count} levels show icons.");

            Debug.Log(report.ToString());
        }

        #endregion

        #region Prefab Construction

        /// <summary>
        /// Finds or creates the container the icons live in. It carries no graphic of its own —
        /// the icons are drawn bare on the route line, with no panel behind them — so any Image an
        /// older version of this tool left here is stripped.
        /// </summary>
        private static RectTransform BuildIconRoot(Transform nodeRoot)
        {
            Transform existing = nodeRoot.Find(IconsObjectName);
            GameObject rootObject;

            if (existing != null)
            {
                rootObject = existing.gameObject;

                // The first version of this drew a two-slot plate; that Image is now a blue
                // rectangle sitting on the map with nothing to do.
                var stalePlate = rootObject.GetComponent<Image>();
                if (stalePlate != null) Object.DestroyImmediate(stalePlate, true);

                // Unity adds the CanvasRenderer alongside the Image but does not take it away
                // with it, so the container keeps a renderer with nothing to render.
                var staleRenderer = rootObject.GetComponent<CanvasRenderer>();
                if (staleRenderer != null) Object.DestroyImmediate(staleRenderer, true);
            }
            else
            {
                rootObject = new GameObject(IconsObjectName, typeof(RectTransform));
                rootObject.transform.SetParent(nodeRoot, false);
            }

            var rect = rootObject.GetComponent<RectTransform>();
            if (rect == null) rect = rootObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = LineAnchorFallback;
            rect.localScale = Vector3.one;

            // Behind the node's own marker. They never overlap — the icons sit out on the line —
            // but if the layout is ever retuned, the level marker is the thing that must win.
            rootObject.transform.SetAsFirstSibling();

            // Off until a node binds itself to a level. Most of the map is drawn before anything
            // is known about it, and an Image with no sprite draws a plain white box.
            rootObject.SetActive(false);

            return rect;
        }

        /// <summary>
        /// Finds or creates one icon. Position, size and tint are rewritten every time the
        /// indicator repaints, so only what is not runtime-driven is set here.
        /// </summary>
        private static Image BuildIcon(RectTransform root, string iconName)
        {
            Transform existing = root.Find(iconName);
            GameObject iconObject;

            if (existing != null)
            {
                iconObject = existing.gameObject;
            }
            else
            {
                iconObject = new GameObject(iconName, typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(root, false);
            }

            var rect = iconObject.GetComponent<RectTransform>();
            if (rect == null) rect = iconObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.localScale = Vector3.one;

            var image = iconObject.GetComponent<Image>();
            if (image == null) image = iconObject.AddComponent<Image>();
            image.type = Image.Type.Simple;

            // The part and shard art are different shapes on different canvases; without this an
            // icon stretches them to a square.
            image.preserveAspect = true;

            // The icons hang outside the node's own rect, over the route line. Left as raycast
            // targets they would catch clicks meant for whatever they overlap.
            image.raycastTarget = false;

            iconObject.SetActive(false);

            return image;
        }

        /// <summary>
        /// Finds or creates the number drawn inside the level marker, sized to the marker circle
        /// and auto-shrinking so a two-digit level still fits.
        /// </summary>
        /// <summary>
        /// Removes anything else under the icon container. The first version of this tool named
        /// the icons after the plate's slots (PartSlot / ShardSlot); without this they survive as
        /// invisible leftovers that the next person has to work out the meaning of.
        /// </summary>
        private static void PruneStaleIcons(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == PartIconName || child.name == ShardIconName) continue;

                Object.DestroyImmediate(child.gameObject, true);
            }
        }

        private static TMP_Text BuildLevelNumber(Transform nodeRoot)
        {
            Transform existing = nodeRoot.Find(LevelNumberName);
            GameObject labelObject;

            if (existing != null)
            {
                labelObject = existing.gameObject;
            }
            else
            {
                labelObject = new GameObject(LevelNumberName, typeof(RectTransform));
                labelObject.transform.SetParent(nodeRoot, false);
            }

            var rect = labelObject.GetComponent<RectTransform>();
            if (rect == null) rect = labelObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = LevelNumberInset;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelObject.AddComponent<TextMeshProUGUI>();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            else Debug.LogWarning($"[LevelCollectables] No font at {FontPath}; the level number " +
                                  "falls back to the TMP default.");

            label.text = "1";
            label.color = LevelNumberColor;
            // Midline, not Center. Center squares up the *line box*, and this font's ascender is
            // tall enough that a digit then sits a third of the way out of the top of the marker.
            // Midline centres on the glyphs themselves.
            label.alignment = TextAlignmentOptions.Midline;
            label.enableAutoSizing = true;
            label.fontSizeMin = LevelNumberMinSize;
            label.fontSizeMax = LevelNumberMaxSize;
            label.raycastTarget = false;

            // Last, so the number draws over the marker face rather than under it.
            labelObject.transform.SetAsLastSibling();

            return label;
        }

        private static void WireIndicator(LevelNodeCollectables indicator, RectTransform root,
                                          Image partIcon, Image shardIcon)
        {
            var so = new SerializedObject(indicator);

            so.FindProperty("m_Root").objectReferenceValue = root;
            so.FindProperty("m_LineAnchor").vector2Value = LineAnchorFallback;
            so.FindProperty("m_IconSize").floatValue = IconSize;
            so.FindProperty("m_IconGap").floatValue = IconGap;
            so.FindProperty("m_RobotPartIcon").objectReferenceValue = partIcon;
            so.FindProperty("m_MemoryShardIcon").objectReferenceValue = shardIcon;

            // Written rather than left to the field defaults: a component carried over from an
            // earlier build keeps its serialized colours, and the old ones were tuned for a blue
            // plate that no longer exists.
            so.FindProperty("m_CollectedTint").colorValue = CollectedTint;
            so.FindProperty("m_UncollectedTint").colorValue = UncollectedTint;

            WriteRobotIcons(so.FindProperty("m_RobotIcons"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Seeds the per-robot stand-in icons. Only PIXEL ships, so only PIXEL has one; a robot
        /// with no entry falls back to its part artwork on its own.
        /// </summary>
        private static void WriteRobotIcons(SerializedProperty icons)
        {
            if (icons == null) return;

            Sprite silhouette = AssetDatabase.LoadAssetAtPath<Sprite>(PixelSilhouettePath);
            if (silhouette == null)
            {
                Debug.LogWarning($"[LevelCollectables] No sprite at {PixelSilhouettePath}, so the " +
                                 "part icon falls back to the part artwork, which is near-black " +
                                 "and will not read on the map.");
                icons.arraySize = 0;
                return;
            }

            icons.arraySize = 1;
            SerializedProperty entry = icons.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("robot").enumValueIndex = (int)RobotId.Pixel;
            entry.FindPropertyRelative("icon").objectReferenceValue = silhouette;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Every LevelConfig in the configs folder, ordered by level number rather than filename
        /// so the list reads in play order instead of "Level10" before "Level2".
        /// </summary>
        internal static List<LevelConfig> LoadLevelConfigs()
        {
            return AssetDatabase.FindAssets("t:LevelConfig", new[] { LevelConfigFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
                .Where(c => c != null)
                .OrderBy(c => c.levelNumber)
                .ToList();
        }

        /// <summary>
        /// Two configs claiming one level number means the map silently draws one of them and
        /// ignores the other, which is the kind of thing that is only ever found by accident.
        /// </summary>
        private static void WarnAboutDuplicateLevelNumbers(List<LevelConfig> configs)
        {
            foreach (var clash in configs.GroupBy(c => c.levelNumber).Where(g => g.Count() > 1))
            {
                Debug.LogWarning($"[LevelCollectables] Level {clash.Key} is claimed by " +
                                 $"{string.Join(", ", clash.Select(c => c.name))}. The map will use " +
                                 "the first and ignore the rest.");
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            int split = folder.LastIndexOf('/');
            string parent = folder.Substring(0, split);
            string leaf = folder.Substring(split + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Writes a [SerializeField] private field. This tool authors components the inspector
        /// owns, so it needs to reach the same fields the inspector shows.
        /// </summary>
        private static void SetPrivateField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[LevelCollectables] {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }

    /// <summary>
    /// Keeps the level config database's <i>membership</i> current by itself, so adding a level
    /// is creating its config and nothing else.
    ///
    /// Only the list of configs can go stale — the database stores references, so editing a
    /// level's collectables inside an existing config needs no rebuild. That is why this watches
    /// for configs appearing, disappearing and moving, and ignores plain edits.
    /// </summary>
    public class LevelConfigDatabaseWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                   string[] moved, string[] movedFrom)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!TouchesALevelConfig(imported) && !TouchesALevelConfig(deleted) &&
                !TouchesALevelConfig(moved) && !TouchesALevelConfig(movedFrom))
            {
                return;
            }

            // Deferred out of the import callback: the rebuild writes an asset, and writing one
            // while assets are still being imported is what leaves the database half-written.
            if (s_RebuildQueued) return;
            s_RebuildQueued = true;
            EditorApplication.delayCall += RebuildIfStale;
        }

        private static bool s_RebuildQueued;

        private static void RebuildIfStale()
        {
            s_RebuildQueued = false;

            // The rebuild writes the database asset, which comes back through here on the next
            // import. Membership is unchanged by then, so this settles after one pass.
            if (MembershipIsCurrent()) return;

            LevelCollectableIndicatorSetup.RebuildDatabase();
        }

        /// <summary>
        /// Matched on the path rather than by loading the asset, because a deleted config can no
        /// longer be loaded — and deletions are exactly the case that must not be missed.
        /// </summary>
        private static bool TouchesALevelConfig(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".asset") &&
                    path.StartsWith(LevelCollectableIndicatorSetup.LevelConfigFolder))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the database already lists exactly the configs on disk.</summary>
        private static bool MembershipIsCurrent()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelConfigDatabase>(
                LevelCollectableIndicatorSetup.DatabasePath);
            if (database == null) return false;

            List<LevelConfig> onDisk = LevelCollectableIndicatorSetup.LoadLevelConfigs();
            if (database.Count != onDisk.Count) return false;

            for (int i = 0; i < onDisk.Count; i++)
            {
                if (database.Configs[i] != onDisk[i]) return false;
            }
            return true;
        }
    }
}
