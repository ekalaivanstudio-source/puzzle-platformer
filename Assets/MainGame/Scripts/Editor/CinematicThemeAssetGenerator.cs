using System;
using System.IO;
using MainGame.UI.CinematicEffects;
using UnityEditor;
using UnityEngine;

namespace MainGame.UI.Editor
{
    public static class CinematicThemeAssetGenerator
    {
        public const string k_ThemeFolder = "Assets/MainGame/Data/UIThemes";

        [MenuItem("Tools/UI/Generate Cinematic Color Theme Assets")]
        public static void GenerateAllThemesMenu()
        {
            GenerateAllThemes(true);
        }

        public static void GenerateAllThemes(bool notifyUser)
        {
            if (!Directory.Exists(k_ThemeFolder))
            {
                Directory.CreateDirectory(k_ThemeFolder);
                AssetDatabase.Refresh();
            }

            int count = 0;
            foreach (CinematicThemePreset preset in Enum.GetValues(typeof(CinematicThemePreset)))
            {
                if (preset == CinematicThemePreset.Custom) continue;

                string assetPath = $"{k_ThemeFolder}/Theme_{preset}.asset";
                CinematicUIColorTheme theme = AssetDatabase.LoadAssetAtPath<CinematicUIColorTheme>(assetPath);

                bool isNew = false;
                if (theme == null)
                {
                    theme = ScriptableObject.CreateInstance<CinematicUIColorTheme>();
                    isNew = true;
                }

                theme.ApplyPreset(preset);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(theme, assetPath);
                }
                else
                {
                    EditorUtility.SetDirty(theme);
                }
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CinematicThemeAssetGenerator] Successfully generated/updated {count} color theme assets in '{k_ThemeFolder}'.");

            if (notifyUser && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Cinematic Color Themes",
                    $"Successfully generated/updated {count} color theme assets in:\n{k_ThemeFolder}\n\n" +
                    "• Cyber Cyan\n" +
                    "• Villain Crimson\n" +
                    "• Electric Purple\n" +
                    "• Plasma Blue\n" +
                    "• Toxic Green\n" +
                    "• Golden Power\n" +
                    "• Orange Energy\n" +
                    "• Magenta\n" +
                    "• Ice\n" +
                    "• Retro Arcade\n" +
                    "• Industrial\n" +
                    "• Night Tech", "OK");
            }
        }

        public static CinematicUIColorTheme GetOrCreateThemeAsset(CinematicThemePreset preset)
        {
            if (preset == CinematicThemePreset.Custom) return null;

            string assetPath = $"{k_ThemeFolder}/Theme_{preset}.asset";
            CinematicUIColorTheme theme = AssetDatabase.LoadAssetAtPath<CinematicUIColorTheme>(assetPath);

            if (theme == null)
            {
                if (!Directory.Exists(k_ThemeFolder))
                {
                    Directory.CreateDirectory(k_ThemeFolder);
                    AssetDatabase.Refresh();
                }

                theme = ScriptableObject.CreateInstance<CinematicUIColorTheme>();
                theme.ApplyPreset(preset);
                AssetDatabase.CreateAsset(theme, assetPath);
                AssetDatabase.SaveAssets();
            }

            return theme;
        }
    }
}
