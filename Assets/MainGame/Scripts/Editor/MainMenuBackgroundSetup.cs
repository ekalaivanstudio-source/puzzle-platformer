using System;
using System.Collections.Generic;
using MainGame.UI.CinematicEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainGame.UI.Editor
{
    public static class MainMenuBackgroundSetup
    {
        private const string k_MenuPath = "Tools/UI/Setup Living Cinematic Background";
        private const string k_OutlineMenuPath = "Tools/UI/Setup Living Character Outlines";
        private const string k_HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";
        private const string k_ShaderName = "MainGame/UI/CinematicPixelBackground";

        [MenuItem(k_MenuPath)]
        public static void RunSetupManual()
        {
            RunSetup(true);
        }

        [MenuItem(k_OutlineMenuPath)]
        public static void RunOutlineSetupManual()
        {
            SetupLivingCharacterOutlines(true);
        }

        public static bool RunSetup(bool notifyUser)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != k_HomeScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(k_HomeScenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[MainMenuBackgroundSetup] Could not open scene at '{k_HomeScenePath}'.");
                return false;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            Transform screenManager = null;

            foreach (var root in roots)
            {
                if (root.name.Contains("Canvas") && root.name.Contains("Main Menu"))
                {
                    screenManager = root.transform.Find("ScreenManager");
                }
                else if (root.name == "ScreenManager")
                {
                    screenManager = root.transform;
                }
            }

            if (screenManager == null)
            {
                var smGo = GameObject.Find("ScreenManager");
                if (smGo != null) screenManager = smGo.transform;
            }

            if (screenManager == null)
            {
                Debug.LogError("[MainMenuBackgroundSetup] ScreenManager not found in scene!");
                return false;
            }

            // ─── 1. VALIDATION OF REQUIRED HIERARCHY OBJECTS ───────────────────
            Transform bgRoot = screenManager.Find("Backagrond") ?? screenManager.Find("Background");
            if (bgRoot == null)
            {
                Debug.LogError("[MainMenuBackgroundSetup] Missing 'Backagrond' (or 'Background') under ScreenManager!");
                return false;
            }

            Transform bgRed = bgRoot.Find("BG Red") ?? bgRoot.Find("BG_Red");
            Transform redYellow = bgRoot.Find("Red and yellow layer") ?? bgRoot.Find("RedAndYellowLayer");
            Transform villain = bgRoot.Find("DR") ?? bgRoot.Find("Villan") ?? bgRoot.Find("Villain") ?? bgRoot.Find("CharacterArtwork");
            Transform hero = bgRoot.Find("Hero") ?? bgRoot.Find("Robot") ?? bgRoot.Find("Byte");
            Transform spark = bgRoot.Find("Spark") ?? bgRoot.Find("FX");

            Transform panel = screenManager.Find("HomeScreenPanel New") ?? screenManager.Find("HomeScreenPanel_New");
            Transform holderTitle = (panel != null) ? (panel.Find("Holder Tittle") ?? panel.Find("Holder Title") ?? panel.Find("Title/Holder Tittle")) : null;
            if (holderTitle == null)
            {
                holderTitle = screenManager.Find("Holder Tittle") ?? screenManager.Find("Holder Title");
            }

            List<string> missing = new List<string>();
            if (bgRed == null) missing.Add("BG Red");
            if (redYellow == null) missing.Add("Red and yellow layer");
            if (villain == null) missing.Add("Villan / DR");
            if (hero == null) missing.Add("Hero (Robot)");
            if (spark == null) missing.Add("Spark (FX)");
            if (holderTitle == null) missing.Add("Holder Tittle");

            if (missing.Count > 0)
            {
                string msg = "[MainMenuBackgroundSetup] The following required artwork objects were missing:\n" + string.Join("\n - ", missing);
                Debug.LogError(msg);
                if (notifyUser && !Application.isBatchMode) EditorUtility.DisplayDialog("Setup Error", msg, "OK");
                return false;
            }

            Debug.Log("[MainMenuBackgroundSetup] All required artwork objects found. Configuring components...");

            // Ensure Title rest position is set to y = 0 so it does not obscure villain face
            RectTransform titleRect = holderTitle as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(539f, 0f);
            }

            // ─── 2. BG RED LAYER CONTROLLER ────────────────────────────────────
            UIBackgroundLayerController bgRedCtrl = GetOrAdd<UIBackgroundLayerController>(bgRed.gameObject);
            bgRedCtrl.Profile = BackgroundLayerProfile.BackgroundRed;

            // ─── 3. RED AND YELLOW LAYER CONTROLLER ────────────────────────────
            UIBackgroundLayerController redYellowCtrl = GetOrAdd<UIBackgroundLayerController>(redYellow.gameObject);
            redYellowCtrl.Profile = BackgroundLayerProfile.RedYellowTransition;

            // ─── 4. VILLAIN LIVING CHARACTER ANIMATOR & OUTLINE ────────────────
            UIVillainAnimator villainAnim = GetOrAdd<UIVillainAnimator>(villain.gameObject);
            UIBackgroundLayerController villainLayerCtrl = GetOrAdd<UIBackgroundLayerController>(villain.gameObject);
            villainLayerCtrl.Profile = BackgroundLayerProfile.Villain;
            villainLayerCtrl.EnsureOutlineLayer();

            // ─── 5. ROBOT / HERO LIFE ANIMATOR & OUTLINE ───────────────────────
            UIRobotLifeAnimator robotAnim = GetOrAdd<UIRobotLifeAnimator>(hero.gameObject);
            UIBackgroundLayerController robotLayerCtrl = GetOrAdd<UIBackgroundLayerController>(hero.gameObject);
            robotLayerCtrl.Profile = BackgroundLayerProfile.Hero;
            robotLayerCtrl.EnsureOutlineLayer();

            // ─── 6. SPARK ATMOSPHERE SYSTEM ────────────────────────────────────
            UISparkAtmosphereSystem sparkAtmosphere = GetOrAdd<UISparkAtmosphereSystem>(spark.gameObject);

            // ─── 7. TITLE SUSPENSION SYSTEM & OUTLINE ──────────────────────────
            UITitleSuspensionSystem titleSuspension = GetOrAdd<UITitleSuspensionSystem>(holderTitle.gameObject);
            titleSuspension.VillainAnimator = villainAnim;
            titleSuspension.CreateEnergyLinksIfMissing();

            Transform titleChild = holderTitle.Find("Title") ?? holderTitle;
            UIBackgroundLayerController titleShader = null;
            if (titleChild != null && titleChild.GetComponent<Image>() != null)
            {
                titleShader = GetOrAdd<UIBackgroundLayerController>(titleChild.gameObject);
                titleShader.Profile = BackgroundLayerProfile.Title;
                titleShader.EnsureOutlineLayer();
            }

            // ─── 8. PARALLAX CONTROLLER ────────────────────────────────────────
            UIParallaxController parallax = GetOrAdd<UIParallaxController>(screenManager.gameObject);
            SetupParallaxLayers(parallax, bgRed, redYellow, villain, holderTitle, hero, spark);

            // ─── 9. MAIN MENU BACKGROUND DIRECTOR ──────────────────────────────
            MainMenuBackgroundDirector director = GetOrAdd<MainMenuBackgroundDirector>(screenManager.gameObject);
            director.BgRedController = bgRedCtrl;
            director.RedYellowController = redYellowCtrl;
            director.VillainAnimator = villainAnim;
            director.TitleSuspension = titleSuspension;
            director.RobotAnimator = robotAnim;
            director.SparkAtmosphere = sparkAtmosphere;
            director.ParallaxController = parallax;
            director.VillainOutlineController = villainLayerCtrl;
            director.HeroOutlineController = robotLayerCtrl;
            director.TitleOutlineController = titleShader;

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[MainMenuBackgroundSetup] Living Cinematic Background successfully configured and saved!");

            if (notifyUser && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Living Cinematic Background",
                    "The Main Menu living background system has been successfully configured!\n\n" +
                    "• BG Red & Yellow transition shaders connected\n" +
                    "• Villain crimson pixel outline and warm gold inner rim active\n" +
                    "• Hero electric cyan plasma outline and energy cycles wired\n" +
                    "• Title cyber cyan outline & golden bevel rim suspended\n" +
                    "• Dedicated expanded mesh outline layers configured\n" +
                    "• Spark atmosphere & ambient particles enabled\n" +
                    "• Multi-layer subtle parallax configured\n" +
                    "• Music beat sync & button focus reactions wired", "OK");
            }

            return true;
        }

        public static bool SetupLivingCharacterOutlines(bool notifyUser)
        {
            if (!RunSetup(false))
            {
                return false;
            }

            bool isValid = ValidateCharacterOutlines(notifyUser);
            if (isValid && notifyUser && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Character Outlines Setup",
                    "Living Character Outlines successfully configured & validated!\n\n" +
                    "• Villain: Crimson outline (#FF2A47) + warm gold inner rim\n" +
                    "• Hero: Electric cyan outline (#26E0FF) + plasma cyan inner rim\n" +
                    "• Title: Cyber cyan outline (#38E2FF) + gold bevel inner rim\n\n" +
                    "All outlines use isolated runtime materials with expanded geometry to prevent clipping.", "OK");
            }
            return isValid;
        }

        public static bool ValidateCharacterOutlines(bool showDialog)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            // 1. Check Shader
            Shader pixelShader = Shader.Find(k_ShaderName);
            if (pixelShader == null)
            {
                errors.Add($"Shader '{k_ShaderName}' could not be found!");
            }

            // 2. Find ScreenManager
            var smGo = GameObject.Find("ScreenManager");
            if (smGo == null)
            {
                errors.Add("ScreenManager GameObject could not be found in active scene.");
            }
            else
            {
                Transform screenManager = smGo.transform;
                Transform bgRoot = screenManager.Find("Backagrond") ?? screenManager.Find("Background");
                if (bgRoot == null)
                {
                    errors.Add("Missing 'Backagrond' container under ScreenManager.");
                }
                else
                {
                    // Check Villain
                    Transform villain = bgRoot.Find("Villan") ?? bgRoot.Find("DR") ?? bgRoot.Find("Villain");
                    if (villain == null) errors.Add("Missing Villain GameObject under Backagrond.");
                    else ValidateOutlineObject(villain, "Villain", errors, warnings);

                    // Check Hero
                    Transform hero = bgRoot.Find("Hero") ?? bgRoot.Find("Robot") ?? bgRoot.Find("Byte");
                    if (hero == null) errors.Add("Missing Hero GameObject under Backagrond.");
                    else ValidateOutlineObject(hero, "Hero", errors, warnings);
                }

                // Check Title
                Transform panel = screenManager.Find("HomeScreenPanel New") ?? screenManager.Find("HomeScreenPanel_New");
                Transform titleRoot = (panel != null) ? (panel.Find("Holder Tittle") ?? panel.Find("Holder Title")) : screenManager.Find("Holder Tittle");
                if (titleRoot == null)
                {
                    errors.Add("Missing 'Holder Tittle' GameObject.");
                }
                else
                {
                    Transform titleChild = titleRoot.Find("Title") ?? titleRoot;
                    ValidateOutlineObject(titleChild, "Title", errors, warnings);
                }
            }

            if (errors.Count > 0)
            {
                string msg = "[Outline Validation Failed]\n" + string.Join("\n• ", errors);
                Debug.LogError(msg);
                if (showDialog && !Application.isBatchMode) EditorUtility.DisplayDialog("Outline Validation Error", msg, "OK");
                return false;
            }

            if (warnings.Count > 0)
            {
                string msg = "[Outline Validation Warnings]\n" + string.Join("\n• ", warnings);
                Debug.LogWarning(msg);
            }

            Debug.Log("[MainMenuBackgroundSetup] All Living Character Outlines successfully validated! 0 Errors.");
            return true;
        }

        private static void ValidateOutlineObject(Transform target, string label, List<string> errors, List<string> warnings)
        {
            if (target == null) return;

            Image img = target.GetComponent<Image>();
            if (img == null)
            {
                errors.Add($"{label} '{target.name}' is missing an Image component.");
                return;
            }

            if (img.sprite == null)
            {
                warnings.Add($"{label} '{target.name}' has no Sprite assigned to its Image.");
            }

            UIBackgroundLayerController ctrl = target.GetComponent<UIBackgroundLayerController>();
            if (ctrl == null)
            {
                errors.Add($"{label} '{target.name}' is missing UIBackgroundLayerController.");
            }

            string outlineName = $"{target.name}_Outline";
            Transform outlineTrans = target.parent != null ? target.parent.Find(outlineName) : null;
            if (outlineTrans == null)
            {
                warnings.Add($"{label} '{target.name}' has no dedicated outline child/sibling '{outlineName}'.");
            }
            else
            {
                if (!(outlineTrans is RectTransform))
                {
                    errors.Add($"Outline layer '{outlineName}' has a Transform instead of a RectTransform.");
                }
                if (outlineTrans.GetComponent<UIOutlineMeshExpansion>() == null)
                {
                    warnings.Add($"Outline layer '{outlineName}' is missing UIOutlineMeshExpansion.");
                }
                if (outlineTrans.GetComponent<UIOutlineFollower>() == null)
                {
                    warnings.Add($"Outline layer '{outlineName}' is missing UIOutlineFollower.");
                }
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        private static void SetupParallaxLayers(UIParallaxController parallax,
            Transform bgRed, Transform redYellow, Transform villain, Transform title, Transform hero, Transform spark)
        {
            if (parallax == null) return;

            var layers = parallax.Layers;
            layers.Clear();

            if (bgRed != null)
                layers.Add(new ParallaxLayerItem { name = "BG Red", target = bgRed.GetComponent<RectTransform>(), maxOffset = 2.5f });

            if (redYellow != null)
                layers.Add(new ParallaxLayerItem { name = "Red & Yellow Layer", target = redYellow.GetComponent<RectTransform>(), maxOffset = 5.0f });

            if (villain != null)
                layers.Add(new ParallaxLayerItem { name = "Villain", target = villain.GetComponent<RectTransform>(), maxOffset = 8.0f });

            if (title != null)
                layers.Add(new ParallaxLayerItem { name = "Title", target = title.GetComponent<RectTransform>(), maxOffset = 11.0f });

            if (hero != null)
                layers.Add(new ParallaxLayerItem { name = "Robot", target = hero.GetComponent<RectTransform>(), maxOffset = 13.0f });

            if (spark != null)
                layers.Add(new ParallaxLayerItem { name = "Spark / FX", target = spark.GetComponent<RectTransform>(), maxOffset = 16.0f });

            parallax.CaptureRestPositions();
        }
    }
}
