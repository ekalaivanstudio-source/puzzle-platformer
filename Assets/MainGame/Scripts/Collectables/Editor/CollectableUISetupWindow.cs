using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Builds a self‑contained Collectable HUD prefab (its own Screen‑Space canvas +
    /// two counters) and injects it — plus a <see cref="CollectableLevelManager"/> — into
    /// level scenes automatically.
    ///
    /// Level scenes here have no shared Canvas, so the HUD carries its own; the tool only
    /// needs to instantiate one prefab per scene.
    ///
    /// Open via Tools ▸ Collectables ▸ Setup Collectable UI.
    /// </summary>
    public class CollectableUISetupWindow : EditorWindow
    {
        private const string PrefabFolder = "Assets/MainGame/Prefabs/Collectables";
        private const string HudPrefabPath = PrefabFolder + "/CollectableHUD.prefab";
        private const string ConfigFolder = "Assets/MainGame/ScriptableObjects/LevelConfigs";

        [SerializeField] private Sprite _robotPartIcon;
        [SerializeField] private Sprite _memoryShardIcon;
        [SerializeField] private bool _onlyLevelScenes = true;
        [SerializeField] private bool _ensureManager = true;
        [SerializeField] private bool _createLevelConfig = true;

        [MenuItem("Tools/Collectables/Setup Collectable UI")]
        public static void Open()
        {
            var window = GetWindow<CollectableUISetupWindow>("Collectable UI");
            window.minSize = new Vector2(360, 360);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("HUD Prefab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds a self‑contained HUD (own Screen‑Space‑Overlay canvas) at\n" +
                HudPrefabPath + "\nRe‑running overwrites it with the icons below.",
                MessageType.None);

            _robotPartIcon = (Sprite)EditorGUILayout.ObjectField("Robot Part Icon", _robotPartIcon, typeof(Sprite), false);
            _memoryShardIcon = (Sprite)EditorGUILayout.ObjectField("Memory Shard Icon", _memoryShardIcon, typeof(Sprite), false);

            if (GUILayout.Button("Build / Update HUD Prefab", GUILayout.Height(28)))
                BuildHudPrefab();

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            EditorGUILayout.LabelField("Prefab status:", existing != null ? "Found" : "Not built yet", EditorStyles.miniLabel);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Scene Injection", EditorStyles.boldLabel);

            _ensureManager = EditorGUILayout.Toggle("Ensure Level Manager", _ensureManager);
            _createLevelConfig = EditorGUILayout.Toggle("Create + assign LevelConfig", _createLevelConfig);
            _onlyLevelScenes = EditorGUILayout.Toggle("Only 'Level*' Scenes", _onlyLevelScenes);

            EditorGUILayout.HelpBox(
                "Per scene: adds the HUD, a CollectableLevelManager, and (if enabled) a LevelContext " +
                "with a per-level LevelConfig. The config captures that scene's current camera dead-zone " +
                "and sequence values, so nothing is lost when those fields move into the asset.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Setup Current Scene", GUILayout.Height(26)))
                    SetupCurrentSceneInteractive();

                if (GUILayout.Button("Setup In All Build Scenes", GUILayout.Height(30)))
                    SetupAllScenes();
            }
        }

        // ─── HUD prefab construction ────────────────────────────────────────────────

        private GameObject BuildHudPrefab()
        {
            CollectableToolsPaths.EnsureFolder(PrefabFolder);

            // Root: self‑contained canvas.
            var root = new GameObject("CollectableHUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above gameplay UI; tweak as needed

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = root.AddComponent<CollectableHUD>();

            // Panel: top‑left vertical stack with a translucent background.
            var panel = NewUI("Panel", root.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
            panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(30, -30);

            var panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.35f);
            panelBg.raycastTarget = false;

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 18, 12, 12);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text robotText = BuildRow(panel.transform, "RobotPartsRow", _robotPartIcon, "0/56", out _);
            TMP_Text shardText = BuildRow(panel.transform, "MemoryShardsRow", _memoryShardIcon, "0/6", out GameObject shardRow);

            // Wire the CollectableHUD serialized fields.
            var so = new SerializedObject(hud);
            so.FindProperty("m_RobotPartsText").objectReferenceValue = robotText;
            so.FindProperty("m_MemoryShardsText").objectReferenceValue = shardText;
            so.FindProperty("m_MemoryShardsRoot").objectReferenceValue = shardRow;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (ok)
            {
                Debug.Log($"[Collectables] HUD prefab saved to {HudPrefabPath}", prefab);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                return prefab;
            }

            Debug.LogError("[Collectables] Failed to save HUD prefab.");
            return null;
        }

        private static TMP_Text BuildRow(Transform parent, string name, Sprite icon, string initialText, out GameObject rowGo)
        {
            var row = NewUI(name, parent);
            rowGo = row.gameObject;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Icon
            var iconRt = NewUI("Icon", row.transform);
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = icon != null;
            var iconLe = iconRt.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 48;
            iconLe.preferredHeight = 48;

            // Count text
            var textRt = NewUI("Count", row.transform);
            var tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = initialText;
            tmp.fontSize = 36;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;

            return tmp;
        }

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ─── Scene injection ──────────────────────────────────────────────────────────

        private GameObject GetOrBuildPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                Debug.Log("[Collectables] HUD prefab missing — building it now.");
                prefab = BuildHudPrefab();
            }
            return prefab;
        }

        private void SetupCurrentSceneInteractive()
        {
            var prefab = GetOrBuildPrefab();
            if (prefab == null) return;

            bool changed = SetupScene(SceneManager.GetActiveScene(), prefab);
            AssetDatabase.SaveAssets();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[Collectables] Current scene updated (remember to save).");
            }
            else
            {
                Debug.Log("[Collectables] Current scene set up (LevelConfig captured; no scene-object changes).");
            }
        }

        private void SetupAllScenes()
        {
            var prefab = GetOrBuildPrefab();
            if (prefab == null) return;

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                string fileName = Path.GetFileNameWithoutExtension(s.path);
                if (_onlyLevelScenes && !fileName.StartsWith("Level", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                scenes.Add(s.path);
            }

            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Collectable UI",
                    "No matching scenes found in Build Settings.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Setup Collectable UI",
                $"Add the HUD{(_ensureManager ? " + Level Manager" : "")} to {scenes.Count} scene(s) and save them?\n\n" +
                string.Join("\n", scenes),
                "Setup", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string originalScene = SceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            int changedCount = 0;

            try
            {
                for (int i = 0; i < scenes.Count; i++)
                {
                    string path = scenes[i];
                    EditorUtility.DisplayProgressBar("Collectable UI",
                        $"Processing {Path.GetFileName(path)}", (float)i / scenes.Count);

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = SetupScene(scene, prefab);
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        changedCount++;
                        report.AppendLine($"✔ {Path.GetFileName(path)} — updated");
                    }
                    else
                    {
                        report.AppendLine($"• {Path.GetFileName(path)} — already set up");
                    }
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(originalScene))
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            Debug.Log($"[Collectables] Setup complete. {changedCount}/{scenes.Count} scene(s) changed.\n{report}");
        }

        /// <summary>Ensures the HUD and (optionally) the manager exist in the given open scene.</summary>
        private bool SetupScene(Scene scene, GameObject hudPrefab)
        {
            bool changed = false;

            // HUD
            var existingHud = FindInScene<CollectableHUD>(scene);
            if (existingHud == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
                instance.name = "CollectableHUD";
                changed = true;
            }

            // Single "LevelManager" object holding CollectableLevelManager + LevelContext.
            if (_ensureManager)
            {
                int level = ResolveLevelNumber(scene);
                LevelConfig config = null;
                if (_createLevelConfig)
                {
                    config = GetOrCreateLevelConfig(level);
                    CaptureSceneIntoConfig(scene, config, level);
                }
                changed |= EnsureLevelManager(scene, config);
            }

            return changed;
        }

        /// <summary>
        /// Ensures one "LevelManager" object hosts both CollectableLevelManager and
        /// LevelContext, consolidating any separate objects left by an earlier setup, and
        /// assigns the level's config.
        /// </summary>
        private bool EnsureLevelManager(Scene scene, LevelConfig config)
        {
            bool changed = false;

            // Target object = the CollectableLevelManager's object, else a lone LevelContext's
            // object to reuse, else a fresh "LevelManager".
            var clm = FindInScene<CollectableLevelManager>(scene);
            GameObject target;
            if (clm != null)
            {
                target = clm.gameObject;
            }
            else
            {
                var existingCtx = FindInScene<LevelContext>(scene);
                target = existingCtx != null ? existingCtx.gameObject : NewManagerObject("LevelManager", scene);
                target.AddComponent<CollectableLevelManager>(); // RequireComponent adds LevelContext
                changed = true;
            }

            // Guarantee a LevelContext on the same object.
            var ctx = target.GetComponent<LevelContext>();
            if (ctx == null)
            {
                ctx = target.AddComponent<LevelContext>();
                changed = true;
            }

            // Effective config: the one we were given, else whatever is already assigned.
            LevelConfig finalConfig = config != null ? config : ReadConfig(ctx);

            // Remove stray LevelContexts left on other objects by the old two-object setup,
            // salvaging their config assignment if we don't have one yet.
            foreach (var other in Object.FindObjectsByType<LevelContext>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == ctx || other.gameObject.scene != scene) continue;
                if (finalConfig == null) finalConfig = ReadConfig(other);
                RemoveStrayContext(other);
                changed = true;
            }

            if (target.name != "LevelManager")
            {
                target.name = "LevelManager";
                changed = true;
            }

            // Assign the resolved config.
            if (finalConfig != null)
            {
                var so = new SerializedObject(ctx);
                var prop = so.FindProperty("config");
                if (prop.objectReferenceValue != finalConfig)
                {
                    prop.objectReferenceValue = finalConfig;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            return changed;
        }

        private static LevelConfig ReadConfig(LevelContext ctx)
            => new SerializedObject(ctx).FindProperty("config").objectReferenceValue as LevelConfig;

        /// <summary>Removes a leftover LevelContext — the whole object if it only held that.</summary>
        private static void RemoveStrayContext(LevelContext ctx)
        {
            var go = ctx.gameObject;
            bool loneObject = go.transform.childCount == 0 &&
                              go.GetComponents<Component>().Length <= 2; // Transform + LevelContext
            if (loneObject) Object.DestroyImmediate(go);
            else Object.DestroyImmediate(ctx);
        }

        private static GameObject NewManagerObject(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);

            var managers = GameObject.Find("Managers");
            if (managers != null && managers.scene == scene)
                go.transform.SetParent(managers.transform, false);

            return go;
        }

        // ─── Level config creation / migration ──────────────────────────────────────────

        /// <summary>Build index when the scene is in build settings, else trailing digits of its name.</summary>
        private static int ResolveLevelNumber(Scene scene)
        {
            if (scene.buildIndex >= 1) return scene.buildIndex;

            string digits = new string((scene.name ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int n) && n > 0 ? n : 0;
        }

        private static LevelConfig GetOrCreateLevelConfig(int level)
        {
            CollectableToolsPaths.EnsureFolder(ConfigFolder);
            string path = $"{ConfigFolder}/Level{level}Config.asset";

            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LevelConfig>();
                config.levelNumber = level;
                AssetDatabase.CreateAsset(config, path);
            }
            return config;
        }

        /// <summary>
        /// Copies the scene's current CameraFollowDeadZone and SequenceManager values into the
        /// config, so the (now hidden) component fields survive the move to the asset.
        /// </summary>
        private static void CaptureSceneIntoConfig(Scene scene, LevelConfig config, int level)
        {
            config.levelNumber = level;

            var cam = FindInScene<CameraFollowDeadZone>(scene);
            if (cam != null)
            {
                var so = new SerializedObject(cam);
                var c = config.cameraDeadZone;
                c.deadZoneX = so.FindProperty("deadZoneX").floatValue;
                c.deadZoneY = so.FindProperty("deadZoneY").floatValue;
                c.offset = so.FindProperty("offset").vector2Value;
                c.smoothTime = so.FindProperty("smoothTime").floatValue;
                c.minX = so.FindProperty("minX").floatValue;
                c.maxX = so.FindProperty("maxX").floatValue;
                c.minY = so.FindProperty("minY").floatValue;
                c.maxY = so.FindProperty("maxY").floatValue;
                c.followX = so.FindProperty("followX").boolValue;
                c.followY = so.FindProperty("followY").boolValue;
            }

            var seq = FindInScene<SequenceManager>(scene);
            if (seq != null)
            {
                var so = new SerializedObject(seq);
                config.sequence.maxSequenceLength = Mathf.Max(1, so.FindProperty("m_MaxSequenceLength").intValue);
                config.sequence.requireFullSequence = so.FindProperty("m_RequireFullSequence").boolValue;
            }

            EditorUtility.SetDirty(config);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in all)
                if (c.gameObject.scene == scene) return c;
            return null;
        }
    }
}
