using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MainGame.UI.PauseMenu;
using MainGame.UI.Unified;
using LevelSelection;

namespace MainGame.Editor
{
    public static class PauseMenuPrefabSetup
    {
        private const string PREFAB_PATH = "Assets/MainGame/Prefabs/UI/Pause Menu.prefab";
        private const string INPUT_ASSET_PATH = "Assets/New Input System/PlayerInputAction.inputactions";
        private const string RESET_SPRITE_PATH = "Assets/MainGame/Sprites/UI buttons/02.1Pause Menu/02.1Pause Menu/RESET.png";
        private const string LEVEL_SPRITE_PATH = "Assets/MainGame/Sprites/UI buttons/00 Marked Asssets for UI/BACK TO LEVEL SELECTION.png";
        private const string EXIT_SPRITE_PATH = "Assets/MainGame/Sprites/UI buttons/02.1Pause Menu/02.1Pause Menu/EXIT.png";

        private const string k_RunSessionKey = "PauseMenuPrefabSetup_Completed_v1";

        [InitializeOnLoadMethod]
        private static void OnProjectLoaded()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(k_RunSessionKey, false))
                {
                    SessionState.SetBool(k_RunSessionKey, true);
                    ConfigurePauseMenuPrefab();
                }
            };
        }

        [MenuItem("Tools/Pause Menu/Configure Production Prefab")]
        public static void ConfigurePauseMenuPrefab()
        {
            Debug.Log("[PauseMenuPrefabSetup] Starting configuration of Pause Menu prefab...");
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            if (root == null)
            {
                Debug.LogError($"[PauseMenuPrefabSetup] Failed to load prefab at {PREFAB_PATH}");
                return;
            }

            try
            {
                // 1. Root GameObject Controllers
                PauseMenuController master = root.GetComponent<PauseMenuController>();
                if (master == null) master = root.AddComponent<PauseMenuController>();

                PauseAudioController audioCtrl = root.GetComponent<PauseAudioController>();
                if (audioCtrl == null) audioCtrl = root.AddComponent<PauseAudioController>();

                PauseNavigationController navCtrl = root.GetComponent<PauseNavigationController>();
                if (navCtrl == null) navCtrl = root.AddComponent<PauseNavigationController>();

                PauseInputController inputCtrl = root.GetComponent<PauseInputController>();
                if (inputCtrl == null) inputCtrl = root.AddComponent<PauseInputController>();

                // 2. Pause Screen Child
                Transform pauseScreen = root.transform.Find("Pause screen");
                if (pauseScreen != null)
                {
                    // Remove legacy PauseMenuScreen to prevent duplicate manager
                    PauseMenuScreen legacyScreen = pauseScreen.GetComponent<PauseMenuScreen>();
                    if (legacyScreen != null)
                    {
                        Object.DestroyImmediate(legacyScreen, true);
                    }

                    // Ensure PauseMenuAnimator exists
                    PauseMenuAnimator animator = pauseScreen.GetComponent<PauseMenuAnimator>();
                    if (animator == null) animator = pauseScreen.gameObject.AddComponent<PauseMenuAnimator>();

                    // Ensure CanvasGroup exists
                    CanvasGroup overlayGroup = pauseScreen.GetComponent<CanvasGroup>();
                    if (overlayGroup == null) overlayGroup = pauseScreen.gameObject.AddComponent<CanvasGroup>();

                    // 3. Pause Panel & Buttons
                    Transform pausePanel = pauseScreen.Find("Pause panel");
                    Button resetBtn = null;
                    Button levelBtn = null;
                    Button exitBtn = null;

                    if (pausePanel != null)
                    {
                        Transform resetT = pausePanel.Find("Reset");
                        Transform levelT = pausePanel.Find("Level");
                        Transform exitT = pausePanel.Find("Exit");

                        if (resetT != null)
                        {
                            resetBtn = resetT.GetComponent<Button>();
                            SetupButtonAnimator(resetT.gameObject, isExit: false);
                        }
                        if (levelT != null)
                        {
                            levelBtn = levelT.GetComponent<Button>();
                            SetupButtonAnimator(levelT.gameObject, isExit: false);
                        }
                        if (exitT != null)
                        {
                            exitBtn = exitT.GetComponent<Button>();
                            SetupButtonAnimator(exitT.gameObject, isExit: true);
                        }
                    }

                    // 4. Confirmation Popup
                    Transform confirmTransform = pauseScreen.Find("ConfirmationPopup");
                    PauseConfirmationController confirmCtrl = null;
                    if (confirmTransform != null)
                    {
                        confirmCtrl = confirmTransform.GetComponent<PauseConfirmationController>();
                        if (confirmCtrl == null) confirmCtrl = confirmTransform.gameObject.AddComponent<PauseConfirmationController>();

                        ConfirmationPopupAnimator popupAnim = confirmTransform.GetComponent<ConfirmationPopupAnimator>();
                        if (popupAnim == null) popupAnim = confirmTransform.gameObject.AddComponent<ConfirmationPopupAnimator>();

                        // Wire confirmation popup animator fields
                        SerializedObject serializedPopupAnim = new SerializedObject(popupAnim);
                        CanvasGroup confirmGroup = confirmTransform.GetComponent<CanvasGroup>();
                        serializedPopupAnim.FindProperty("m_DarkOverlay").objectReferenceValue = confirmGroup;
                        serializedPopupAnim.FindProperty("m_OverlayMaxAlpha").floatValue = 0.85f;

                        Transform dialog = confirmTransform.Find("Dialog");
                        if (dialog != null)
                        {
                            serializedPopupAnim.FindProperty("m_DialogWindow").objectReferenceValue = dialog as RectTransform;
                            Transform title = dialog.Find("Title") ?? dialog.Find("Message");
                            if (title != null) serializedPopupAnim.FindProperty("m_TitleImage").objectReferenceValue = title as RectTransform;
                            Transform yes = dialog.Find("YesButton");
                            if (yes != null) serializedPopupAnim.FindProperty("m_YesButton").objectReferenceValue = yes as RectTransform;
                            Transform no = dialog.Find("NoButton");
                            if (no != null) serializedPopupAnim.FindProperty("m_NoButton").objectReferenceValue = no as RectTransform;
                        }
                        serializedPopupAnim.ApplyModifiedPropertiesWithoutUndo();

                        // Wire PauseConfirmationController fields
                        SerializedObject serializedConfirm = new SerializedObject(confirmCtrl);
                        serializedConfirm.FindProperty("m_PopupCanvasGroup").objectReferenceValue = confirmGroup;
                        if (dialog != null)
                        {
                            serializedConfirm.FindProperty("m_DialogTransform").objectReferenceValue = dialog as RectTransform;
                            Transform title = dialog.Find("Title") ?? dialog.Find("Message");
                            if (title != null) serializedConfirm.FindProperty("m_TitleImage").objectReferenceValue = title.GetComponent<Image>();
                            Transform yes = dialog.Find("YesButton");
                            if (yes != null) serializedConfirm.FindProperty("m_YesButton").objectReferenceValue = yes.GetComponent<Button>();
                            Transform no = dialog.Find("NoButton");
                            if (no != null) serializedConfirm.FindProperty("m_NoButton").objectReferenceValue = no.GetComponent<Button>();
                        }
                        serializedConfirm.FindProperty("m_Animator").objectReferenceValue = popupAnim;
                        serializedConfirm.FindProperty("m_Audio").objectReferenceValue = audioCtrl;
                        serializedConfirm.ApplyModifiedPropertiesWithoutUndo();

                        confirmCtrl.BuildHorizontalNavigation();
                    }

                    // 5. Level Selection Panel
                    Transform levelSelectTransform = pauseScreen.Find("Level Selection");
                    PauseLevelSelectionController levelSelectCtrl = null;
                    if (levelSelectTransform != null)
                    {
                        levelSelectCtrl = levelSelectTransform.GetComponent<PauseLevelSelectionController>();
                        if (levelSelectCtrl == null) levelSelectCtrl = levelSelectTransform.gameObject.AddComponent<PauseLevelSelectionController>();

                        SerializedObject serializedLS = new SerializedObject(levelSelectCtrl);
                        CanvasGroup lsGroup = levelSelectTransform.GetComponent<CanvasGroup>();
                        serializedLS.FindProperty("m_CanvasGroup").objectReferenceValue = lsGroup;

                        LevelSelectionManager lsm = levelSelectTransform.GetComponent<LevelSelectionManager>();
                        serializedLS.FindProperty("m_LevelSelectionManager").objectReferenceValue = lsm;

                        LevelSelectionScreenAnimator lsAnim = levelSelectTransform.GetComponent<LevelSelectionScreenAnimator>();
                        serializedLS.FindProperty("m_Animator").objectReferenceValue = lsAnim;

                        Transform backBtn = levelSelectTransform.Find("Holder/B Back") ?? levelSelectTransform.Find("B Back");
                        if (backBtn != null)
                        {
                            serializedLS.FindProperty("m_BackButton").objectReferenceValue = backBtn.GetComponent<Button>();
                        }
                        serializedLS.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // 6. Assets
                    InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ASSET_PATH);
                    Sprite resetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RESET_SPRITE_PATH);
                    Sprite levelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LEVEL_SPRITE_PATH);
                    Sprite exitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EXIT_SPRITE_PATH);

                    // 7. Wire PauseMenuController
                    SerializedObject serializedMaster = new SerializedObject(master);
                    serializedMaster.FindProperty("m_HomeScreenName").stringValue = "HomeScreen";
                    serializedMaster.FindProperty("m_PausePanel").objectReferenceValue = pausePanel as RectTransform;
                    serializedMaster.FindProperty("m_DarkOverlay").objectReferenceValue = overlayGroup;
                    serializedMaster.FindProperty("m_ResetButton").objectReferenceValue = resetBtn;
                    serializedMaster.FindProperty("m_LevelsButton").objectReferenceValue = levelBtn;
                    serializedMaster.FindProperty("m_ExitButton").objectReferenceValue = exitBtn;
                    serializedMaster.FindProperty("m_Audio").objectReferenceValue = audioCtrl;
                    serializedMaster.FindProperty("m_Navigation").objectReferenceValue = navCtrl;
                    serializedMaster.FindProperty("m_Confirmation").objectReferenceValue = confirmCtrl;
                    serializedMaster.FindProperty("m_LevelSelection").objectReferenceValue = levelSelectCtrl;
                    serializedMaster.FindProperty("m_Input").objectReferenceValue = inputCtrl;
                    serializedMaster.FindProperty("m_Animator").objectReferenceValue = animator;
                    serializedMaster.FindProperty("m_ResetTitleSprite").objectReferenceValue = resetSprite;
                    serializedMaster.FindProperty("m_LevelsTitleSprite").objectReferenceValue = levelSprite;
                    serializedMaster.FindProperty("m_ExitTitleSprite").objectReferenceValue = exitSprite;
                    serializedMaster.FindProperty("m_InputActionAsset").objectReferenceValue = inputAsset;
                    serializedMaster.ApplyModifiedPropertiesWithoutUndo();

                    // 8. Wire Navigation & Input controllers
                    SerializedObject serializedNav = new SerializedObject(navCtrl);
                    serializedNav.FindProperty("m_ResetButton").objectReferenceValue = resetBtn;
                    serializedNav.FindProperty("m_LevelsButton").objectReferenceValue = levelBtn;
                    serializedNav.FindProperty("m_ExitButton").objectReferenceValue = exitBtn;
                    serializedNav.FindProperty("m_InputActionAsset").objectReferenceValue = inputAsset;
                    serializedNav.ApplyModifiedPropertiesWithoutUndo();

                    SerializedObject serializedInput = new SerializedObject(inputCtrl);
                    serializedInput.FindProperty("m_InputActionAsset").objectReferenceValue = inputAsset;
                    serializedInput.ApplyModifiedPropertiesWithoutUndo();

                    navCtrl.BuildVerticalNavigationLoop();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
                Debug.Log("[PauseMenuPrefabSetup] Successfully configured and saved Pause Menu prefab!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void SetupButtonAnimator(GameObject go, bool isExit)
        {
            // Disable conflicting legacy animators
            UIAnimatedButton legacyAnim = go.GetComponent<UIAnimatedButton>();
            if (legacyAnim != null) legacyAnim.enabled = false;

            UIButtonEffect legacyEffect = go.GetComponent<UIButtonEffect>();
            if (legacyEffect != null) legacyEffect.enabled = false;

            foreach (var childEffect in go.GetComponentsInChildren<UIButtonEffect>(true))
            {
                childEffect.enabled = false;
            }

            // Attach / configure MainMenuButtonEnergyAnimator
            MainMenuButtonEnergyAnimator energy = go.GetComponent<MainMenuButtonEnergyAnimator>();
            if (energy == null) energy = go.AddComponent<MainMenuButtonEnergyAnimator>();

            SerializedObject serializedEnergy = new SerializedObject(energy);
            serializedEnergy.FindProperty("m_IsExitButton").boolValue = isExit;
            serializedEnergy.FindProperty("m_StartOffset").vector2Value = new Vector2(-24f, 0f);
            serializedEnergy.FindProperty("m_StartScale").floatValue = 0.90f;
            serializedEnergy.FindProperty("m_StartAlpha").floatValue = 0.35f;
            serializedEnergy.FindProperty("m_SnapDuration").floatValue = 0.14f;
            serializedEnergy.FindProperty("m_OvershootScale").floatValue = 1.05f;
            serializedEnergy.FindProperty("m_SettleDuration").floatValue = 0.08f;
            serializedEnergy.FindProperty("m_FocusDuration").floatValue = 0.13f;
            serializedEnergy.FindProperty("m_ConfirmDuration").floatValue = 0.17f;
            serializedEnergy.FindProperty("m_ConfirmImpactScale").floatValue = 1.08f;
            serializedEnergy.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
