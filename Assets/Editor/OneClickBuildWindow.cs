using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// One-click player builds with a version bump.
///
/// Bumps <see cref="PlayerSettings.bundleVersion"/> (semver), builds the enabled
/// scenes from Build Settings into a versioned folder, and reveals the result.
/// The build *number* is not touched here — <c>BuildStampGenerator</c> already
/// increments it in its pre-process callback, and it reads the version we set
/// just before the build starts, so the stamp and the folder name always agree.
///
/// If the build fails or is cancelled the version (and Android version code) are
/// rolled back, so a broken build never burns a version.
/// </summary>
public sealed class OneClickBuildWindow : EditorWindow
{
    // ─── Types ───────────────────────────────────────────────────────────────

    private enum BumpKind { None, Patch, Minor, Major, Custom }

    private enum TargetKind { Windows64, Android }

    // ─── Persisted state ─────────────────────────────────────────────────────
    // Keyed per project so two checkouts on one machine keep separate settings.

    private static string PrefKey(string name) => $"OneClickBuild.{PlayerSettings.productName}.{name}";

    private BumpKind m_Bump = BumpKind.Patch;
    private TargetKind m_Target = TargetKind.Windows64;
    private string m_CustomVersion = "";
    private string m_OutputRoot = "";
    private bool m_Development;
    private bool m_ScriptDebugging;
    private bool m_AutoConnectProfiler;
    private bool m_CleanBuild;
    private bool m_RevealWhenDone = true;
    private bool m_RunWhenDone;

    private string m_LastBuildPath = "";
    private Vector2 m_Scroll;

    // ─── Menu ────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build/One-Click Build %#b", priority = 0)]
    private static void Open()
    {
        var window = GetWindow<OneClickBuildWindow>(utility: false, title: "One-Click Build");
        window.minSize = new Vector2(430f, 520f);
        window.Show();
    }

    /// <summary>Repeats the last configured build without opening the window.</summary>
    [MenuItem("Tools/Build/Build Now (Last Settings)", priority = 1)]
    private static void BuildNowWithLastSettings()
    {
        var window = CreateInstance<OneClickBuildWindow>();
        window.OnEnable();
        window.RunBuild();
        DestroyImmediate(window);
    }

    [MenuItem("Tools/Build/Open Build Folder", priority = 20)]
    private static void OpenBuildFolder()
    {
        string root = EditorPrefs.GetString(PrefKey(nameof(m_OutputRoot)), DefaultOutputRoot());
        Directory.CreateDirectory(root);
        EditorUtility.RevealInFinder(root);
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void OnEnable()
    {
        m_Bump = (BumpKind)EditorPrefs.GetInt(PrefKey(nameof(m_Bump)), (int)BumpKind.Patch);
        m_Target = (TargetKind)EditorPrefs.GetInt(PrefKey(nameof(m_Target)), (int)DefaultTarget());
        m_OutputRoot = EditorPrefs.GetString(PrefKey(nameof(m_OutputRoot)), DefaultOutputRoot());
        m_Development = EditorPrefs.GetBool(PrefKey(nameof(m_Development)), true);
        m_ScriptDebugging = EditorPrefs.GetBool(PrefKey(nameof(m_ScriptDebugging)), false);
        m_AutoConnectProfiler = EditorPrefs.GetBool(PrefKey(nameof(m_AutoConnectProfiler)), false);
        m_CleanBuild = EditorPrefs.GetBool(PrefKey(nameof(m_CleanBuild)), false);
        m_RevealWhenDone = EditorPrefs.GetBool(PrefKey(nameof(m_RevealWhenDone)), true);
        m_RunWhenDone = EditorPrefs.GetBool(PrefKey(nameof(m_RunWhenDone)), false);
        m_LastBuildPath = EditorPrefs.GetString(PrefKey(nameof(m_LastBuildPath)), "");
        m_CustomVersion = EditorPrefs.GetString(PrefKey(nameof(m_CustomVersion)), PlayerSettings.bundleVersion);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetInt(PrefKey(nameof(m_Bump)), (int)m_Bump);
        EditorPrefs.SetInt(PrefKey(nameof(m_Target)), (int)m_Target);
        EditorPrefs.SetString(PrefKey(nameof(m_OutputRoot)), m_OutputRoot);
        EditorPrefs.SetString(PrefKey(nameof(m_CustomVersion)), m_CustomVersion);
        EditorPrefs.SetBool(PrefKey(nameof(m_Development)), m_Development);
        EditorPrefs.SetBool(PrefKey(nameof(m_ScriptDebugging)), m_ScriptDebugging);
        EditorPrefs.SetBool(PrefKey(nameof(m_AutoConnectProfiler)), m_AutoConnectProfiler);
        EditorPrefs.SetBool(PrefKey(nameof(m_CleanBuild)), m_CleanBuild);
        EditorPrefs.SetBool(PrefKey(nameof(m_RevealWhenDone)), m_RevealWhenDone);
        EditorPrefs.SetBool(PrefKey(nameof(m_RunWhenDone)), m_RunWhenDone);
        EditorPrefs.SetString(PrefKey(nameof(m_LastBuildPath)), m_LastBuildPath);
    }

    // ─── GUI ─────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
        EditorGUI.BeginChangeCheck();

        DrawSummary();
        EditorGUILayout.Space();
        DrawVersionSection();
        EditorGUILayout.Space();
        DrawTargetSection();
        EditorGUILayout.Space();
        DrawOutputSection();
        EditorGUILayout.Space();

        if (EditorGUI.EndChangeCheck()) SaveSettings();

        DrawBuildButton();
        DrawFooter();

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField(PlayerSettings.productName, EditorStyles.boldLabel);

        int nextBuild = ReadBuildNumber() + 1;
        int sceneCount = EnabledScenes().Length;

        EditorGUILayout.HelpBox(
            $"Current : v{PlayerSettings.bundleVersion}  (build {ReadBuildNumber():D2})\n" +
            $"Next    : v{ResolveNextVersion()}  (build {nextBuild:D2})\n" +
            $"Scenes  : {sceneCount} enabled in Build Settings",
            sceneCount == 0 ? MessageType.Error : MessageType.None);

        if (sceneCount == 0)
            EditorGUILayout.HelpBox("No enabled scenes — add scenes in File ▸ Build Profiles before building.", MessageType.Error);
    }

    private void DrawVersionSection()
    {
        EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            m_Bump = (BumpKind)EditorGUILayout.EnumPopup(
                new GUIContent("Bump", "Major.Minor.Patch — Patch for routine builds, Minor for features, Major for releases."),
                m_Bump);

            if (m_Bump == BumpKind.Custom)
                m_CustomVersion = EditorGUILayout.TextField("Custom version", m_CustomVersion);

            if (m_Target == TargetKind.Android)
                EditorGUILayout.LabelField("Android version code",
                    $"{PlayerSettings.Android.bundleVersionCode} → {PlayerSettings.Android.bundleVersionCode + (m_Bump == BumpKind.None ? 0 : 1)}");
        }
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            m_Target = (TargetKind)EditorGUILayout.EnumPopup("Platform", m_Target);

            if (ToBuildTarget(m_Target) != EditorUserBuildSettings.activeBuildTarget)
                EditorGUILayout.HelpBox(
                    $"Active target is {EditorUserBuildSettings.activeBuildTarget}. Building will switch platforms first, " +
                    "which can take several minutes on the first switch.", MessageType.Warning);

            m_Development = EditorGUILayout.Toggle(
                new GUIContent("Development build", "Enables the on-screen build stamp and the player log/profiler."),
                m_Development);

            using (new EditorGUI.DisabledScope(!m_Development))
            {
                m_ScriptDebugging = EditorGUILayout.Toggle("  Script debugging", m_ScriptDebugging);
                m_AutoConnectProfiler = EditorGUILayout.Toggle("  Autoconnect profiler", m_AutoConnectProfiler);
            }
        }
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                m_OutputRoot = EditorGUILayout.TextField("Folder", m_OutputRoot);
                if (GUILayout.Button("…", GUILayout.Width(28f)))
                {
                    string picked = EditorUtility.SaveFolderPanel("Build output folder", m_OutputRoot, "");
                    if (!string.IsNullOrEmpty(picked)) m_OutputRoot = picked;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.LabelField(" ", BuildFolderName(ResolveNextVersion(), ReadBuildNumber() + 1), EditorStyles.miniLabel);

            m_CleanBuild = EditorGUILayout.Toggle(
                new GUIContent("Clean", "Delete the destination folder first instead of building on top of it."), m_CleanBuild);
            m_RevealWhenDone = EditorGUILayout.Toggle("Reveal when done", m_RevealWhenDone);
            m_RunWhenDone = EditorGUILayout.Toggle("Run when done", m_RunWhenDone);
        }
    }

    private void DrawBuildButton()
    {
        GUI.backgroundColor = new Color(0.45f, 0.8f, 0.45f);
        using (new EditorGUI.DisabledScope(EnabledScenes().Length == 0 || EditorApplication.isCompiling))
        {
            if (GUILayout.Button($"BUILD  v{ResolveNextVersion()}", GUILayout.Height(42f)))
            {
                // Defer past the layout/repaint pass — BuildPipeline blocks for
                // minutes and must not run inside an IMGUI event.
                EditorApplication.delayCall += RunBuild;
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Directory.Exists(m_OutputRoot)))
        {
            if (GUILayout.Button("Open build folder")) EditorUtility.RevealInFinder(m_OutputRoot);
        }

        using (new EditorGUI.DisabledScope(!File.Exists(m_LastBuildPath)))
        {
            if (GUILayout.Button("Run last build")) Process.Start(m_LastBuildPath);
        }

        if (!string.IsNullOrEmpty(m_LastBuildPath))
            EditorGUILayout.LabelField("Last: " + m_LastBuildPath, EditorStyles.miniLabel);
    }

    // ─── Build ───────────────────────────────────────────────────────────────

    private void RunBuild()
    {
        string[] scenes = EnabledScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[OneClickBuild] No enabled scenes in Build Settings — nothing to build.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildTarget target = ToBuildTarget(m_Target);
        if (target != EditorUserBuildSettings.activeBuildTarget)
        {
            if (!EditorUtility.DisplayDialog("Switch platform?",
                    $"The active build target is {EditorUserBuildSettings.activeBuildTarget}. " +
                    $"Switch to {target} and continue? This can take several minutes.", "Switch and build", "Cancel"))
                return;

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                Debug.LogError($"[OneClickBuild] Could not switch the active build target to {target}.");
                return;
            }
        }

        // Snapshot what we are about to change, so a failed build can put it back.
        string previousVersion = PlayerSettings.bundleVersion;
        int previousVersionCode = PlayerSettings.Android.bundleVersionCode;

        string version = ResolveNextVersion();
        PlayerSettings.bundleVersion = version;
        if (m_Bump != BumpKind.None) PlayerSettings.Android.bundleVersionCode = previousVersionCode + 1;

        string folder = Path.Combine(m_OutputRoot, BuildFolderName(version, ReadBuildNumber() + 1));
        if (m_CleanBuild && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            locationPathName = Path.Combine(folder, ExecutableName(target)),
            options = ResolveBuildOptions()
        };

        Debug.Log($"[OneClickBuild] Building v{version} ({target}) → {options.locationPathName}");

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        catch (Exception exception)
        {
            RestoreVersion(previousVersion, previousVersionCode);
            Debug.LogError($"[OneClickBuild] Build threw: {exception}");
            return;
        }

        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            RestoreVersion(previousVersion, previousVersionCode);
            Debug.LogError($"[OneClickBuild] Build {summary.result} after {summary.totalTime:mm\\:ss} " +
                           $"({summary.totalErrors} error(s)). Version rolled back to v{previousVersion}.");
            EditorUtility.DisplayDialog("Build failed",
                $"The build {summary.result.ToString().ToLowerInvariant()}. See the Console for details.\n\n" +
                $"Version was rolled back to v{previousVersion}.", "OK");
            return;
        }

        m_LastBuildPath = options.locationPathName;
        SaveSettings();

        Debug.Log($"[OneClickBuild] ✔ v{version} build {ReadBuildNumber():D2} — " +
                  $"{summary.totalTime:mm\\:ss}, {summary.totalSize / (1024f * 1024f):F1} MB → {options.locationPathName}");

        if (m_RevealWhenDone) EditorUtility.RevealInFinder(options.locationPathName);
        if (m_RunWhenDone && File.Exists(options.locationPathName)) Process.Start(options.locationPathName);
    }

    private BuildOptions ResolveBuildOptions()
    {
        BuildOptions options = BuildOptions.None;
        if (m_Development)
        {
            options |= BuildOptions.Development;
            if (m_ScriptDebugging) options |= BuildOptions.AllowDebugging;
            if (m_AutoConnectProfiler) options |= BuildOptions.ConnectWithProfiler;
        }
        if (m_CleanBuild) options |= BuildOptions.CleanBuildCache;
        return options;
    }

    private static void RestoreVersion(string version, int androidVersionCode)
    {
        PlayerSettings.bundleVersion = version;
        PlayerSettings.Android.bundleVersionCode = androidVersionCode;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string[] EnabledScenes()
        => EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

    private static TargetKind DefaultTarget()
        => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? TargetKind.Android : TargetKind.Windows64;

    private static BuildTarget ToBuildTarget(TargetKind kind)
        => kind == TargetKind.Android ? BuildTarget.Android : BuildTarget.StandaloneWindows64;

    private static string ExecutableName(BuildTarget target)
    {
        string safeName = string.Join("_", PlayerSettings.productName.Split(Path.GetInvalidFileNameChars()));
        return target == BuildTarget.Android ? safeName + ".apk" : safeName + ".exe";
    }

    /// <summary>Default output root: a gitignored <c>Builds/</c> beside the Assets folder.</summary>
    private static string DefaultOutputRoot()
        => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));

    private string BuildFolderName(string version, int build)
        => $"{m_Target}/v{version}_b{build:D2}";

    /// <summary>The version this build would ship with, given the selected bump.</summary>
    private string ResolveNextVersion()
    {
        if (m_Bump == BumpKind.Custom) return string.IsNullOrWhiteSpace(m_CustomVersion) ? PlayerSettings.bundleVersion : m_CustomVersion.Trim();
        if (m_Bump == BumpKind.None) return PlayerSettings.bundleVersion;

        ParseSemver(PlayerSettings.bundleVersion, out int major, out int minor, out int patch);

        switch (m_Bump)
        {
            case BumpKind.Major: major++; minor = 0; patch = 0; break;
            case BumpKind.Minor: minor++; patch = 0; break;
            default:             patch++; break;
        }

        return $"{major}.{minor}.{patch}";
    }

    /// <summary>
    /// Lenient semver parse. Anything unparseable reads as 0, so a hand-edited
    /// version like "0.2" still bumps to something sensible instead of throwing.
    /// </summary>
    private static void ParseSemver(string version, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        if (string.IsNullOrWhiteSpace(version)) return;

        string[] parts = version.Trim().Split('.');
        if (parts.Length > 0) int.TryParse(parts[0], out major);
        if (parts.Length > 1) int.TryParse(parts[1], out minor);
        if (parts.Length > 2) int.TryParse(parts[2], out patch);
    }

    /// <summary>
    /// Current build number, read straight off disk rather than through
    /// <c>Resources.Load</c> — the editor caches that TextAsset and would report a
    /// stale number right after a build bumped it.
    /// </summary>
    private static int ReadBuildNumber()
    {
        string path = "Assets/Resources/" + DevBuildStamp.ResourceName + ".txt";
        if (!File.Exists(path)) return 0;

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("build=", StringComparison.Ordinal)) continue;
            if (int.TryParse(line.Substring("build=".Length).Trim(), out int build)) return build;
        }

        return 0;
    }
}
