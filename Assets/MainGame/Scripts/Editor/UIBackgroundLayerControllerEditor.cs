using UnityEditor;
using UnityEngine;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Editor
{
    [CustomEditor(typeof(UIBackgroundLayerController))]
    [CanEditMultipleObjects]
    public class UIBackgroundLayerControllerEditor : UnityEditor.Editor
    {
        private static readonly (CinematicThemePreset preset, string label, Color color)[] s_Presets = new[]
        {
            (CinematicThemePreset.CyberCyan, "Cyber Cyan", new Color(0.15f, 0.88f, 1f)),
            (CinematicThemePreset.VillainCrimson, "Villain Crimson", new Color(1f, 0.165f, 0.28f)),
            (CinematicThemePreset.ElectricPurple, "Electric Purple", new Color(0.72f, 0.2f, 1f)),
            (CinematicThemePreset.PlasmaBlue, "Plasma Blue", new Color(0.22f, 0.63f, 1f)),
            (CinematicThemePreset.ToxicGreen, "Toxic Green", new Color(0.22f, 1f, 0.08f)),
            (CinematicThemePreset.GoldenPower, "Golden Power", new Color(1f, 0.78f, 0.23f)),
            (CinematicThemePreset.OrangeEnergy, "Orange Energy", new Color(1f, 0.48f, 0.1f)),
            (CinematicThemePreset.Magenta, "Magenta", new Color(1f, 0.08f, 0.58f)),
            (CinematicThemePreset.Ice, "Ice", new Color(0.66f, 0.94f, 1f)),
            (CinematicThemePreset.RetroArcade, "Retro Arcade", new Color(1f, 0.9f, 0f)),
            (CinematicThemePreset.Industrial, "Industrial", new Color(1f, 0.34f, 0.13f)),
            (CinematicThemePreset.NightTech, "Night Tech", new Color(0f, 0.9f, 1f))
        };

        public override void OnInspectorGUI()
        {
            UIBackgroundLayerController ctrl = (UIBackgroundLayerController)target;

            serializedObject.Update();

            // ─── 1. STATUS & PERMISSION BANNER ─────────────────────────────────
            EditorGUILayout.Space(4);
            if (ctrl.IsGlitchPermitted())
            {
                EditorGUILayout.HelpBox(
                    $"GLITCH & SCAN PERMITTED (Profile: {ctrl.Profile})\nThis background layer is authorized for digital glitch distortion and scanline bursts.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"PROTECTED ARTWORK - NO SCAN / NO GLITCH (Profile: {ctrl.Profile})\nGlitch, scanlines, and noise are strictly stripped from this layer to preserve clean pixel-art artwork. Only Living Outline & Inner Rim lighting are active.",
                    MessageType.None);
            }

            // ─── 2. THEME TESTING & 1-CLICK PALETTES ────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Live Theme Testing (1-Click Switch)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            CinematicThemePreset newPreset = (CinematicThemePreset)EditorGUILayout.EnumPopup("Active Theme Preset", ctrl.ThemePreset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ctrl, "Change Theme Preset");
                ctrl.ApplyThemePreset(newPreset);
                EditorUtility.SetDirty(ctrl);
                SceneView.RepaintAll();
            }

            // Preset Quick Buttons (2 columns)
            EditorGUILayout.Space(2);
            for (int i = 0; i < s_Presets.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < 2 && (i + j) < s_Presets.Length; j++)
                {
                    var p = s_Presets[i + j];
                    GUI.backgroundColor = (ctrl.ThemePreset == p.preset) ? Color.yellow : Color.white;
                    if (GUILayout.Button(p.label, GUILayout.Height(24)))
                    {
                        Undo.RecordObject(ctrl, "Apply Theme Preset Button");
                        ctrl.ApplyThemePreset(p.preset);
                        EditorUtility.SetDirty(ctrl);
                        SceneView.RepaintAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎲 Pick Random Theme", GUILayout.Height(26)))
            {
                Undo.RecordObject(ctrl, "Pick Random Theme");
                ctrl.ApplyRandomTheme(smooth: Application.isPlaying, duration: 0.8f);
                EditorUtility.SetDirty(ctrl);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("🎲 Randomize All Themes", GUILayout.Height(26)))
            {
                var director = MainMenuBackgroundDirector.Instance ?? UnityEngine.Object.FindObjectOfType<MainMenuBackgroundDirector>();
                if (director != null)
                {
                    Undo.RecordObject(director, "Randomize All Menu Themes");
                    director.RandomizeAllThemes(immediate: !Application.isPlaying);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (ctrl.LinkedOutlineLayer != null)
            {
                EditorGUILayout.Space(2);
                if (GUILayout.Button("Sync Active Theme to Linked Outline Layer", GUILayout.Height(26)))
                {
                    Undo.RecordObject(ctrl.LinkedOutlineLayer, "Sync Theme to Linked Outline");
                    ctrl.LinkedOutlineLayer.ApplyThemePreset(ctrl.ThemePreset);
                    EditorUtility.SetDirty(ctrl.LinkedOutlineLayer);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Outline & Rim Management", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure / Setup Linked Outline Layer", GUILayout.Height(26)))
            {
                Undo.RecordObject(ctrl, "Ensure Outline Layer");
                ctrl.EnsureOutlineLayer();
                EditorUtility.SetDirty(ctrl);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6);
            // ─── 3. DEFAULT INSPECTOR PROPERTIES ───────────────────────────────
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                ctrl.ApplyThemeColors();
                ctrl.SyncShaderProperties();
                EditorUtility.SetDirty(ctrl);
                SceneView.RepaintAll();
            }
        }
    }
}
