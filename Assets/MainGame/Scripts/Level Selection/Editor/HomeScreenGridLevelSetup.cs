using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MainGame.UI.Unified;

namespace LevelSelection.Editor
{
    [InitializeOnLoad]
    public static class HomeScreenGridLevelSetup
    {
        private const string k_MenuPath = "Tools/Level Selection/Setup Grid Level Generator in HomeScreen";
        private const string k_HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";
        private const string k_PrefabPath = "Assets/MainGame/Prefabs/UI/Menu Level Creation/New Level Prefab.prefab";
        private const string k_RunSessionKey = "HomeScreenGridLevelSetup_Completed_v3";

        static HomeScreenGridLevelSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(k_RunSessionKey, false))
                {
                    SessionState.SetBool(k_RunSessionKey, true);
                    RunSetup(false);
                }
            };
        }

        [MenuItem(k_MenuPath)]
        public static void RunSetupManual()
        {
            RunSetup(true);
        }

        public static void RunSetup(bool notifyUser)
        {
            Debug.Log("[HomeScreenGridLevelSetup] Starting Grid Level Selection setup...");

            // 1. Ensure New Level Prefab has GridLevelButton attached and configured
            SetupPrefab();

            // 2. Open HomeScreen scene if not open
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != k_HomeScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(k_HomeScenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[HomeScreenGridLevelSetup] Could not open scene at '{k_HomeScenePath}'.");
                return;
            }

            // 3. Locate Level Selection hierarchy
            GameObject levelSelectionGo = GameObject.Find("Level Selection");
            if (levelSelectionGo == null)
            {
                var allRoots = activeScene.GetRootGameObjects();
                foreach (var r in allRoots)
                {
                    var found = FindChildRecursive(r.transform, "Level Selection");
                    if (found != null)
                    {
                        levelSelectionGo = found.gameObject;
                        break;
                    }
                }
            }

            if (levelSelectionGo == null)
            {
                Debug.LogError("[HomeScreenGridLevelSetup] Could not find 'Level Selection' GameObject in HomeScreen.");
                return;
            }

            // 4. Locate Grid View
            Transform gridViewTrans = FindChildRecursive(levelSelectionGo.transform, "Grid View");
            if (gridViewTrans == null)
            {
                var gridLayout = levelSelectionGo.GetComponentInChildren<GridLayoutGroup>(true);
                if (gridLayout != null) gridViewTrans = gridLayout.transform;
            }

            if (gridViewTrans == null)
            {
                Debug.LogError("[HomeScreenGridLevelSetup] Could not find 'Grid View' with GridLayoutGroup.");
                return;
            }

            RectTransform gridContainer = gridViewTrans.GetComponent<RectTransform>();

            // 5. Locate navigation buttons and headers
            Transform holder = FindChildRecursive(levelSelectionGo.transform, "Holder") ?? levelSelectionGo.transform;
            if (holder != null)
            {
                CanvasGroup holderCg = holder.GetComponent<CanvasGroup>();
                if (holderCg == null)
                {
                    holderCg = holder.gameObject.AddComponent<CanvasGroup>();
                }
                holderCg.alpha = 1f;
                holderCg.interactable = true;
                holderCg.blocksRaycasts = true;
                EditorUtility.SetDirty(holder.gameObject);
            }

            Transform bNextTrans = FindChildRecursive(holder, "B Next");
            Transform bPrevTrans = FindChildRecursive(holder, "B Perivous") ?? FindChildRecursive(holder, "B Previous");
            Transform bBackTrans = FindChildRecursive(holder, "B Back");
            Transform imgArcTrans = FindChildRecursive(holder, "IMG Arc");

            Button nextBtn = bNextTrans != null ? bNextTrans.GetComponent<Button>() : null;
            Button prevBtn = bPrevTrans != null ? bPrevTrans.GetComponent<Button>() : null;
            Button backBtn = bBackTrans != null ? bBackTrans.GetComponent<Button>() : null;
            Image arcTitleImg = imgArcTrans != null ? imgArcTrans.GetComponent<Image>() : null;

            // Requirement 3: Hide next and previous buttons so navigation is handled via Arrow keys / A-D
            if (bNextTrans != null)
            {
                bNextTrans.gameObject.SetActive(false);
                EditorUtility.SetDirty(bNextTrans.gameObject);
            }
            if (bPrevTrans != null)
            {
                bPrevTrans.gameObject.SetActive(false);
                EditorUtility.SetDirty(bPrevTrans.gameObject);
            }
            if (bBackTrans != null)
            {
                bBackTrans.gameObject.SetActive(true);
                EditorUtility.SetDirty(bBackTrans.gameObject);
            }

            // 6. Locate Pips
            List<GameObject> pips = new List<GameObject>();
            for (int i = 1; i <= 10; i++)
            {
                Transform pip = FindChildRecursive(levelSelectionGo.transform, $"Pip_{i}");
                if (pip != null) pips.Add(pip.gameObject);
            }

            // 7. Add or get GridLevelGenerator on Level Selection
            GridLevelGenerator generator = levelSelectionGo.GetComponent<GridLevelGenerator>();
            if (generator == null)
            {
                generator = levelSelectionGo.AddComponent<GridLevelGenerator>();
            }

            // Load ArcData assets
            List<ArcData> arcDataList = new List<ArcData>();
            string[] arcGuids = AssetDatabase.FindAssets("t:ArcData");
            if (arcGuids != null)
            {
                foreach (var g in arcGuids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var arc = AssetDatabase.LoadAssetAtPath<ArcData>(p);
                    if (arc != null && !arcDataList.Contains(arc))
                    {
                        arcDataList.Add(arc);
                    }
                }
                arcDataList.Sort((a, b) =>
                {
                    bool aIsTut = a.name.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0 || a.arcName.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool bIsTut = b.name.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0 || b.arcName.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (aIsTut && !bIsTut) return -1;
                    if (!aIsTut && bIsTut) return 1;
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Ensure theme images on ArcData
            Texture2D tutBg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MainGame/Sprites/LEVEL SELECT UI new/TUT_BG.png");
            Texture2D lvlBg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL_BG.png");
            foreach (var arc in arcDataList)
            {
                if (arc != null)
                {
                    if (arc.name.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0 || arc.arcName.IndexOf("tut", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (arc.arcThemeImage == null && tutBg != null)
                        {
                            arc.arcThemeImage = tutBg;
                            EditorUtility.SetDirty(arc);
                        }
                    }
                    else
                    {
                        if (arc.arcThemeImage == null && lvlBg != null)
                        {
                            arc.arcThemeImage = lvlBg;
                            EditorUtility.SetDirty(arc);
                        }
                    }
                }
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);

            // Serialize properties on generator
            SerializedObject genSo = new SerializedObject(generator);
            genSo.Update();

            SerializedProperty propContainer = genSo.FindProperty("m_GridContainer");
            if (propContainer != null) propContainer.objectReferenceValue = gridContainer;

            SerializedProperty propPrefab = genSo.FindProperty("m_LevelButtonPrefab");
            if (propPrefab != null) propPrefab.objectReferenceValue = prefabAsset;

            SerializedProperty propLevelsPerPage = genSo.FindProperty("m_LevelsPerPage");
            if (propLevelsPerPage != null) propLevelsPerPage.intValue = 10;

            SerializedProperty propNextBtn = genSo.FindProperty("m_NextButton");
            if (propNextBtn != null) propNextBtn.objectReferenceValue = nextBtn;

            SerializedProperty propPrevBtn = genSo.FindProperty("m_PrevButton");
            if (propPrevBtn != null) propPrevBtn.objectReferenceValue = prevBtn;

            SerializedProperty propArcImg = genSo.FindProperty("m_ArcTitleImage");
            if (propArcImg != null) propArcImg.objectReferenceValue = arcTitleImg;

            // Wire ArcData list
            SerializedProperty propArcList = genSo.FindProperty("m_ArcDataList");
            if (propArcList != null)
            {
                propArcList.ClearArray();
                for (int i = 0; i < arcDataList.Count; i++)
                {
                    propArcList.InsertArrayElementAtIndex(i);
                    propArcList.GetArrayElementAtIndex(i).objectReferenceValue = arcDataList[i];
                }
            }

            SerializedProperty propCustomArcs = genSo.FindProperty("m_CustomArcs");
            if (propCustomArcs != null)
            {
                propCustomArcs.ClearArray();
            }

            // Wire Pips list
            SerializedProperty propPips = genSo.FindProperty("m_PagePips");
            if (propPips != null)
            {
                propPips.ClearArray();
                for (int i = 0; i < pips.Count; i++)
                {
                    propPips.InsertArrayElementAtIndex(i);
                    propPips.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
                }
            }

            genSo.ApplyModifiedProperties();

            // Load sprite sheet thumbnails into generator
            generator.LoadThumbnailsFromSpriteSheet();
            EditorUtility.SetDirty(generator);

            // 8. Configure LevelSelectionScreenAnimator on Level Selection
            LevelSelectionScreenAnimator animator = levelSelectionGo.GetComponent<LevelSelectionScreenAnimator>();
            if (animator == null)
            {
                animator = levelSelectionGo.AddComponent<LevelSelectionScreenAnimator>();
            }
            animator.enabled = true;

            SerializedObject animSo = new SerializedObject(animator);
            animSo.Update();
            SerializedProperty animPanelBg = animSo.FindProperty("m_PanelBackground");
            if (animPanelBg != null) animPanelBg.objectReferenceValue = holder.GetComponent<RectTransform>();
            SerializedProperty animArcSign = animSo.FindProperty("m_ArcSign");
            if (animArcSign != null && imgArcTrans != null) animArcSign.objectReferenceValue = imgArcTrans.GetComponent<RectTransform>();
            SerializedProperty animBackBtn = animSo.FindProperty("m_BackButton");
            if (animBackBtn != null && bBackTrans != null) animBackBtn.objectReferenceValue = bBackTrans.GetComponent<RectTransform>();
            SerializedProperty animNextBtn = animSo.FindProperty("m_NextButton");
            if (animNextBtn != null && bNextTrans != null) animNextBtn.objectReferenceValue = bNextTrans.GetComponent<RectTransform>();
            SerializedProperty animPrevBtn = animSo.FindProperty("m_PrevButton");
            if (animPrevBtn != null && bPrevTrans != null) animPrevBtn.objectReferenceValue = bPrevTrans.GetComponent<RectTransform>();
            SerializedProperty animGridCont = animSo.FindProperty("m_GridContainer");
            if (animGridCont != null) animGridCont.objectReferenceValue = gridContainer;
            animSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(animator);

            // 9. Add or get LevelSelectionScreen on Level Selection
            LevelSelectionScreen lsScreen = levelSelectionGo.GetComponent<LevelSelectionScreen>();
            if (lsScreen == null)
            {
                lsScreen = levelSelectionGo.AddComponent<LevelSelectionScreen>();
            }
            lsScreen.enabled = true;

            SerializedObject lsSo = new SerializedObject(lsScreen);
            lsSo.Update();
            SerializedProperty propLsGen = lsSo.FindProperty("m_GridLevelGenerator");
            if (propLsGen != null) propLsGen.objectReferenceValue = generator;
            SerializedProperty propLsAnim = lsSo.FindProperty("m_Animator");
            if (propLsAnim != null) propLsAnim.objectReferenceValue = animator;
            SerializedProperty propLsBack = lsSo.FindProperty("m_BackButton");
            if (propLsBack != null) propLsBack.objectReferenceValue = backBtn;
            lsSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(lsScreen);

            // 10. Wire MainMenuScreen reference to LevelSelectionScreen
            MainMenuScreen mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
            if (mainMenu != null)
            {
                SerializedObject menuSo = new SerializedObject(mainMenu);
                menuSo.Update();
                SerializedProperty propMenuLs = menuSo.FindProperty("m_LevelSelectionScreen");
                if (propMenuLs != null)
                {
                    propMenuLs.objectReferenceValue = lsScreen;
                }
                menuSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(mainMenu);
                Debug.Log("[HomeScreenGridLevelSetup] Successfully wired MainMenuScreen.m_LevelSelectionScreen.");
            }
            else
            {
                Debug.LogWarning("[HomeScreenGridLevelSetup] Could not locate MainMenuScreen in scene.");
            }

            // 11. Ensure all existing child buttons in Grid View have GridLevelButton and are configured
            EnsureGridChildButtons(gridContainer, prefabAsset);

            // 12. Initialise grid display in editor
            generator.ShowPage(0, 0, selectInEventSystem: false);

            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(levelSelectionGo);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[HomeScreenGridLevelSetup] Successfully configured GridLevelGenerator and LevelSelectionScreen in HomeScreen and saved scene!");
            if (notifyUser)
            {
                EditorUtility.DisplayDialog("Setup Complete", "Grid Level Selection and Level Buttons have been configured and saved in HomeScreen!", "OK");
            }
        }

        private static void SetupPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[HomeScreenGridLevelSetup] Could not find prefab at '{k_PrefabPath}'.");
                return;
            }

            GridLevelButton btn = prefab.GetComponent<GridLevelButton>();
            if (btn == null)
            {
                btn = prefab.AddComponent<GridLevelButton>();
            }

            ConfigureGridLevelButtonComponent(btn);

            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[HomeScreenGridLevelSetup] GridLevelButton attached and configured on New Level Prefab.");
        }

        private static void EnsureGridChildButtons(RectTransform gridContainer, GameObject prefabAsset)
        {
            if (gridContainer == null) return;

            // If Grid View already has child buttons, configure GridLevelButton on each
            int childCount = gridContainer.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridContainer.GetChild(i);
                GridLevelButton btn = child.GetComponent<GridLevelButton>();
                if (btn == null)
                {
                    btn = child.gameObject.AddComponent<GridLevelButton>();
                }
                ConfigureGridLevelButtonComponent(btn);
                EditorUtility.SetDirty(child.gameObject);
            }

            // If less than 10 children and prefab is available, spawn up to 10
            if (childCount < 10 && prefabAsset != null)
            {
                for (int i = childCount; i < 10; i++)
                {
                    GameObject newBtn = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, gridContainer);
                    newBtn.name = $"New Level Prefab ({i + 1})";
                    GridLevelButton btn = newBtn.GetComponent<GridLevelButton>() ?? newBtn.AddComponent<GridLevelButton>();
                    ConfigureGridLevelButtonComponent(btn);
                    EditorUtility.SetDirty(newBtn);
                }
            }
        }

        private static void ConfigureGridLevelButtonComponent(GridLevelButton btn)
        {
            if (btn == null) return;

            btn.AutoWireComponents();
            btn.ConfigureArrowTransform();

            SerializedObject btnSo = new SerializedObject(btn);
            btnSo.Update();

            Sprite normalBanner = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL Button.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Level 1 button.png");
            Sprite selectedBanner = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/LVL Button_selected.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Level 1 button select.png");
            Sprite uncollectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/LEVEL SELECT UI new/Collectable unknown.png");
            Sprite collectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/Collect sprite/Collect.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/Sprites/UI buttons/00 Marked Asssets for UI/collect.png");

            SerializedProperty propNorm = btnSo.FindProperty("m_BannerNormalSprite");
            if (propNorm != null && normalBanner != null) propNorm.objectReferenceValue = normalBanner;

            SerializedProperty propSel = btnSo.FindProperty("m_BannerSelectedSprite");
            if (propSel != null && selectedBanner != null) propSel.objectReferenceValue = selectedBanner;

            SerializedProperty propUncol = btnSo.FindProperty("m_CollectableUncollectedSprite");
            if (propUncol != null && uncollectedSprite != null) propUncol.objectReferenceValue = uncollectedSprite;

            SerializedProperty propCol = btnSo.FindProperty("m_CollectableCollectedSprite");
            if (propCol != null && collectedSprite != null) propCol.objectReferenceValue = collectedSprite;

            SerializedProperty propBorder = btnSo.FindProperty("m_BorderImage");
            if (propBorder != null && propBorder.objectReferenceValue == null)
            {
                propBorder.objectReferenceValue = btn.GetComponent<Image>();
            }

            SerializedProperty propSelColor = btnSo.FindProperty("m_SelectedBorderColor");
            if (propSelColor != null) propSelColor.colorValue = new Color(1f, 0.6588f, 0f, 1f); // Orange #FFA800

            SerializedProperty propNormColor = btnSo.FindProperty("m_NormalBorderColor");
            if (propNormColor != null) propNormColor.colorValue = new Color(0.9f, 0.9f, 0.9f, 1f);

            SerializedProperty propLockColor = btnSo.FindProperty("m_LockedBorderColor");
            if (propLockColor != null) propLockColor.colorValue = new Color(0.45f, 0.45f, 0.45f, 0.7f);

            SerializedProperty propThumbLockColor = btnSo.FindProperty("m_LockedColor");
            if (propThumbLockColor != null) propThumbLockColor.colorValue = Color.white;

            SerializedProperty propUncolColor = btnSo.FindProperty("m_CollectableUncollectedColor");
            if (propUncolColor != null) propUncolColor.colorValue = Color.white;

            SerializedProperty propColColor = btnSo.FindProperty("m_CollectableCollectedColor");
            if (propColColor != null) propColColor.colorValue = Color.white;

            btnSo.ApplyModifiedProperties();

            // Disable raycast target on all child graphics so clicks hit root button
            var graphics = btn.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (g.gameObject != btn.gameObject)
                {
                    g.raycastTarget = false;
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform deeper = FindChildRecursive(child, targetName);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
