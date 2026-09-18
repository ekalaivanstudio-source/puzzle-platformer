using System;
using UnityEngine;
using UnityEditor;
using MainGame.UI.CinematicEffects;

namespace MainGame.Editor
{
    [CustomEditor(typeof(MainMenuBackgroundDirector))]
    public class MainMenuBackgroundDirectorEditor : UnityEditor.Editor
    {
        private static readonly (CinematicThemePreset preset, string label)[] s_Presets = new[]
        {
            (CinematicThemePreset.CyberCyan, "Cyan"),
            (CinematicThemePreset.VillainCrimson, "Crimson"),
            (CinematicThemePreset.ElectricPurple, "Purple"),
            (CinematicThemePreset.PlasmaBlue, "Plasma"),
            (CinematicThemePreset.ToxicGreen, "Toxic"),
            (CinematicThemePreset.GoldenPower, "Gold"),
            (CinematicThemePreset.OrangeEnergy, "Orange"),
            (CinematicThemePreset.Magenta, "Magenta"),
            (CinematicThemePreset.Ice, "Ice"),
            (CinematicThemePreset.RetroArcade, "Arcade"),
            (CinematicThemePreset.Industrial, "Industrial"),
            (CinematicThemePreset.NightTech, "NightTech")
        };

        [MenuItem("Tools/Cinematic UI/Setup Main Menu Controllers In Scene")]
        public static void SetupControllersFromMenu()
        {
            var director = MainMenuBackgroundDirector.Instance ?? UnityEngine.Object.FindObjectOfType<MainMenuBackgroundDirector>();
            if (director == null)
            {
                var screenMgr = GameObject.Find("ScreenManager");
                if (screenMgr == null)
                {
                    var canvas = GameObject.Find("Canvas  Main Menu") ?? GameObject.Find("Canvas Main Menu");
                    if (canvas != null)
                    {
                        var smTrans = canvas.transform.Find("ScreenManager");
                        if (smTrans != null) screenMgr = smTrans.gameObject;
                    }
                }
                if (screenMgr != null)
                {
                    director = screenMgr.GetComponent<MainMenuBackgroundDirector>() ?? screenMgr.AddComponent<MainMenuBackgroundDirector>();
                }
            }

            if (director != null)
            {
                director.SetupSceneControllersInEditor();
                SceneView.RepaintAll();
                EditorUtility.DisplayDialog("Cinematic UI", "Successfully set up all UIBackgroundLayerControllers on Hero, Villan, Title, BG Red, and Spark!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Cinematic UI", "Could not find ScreenManager in the active scene. Please open HomeScreen scene.", "OK");
            }
        }

        public override void OnInspectorGUI()
        {
            MainMenuBackgroundDirector director = (MainMenuBackgroundDirector)target;

            // ─── 1. SCENE SETUP BANNER ─────────────────────────────────────────
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f, 1f);
            if (GUILayout.Button("⚡ Setup / Attach Controllers to Scene Objects Now", GUILayout.Height(34)))
            {
                Undo.RecordObject(director, "Setup Scene Controllers");
                director.SetupSceneControllersInEditor();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Click above to find Hero, Villan, Title, BG Red, and Spark in the scene and attach / configure their UIBackgroundLayerControllers in Edit Mode.", MessageType.Info);

            EditorGUILayout.Space(8);

            // ─── 2. THEME TESTING & RANDOMIZATION ──────────────────────────────
            EditorGUILayout.LabelField("Cinematic Theme Management", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f, 1f);
            if (GUILayout.Button("🎲 Randomize All Menu Themes Now", GUILayout.Height(30)))
            {
                Undo.RecordObject(director, "Randomize All Themes");
                director.RandomizeAllThemes(immediate: !Application.isPlaying);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Apply Specific Theme To All Elements (1-Click Preview):", EditorStyles.miniLabel);

            for (int i = 0; i < s_Presets.Length; i += 3)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < 3 && (i + j) < s_Presets.Length; j++)
                {
                    var p = s_Presets[i + j];
                    if (GUILayout.Button(p.label, GUILayout.Height(22)))
                    {
                        Undo.RecordObject(director, "Apply Theme To All");
                        director.ApplyThemePresetToAll(p.preset, immediate: !Application.isPlaying);
                        SceneView.RepaintAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            // ─── 3. LIVE TEST FX ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Live Visual FX Testing", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pulse Hero Outline", GUILayout.Height(24)))
            {
                if (director.HeroOutlineController != null)
                {
                    director.HeroOutlineController.PulseOutlineImmediate(2.0f, eventName: "HeroTest");
                }
            }
            if (GUILayout.Button("Pulse Villain Outline", GUILayout.Height(24)))
            {
                if (director.VillainOutlineController != null)
                {
                    director.VillainOutlineController.PulseOutlineImmediate(2.0f, eventName: "VillainTest");
                }
            }
            if (GUILayout.Button("Pulse Title Outline", GUILayout.Height(24)))
            {
                if (director.TitleOutlineController != null)
                {
                    director.TitleOutlineController.PulseOutlineImmediate(2.0f, eventName: "TitleTest");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Glitch BG Red", GUILayout.Height(24)))
            {
                if (director.BgRedController != null)
                {
                    director.BgRedController.TriggerControlledGlitch(ControlledGlitchType.TypeB_DigitalTear, 1f);
                }
            }
            if (GUILayout.Button("Fly Spark Burst", GUILayout.Height(24)))
            {
                if (director.SparkAtmosphere != null)
                {
                    director.SparkAtmosphere.TriggerLuminousFlyingBurst(12);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // ─── 4. DEFAULT INSPECTOR ──────────────────────────────────────────
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }
        }
    }
}
