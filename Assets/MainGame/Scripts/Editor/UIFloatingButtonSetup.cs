using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MainGame.UI;
using MainGame.UI.Unified;

namespace MainGame.Editor
{
    public static class UIFloatingButtonSetup
    {
        [MenuItem("Tools/UI/Attach Floating Effect To All Buttons In Active Scene")]
        public static void AttachFloatingToActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[UIFloatingButtonSetup] No active scene found.");
                return;
            }

            int attachedCount = 0;
            int alreadyConfiguredCount = 0;

            GameObject[] roots = activeScene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.GetComponent<MainMenuButtonEnergyAnimator>() != null ||
                        btn.GetComponent<UIAnimatedButton>() != null)
                    {
                        alreadyConfiguredCount++;
                        continue;
                    }

                    UIFloatingButton floatBtn = btn.GetComponent<UIFloatingButton>();
                    if (floatBtn == null)
                    {
                        btn.gameObject.AddComponent<UIFloatingButton>();
                        attachedCount++;
                    }
                    else
                    {
                        alreadyConfiguredCount++;
                    }
                }
            }

            if (attachedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log($"[UIFloatingButtonSetup] Active scene '{activeScene.name}': {attachedCount} buttons equipped with UIFloatingButton, {alreadyConfiguredCount} already natively floating.");
            EditorUtility.DisplayDialog("Floating Effect Setup",
                $"Successfully scanned '{activeScene.name}'!\n\n• New buttons equipped: {attachedCount}\n• Already floating (MainMenu / UIAnimated): {alreadyConfiguredCount}\n\nAll buttons now have the floating effect!", "OK");
        }

        [MenuItem("Tools/UI/Attach Floating Effect To All UI Prefabs")]
        public static void AttachFloatingToPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/MainGame/Prefabs" });
            int modifiedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool changed = false;
                Button[] buttons = prefabRoot.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.GetComponent<MainMenuButtonEnergyAnimator>() != null ||
                        btn.GetComponent<UIAnimatedButton>() != null ||
                        btn.GetComponent<UIFloatingButton>() != null)
                    {
                        continue;
                    }

                    btn.gameObject.AddComponent<UIFloatingButton>();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    modifiedCount++;
                    Debug.Log($"[UIFloatingButtonSetup] Attached UIFloatingButton to prefab: {path}");
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UIFloatingButtonSetup] Prefab scan complete. {modifiedCount} prefabs updated with UIFloatingButton.");
            EditorUtility.DisplayDialog("Floating Effect Setup",
                $"Prefab scan complete!\n\n• Updated {modifiedCount} UI prefabs with UIFloatingButton.\n\nAll buttons in prefabs now have the floating effect!", "OK");
        }
    }
}
