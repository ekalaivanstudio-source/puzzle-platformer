#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HomeUI.EditorTools
{
    /// <summary>
    /// One-click scene builder for the entire HomeUI system. Constructs the Canvas, ScreenManager
    /// with all panels (Home / Level Selection / Settings / Collections), the reusable confirmation
    /// popup, the settings managers, sample Collection data, the RoboTab/PartSlot prefabs, and wires
    /// every (private, [SerializeField]) reference via SerializedObject.
    ///
    /// Functional UI controls (TMP dropdowns, sliders, toggles) are created through Unity's own
    /// public TMP_DefaultControls / DefaultControls factories — the same code the GameObject ▸ UI
    /// menu uses — so they are real, working widgets, not stubs.
    ///
    /// Run: Tools ▸ Home UI ▸ Setup Everything In Active Scene  (with your menu scene open).
    /// </summary>
    public static class HomeUISetup
    {
        private const string DataFolder = "Assets/MainGame/HomeUIData";
        private const string CollFolder = DataFolder + "/Collections";
        private const string PrefabFolder = "Assets/MainGame/Prefabs";
        private const string SettingsDefaultsPath = DataFolder + "/SettingsDefaults.asset";
        private const string CollectionDbPath = CollFolder + "/CollectionDatabase.asset";
        private const string RoboTabPrefabPath = PrefabFolder + "/RoboTab.prefab";
        private const string PartSlotPrefabPath = PrefabFolder + "/PartSlot.prefab";
        private const string LevelDbPath = "Assets/MainGame/LevelSelectionData/LevelDatabase.asset";
        private const string LevelButtonPrefabPath = PrefabFolder + "/LevelButton.prefab";

        private static readonly string[] PartIds = { "head", "body", "left_arm", "right_arm", "left_leg", "right_leg" };
        private static readonly string[] PartNames = { "Head", "Body", "Left Arm", "Right Arm", "Left Leg", "Right Leg" };

        [MenuItem("Tools/Home UI/Setup Everything In Active Scene", priority = 0)]
        public static void SetupEverything()
        {
            EnsureFolder(CollFolder);
            EnsureFolder(PrefabFolder);

            SettingsDefaults defaults = GenerateSettingsDefaults();
            CollectionDatabase collDb = GenerateCollectionData();
            GameObject roboTabPrefab = BuildRoboTabPrefab();
            GameObject partSlotPrefab = BuildPartSlotPrefab();

            Canvas canvas = EnsureCanvasAndEventSystem();
            ClearPrevious(canvas);

            // Managers (persist across scenes).
            var (settingsMgr, graphicsMgr, inputMgr) = BuildManagers(defaults);

            // ScreenManager + the four main panels.
            GameObject screenRoot = NewUI("ScreenManager", canvas.transform);
            Stretch((RectTransform)screenRoot.transform, 0, 0, 0, 0);
            var screen = screenRoot.AddComponent<ScreenManager>();

            ConfirmationPopup popup = BuildConfirmationPopup(canvas.transform);

            UIPanel home = BuildHomePanel(screenRoot.transform, screen, popup);
            UIPanel levelSel = BuildLevelSelectionPanel(screenRoot.transform);
            UIPanel settings = BuildSettingsPanel(screenRoot.transform, settingsMgr, graphicsMgr, popup);
            UIPanel collections = BuildCollectionsPanel(screenRoot.transform, collDb, roboTabPrefab, partSlotPrefab);

            // Wire ScreenManager.
            var so = SO(screen);
            Arr(so, "m_Panels", new Object[] { home, levelSel, settings, collections });
            Str(so, "m_InitialPanelId", "Home");
            Apply(so);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Home UI",
                "Setup complete.\n\n" +
                "• Settings defaults + Collection data: " + DataFolder + "\n" +
                "• RoboTab / PartSlot prefabs: " + PrefabFolder + "\n" +
                "• Canvas with Home / Level Select / Settings / Collections panels, popup and managers.\n\n" +
                "Assign your Input Actions asset on the InputManager if needed, reskin freely, " +
                "press Play, then SAVE the scene (Ctrl+S).",
                "Got it");
            Selection.activeGameObject = screenRoot;
        }

        // ─── Data generation ────────────────────────────────────────────────────

        private static SettingsDefaults GenerateSettingsDefaults()
        {
            var d = AssetDatabase.LoadAssetAtPath<SettingsDefaults>(SettingsDefaultsPath);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<SettingsDefaults>();
                AssetDatabase.CreateAsset(d, SettingsDefaultsPath);
            }
            return d;
        }

        private static CollectionDatabase GenerateCollectionData()
        {
            // Six shared part definitions (progress is keyed by roboId + partId).
            var parts = new RobotPartData[PartIds.Length];
            Sprite collected = Builtin("UI/Skin/Knob.psd");
            Sprite silhouette = Builtin("UI/Skin/Background.psd");
            for (int i = 0; i < PartIds.Length; i++)
            {
                string path = CollFolder + "/Part_" + PartIds[i] + ".asset";
                var p = AssetDatabase.LoadAssetAtPath<RobotPartData>(path);
                if (p == null) { p = ScriptableObject.CreateInstance<RobotPartData>(); AssetDatabase.CreateAsset(p, path); }
                var so = SO(p);
                Str(so, "m_PartId", PartIds[i]);
                Str(so, "m_PartName", PartNames[i]);
                Ref(so, "m_CollectedSprite", collected);
                Ref(so, "m_SilhouetteSprite", silhouette);
                Apply(so);
                parts[i] = p;
            }

            // Four Robos, each containing all six parts.
            var robos = new RoboData[4];
            for (int r = 0; r < 4; r++)
            {
                string id = "robo" + (r + 1);
                string path = CollFolder + "/Robo_" + (r + 1) + ".asset";
                var robo = AssetDatabase.LoadAssetAtPath<RoboData>(path);
                if (robo == null) { robo = ScriptableObject.CreateInstance<RoboData>(); AssetDatabase.CreateAsset(robo, path); }
                var so = SO(robo);
                Str(so, "m_RoboId", id);
                Str(so, "m_RoboName", "Robo " + (r + 1));
                Ref(so, "m_Icon", Builtin("UI/Skin/Knob.psd"));
                Arr(so, "m_Parts", parts);
                Apply(so);
                robos[r] = robo;
            }

            var db = AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<CollectionDatabase>(); AssetDatabase.CreateAsset(db, CollectionDbPath); }
            var dbSo = SO(db);
            Arr(dbSo, "m_Robos", robos);
            Apply(dbSo);

            AssetDatabase.SaveAssets();
            return db;
        }

        // ─── Prefabs ────────────────────────────────────────────────────────────

        private static GameObject BuildRoboTabPrefab()
        {
            GameObject root = NewUI("RoboTab", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(160, 80);
            Image bg = root.AddComponent<Image>();
            bg.sprite = Builtin("UI/Skin/UISprite.psd"); bg.type = Image.Type.Sliced;
            bg.color = new Color(0.85f, 0.6f, 0.2f);
            Button button = root.AddComponent<Button>(); button.targetGraphic = bg;
            var tab = root.AddComponent<RoboTabUI>();

            TextMeshProUGUI name = TMPText("Name", root.transform, "Robo 1", 26, Color.white);
            AnchorRect((RectTransform)name.transform, 0, 1, 1, 1, new Vector2(0, -6), new Vector2(0, 34));
            TextMeshProUGUI prog = TMPText("Progress", root.transform, "0/6", 20, new Color(1, 1, 1, 0.85f));
            AnchorRect((RectTransform)prog.transform, 0, 0, 1, 0, new Vector2(0, 6), new Vector2(0, 28));

            GameObject icon = NewUI("Icon", root.transform);
            var iconRT = (RectTransform)icon.transform; iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.12f, 0.5f);
            iconRT.sizeDelta = new Vector2(40, 40); iconRT.anchoredPosition = Vector2.zero;
            Image iconImg = icon.AddComponent<Image>(); iconImg.raycastTarget = false;

            GameObject lockGO = NewUI("LockIcon", root.transform);
            var lockRT = (RectTransform)lockGO.transform; lockRT.anchorMin = lockRT.anchorMax = new Vector2(0.5f, 0.5f);
            lockRT.sizeDelta = new Vector2(40, 40); lockRT.anchoredPosition = Vector2.zero;
            Image lockImg = lockGO.AddComponent<Image>(); lockImg.sprite = Builtin("UI/Skin/Knob.psd"); lockImg.raycastTarget = false;
            lockGO.SetActive(false);

            GameObject hl = NewUI("SelectedHighlight", root.transform);
            Stretch((RectTransform)hl.transform, -4, -4, -4, -4);
            Image hlImg = hl.AddComponent<Image>(); hlImg.color = new Color(1f, 1f, 1f, 0.35f); hlImg.raycastTarget = false;
            hl.SetActive(false);

            var so = SO(tab);
            Ref(so, "m_Button", button); Ref(so, "m_NameText", name); Ref(so, "m_ProgressText", prog);
            Ref(so, "m_IconImage", iconImg); Ref(so, "m_LockIcon", lockGO); Ref(so, "m_SelectedHighlight", hl);
            Apply(so);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RoboTabPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildPartSlotPrefab()
        {
            GameObject root = NewUI("PartSlot", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(150, 150);
            Image bg = root.AddComponent<Image>();
            bg.sprite = Builtin("UI/Skin/UISprite.psd"); bg.type = Image.Type.Sliced;
            bg.color = new Color(0.18f, 0.22f, 0.3f);
            var slot = root.AddComponent<PartSlotUI>();

            GameObject part = NewUI("PartImage", root.transform);
            Stretch((RectTransform)part.transform, 12, 12, 12, 30);
            Image partImg = part.AddComponent<Image>(); partImg.raycastTarget = false;

            GameObject lockOverlay = NewUI("LockedOverlay", root.transform);
            Stretch((RectTransform)lockOverlay.transform, 0, 0, 0, 0);
            Image lockImg = lockOverlay.AddComponent<Image>(); lockImg.color = new Color(0, 0, 0, 0.45f); lockImg.raycastTarget = false;

            TextMeshProUGUI label = TMPText("PartName", root.transform, "Part", 18, Color.white);
            AnchorRect((RectTransform)label.transform, 0, 0, 1, 0, new Vector2(0, 4), new Vector2(0, 26));

            var so = SO(slot);
            Ref(so, "m_PartImage", partImg); Ref(so, "m_LockedOverlay", lockOverlay); Ref(so, "m_PartNameText", label);
            Apply(so);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PartSlotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ─── Managers ───────────────────────────────────────────────────────────

        private static (SettingsManager, GraphicsManager, InputManager) BuildManagers(SettingsDefaults defaults)
        {
            GameObject go = GameObject.Find("HomeUI_Managers");
            if (go != null) Object.DestroyImmediate(go);
            go = new GameObject("HomeUI_Managers");

            var settings = go.AddComponent<SettingsManager>();
            var graphics = go.AddComponent<GraphicsManager>();
            go.AddComponent<AudioSettingsManager>();
            var input = go.AddComponent<InputManager>();

            // Wire the project's input actions asset if one exists (prefer PlayerInputAction).
            InputActionAsset actions = FindInputActions();
            if (actions != null) { var iso = SO(input); Ref(iso, "m_Actions", actions); Apply(iso); }

            var so = SO(settings);
            Ref(so, "m_Defaults", defaults);
            Ref(so, "m_Graphics", graphics);
            Ref(so, "m_Input", input);
            Apply(so); // m_ModuleBehaviours left empty → auto-discovers the three ISettingsModule siblings
            return (settings, graphics, input);
        }

        // ─── Panels ─────────────────────────────────────────────────────────────

        private static UIPanel BuildHomePanel(Transform parent, ScreenManager screen, ConfirmationPopup popup)
        {
            GameObject panelGO = MakePanel("HomeScreenPanel", parent, "Home", out UIPanel panel);
            var ctrl = panelGO.AddComponent<HomeScreenController>();

            TMPText("Title", panelGO.transform, "RERERE", 72, new Color(1f, 0.7f, 0.1f))
                .rectTransform.anchoredPosition = new Vector2(0, 280);

            var (settingsGO, settingsBtn, _) = MakeButton("SettingsButton", panelGO.transform, "Settings", new Color(0.9f, 0.7f, 0.2f));
            Place((RectTransform)settingsGO.transform, new Vector2(-700, 120), new Vector2(200, 90));
            var (collGO, collBtn, _) = MakeButton("CollectionsButton", panelGO.transform, "Collections", new Color(0.9f, 0.7f, 0.2f));
            Place((RectTransform)collGO.transform, new Vector2(-700, 0), new Vector2(200, 90));
            var (playGO, playBtn, _) = MakeButton("PlayButton", panelGO.transform, "Play", new Color(0.3f, 0.75f, 0.3f));
            Place((RectTransform)playGO.transform, new Vector2(-130, -180), new Vector2(360, 120));
            var (quitGO, quitBtn, _) = MakeButton("QuitButton", panelGO.transform, "Quit", new Color(0.8f, 0.25f, 0.25f));
            Place((RectTransform)quitGO.transform, new Vector2(320, -180), new Vector2(360, 120));

            var so = SO(ctrl);
            Ref(so, "m_PlayButton", playBtn); Ref(so, "m_CollectionsButton", collBtn);
            Ref(so, "m_SettingsButton", settingsBtn); Ref(so, "m_QuitButton", quitBtn);
            Ref(so, "m_ScreenManager", screen); Ref(so, "m_ConfirmationPopup", popup);
            Str(so, "m_LevelSelectionPanelId", "LevelSelection");
            Str(so, "m_CollectionsPanelId", "Collections");
            Str(so, "m_SettingsPanelId", "Settings");
            Apply(so);

            SetFirstSelected(panel, playGO);
            return panel;
        }

        private static UIPanel BuildLevelSelectionPanel(Transform parent)
        {
            GameObject panelGO = MakePanel("LevelSelectionPanel", parent, "LevelSelection", out UIPanel panel);
            var wrapper = panelGO.AddComponent<LevelSelectionPanel>();

            var (backGO, backBtn, _) = MakeButton("BackButton", panelGO.transform, "Back", new Color(0.8f, 0.25f, 0.25f));
            Place((RectTransform)backGO.transform, new Vector2(-820, 440), new Vector2(150, 70));

            // Ensure the Level Selection assets exist (reuse that system's editor tool), then host its grid.
            EnsureLevelAssets();
            LevelSelectionSystem.LevelSelectionUI levelUI = BuildLevelGrid(panelGO.transform);

            var so = SO(wrapper);
            Ref(so, "m_BackButton", backBtn);
            Apply(so);
            return panel;
        }

        private static UIPanel BuildCollectionsPanel(Transform parent, CollectionDatabase db,
                                                     GameObject roboTabPrefab, GameObject partSlotPrefab)
        {
            GameObject panelGO = MakePanel("CollectionsPanel", parent, "Collections", out UIPanel panel);
            var ui = panelGO.AddComponent<CollectionsPanelUI>();

            var (backGO, backBtn, _) = MakeButton("BackButton", panelGO.transform, "Back", new Color(0.8f, 0.25f, 0.25f));
            Place((RectTransform)backGO.transform, new Vector2(-820, 440), new Vector2(150, 70));

            TextMeshProUGUI title = TMPText("RoboTitle", panelGO.transform, "Robo 1", 44, new Color(1f, 0.7f, 0.1f));
            title.rectTransform.anchoredPosition = new Vector2(0, 400);
            TextMeshProUGUI completion = TMPText("Completion", panelGO.transform, "0%  (0/6)", 28, Color.white);
            completion.rectTransform.anchoredPosition = new Vector2(0, 350);

            // Tabs row (HorizontalLayoutGroup).
            GameObject tabs = NewUI("TabContainer", panelGO.transform);
            var tabsRT = (RectTransform)tabs.transform;
            tabsRT.anchorMin = tabsRT.anchorMax = new Vector2(0.5f, 0.5f);
            tabsRT.sizeDelta = new Vector2(760, 90); tabsRT.anchoredPosition = new Vector2(0, 250);
            var thlg = tabs.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 16; thlg.childAlignment = TextAnchor.MiddleCenter;
            thlg.childControlWidth = thlg.childControlHeight = true;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;

            // Parts grid.
            GameObject grid = NewUI("PartContainer", panelGO.transform);
            var gridRT = (RectTransform)grid.transform;
            gridRT.anchorMin = gridRT.anchorMax = new Vector2(0.5f, 0.5f);
            gridRT.sizeDelta = new Vector2(720, 360); gridRT.anchoredPosition = new Vector2(0, -40);
            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(150, 150); glg.spacing = new Vector2(20, 20);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 3;
            glg.childAlignment = TextAnchor.MiddleCenter;

            // Completion bar.
            GameObject barBg = NewUI("CompletionBar", panelGO.transform);
            var barRT = (RectTransform)barBg.transform;
            barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0.5f);
            barRT.sizeDelta = new Vector2(600, 24); barRT.anchoredPosition = new Vector2(0, -280);
            barBg.AddComponent<Image>().color = new Color(0, 0, 0, 0.4f);
            GameObject barFill = NewUI("Fill", barBg.transform);
            Stretch((RectTransform)barFill.transform, 0, 0, 0, 0);
            Image fill = barFill.AddComponent<Image>();
            fill.sprite = Builtin("UI/Skin/UISprite.psd"); fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal; fill.color = new Color(0.3f, 0.8f, 0.4f); fill.fillAmount = 0f;

            var so = SO(ui);
            Ref(so, "m_Database", db);
            Ref(so, "m_TabPrefab", roboTabPrefab.GetComponent<RoboTabUI>());
            Ref(so, "m_TabContainer", tabs.transform);
            Ref(so, "m_PartSlotPrefab", partSlotPrefab.GetComponent<PartSlotUI>());
            Ref(so, "m_PartContainer", grid.transform);
            Ref(so, "m_RoboTitleText", title); Ref(so, "m_CompletionText", completion);
            Ref(so, "m_CompletionBarFill", fill); Ref(so, "m_BackButton", backBtn);
            Apply(so);
            return panel;
        }

        // ─── Settings panel (the big one) ─────────────────────────────────────────

        private static UIPanel BuildSettingsPanel(Transform parent, SettingsManager settingsMgr,
                                                  GraphicsManager graphicsMgr, ConfirmationPopup popup)
        {
            GameObject panelGO = MakePanel("SettingsPanel", parent, "Settings", out UIPanel panel);
            var ui = panelGO.AddComponent<SettingsPanelUI>();

            var (backGO, backBtn, _) = MakeButton("BackButton", panelGO.transform, "Back", new Color(0.8f, 0.25f, 0.25f));
            Place((RectTransform)backGO.transform, new Vector2(-820, 440), new Vector2(150, 70));
            var (resetGO, resetBtn, _) = MakeButton("ResetAllButton", panelGO.transform, "Reset", new Color(0.6f, 0.6f, 0.6f));
            Place((RectTransform)resetGO.transform, new Vector2(820, 440), new Vector2(150, 70));

            // Tab buttons.
            var (gTabGO, gTab, _) = MakeButton("Tab_Graphics", panelGO.transform, "Graphics", new Color(0.3f, 0.4f, 0.6f));
            Place((RectTransform)gTabGO.transform, new Vector2(-280, 360), new Vector2(220, 70));
            var (aTabGO, aTab, _) = MakeButton("Tab_Audio", panelGO.transform, "Audio", new Color(0.3f, 0.4f, 0.6f));
            Place((RectTransform)aTabGO.transform, new Vector2(0, 360), new Vector2(220, 70));
            var (cTabGO, cTab, _) = MakeButton("Tab_Controls", panelGO.transform, "Controls", new Color(0.3f, 0.4f, 0.6f));
            Place((RectTransform)cTabGO.transform, new Vector2(280, 360), new Vector2(220, 70));

            GraphicsSettingsUI gfxUI = BuildGraphicsSection(panelGO.transform, settingsMgr, out GameObject gfxSection);
            AudioSettingsUI audUI = BuildAudioSection(panelGO.transform, settingsMgr, out GameObject audSection);
            ControlsSettingsUI ctrlUI = BuildControlsSection(panelGO.transform, settingsMgr, popup, out GameObject ctrlSection);

            var so = SO(ui);
            Arr(so, "m_Sections", new Object[] { gfxSection, audSection, ctrlSection });
            Arr(so, "m_TabButtons", new Object[] { gTab, aTab, cTab });
            Ref(so, "m_Graphics", gfxUI); Ref(so, "m_Audio", audUI); Ref(so, "m_Controls", ctrlUI);
            Ref(so, "m_BackButton", backBtn); Ref(so, "m_ResetAllButton", resetBtn);
            Ref(so, "m_Settings", settingsMgr); Ref(so, "m_ConfirmationPopup", popup);
            Apply(so);

            SetFirstSelected(panel, backGO);
            return panel;
        }

        private static GameObject MakeSection(Transform parent, string name)
        {
            GameObject section = NewUI(name, parent);
            var rt = (RectTransform)section.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900, 560); rt.anchoredPosition = new Vector2(0, -40);
            var vlg = section.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14; vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            return section;
        }

        private static GraphicsSettingsUI BuildGraphicsSection(Transform parent, SettingsManager mgr, out GameObject section)
        {
            section = MakeSection(parent, "GraphicsSection");
            var ui = section.AddComponent<GraphicsSettingsUI>();

            TMP_Dropdown res = AddDropdownRow(section.transform, "Resolution");
            TMP_Dropdown disp = AddDropdownRow(section.transform, "Display Mode");
            Toggle vsync = AddToggleRow(section.transform, "VSync");
            TMP_Dropdown fps = AddDropdownRow(section.transform, "FPS Limit");
            TMP_Dropdown quality = AddDropdownRow(section.transform, "Quality Preset");
            Slider gamma = AddSliderRow(section.transform, "Brightness", 0.5f, 2f);
            TMP_Dropdown tex = AddDropdownRow(section.transform, "Texture Quality");
            TMP_Dropdown aa = AddDropdownRow(section.transform, "Anti-Aliasing");

            var so = SO(ui);
            Ref(so, "m_Settings", mgr);
            Ref(so, "m_ResolutionDropdown", res); Ref(so, "m_DisplayModeDropdown", disp);
            Ref(so, "m_VSyncToggle", vsync); Ref(so, "m_FpsDropdown", fps);
            Ref(so, "m_QualityDropdown", quality); Ref(so, "m_GammaSlider", gamma);
            Ref(so, "m_TextureDropdown", tex); Ref(so, "m_AntiAliasingDropdown", aa);
            Apply(so);
            return ui;
        }

        private static AudioSettingsUI BuildAudioSection(Transform parent, SettingsManager mgr, out GameObject section)
        {
            section = MakeSection(parent, "AudioSection");
            section.SetActive(false);
            var ui = section.AddComponent<AudioSettingsUI>();

            Slider master = AddSliderRow(section.transform, "Master Volume", 0f, 1f);
            Slider music = AddSliderRow(section.transform, "Music Volume", 0f, 1f);
            Slider sfx = AddSliderRow(section.transform, "SFX Volume", 0f, 1f);
            Slider uiVol = AddSliderRow(section.transform, "UI Volume", 0f, 1f);
            Toggle mute = AddToggleRow(section.transform, "Mute All");

            var so = SO(ui);
            Ref(so, "m_Settings", mgr);
            Ref(so, "m_MasterSlider", master); Ref(so, "m_MusicSlider", music);
            Ref(so, "m_SfxSlider", sfx); Ref(so, "m_UiSlider", uiVol); Ref(so, "m_MuteAllToggle", mute);
            Apply(so);
            return ui;
        }

        private static ControlsSettingsUI BuildControlsSection(Transform parent, SettingsManager mgr,
                                                               ConfirmationPopup popup, out GameObject section)
        {
            section = MakeSection(parent, "ControlsSection");
            section.SetActive(false);
            var ui = section.AddComponent<ControlsSettingsUI>();

            Slider sens = AddSliderRow(section.transform, "Mouse Sensitivity", 0.1f, 5f);
            Toggle vib = AddToggleRow(section.transform, "Controller Vibration");

            // A few example rebind rows — set their Action Name to match your Input Actions asset.
            var rows = new List<RebindButtonUI>();
            rows.Add(AddRebindRow(section.transform, mgr, "Jump", "Jump"));
            rows.Add(AddRebindRow(section.transform, mgr, "Move Left", "Move"));
            rows.Add(AddRebindRow(section.transform, mgr, "Interact", "Interact"));

            var (resetGO, resetBtn, _) = MakeButton("ResetControls", section.transform, "Reset Controls", new Color(0.6f, 0.6f, 0.6f));
            var le = resetGO.AddComponent<LayoutElement>(); le.preferredHeight = 60; le.preferredWidth = 300;

            var so = SO(ui);
            Ref(so, "m_Settings", mgr);
            Ref(so, "m_SensitivitySlider", sens); Ref(so, "m_VibrationToggle", vib);
            Ref(so, "m_ResetButton", resetBtn); Ref(so, "m_ConfirmationPopup", popup);
            Arr(so, "m_RebindRows", rows.ToArray());
            Apply(so);
            return ui;
        }

        private static ConfirmationPopup BuildConfirmationPopup(Transform parent)
        {
            GameObject layer = NewUI("ConfirmationPopup", parent);
            Stretch((RectTransform)layer.transform, 0, 0, 0, 0);
            layer.AddComponent<CanvasGroup>();
            Image dim = layer.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.6f);
            var popup = layer.AddComponent<ConfirmationPopup>();

            GameObject dialog = NewUI("Dialog", layer.transform);
            var dRT = (RectTransform)dialog.transform;
            dRT.anchorMin = dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.sizeDelta = new Vector2(700, 360); dRT.anchoredPosition = Vector2.zero;
            Image dbg = dialog.AddComponent<Image>(); dbg.sprite = Builtin("UI/Skin/UISprite.psd");
            dbg.type = Image.Type.Sliced; dbg.color = new Color(0.15f, 0.18f, 0.28f);

            TextMeshProUGUI title = TMPText("Title", dialog.transform, "Quit Game", 40, new Color(1f, 0.7f, 0.1f));
            title.rectTransform.anchoredPosition = new Vector2(0, 120);
            TextMeshProUGUI msg = TMPText("Message", dialog.transform, "Are you sure?", 28, Color.white);
            msg.rectTransform.sizeDelta = new Vector2(620, 120); msg.rectTransform.anchoredPosition = new Vector2(0, 10);

            var (yesGO, yesBtn, yesLabel) = MakeButton("YesButton", dialog.transform, "Yes", new Color(0.3f, 0.75f, 0.3f));
            Place((RectTransform)yesGO.transform, new Vector2(-160, -110), new Vector2(240, 90));
            var (noGO, noBtn, noLabel) = MakeButton("NoButton", dialog.transform, "No", new Color(0.8f, 0.25f, 0.25f));
            Place((RectTransform)noGO.transform, new Vector2(160, -110), new Vector2(240, 90));

            var so = SO(popup);
            Str(so, "m_PanelId", "ConfirmationPopup");
            Ref(so, "m_TitleText", title); Ref(so, "m_MessageText", msg);
            Ref(so, "m_YesButton", yesBtn); Ref(so, "m_NoButton", noBtn);
            Ref(so, "m_YesLabel", yesLabel); Ref(so, "m_NoLabel", noLabel);
            Apply(so);
            return popup;
        }

        // ─── Level grid (reuses the Level Selection assets) ─────────────────────────

        private static void EnsureLevelAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(LevelDbPath) == null)
                LevelSelectionSystem.EditorTools.LevelSelectionSetup.GenerateDataMenu();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LevelButtonPrefabPath) == null)
                LevelSelectionSystem.EditorTools.LevelSelectionSetup.CreatePrefabMenu();
        }

        private static LevelSelectionSystem.LevelSelectionUI BuildLevelGrid(Transform parent)
        {
            var db = AssetDatabase.LoadAssetAtPath<LevelSelectionSystem.LevelDatabase>(LevelDbPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelButtonPrefabPath);

            GameObject host = NewUI("LevelGrid", parent);
            var hostRT = (RectTransform)host.transform;
            hostRT.anchorMin = new Vector2(0, 0); hostRT.anchorMax = new Vector2(1, 1);
            hostRT.offsetMin = new Vector2(60, 60); hostRT.offsetMax = new Vector2(-60, -120);
            var levelUI = host.AddComponent<LevelSelectionSystem.LevelSelectionUI>();

            GameObject scrollGO = NewUI("Scroll View", host.transform);
            Stretch((RectTransform)scrollGO.transform, 0, 0, 0, 0);
            scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.15f);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;

            GameObject viewport = NewUI("Viewport", scrollGO.transform);
            Stretch((RectTransform)viewport.transform, 0, 0, 0, 0);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = (RectTransform)viewport.transform;

            GameObject content = NewUI("Content", viewport.transform);
            var contentRT = (RectTransform)content.transform;
            contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f); contentRT.anchoredPosition = Vector2.zero;
            scrollRect.content = contentRT;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            grid.cellSize = new Vector2(150, 150); grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(20, 20, 20, 20); grid.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var so = SO(levelUI);
            if (db != null) Ref(so, "m_Database", db);
            if (buttonPrefab != null) Ref(so, "m_LevelButtonPrefab", buttonPrefab.GetComponent<LevelSelectionSystem.LevelButtonUI>());
            Ref(so, "m_Content", contentRT); Ref(so, "m_ScrollRect", scrollRect);
            so.FindProperty("m_Columns").intValue = 5;
            so.FindProperty("m_CellSize").vector2Value = new Vector2(150, 150);
            so.FindProperty("m_Spacing").vector2Value = new Vector2(20, 20);
            var pad = so.FindProperty("m_Padding");
            pad.FindPropertyRelative("m_Left").intValue = 20; pad.FindPropertyRelative("m_Right").intValue = 20;
            pad.FindPropertyRelative("m_Top").intValue = 20; pad.FindPropertyRelative("m_Bottom").intValue = 20;
            Apply(so);
            return levelUI;
        }

        // ─── UI control factories (functional, via Unity's own DefaultControls) ─────

        private static TMP_DefaultControls.Resources TmpRes() => new TMP_DefaultControls.Resources
        {
            standard = Builtin("UI/Skin/UISprite.psd"), background = Builtin("UI/Skin/Background.psd"),
            inputField = Builtin("UI/Skin/InputFieldBackground.psd"), knob = Builtin("UI/Skin/Knob.psd"),
            checkmark = Builtin("UI/Skin/Checkmark.psd"), dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd"),
        };

        private static DefaultControls.Resources UiRes() => new DefaultControls.Resources
        {
            standard = Builtin("UI/Skin/UISprite.psd"), background = Builtin("UI/Skin/Background.psd"),
            inputField = Builtin("UI/Skin/InputFieldBackground.psd"), knob = Builtin("UI/Skin/Knob.psd"),
            checkmark = Builtin("UI/Skin/Checkmark.psd"), dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd"),
        };

        /// <summary>Creates a labeled row [label | control] inside a vertical section.</summary>
        private static GameObject AddRow(Transform parent, string label, out RectTransform controlSlot)
        {
            GameObject row = NewUI("Row_" + label, parent);
            var rle = row.AddComponent<LayoutElement>(); rle.preferredHeight = 50; rle.minHeight = 44;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true; hlg.childAlignment = TextAnchor.MiddleLeft;

            TextMeshProUGUI lbl = TMPText("Label", row.transform, label, 24, Color.white);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            var lblLE = lbl.gameObject.AddComponent<LayoutElement>(); lblLE.flexibleWidth = 1f; lblLE.preferredWidth = 320;

            GameObject slot = NewUI("Control", row.transform);
            controlSlot = (RectTransform)slot.transform;
            var slotLE = slot.AddComponent<LayoutElement>(); slotLE.preferredWidth = 360; slotLE.preferredHeight = 40;
            return row;
        }

        private static TMP_Dropdown AddDropdownRow(Transform parent, string label)
        {
            AddRow(parent, label, out RectTransform slot);
            GameObject dd = TMP_DefaultControls.CreateDropdown(TmpRes());
            dd.transform.SetParent(slot, false);
            Stretch((RectTransform)dd.transform, 0, 0, 0, 0);
            return dd.GetComponent<TMP_Dropdown>();
        }

        private static Slider AddSliderRow(Transform parent, string label, float min, float max)
        {
            AddRow(parent, label, out RectTransform slot);
            GameObject s = DefaultControls.CreateSlider(UiRes());
            s.transform.SetParent(slot, false);
            var srt = (RectTransform)s.transform;
            srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f);
            srt.sizeDelta = new Vector2(0, 20); srt.anchoredPosition = Vector2.zero;
            Slider slider = s.GetComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            return slider;
        }

        private static Toggle AddToggleRow(Transform parent, string label)
        {
            AddRow(parent, label, out RectTransform slot);
            GameObject t = DefaultControls.CreateToggle(UiRes());
            t.transform.SetParent(slot, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0, 0.5f); trt.anchorMax = new Vector2(0, 0.5f);
            trt.anchoredPosition = new Vector2(20, 0);
            return t.GetComponent<Toggle>();
        }

        private static RebindButtonUI AddRebindRow(Transform parent, SettingsManager mgr, string display, string actionName)
        {
            GameObject row = AddRow(parent, display, out RectTransform slot);
            var rebind = row.AddComponent<RebindButtonUI>();
            // The action label is the row's left label; we reuse it.
            TextMeshProUGUI actionLabel = row.transform.GetComponentInChildren<TextMeshProUGUI>();

            var (btnGO, btn, btnLabel) = MakeButton("RebindButton", slot, "—", new Color(0.3f, 0.4f, 0.6f));
            Stretch((RectTransform)btnGO.transform, 0, 0, 0, 0);

            var so = SO(rebind);
            Ref(so, "m_Settings", mgr);
            Str(so, "m_ActionName", actionName);
            so.FindProperty("m_BindingIndex").intValue = 0;
            Ref(so, "m_Button", btn); Ref(so, "m_BindingLabel", btnLabel);
            Ref(so, "m_ActionLabel", actionLabel); Str(so, "m_DisplayName", display);
            Apply(so);
            return rebind;
        }

        // ─── Primitive builders / helpers ───────────────────────────────────────────

        private static GameObject MakePanel(string name, Transform parent, string panelId, out UIPanel panel)
        {
            GameObject go = NewUI(name, parent);
            Stretch((RectTransform)go.transform, 0, 0, 0, 0);
            go.AddComponent<CanvasGroup>();
            panel = go.AddComponent<UIPanel>();
            var so = SO(panel); Str(so, "m_PanelId", panelId); Apply(so);
            return go;
        }

        private static (GameObject, Button, TextMeshProUGUI) MakeButton(string name, Transform parent, string label, Color color)
        {
            GameObject go = NewUI(name, parent);
            ((RectTransform)go.transform).sizeDelta = new Vector2(200, 80);
            Image bg = go.AddComponent<Image>();
            bg.sprite = Builtin("UI/Skin/UISprite.psd"); bg.type = Image.Type.Sliced; bg.color = color;
            Button btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
            TextMeshProUGUI text = TMPText("Label", go.transform, label, 28, Color.white);
            Stretch((RectTransform)text.transform, 0, 0, 0, 0);
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return (go, btn, text);
        }

        private static TextMeshProUGUI TMPText(string name, Transform parent, string text, float size, Color color)
        {
            GameObject go = NewUI(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 60); rt.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        private static Canvas EnsureCanvasAndEventSystem()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            return canvas;
        }

        private static void ClearPrevious(Canvas canvas)
        {
            DestroyChild(canvas.transform, "ScreenManager");
            DestroyChild(canvas.transform, "ConfirmationPopup");
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static InputActionAsset FindInputActions()
        {
            string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
            InputActionAsset first = null;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset == null) continue;
                if (path.Contains("PlayerInputAction")) return asset; // preferred
                first ??= asset;
            }
            return first;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt, float l, float r, float t, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        private static void Place(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        }

        private static void AnchorRect(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(minX, minY); rt.anchorMax = new Vector2(maxX, maxY);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static void SetFirstSelected(UIPanel panel, GameObject go)
        {
            var so = SO(panel); Ref(so, "m_FirstSelected", go); Apply(so);
        }

        private static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        // SerializedObject conveniences.
        private static SerializedObject SO(Object o) => new SerializedObject(o);
        private static void Apply(SerializedObject so) => so.ApplyModifiedPropertiesWithoutUndo();
        private static void Ref(SerializedObject so, string p, Object v) => so.FindProperty(p).objectReferenceValue = v;
        private static void Str(SerializedObject so, string p, string v) => so.FindProperty(p).stringValue = v;
        private static void Arr(SerializedObject so, string p, Object[] vs)
        {
            SerializedProperty sp = so.FindProperty(p);
            sp.arraySize = vs.Length;
            for (int i = 0; i < vs.Length; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = vs[i];
        }

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
