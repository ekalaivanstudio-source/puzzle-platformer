using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Tools ▸ Memory Shards ▸ Video Checker — answers "will the memory story videos actually
    /// play?" without having to collect shards and finish levels to find out.
    ///
    /// <b>Validate</b> (edit or play mode) checks everything a story needs on the way to the
    /// screen: the database entries and thresholds, each clip's import, the presenter prefab,
    /// and the presenter in every level scene. The scene check reads the scene files directly,
    /// so it never opens a scene or touches unsaved work. It exists because the videos once
    /// stopped playing silently: the presenter had been switched off in every level scene, so
    /// the level end found nothing to play them with.
    ///
    /// <b>Play test</b> (play mode, in a level) plays a clip through the real presenter via
    /// <see cref="MemoryStoryPresenter.PreviewRoutine"/> — same prepare, same end detection as
    /// a level end — and judges the result from what the decoder actually did: did it buffer,
    /// did the frames advance, did the picture reach the screen, did it report its own end.
    /// A preview never marks the story as seen, so it can be run any number of times.
    /// </summary>
    public class MemoryStoryVideoChecker : EditorWindow
    {
        private enum Severity { Pass, Info, Warning, Error }

        private struct Finding
        {
            public Severity Severity;
            public string Message;
            public Finding(Severity severity, string message) { Severity = severity; Message = message; }
        }

        private readonly List<Finding> m_Validation = new List<Finding>();
        private readonly List<Finding> m_PlayResults = new List<Finding>();
        private bool m_Validated;

        private Vector2 m_Scroll;

        // Play-test state. Driven by a coroutine on the presenter, read by OnGUI. Static so two
        // checker windows cannot start overlapping runs — the presenter can only play one clip,
        // and a second run would both fail and wipe the first run's results.
        private static bool s_Testing;
        private static int s_RunId;
        private string m_TestingStory;
        private double m_TestStartTime;

        [MenuItem("Tools/Memory Shards/Video Checker", priority = 50)]
        public static void Open()
        {
            var window = GetWindow<MemoryStoryVideoChecker>("Story Video Checker");
            window.minSize = new Vector2(460f, 360f);
            window.Show();
        }

        private void OnEnable() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        private void OnDisable() => EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

        private void HandlePlayModeChanged(PlayModeStateChange change)
        {
            // A test cut short by leaving play mode never reports back — clear it so the buttons
            // come back rather than staying greyed out forever.
            if (change == PlayModeStateChange.ExitingPlayMode && s_Testing)
            {
                s_Testing = false;
                s_RunId++; // the interrupted run must not report into a later one
                m_PlayResults.Add(new Finding(Severity.Warning,
                    $"'{m_TestingStory}': test stopped because play mode ended."));
            }
            Repaint();
        }

        // Keeps the elapsed-time readout ticking while a clip plays.
        private void Update()
        {
            if (s_Testing) Repaint();
        }

        // ─── GUI ──────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Memory Story Video Checker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Validate checks the database, clips, prefab and every level scene (safe any time).\n" +
                "Play test plays each clip through the real presenter — enter play mode in any level " +
                "first. Previews never mark a story as seen.", MessageType.None);

            DrawValidateSection();
            EditorGUILayout.Space(8);
            DrawPlayTestSection();

            EditorGUILayout.Space(8);
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            if (m_Validation.Count > 0)
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                DrawFindings(m_Validation);
            }

            if (m_PlayResults.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Play test", EditorStyles.boldLabel);
                DrawFindings(m_PlayResults);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawValidateSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Height(26))) RunValidation();

                if (m_Validated)
                {
                    int errors = Count(m_Validation, Severity.Error);
                    int warnings = Count(m_Validation, Severity.Warning);
                    var style = new GUIStyle(EditorStyles.boldLabel);
                    style.normal.textColor = errors > 0 ? new Color(0.9f, 0.3f, 0.3f)
                        : warnings > 0 ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.3f, 0.8f, 0.4f);
                    GUILayout.Label(errors > 0 ? $"{errors} error(s), {warnings} warning(s)"
                        : warnings > 0 ? $"OK with {warnings} warning(s)" : "All checks passed", style);
                }
            }
        }

        private void DrawPlayTestSection()
        {
            EditorGUILayout.LabelField("Play test", EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode in a level scene to play-test the clips.", MessageType.Info);
                return;
            }

            if (MemoryStoryPresenter.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "No active MemoryStoryPresenter in the running scene — at a level end the story " +
                    "would NOT play here. Open a level scene, or run Validate to see which scenes are " +
                    "missing it or have it switched off.", MessageType.Error);
                return;
            }

            MemoryShardDatabase database = MemoryShardService.Database;
            List<MemoryStoryEntry> stories = database != null ? database.StoriesByThreshold() : new List<MemoryStoryEntry>();
            if (stories.Count == 0)
            {
                EditorGUILayout.HelpBox("The database has no stories.", MessageType.Warning);
                return;
            }

            if (s_Testing)
            {
                double elapsed = EditorApplication.timeSinceStartup - m_TestStartTime;
                EditorGUILayout.HelpBox($"Playing '{m_TestingStory}'…  {elapsed:0.0}s  (watch the Game view)",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(s_Testing || MemoryStoryPresenter.IsPlaying))
            {
                if (GUILayout.Button("Play all clips", GUILayout.Height(24)))
                    StartTest(stories);

                foreach (var story in stories)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string clip = story.HasClip ? $"{story.clip.name}  ({story.clip.length:0.0}s)" : "no clip";
                        GUILayout.Label($"{story.SafeId}  @ {story.requiredShards} shards  —  {clip}");
                        using (new EditorGUI.DisabledScope(!story.HasClip))
                        {
                            if (GUILayout.Button("Play", GUILayout.Width(60)))
                                StartTest(new List<MemoryStoryEntry> { story });
                        }
                    }
                }
            }

            if (m_PlayResults.Count > 0 && !s_Testing && GUILayout.Button("Clear play-test results"))
                m_PlayResults.Clear();
        }

        private static void DrawFindings(List<Finding> findings)
        {
            foreach (var finding in findings)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(IconFor(finding.Severity), GUILayout.Width(20), GUILayout.Height(18));
                    EditorGUILayout.LabelField(finding.Message, EditorStyles.wordWrappedLabel);
                }
            }
        }

        private static GUIContent IconFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error: return EditorGUIUtility.IconContent("console.erroricon.sml");
                case Severity.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                case Severity.Info: return EditorGUIUtility.IconContent("console.infoicon.sml");
                default: return EditorGUIUtility.IconContent("TestPassed");
            }
        }

        private static int Count(List<Finding> findings, Severity severity)
        {
            int count = 0;
            foreach (var f in findings) if (f.Severity == severity) count++;
            return count;
        }

        // ─── Validation ───────────────────────────────────────────────────────────

        private void RunValidation()
        {
            m_Validation.Clear();

            ValidateDatabase(m_Validation);
            ValidatePresenterPrefab(m_Validation);
            ValidateScenes(m_Validation);

            m_Validated = true;
            LogFindings("Validation", m_Validation);
            Repaint();
        }

        private static void ValidateDatabase(List<Finding> results)
        {
            var database = AssetDatabase.LoadAssetAtPath<MemoryShardDatabase>(MemoryShardSetup.DatabasePath);
            if (database == null)
            {
                results.Add(new Finding(Severity.Error,
                    $"No MemoryShardDatabase at {MemoryShardSetup.DatabasePath}. Run Tools ▸ Memory Shards ▸ Run Full Setup."));
                return;
            }

            List<MemoryStoryEntry> stories = database.StoriesByThreshold();
            if (stories.Count == 0)
            {
                results.Add(new Finding(Severity.Error, "The database has no stories."));
                return;
            }

            int placedShards = CountPlacedShards();
            var seenIds = new HashSet<string>();
            var seenClips = new HashSet<VideoClip>();
            var seenThresholds = new HashSet<int>();

            foreach (var story in stories)
            {
                string label = $"'{story.SafeId}' @ {story.requiredShards} shards";

                if (!seenIds.Add(story.SafeId))
                    results.Add(new Finding(Severity.Error,
                        $"{label}: duplicate id — both entries share one 'already played' flag in the save."));

                if (!seenThresholds.Add(story.requiredShards))
                    results.Add(new Finding(Severity.Warning,
                        $"{label}: another story has the same threshold; both will play back to back."));

                if (placedShards >= 0 && story.requiredShards > placedShards)
                    results.Add(new Finding(Severity.Info,
                        $"{label}: needs more shards than are placed ({placedShards}) — unreachable until more " +
                        "levels hide shards."));

                if (!story.HasClip)
                {
                    results.Add(new Finding(Severity.Warning,
                        $"{label}: no clip assigned — it unlocks but nothing plays (stays pending)."));
                    continue;
                }

                if (!seenClips.Add(story.clip))
                    results.Add(new Finding(Severity.Warning, $"{label}: uses the same clip as another story."));

                ValidateClip(story, label, results);
            }
        }

        private static void ValidateClip(MemoryStoryEntry story, string label, List<Finding> results)
        {
            VideoClip clip = story.clip;
            string path = AssetDatabase.GetAssetPath(clip);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                results.Add(new Finding(Severity.Error, $"{label}: clip file is missing on disk ({path})."));
                return;
            }

            if (clip.length <= 0 || clip.frameCount == 0 || clip.width == 0 || clip.height == 0)
            {
                results.Add(new Finding(Severity.Error,
                    $"{label}: '{clip.name}' imported with no picture (length {clip.length:0.00}s, " +
                    $"{clip.frameCount} frames, {clip.width}x{clip.height}). The file may be corrupt or an " +
                    "unsupported codec — re-encode as H.264 MP4."));
                return;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".mp4" && ext != ".webm" && ext != ".mov")
                results.Add(new Finding(Severity.Warning,
                    $"{label}: '{ext}' clips may not decode on every platform; H.264 .mp4 is the safe choice."));

            if (clip.audioTrackCount == 0)
                results.Add(new Finding(Severity.Info, $"{label}: '{clip.name}' has no audio track (plays silent)."));

            long bytes = new FileInfo(path).Length;
            results.Add(new Finding(Severity.Pass,
                $"{label}: '{clip.name}' {clip.width}x{clip.height}, {clip.frameRate:0.#} fps, " +
                $"{clip.length:0.0}s, {clip.audioTrackCount} audio track(s), {bytes / (1024f * 1024f):0.0} MB."));
        }

        /// <summary>How many levels hide a shard, or -1 when the level database is missing.</summary>
        private static int CountPlacedShards()
        {
            var levels = Resources.Load<LevelConfigDatabase>(LevelConfigDatabase.ResourcePath);
            if (levels == null || levels.Configs == null) return -1;

            int count = 0;
            foreach (var config in levels.Configs)
                if (config != null && config.memoryShard != null && config.memoryShard.placeShard) count++;
            return count;
        }

        private static void ValidatePresenterPrefab(List<Finding> results)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MemoryShardSetup.PresenterPrefabPath);
            if (prefab == null)
            {
                results.Add(new Finding(Severity.Error,
                    $"No presenter prefab at {MemoryShardSetup.PresenterPrefabPath}. Run Tools ▸ Memory Shards ▸ 2. Build Prefabs."));
                return;
            }

            var presenter = prefab.GetComponent<MemoryStoryPresenter>();
            if (presenter == null)
            {
                results.Add(new Finding(Severity.Error, "The presenter prefab has no MemoryStoryPresenter component."));
                return;
            }

            var serialized = new SerializedObject(presenter);
            bool ok = true;
            foreach (string field in new[] { "m_Player", "m_Surface" })
            {
                if (serialized.FindProperty(field).objectReferenceValue == null)
                {
                    results.Add(new Finding(Severity.Error, $"Presenter prefab: '{field}' is not assigned."));
                    ok = false;
                }
            }

            if (!prefab.activeSelf)
            {
                results.Add(new Finding(Severity.Error, "Presenter prefab root is switched off — no level can play a story."));
                ok = false;
            }

            var canvas = prefab.GetComponent<Canvas>();
            if (canvas != null && canvas.enabled)
                results.Add(new Finding(Severity.Warning,
                    "Presenter prefab's Canvas is enabled, so its black backdrop covers the Game view in " +
                    "edit mode — which tempts people into switching the object off. Save it with the Canvas disabled."));

            if (ok) results.Add(new Finding(Severity.Pass, "Presenter prefab is wired up."));
        }

        // ─── Scene scan ───────────────────────────────────────────────────────────

        private static readonly Regex s_Modification = new Regex(
            @"- target: \{fileID: (-?\d+), guid: (\w+), type: \d+\}\s*\n\s*propertyPath: (.+?)\s*\n\s*value: (.*?)\s*\n\s*objectReference: \{fileID: (-?\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Reads each level scene as text and checks its presenter the way the runtime will see
        /// it: present, switched on, component enabled, references intact. Text rather than
        /// OpenScene so it is instant and never disturbs whatever the user has open.
        /// </summary>
        private static void ValidateScenes(List<Finding> results)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MemoryShardSetup.PresenterPrefabPath);
            var presenterScript = prefab != null ? prefab.GetComponent<MemoryStoryPresenter>() : null;
            if (prefab == null || presenterScript == null) return; // already reported by the prefab check

            string prefabGuid = AssetDatabase.AssetPathToGUID(MemoryShardSetup.PresenterPrefabPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out _, out long rootId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(presenterScript, out _, out long componentId);

            string scriptGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(presenterScript)));

            List<string> scenes = MemoryShardSetup.FindLevelScenes();
            if (scenes.Count == 0)
            {
                results.Add(new Finding(Severity.Warning, "No level scenes found to check."));
                return;
            }

            int good = 0;
            foreach (string path in scenes)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                string[] docs = text.Split(new[] { "\n--- " }, System.StringSplitOptions.None);

                var problems = new List<string>();
                int instances = 0;

                foreach (string doc in docs)
                {
                    bool isPrefabInstance = doc.StartsWith("!u!1001 ") && doc.Contains($"m_SourcePrefab: {{fileID: 100100000, guid: {prefabGuid}");
                    if (isPrefabInstance)
                    {
                        instances++;
                        CheckInstanceOverrides(doc, prefabGuid, rootId, componentId, problems);
                        continue;
                    }

                    // A presenter added by hand rather than from the prefab.
                    if (doc.StartsWith("!u!114 ") && doc.Contains($"m_Script: {{fileID: 11500000, guid: {scriptGuid}") &&
                        doc.Contains("m_PrefabInstance: {fileID: 0}"))
                    {
                        instances++;
                        CheckLoosePresenter(doc, docs, problems);
                    }
                }

                if (instances == 0)
                    problems.Add("no MemoryStoryPresenter in the scene — run Tools ▸ Memory Shards ▸ 3. Setup All Level Scenes " +
                                 "(if it is nested inside another prefab, use the play test to confirm instead)");
                else if (instances > 1)
                    problems.Add($"{instances} presenters — only one can be active; remove the extras");

                if (problems.Count == 0) good++;
                else foreach (string problem in problems)
                    results.Add(new Finding(Severity.Error, $"{name}: {problem}."));
            }

            results.Add(new Finding(good == scenes.Count ? Severity.Pass : Severity.Info,
                $"Level scenes: {good}/{scenes.Count} have one active, correctly wired presenter."));
        }

        private static void CheckInstanceOverrides(string doc, string prefabGuid, long rootId, long componentId,
                                                    List<string> problems)
        {
            foreach (Match m in s_Modification.Matches(doc))
            {
                if (m.Groups[2].Value != prefabGuid) continue;

                long target = long.Parse(m.Groups[1].Value);
                string property = m.Groups[3].Value;
                string value = m.Groups[4].Value;
                string reference = m.Groups[5].Value;

                if (target == rootId && property == "m_IsActive" && value == "0")
                    problems.Add("the presenter object is switched OFF in this scene, so stories never play here " +
                                 "(tick it back on — it is invisible until a story plays)");

                if (target == componentId && property == "m_Enabled" && value == "0")
                    problems.Add("the MemoryStoryPresenter component is disabled in this scene");

                if (target == componentId && (property == "m_Player" || property == "m_Surface") && reference == "0")
                    problems.Add($"'{property}' is cleared on this scene's presenter");
            }

            if (Regex.IsMatch(doc, @"m_TransformParent: \{fileID: [1-9]"))
                problems.Add("the presenter is parented under another object — keep it at the scene root so " +
                             "switching that parent off cannot take the videos with it");

            Match removed = Regex.Match(doc, @"m_RemovedComponents:((?:\n\s+- \{[^\n]*)*)");
            if (removed.Success && removed.Groups[1].Value.Contains($"fileID: {componentId}, guid: {prefabGuid}"))
                problems.Add("the MemoryStoryPresenter component has been removed from this scene's instance");
        }

        private static void CheckLoosePresenter(string componentDoc, string[] docs, List<string> problems)
        {
            if (Regex.IsMatch(componentDoc, @"\n\s*m_Enabled: 0"))
                problems.Add("the MemoryStoryPresenter component is disabled in this scene");

            Match go = Regex.Match(componentDoc, @"m_GameObject: \{fileID: (\d+)\}");
            if (!go.Success) return;

            foreach (string doc in docs)
            {
                if (!doc.StartsWith($"!u!1 &{go.Groups[1].Value}\n")) continue;
                if (Regex.IsMatch(doc, @"m_IsActive: 0"))
                    problems.Add("the presenter object is switched OFF in this scene, so stories never play here");
                return;
            }
        }

        // ─── Play test ────────────────────────────────────────────────────────────

        private void StartTest(List<MemoryStoryEntry> stories)
        {
            var presenter = MemoryStoryPresenter.Instance;
            if (presenter == null || s_Testing || MemoryStoryPresenter.IsPlaying) return;

            m_PlayResults.Clear();
            s_Testing = true;
            int run = ++s_RunId;
            presenter.StartCoroutine(TestRoutine(stories, run));
        }

        private IEnumerator TestRoutine(List<MemoryStoryEntry> stories, int run)
        {
            foreach (var story in stories)
            {
                if (!story.HasClip)
                {
                    m_PlayResults.Add(new Finding(Severity.Warning, $"'{story.SafeId}': no clip assigned — skipped."));
                    continue;
                }

                m_TestingStory = story.SafeId;
                m_TestStartTime = EditorApplication.timeSinceStartup;
                Repaint();

                MemoryStoryPresenter.PlaybackReport report = null;
                yield return MemoryStoryPresenter.PreviewRoutine(story, r => report = r);

                if (run != s_RunId) yield break;
                Judge(report, m_PlayResults);
                Repaint();

                if (!EditorApplication.isPlaying) yield break;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            if (AudioManager.Instance != null && AudioManager.Instance.Muted)
                m_PlayResults.Add(new Finding(Severity.Info,
                    "The game's audio is muted, so the clips played silent — unmute to check their sound."));

            if (run != s_RunId) yield break;
            s_Testing = false;
            LogFindings("Play test", m_PlayResults);
            Repaint();
        }

        /// <summary>
        /// Turns a playback report into a verdict. "It was asked to play" proves nothing — this
        /// looks at whether the decoder buffered, whether the frames moved, whether the picture
        /// was on the surface, and whether the player reported its own end.
        /// </summary>
        private static void Judge(MemoryStoryPresenter.PlaybackReport r, List<Finding> results)
        {
            if (r == null)
            {
                results.Add(new Finding(Severity.Error, "No report came back — the test was interrupted."));
                return;
            }

            string who = $"'{r.StoryId}'" + (r.ClipName != null ? $" ({r.ClipName})" : "");
            string stats = $"prepare {r.PrepareSeconds:0.00}s, played {r.PlaySeconds:0.0}s of {r.ClipLength:0.0}s, " +
                           $"reached frame {r.HighestFrame + 1}/{r.FrameCount} ({r.Progress:P0})";

            if (!string.IsNullOrEmpty(r.Error) && !r.Prepared)
            {
                results.Add(new Finding(Severity.Error, $"{who}: FAILED — {r.Error}"));
                return;
            }

            if (r.Skipped)
            {
                results.Add(new Finding(Severity.Warning,
                    $"{who}: skipped by a key press, so this is not a full test — {stats}. Run it again without pressing anything."));
                return;
            }

            var failures = new List<string>();
            if (!string.IsNullOrEmpty(r.Error)) failures.Add($"player error: {r.Error}");
            if (!r.SurfaceBound) failures.Add("the picture never reached the screen surface");
            if (r.HighestFrame < 1) failures.Add("the frames never advanced (frozen on the first frame)");
            else if (!r.Finished && r.Progress < 0.9f) failures.Add("playback stalled before the end");
            if (r.HitBackstop) failures.Add("the player never reported the end of the clip (ran to its time limit)");

            if (failures.Count > 0)
            {
                results.Add(new Finding(Severity.Error, $"{who}: FAILED — {string.Join("; ", failures)}. {stats}."));
                return;
            }

            if (r.PlaySeconds > r.ClipLength * 1.5 + 1.0)
                results.Add(new Finding(Severity.Warning,
                    $"{who}: played, but took much longer than the clip — decoding may be too slow. {stats}."));
            else if (r.PrepareSeconds > 2f)
                results.Add(new Finding(Severity.Warning,
                    $"{who}: played, but took {r.PrepareSeconds:0.0}s to start — a long pause at the level end. {stats}."));
            else
                results.Add(new Finding(Severity.Pass, $"{who}: PLAYED correctly — {stats}."));
        }

        private static void LogFindings(string title, List<Finding> findings)
        {
            int errors = Count(findings, Severity.Error);
            int warnings = Count(findings, Severity.Warning);
            var text = new System.Text.StringBuilder();
            text.Append($"[StoryVideoChecker] {title}: {errors} error(s), {warnings} warning(s)");
            foreach (var f in findings) text.Append($"\n  [{f.Severity}] {f.Message}");

            if (errors > 0) Debug.LogError(text.ToString());
            else if (warnings > 0) Debug.LogWarning(text.ToString());
            else Debug.Log(text.ToString());
        }
    }
}
