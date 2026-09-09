using System;
using System.Collections.Generic;
using MainGame.UI.Animation;
using MainGame.UI.Unified;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainGame.UI.Editor
{
    public static class HomeScreenCinematicSetup
    {
        private const string k_MenuPath = "Tools/UI/Setup Cinematic Menu Animations";
        private const string k_HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";
        private const string k_RunSessionKey = "HomeScreenCinematicSetup_Completed_v2";

        [InitializeOnLoadMethod]
        private static void OnProjectLoaded()
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
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != k_HomeScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(k_HomeScenePath, OpenSceneMode.Single);
            }

            if (!activeScene.IsValid())
            {
                Debug.LogError($"[HomeScreenCinematicSetup] Could not open scene at '{k_HomeScenePath}'.");
                return;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            Transform screenManager = null;
            Transform uiManagers = null;

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

                if (root.name == "UI_Managers")
                {
                    uiManagers = root.transform;
                }
            }

            if (screenManager == null)
            {
                var smGo = GameObject.Find("ScreenManager");
                if (smGo != null) screenManager = smGo.transform;
            }

            if (uiManagers == null)
            {
                var umGo = GameObject.Find("UI_Managers");
                if (umGo != null) uiManagers = umGo.transform;
            }

            if (screenManager == null)
            {
                Debug.LogError("[HomeScreenCinematicSetup] ScreenManager not found in scene!");
                return;
            }

            Debug.Log("[HomeScreenCinematicSetup] Configuring cinematic UI hierarchy...");

            SetupScreenManagerMicroShake(screenManager);
            SetupHomeScreenPanel(screenManager);
            SetupLevelSelection(screenManager);
            SetupCollectionPanel(screenManager);
            SetupSettingsPanel(screenManager);
            SetupCreditsPanel(screenManager);
            SetupConfirmationPopup(screenManager);

            if (uiManagers != null)
            {
                SetupUINavigationManager(uiManagers);
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[HomeScreenCinematicSetup] All cinematic animation systems successfully configured and saved!");

            if (notifyUser)
            {
                EditorUtility.DisplayDialog("Cinematic Menu Setup", "All screens, animators, buttons, and audio hooks have been successfully configured for controller-first physical animation!", "OK");
            }
        }

        private static void SetupScreenManagerMicroShake(Transform screenManager)
        {
            if (screenManager == null) return;
            UIMicroShake shake = screenManager.GetComponent<UIMicroShake>();
            if (shake == null)
            {
                shake = screenManager.gameObject.AddComponent<UIMicroShake>();
            }

            SerializedObject so = new SerializedObject(shake);
            SetObjProp(so, "m_TargetTransform", screenManager.GetComponent<RectTransform>());
            so.ApplyModifiedProperties();
        }

        private static void SetObjProp(SerializedObject so, string propName, UnityEngine.Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }

        private static void SetBoolProp(SerializedObject so, string propName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        private static void SetupHomeScreenPanel(Transform screenManager)
        {
            Transform panel = screenManager.Find("HomeScreenPanel  New") ?? screenManager.Find("HomeScreenPanel_New");
            if (panel == null) return;

            HomeScreenAnimator animator = panel.GetComponent<HomeScreenAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<HomeScreenAnimator>();
            }

            MainMenuScreen mainMenu = panel.GetComponent<MainMenuScreen>();

            SerializedObject so = new SerializedObject(animator);
            Transform bgLayer1 = screenManager.Find("Backagrond/Layer 01") ?? screenManager.Find("Background/Layer 01");
            Transform bgLayer2 = screenManager.Find("Backagrond/Layer 02") ?? screenManager.Find("Background/Layer 02");
            Transform logo = panel.Find("Holder Tittle") ?? panel.Find("Title/Holder Tittle");

            if (bgLayer1 != null) SetObjProp(so, "m_Background", bgLayer1.GetComponent<RectTransform>());
            if (bgLayer2 != null) SetObjProp(so, "m_BackgroundLayer2", bgLayer2.GetComponent<RectTransform>());
            if (logo != null)
            {
                SetObjProp(so, "m_Logo", logo.GetComponent<RectTransform>());
                Transform maskTrans = logo.Find("LogoMask");
                if (maskTrans != null)
                {
                    SetObjProp(so, "m_LogoMask", maskTrans.GetComponent<RectMask2D>());
                }
            }

            Transform btnHolder = panel.Find("Button Holder/Buttons") ?? panel.Find("Button Holder");
            if (btnHolder != null)
            {
                SetButtonRef(so, "m_ContinueButton", btnHolder.Find("Continue") ?? btnHolder.Find("Button Continue"), false);
                SetButtonRef(so, "m_NewGameButton", btnHolder.Find("New Game") ?? btnHolder.Find("Button NewGame"), false);
                SetButtonRef(so, "m_CollectButton", btnHolder.Find("COLLECT") ?? btnHolder.Find("Button Collection"), false);
                SetButtonRef(so, "m_OptionsButton", btnHolder.Find("OPTION") ?? btnHolder.Find("Button Setting"), false);
                SetButtonRef(so, "m_CreditsButton", btnHolder.Find("CREDITES") ?? btnHolder.Find("Button Credits"), false);
                SetButtonRef(so, "m_ExitButton", btnHolder.Find("Exit") ?? btnHolder.Find("Button Quit"), true);
            }

            so.ApplyModifiedProperties();

            if (mainMenu != null)
            {
                SerializedObject menuSo = new SerializedObject(mainMenu);
                SetObjProp(menuSo, "m_HomeScreenAnimator", animator);
                menuSo.ApplyModifiedProperties();
            }
        }

        private static void SetButtonRef(SerializedObject so, string propName, Transform btnTrans, bool isDestructive)
        {
            if (btnTrans == null) return;
            Button btn = btnTrans.GetComponent<Button>();
            if (btn != null)
            {
                SetObjProp(so, propName, btn);
            }

            UIAnimatedButton animBtn = btnTrans.GetComponent<UIAnimatedButton>();
            if (animBtn == null)
            {
                animBtn = btnTrans.gameObject.AddComponent<UIAnimatedButton>();
            }

            SerializedObject btnSo = new SerializedObject(animBtn);
            SetBoolProp(btnSo, "m_IsDestructiveOrBack", isDestructive);
            Transform icon = btnTrans.Find("Select Icon") ?? btnTrans.Find("pointer");
            if (icon != null)
            {
                SetObjProp(btnSo, "m_LeftPointer", icon.GetComponent<RectTransform>());
            }
            btnSo.ApplyModifiedProperties();
        }

        private static void SetupLevelSelection(Transform screenManager)
        {
            Transform panel = screenManager.Find("Level Selection");
            if (panel == null) return;

            LevelSelectionScreenAnimator animator = panel.GetComponent<LevelSelectionScreenAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<LevelSelectionScreenAnimator>();
            }

            LevelSelectionScreen levelScreen = panel.GetComponent<LevelSelectionScreen>();

            SerializedObject so = new SerializedObject(animator);
            Transform holder = panel.Find("Holder") ?? panel;

            Transform bg = holder.Find("BG");
            Transform arc = holder.Find("IMG Arc");
            Transform levels = holder.Find("Setting IMG");
            Transform title = holder.Find("Tittle IMG");
            Transform back = holder.Find("B Back") ?? holder.Find("Back B");
            Transform grid = panel.Find("LevelGrid");

            if (bg != null) SetObjProp(so, "m_PanelBackground", bg.GetComponent<RectTransform>());
            if (arc != null) SetObjProp(so, "m_ArcSign", arc.GetComponent<RectTransform>());
            if (levels != null) SetObjProp(so, "m_LevelsSign", levels.GetComponent<RectTransform>());
            if (title != null) SetObjProp(so, "m_TitleLogo", title.GetComponent<RectTransform>());
            if (back != null)
            {
                SetObjProp(so, "m_BackButton", back.GetComponent<RectTransform>());
                EnsureBackButton(back);
            }
            if (grid != null)
            {
                SetObjProp(so, "m_GridContainer", grid.GetComponent<RectTransform>());
            }

            Transform next = holder.Find("B Next");
            Transform prev = holder.Find("B Perivous") ?? holder.Find("B Previous");
            if (next != null) SetObjProp(so, "m_NextButton", next.GetComponent<RectTransform>());
            if (prev != null) SetObjProp(so, "m_PrevButton", prev.GetComponent<RectTransform>());

            so.ApplyModifiedProperties();

            if (levelScreen != null)
            {
                SerializedObject screenSo = new SerializedObject(levelScreen);
                SetObjProp(screenSo, "m_Animator", animator);
                if (back != null) SetObjProp(screenSo, "m_BackButton", back.GetComponent<Button>());
                LevelSelection.LevelSelectionManager manager = panel.GetComponent<LevelSelection.LevelSelectionManager>() ?? panel.GetComponentInChildren<LevelSelection.LevelSelectionManager>(true);
                if (manager != null)
                {
                    SetObjProp(screenSo, "m_LevelSelectionManager", manager);
                    SerializedObject mgrSo = new SerializedObject(manager);
                    SetObjProp(mgrSo, "animator", animator);
                    mgrSo.ApplyModifiedProperties();
                }
                screenSo.ApplyModifiedProperties();
            }
        }

        private static void SetupCollectionPanel(Transform screenManager)
        {
            Transform panel = screenManager.Find("Collection Panel");
            if (panel == null) return;

            CollectionScreenAnimator animator = panel.GetComponent<CollectionScreenAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<CollectionScreenAnimator>();
            }

            CollectionScreen collScreen = panel.GetComponent<CollectionScreen>();

            SerializedObject so = new SerializedObject(animator);
            Transform holder = panel.Find("Holder") ?? panel;

            Transform bg = holder.Find("BG");
            Transform sign = holder.Find("Setting IMG");
            Transform robotCollection = holder.Find("RobotCollection");
            Transform total = holder.Find("RobotCollection/Total");
            Transform back = holder.Find("Back B") ?? holder.Find("B Back");

            // Elevate RobotCollection to centered vertical position (Y = +60, size = 1200x450)
            if (robotCollection != null)
            {
                RectTransform rcRt = robotCollection.GetComponent<RectTransform>();
                if (rcRt != null)
                {
                    rcRt.anchoredPosition = new Vector2(0f, 60f);
                    rcRt.sizeDelta = new Vector2(1200f, 450f);
                }
            }

            // Center and style Total counter
            if (total != null)
            {
                TMPro.TMP_Text tmp = total.GetComponent<TMPro.TMP_Text>();
                if (tmp != null)
                {
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.fontSize = 24f;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    tmp.color = new Color(0.85f, 0.94f, 1f, 0.95f);
                }
            }

            // Format total counter on RobotCollectionView
            Collectables.RobotCollectionView rcView = holder.GetComponentInChildren<Collectables.RobotCollectionView>();
            if (rcView != null)
            {
                SerializedObject viewSo = new SerializedObject(rcView);
                SerializedProperty formatProp = viewSo.FindProperty("m_TotalFormat");
                if (formatProp != null)
                {
                    formatProp.stringValue = "TOTAL CHASSIS RECOVERED: {0} / {1}";
                }
                viewSo.ApplyModifiedProperties();
            }

            Transform robots = holder.Find("RobotCollection/Robots");
            if (robots != null)
            {
                RectTransform rbRt = robots.GetComponent<RectTransform>();
                if (rbRt != null)
                {
                    rbRt.sizeDelta = new Vector2(1200f, 370f);
                }
                LayoutElement rbLe = robots.GetComponent<LayoutElement>();
                if (rbLe == null) rbLe = robots.gameObject.AddComponent<LayoutElement>();
                rbLe.preferredHeight = 370f;

                HorizontalLayoutGroup hlg = robots.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.spacing = 34f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }
            }

            Transform echo = robots != null ? robots.Find("Slot_Echo") : null;
            Transform nova = robots != null ? robots.Find("Slot_Nova") : null;
            Transform patch = robots != null ? robots.Find("Slot_Patch") : null;
            Transform pixel = robots != null ? robots.Find("Slot_Pixel") : null;

            Transform[] allSlots = new Transform[] { echo, nova, patch, pixel };
            foreach (var slot in allSlots)
            {
                if (slot == null) continue;
                RectTransform sRt = slot.GetComponent<RectTransform>();
                if (sRt != null) sRt.sizeDelta = new Vector2(270f, 370f);

                LayoutElement le = slot.GetComponent<LayoutElement>();
                if (le == null) le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 270f;
                le.preferredHeight = 370f;

                Transform portrait = slot.Find("Portrait");
                if (portrait != null)
                {
                    RectTransform pRt = portrait.GetComponent<RectTransform>();
                    if (pRt != null) pRt.sizeDelta = new Vector2(260f, 260f);

                    LayoutElement pLe = portrait.GetComponent<LayoutElement>();
                    if (pLe != null)
                    {
                        pLe.preferredWidth = 260f;
                        pLe.preferredHeight = 260f;
                    }
                }
            }

            if (bg != null) SetObjProp(so, "m_PanelBackground", bg.GetComponent<RectTransform>());
            if (sign != null) SetObjProp(so, "m_CollectionSign", sign.GetComponent<RectTransform>());
            if (total != null) SetObjProp(so, "m_TotalCounter", total.GetComponent<RectTransform>());
            if (back != null)
            {
                SetObjProp(so, "m_BackButton", back.GetComponent<RectTransform>());
                EnsureBackButton(back);
            }

            CollectionCardSelectable cEcho = EnsureCardSelectable(echo);
            CollectionCardSelectable cNova = EnsureCardSelectable(nova);
            CollectionCardSelectable cPatch = EnsureCardSelectable(patch);
            CollectionCardSelectable cPixel = EnsureCardSelectable(pixel);

            if (echo != null) SetObjProp(so, "m_CardEcho", echo.GetComponent<RectTransform>());
            if (nova != null) SetObjProp(so, "m_CardNova", nova.GetComponent<RectTransform>());
            if (patch != null) SetObjProp(so, "m_CardPatch", patch.GetComponent<RectTransform>());
            if (pixel != null) SetObjProp(so, "m_CardPixel", pixel.GetComponent<RectTransform>());

            so.ApplyModifiedProperties();

            Button backBtn = back != null ? back.GetComponent<Button>() : null;
            if (backBtn != null && cEcho != null && cNova != null && cPatch != null && cPixel != null)
            {
                Navigation navBack = backBtn.navigation;
                navBack.mode = Navigation.Mode.Explicit;
                navBack.selectOnDown = cEcho;
                backBtn.navigation = navBack;

                Navigation navEcho = cEcho.navigation;
                navEcho.mode = Navigation.Mode.Explicit;
                navEcho.selectOnUp = backBtn;
                navEcho.selectOnRight = cNova;
                cEcho.navigation = navEcho;

                Navigation navNova = cNova.navigation;
                navNova.mode = Navigation.Mode.Explicit;
                navNova.selectOnUp = backBtn;
                navNova.selectOnLeft = cEcho;
                navNova.selectOnRight = cPatch;
                cNova.navigation = navNova;

                Navigation navPatch = cPatch.navigation;
                navPatch.mode = Navigation.Mode.Explicit;
                navPatch.selectOnUp = backBtn;
                navPatch.selectOnLeft = cNova;
                navPatch.selectOnRight = cPixel;
                cPatch.navigation = navPatch;

                Navigation navPixel = cPixel.navigation;
                navPixel.mode = Navigation.Mode.Explicit;
                navPixel.selectOnUp = backBtn;
                navPixel.selectOnLeft = cPatch;
                cPixel.navigation = navPixel;
            }

            animator.EnsureHangarElements();

            if (collScreen != null)
            {
                SerializedObject screenSo = new SerializedObject(collScreen);
                SetObjProp(screenSo, "m_Animator", animator);
                if (back != null) SetObjProp(screenSo, "m_BackButton", back.GetComponent<Button>());
                screenSo.ApplyModifiedProperties();
            }
        }

        private static CollectionCardSelectable EnsureCardSelectable(Transform cardTrans)
        {
            if (cardTrans == null) return null;
            CollectionCardSelectable card = cardTrans.GetComponent<CollectionCardSelectable>();
            if (card == null)
            {
                card = cardTrans.gameObject.AddComponent<CollectionCardSelectable>();
            }
            return card;
        }

        private static void SetupSettingsPanel(Transform screenManager)
        {
            Transform panel = screenManager.Find("SettingsPanel");
            if (panel == null) return;

            SettingsScreenAnimator animator = panel.GetComponent<SettingsScreenAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<SettingsScreenAnimator>();
            }

            OptionsScreen optionsScreen = panel.GetComponent<OptionsScreen>();

            SerializedObject so = new SerializedObject(animator);
            Transform holder = panel.Find("Holder") ?? panel;

            Transform bg = holder.Find("BG");
            Transform sign = holder.Find("Setting IMG");
            Transform back = holder.Find("Back B") ?? holder.Find("B Back");

            if (bg != null) SetObjProp(so, "m_PanelBackground", bg.GetComponent<RectTransform>());
            if (sign != null) SetObjProp(so, "m_SettingsSign", sign.GetComponent<RectTransform>());
            if (back != null)
            {
                SetObjProp(so, "m_BackButton", back.GetComponent<RectTransform>());
                EnsureBackButton(back);
            }

            Transform ctrlHolder = holder.Find("Controlles Holder");
            if (ctrlHolder != null)
            {
                List<RectTransform> rows = new List<RectTransform>();
                string[] rowNames = new string[] { "Master Volume", "Music", "Sound FX", "Brithtness" };
                foreach (var rName in rowNames)
                {
                    Transform rowTrans = ctrlHolder.Find(rName);
                    if (rowTrans != null)
                    {
                        rows.Add(rowTrans.GetComponent<RectTransform>());
                    }
                }

                SerializedProperty rowsProp = so.FindProperty("m_SettingsRows");
                if (rowsProp != null)
                {
                    rowsProp.arraySize = rows.Count;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                    }
                }
            }

            so.ApplyModifiedProperties();

            if (optionsScreen != null)
            {
                SerializedObject screenSo = new SerializedObject(optionsScreen);
                SetObjProp(screenSo, "m_Animator", animator);
                screenSo.ApplyModifiedProperties();
            }
        }

        private static void SetupCreditsPanel(Transform screenManager)
        {
            Transform panel = screenManager.Find("Credits panel");
            if (panel == null) return;

            CreditsScreenAnimator animator = panel.GetComponent<CreditsScreenAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<CreditsScreenAnimator>();
            }

            CreditsScreen creditsScreen = panel.GetComponent<CreditsScreen>();

            SerializedObject so = new SerializedObject(animator);
            Transform bg = panel.Find("BG") ?? panel;
            Transform holder = bg.Find("Holder") ?? bg;

            Transform sign = holder.Find("Setting IMG");
            Transform content = holder.Find("Controlles Holder");
            Transform back = holder.Find("Back B") ?? holder.Find("B Back");

            if (bg != null) SetObjProp(so, "m_PanelBackground", bg.GetComponent<RectTransform>());
            if (sign != null) SetObjProp(so, "m_CreditsSign", sign.GetComponent<RectTransform>());
            if (content != null) SetObjProp(so, "m_ContentContainer", content.GetComponent<RectTransform>());
            if (back != null)
            {
                SetObjProp(so, "m_BackButton", back.GetComponent<RectTransform>());
                EnsureBackButton(back);
            }

            so.ApplyModifiedProperties();

            if (creditsScreen != null)
            {
                SerializedObject screenSo = new SerializedObject(creditsScreen);
                SetObjProp(screenSo, "m_Animator", animator);
                screenSo.ApplyModifiedProperties();
            }
        }

        private static void SetupConfirmationPopup(Transform screenManager)
        {
            Transform panel = screenManager.Find("ConfirmationPopup");
            if (panel == null) return;

            ConfirmationPopupAnimator animator = panel.GetComponent<ConfirmationPopupAnimator>();
            if (animator == null)
            {
                animator = panel.gameObject.AddComponent<ConfirmationPopupAnimator>();
            }

            ConfirmationPopupScreen popupScreen = panel.GetComponent<ConfirmationPopupScreen>();

            SerializedObject so = new SerializedObject(animator);
            CanvasGroup overlay = panel.GetComponent<CanvasGroup>();
            Transform dialog = panel.Find("Dialog");
            Transform msg = dialog != null ? (dialog.Find("Message") ?? dialog.Find("Title")) : null;
            Transform yesBtn = dialog != null ? dialog.Find("YesButton") : null;
            Transform noBtn = dialog != null ? dialog.Find("NoButton") : null;

            if (overlay != null) SetObjProp(so, "m_DarkOverlay", overlay);
            if (dialog != null) SetObjProp(so, "m_DialogWindow", dialog.GetComponent<RectTransform>());
            if (msg != null) SetObjProp(so, "m_TitleImage", msg.GetComponent<RectTransform>());

            if (yesBtn != null)
            {
                SetObjProp(so, "m_YesButton", yesBtn.GetComponent<RectTransform>());
                UIAnimatedButton anim = yesBtn.GetComponent<UIAnimatedButton>() ?? yesBtn.gameObject.AddComponent<UIAnimatedButton>();
                SerializedObject btnSo = new SerializedObject(anim);
                SetBoolProp(btnSo, "m_IsDestructiveOrBack", true);
                btnSo.ApplyModifiedProperties();
            }

            if (noBtn != null)
            {
                SetObjProp(so, "m_NoButton", noBtn.GetComponent<RectTransform>());
                UIAnimatedButton anim = noBtn.GetComponent<UIAnimatedButton>() ?? noBtn.gameObject.AddComponent<UIAnimatedButton>();
                SerializedObject btnSo = new SerializedObject(anim);
                SetBoolProp(btnSo, "m_IsDestructiveOrBack", false);
                btnSo.ApplyModifiedProperties();
            }

            so.ApplyModifiedProperties();

            if (popupScreen != null)
            {
                SerializedObject screenSo = new SerializedObject(popupScreen);
                SetObjProp(screenSo, "m_Animator", animator);
                screenSo.ApplyModifiedProperties();
            }
        }

        private static void EnsureBackButton(Transform backTrans)
        {
            if (backTrans == null) return;
            UIAnimatedButton animBtn = backTrans.GetComponent<UIAnimatedButton>();
            if (animBtn == null)
            {
                animBtn = backTrans.gameObject.AddComponent<UIAnimatedButton>();
            }

            SerializedObject so = new SerializedObject(animBtn);
            SetBoolProp(so, "m_IsDestructiveOrBack", true);
            Transform pointer = backTrans.Find("pointer") ?? backTrans.Find("Select Icon");
            if (pointer != null)
            {
                SetObjProp(so, "m_LeftPointer", pointer.GetComponent<RectTransform>());
            }
            so.ApplyModifiedProperties();
        }

        private static void SetupUINavigationManager(Transform uiManagers)
        {
            UINavigationManager nav = uiManagers.GetComponent<UINavigationManager>();
            if (nav == null) return;

            SerializedObject so = new SerializedObject(nav);
            AudioClip keyPress = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Test/Audio/KeyPressSound.mp3");
            AudioClip wrong = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Test/Audio/Wrong.mp3");
            AudioClip keyCollect = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Test/Audio/KeyCollect.mp3");

            if (keyPress != null)
            {
                SetObjProp(so, "m_ButtonNavigateSound", keyPress);
                SetObjProp(so, "m_ButtonSubmitSound", keyPress);
            }
            if (wrong != null)
            {
                SetObjProp(so, "m_BackSound", wrong);
                SetObjProp(so, "m_PanelCloseSound", wrong);
            }
            if (keyCollect != null)
            {
                SetObjProp(so, "m_PanelOpenSound", keyCollect);
            }

            so.ApplyModifiedProperties();
        }
    }
}
