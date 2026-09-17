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
        private const string k_HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";

        [MenuItem(k_MenuPath)]
        public static void RunSetupManual()
        {
            RunSetup(true);
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

            Transform panel = screenManager.Find("HomeScreenPanel  New") ?? screenManager.Find("HomeScreenPanel_New");
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
                if (notifyUser) EditorUtility.DisplayDialog("Setup Error", msg, "OK");
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

            // ─── 4. VILLAIN LIVING CHARACTER ANIMATOR ──────────────────────────
            UIVillainAnimator villainAnim = GetOrAdd<UIVillainAnimator>(villain.gameObject);
            UIBackgroundLayerController villainLayerCtrl = GetOrAdd<UIBackgroundLayerController>(villain.gameObject);
            villainLayerCtrl.Profile = BackgroundLayerProfile.Custom;

            // ─── 5. ROBOT / HERO LIFE ANIMATOR ─────────────────────────────────
            UIRobotLifeAnimator robotAnim = GetOrAdd<UIRobotLifeAnimator>(hero.gameObject);
            UIBackgroundLayerController robotLayerCtrl = GetOrAdd<UIBackgroundLayerController>(hero.gameObject);
            robotLayerCtrl.Profile = BackgroundLayerProfile.Custom;

            // ─── 6. SPARK ATMOSPHERE SYSTEM ────────────────────────────────────
            UISparkAtmosphereSystem sparkAtmosphere = GetOrAdd<UISparkAtmosphereSystem>(spark.gameObject);

            // ─── 7. TITLE SUSPENSION SYSTEM & ENERGY LINKS ─────────────────────
            UITitleSuspensionSystem titleSuspension = GetOrAdd<UITitleSuspensionSystem>(holderTitle.gameObject);
            titleSuspension.VillainAnimator = villainAnim;
            titleSuspension.CreateEnergyLinksIfMissing();

            Transform titleChild = holderTitle.Find("Title");
            if (titleChild != null)
            {
                UIBackgroundLayerController titleShader = GetOrAdd<UIBackgroundLayerController>(titleChild.gameObject);
                titleShader.Profile = BackgroundLayerProfile.Custom;
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

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[MainMenuBackgroundSetup] Living Cinematic Background successfully configured and saved!");

            if (notifyUser)
            {
                EditorUtility.DisplayDialog("Living Cinematic Background",
                    "The Main Menu living background system has been successfully configured!\n\n" +
                    "• BG Red & Yellow transition shaders connected\n" +
                    "• Villain breathing and hand anchors dynamic\n" +
                    "• Title physically suspended via spring physics & energy links\n" +
                    "• Robot living lab idle and energy cycle active\n" +
                    "• Spark atmosphere & ambient particles enabled\n" +
                    "• Multi-layer subtle parallax configured\n" +
                    "• Music beat sync & button focus reactions wired", "OK");
            }

            return true;
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
