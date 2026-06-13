#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LevelSelectionSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LevelSelectionSystem.EditorTools
{
    /// <summary>
    /// One-click scene setup for the Level Selection System.
    ///
    /// Because prefabs/scenes can't be hand-authored safely as text, this Editor tool builds
    /// everything procedurally and wires all the (private, [SerializeField]) references via
    /// SerializedObject — exactly what you'd otherwise do by hand in the Inspector.
    ///
    /// Run:  Tools ▸ Level Selection ▸ Setup Everything In Active Scene
    /// while the scene you want the level-select screen in is open. Steps are also exposed
    /// individually under the same menu if you want to re-run just one.
    ///
    /// What it creates (placeholder art = Unity built-in UI sprites; reskin freely):
    ///   • One LevelData asset per enabled scene in Build Settings + a LevelDatabase.
    ///   • A LevelButton prefab (background, thumbnail, number, lock, 3 stars, Button).
    ///   • A Canvas + Scroll View (Viewport + Content with GridLayoutGroup) + LevelSelectionUI.
    /// </summary>
    public static class LevelSelectionSetup
    {
        private const string DataFolder   = "Assets/MainGame/LevelSelectionData";
        private const string PrefabFolder = "Assets/MainGame/Prefabs";
        private const string DatabasePath = DataFolder + "/LevelDatabase.asset";
        private const string PrefabPath   = PrefabFolder + "/LevelButton.prefab";

        // ─── Menu entry points ────────────────────────────────────────────────────

        [MenuItem("Tools/Level Selection/Setup Everything In Active Scene", priority = 0)]
        public static void SetupEverything()
        {
            LevelDatabase db = GenerateData();
            GameObject prefab = CreateButtonPrefab();
            BuildScreen(db, prefab);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "Level Selection",
                "Setup complete.\n\n" +
                "• Data + Database: " + DataFolder + "\n" +
                "• Button prefab: " + PrefabPath + "\n" +
                "• Selection screen built in the active scene.\n\n" +
                "Press Play to test. Reskin by swapping sprites on the prefab. " +
                "Don't forget to SAVE the scene (Ctrl+S).",
                "Got it");
            Selection.activeObject = db;
        }

        [MenuItem("Tools/Level Selection/1. Generate Level Data + Database (from Build Settings)", priority = 20)]
        public static void GenerateDataMenu() { GenerateData(); AssetDatabase.SaveAssets(); }

        [MenuItem("Tools/Level Selection/2. Create Level Button Prefab", priority = 21)]
        public static void CreatePrefabMenu() { CreateButtonPrefab(); }

        [MenuItem("Tools/Level Selection/3. Build Selection Screen In Active Scene", priority = 22)]
        public static void BuildScreenMenu()
        {
            LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (db == null || prefab == null)
            {
                EditorUtility.DisplayDialog("Level Selection",
                    "Run steps 1 and 2 first (or use 'Setup Everything').", "OK");
                return;
            }
            BuildScreen(db, prefab);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ─── Step 1: data ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates one <see cref="LevelData"/> per enabled Build Settings scene and a
        /// <see cref="LevelDatabase"/> listing them in order. Re-running reuses existing assets.
        /// </summary>
        private static LevelDatabase GenerateData()
        {
            EnsureFolder(DataFolder);

            // Drive the level list from Build Settings so it matches the real, shippable scenes.
            var sceneNames = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                sceneNames.Add(Path.GetFileNameWithoutExtension(s.path));
            }
            if (sceneNames.Count == 0)
            {
                // Fallback so the tool still produces a usable demo if Build Settings is empty.
                for (int i = 1; i <= 12; i++) sceneNames.Add("Prototype_Level" + i);
                Debug.LogWarning("[LevelSelectionSetup] No scenes in Build Settings — generated 12 placeholder levels.");
            }

            LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            var levels = new List<LevelData>();
            for (int i = 0; i < sceneNames.Count; i++)
            {
                int id = i + 1;
                string path = DataFolder + "/Level_" + id + ".asset";
                LevelData data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(data, path);
                }

                var so = new SerializedObject(data);
                so.FindProperty("m_LevelId").intValue = id;
                so.FindProperty("m_LevelName").stringValue = "Level " + id;
                so.FindProperty("m_SceneName").stringValue = sceneNames[i];
                // Thumbnail left null — assign real art later.
                so.ApplyModifiedPropertiesWithoutUndo();
                levels.Add(data);
            }

            var dbSo = new SerializedObject(db);
            SerializedProperty list = dbSo.FindProperty("m_Levels");
            list.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            dbSo.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelSelectionSetup] Generated {levels.Count} LevelData assets + database at {DatabasePath}.");
            return db;
        }

        // ─── Step 2: prefab ───────────────────────────────────────────────────────────

        /// <summary>Builds the reusable level-button prefab and wires its LevelButtonUI references.</summary>
        private static GameObject CreateButtonPrefab()
        {
            EnsureFolder(PrefabFolder);

            // Build the hierarchy in-memory, save it as a prefab, then discard the scene copy.
            GameObject root = NewUI("LevelButton", null);
            var rootRT = (RectTransform)root.transform;
            rootRT.sizeDelta = new Vector2(160, 160);

            Image bg = root.AddComponent<Image>();
            bg.sprite = Builtin("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.20f, 0.27f, 0.50f); // bluish tile, like the reference

            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;

            var buttonUI = root.AddComponent<LevelButtonUI>();

            // Thumbnail (fills most of the tile).
            GameObject thumb = NewUI("Thumbnail", root.transform);
            Stretch((RectTransform)thumb.transform, 10, 10, 10, 30);
            Image thumbImg = thumb.AddComponent<Image>();
            thumbImg.color = new Color(1f, 1f, 1f, 0.15f);
            thumbImg.raycastTarget = false;

            // Level number text.
            GameObject numberGO = NewUI("LevelNumberText", root.transform);
            Stretch((RectTransform)numberGO.transform, 0, 0, 0, 0);
            var number = numberGO.AddComponent<TextMeshProUGUI>();
            number.text = "1";
            number.alignment = TextAlignmentOptions.Center;
            number.fontSize = 64;
            number.color = new Color(1f, 0.55f, 0.1f); // orange numerals
            number.raycastTarget = false;

            // Lock icon (hidden by default; the view toggles it).
            GameObject lockGO = NewUI("LockIcon", root.transform);
            var lockRT = (RectTransform)lockGO.transform;
            lockRT.anchorMin = lockRT.anchorMax = new Vector2(0.5f, 0.5f);
            lockRT.sizeDelta = new Vector2(64, 64);
            lockRT.anchoredPosition = Vector2.zero;
            Image lockImg = lockGO.AddComponent<Image>();
            lockImg.sprite = Builtin("UI/Skin/Knob.psd");
            lockImg.raycastTarget = false;
            lockGO.SetActive(false);

            // Star container (Horizontal Layout) with 3 stars.
            GameObject starContainer = NewUI("StarContainer", root.transform);
            var starRT = (RectTransform)starContainer.transform;
            starRT.anchorMin = new Vector2(0.5f, 0f);
            starRT.anchorMax = new Vector2(0.5f, 0f);
            starRT.pivot = new Vector2(0.5f, 0f);
            starRT.anchoredPosition = new Vector2(0, 6);
            starRT.sizeDelta = new Vector2(150, 36);
            var hlg = starContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            Sprite starFilled = Builtin("UI/Skin/Knob.psd");
            Sprite starEmpty  = Builtin("UI/Skin/UISprite.psd");
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject star = NewUI("Star" + i, starContainer.transform);
                var le = star.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 30;
                Image si = star.AddComponent<Image>();
                si.sprite = starEmpty;
                si.raycastTarget = false;
                stars[i] = si;
            }

            // Wire LevelButtonUI's private serialized fields.
            var ui = new SerializedObject(buttonUI);
            SetRef(ui, "m_Button", button);
            SetRef(ui, "m_Background", bg);
            SetRef(ui, "m_Thumbnail", thumbImg);
            SetRef(ui, "m_LevelNumberText", number);
            SetRef(ui, "m_LockIcon", lockGO);
            SetRef(ui, "m_StarContainer", starContainer);
            SetRef(ui, "m_StarFilled", starFilled);
            SetRef(ui, "m_StarEmpty", starEmpty);
            ui.FindProperty("m_HighlightColor").colorValue = new Color(1f, 0.7f, 0.2f);
            SerializedProperty starArr = ui.FindProperty("m_StarImages");
            starArr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                starArr.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            ui.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[LevelSelectionSetup] Created button prefab at {PrefabPath}.");
            return prefab;
        }

        // ─── Step 3: screen ─────────────────────────────────────────────────────────

        /// <summary>Builds Canvas + Scroll View + grid + LevelSelectionUI in the active scene and wires it.</summary>
        private static void BuildScreen(LevelDatabase db, GameObject buttonPrefab)
        {
            Canvas canvas = EnsureCanvasAndEventSystem();

            // Remove a previous run's screen so re-running is idempotent.
            Transform old = canvas.transform.Find("LevelSelectScreen");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject screen = NewUI("LevelSelectScreen", canvas.transform);
            Stretch((RectTransform)screen.transform, 0, 0, 0, 0);
            var selectionUI = screen.AddComponent<LevelSelectionUI>();

            // ScrollRect.
            GameObject scrollGO = NewUI("Scroll View", screen.transform);
            Stretch((RectTransform)scrollGO.transform, 40, 40, 120, 40);
            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.15f);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Viewport (masked).
            GameObject viewport = NewUI("Viewport", scrollGO.transform);
            Stretch((RectTransform)viewport.transform, 0, 0, 0, 0);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = (RectTransform)viewport.transform;

            // Content (grid, grows downward).
            GameObject content = NewUI("Content", viewport.transform);
            var contentRT = (RectTransform)content.transform;
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;
            scrollRect.content = contentRT;

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.cellSize = new Vector2(160, 160);
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Optional title.
            GameObject titleGO = NewUI("Title", screen.transform);
            var titleRT = (RectTransform)titleGO.transform;
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -20);
            titleRT.sizeDelta = new Vector2(0, 80);
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "SELECT LEVEL";
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 56;
            title.color = new Color(1f, 0.55f, 0.1f);

            // Wire LevelSelectionUI private serialized fields.
            var so = new SerializedObject(selectionUI);
            SetRef(so, "m_Database", db);
            SetRef(so, "m_LevelButtonPrefab", buttonPrefab.GetComponent<LevelButtonUI>());
            SetRef(so, "m_Content", contentRT);
            SetRef(so, "m_ScrollRect", scrollRect);
            so.FindProperty("m_Columns").intValue = 5;
            so.FindProperty("m_CellSize").vector2Value = new Vector2(160, 160);
            so.FindProperty("m_Spacing").vector2Value = new Vector2(20, 20);
            // RectOffset is set via its serialized sub-properties (Reset isn't guaranteed on scripted AddComponent).
            SerializedProperty pad = so.FindProperty("m_Padding");
            pad.FindPropertyRelative("m_Left").intValue = 20;
            pad.FindPropertyRelative("m_Right").intValue = 20;
            pad.FindPropertyRelative("m_Top").intValue = 20;
            pad.FindPropertyRelative("m_Bottom").intValue = 20;
            so.FindProperty("m_HighlightLatestLevel").boolValue = true;
            so.FindProperty("m_ScrollToLatestLevel").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = screen;
            Debug.Log("[LevelSelectionSetup] Built selection screen in the active scene.");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static Canvas EnsureCanvasAndEventSystem()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem));
                // This project uses the new Input System, so add its UI module (not StandardInputModule).
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            return canvas;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Stretches a RectTransform to fill its parent with the given per-side insets.</summary>
        private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void SetRef(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[LevelSelectionSetup] Missing property '{property}'."); return; }
            p.objectReferenceValue = value;
        }

        private static Sprite Builtin(string path) =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
