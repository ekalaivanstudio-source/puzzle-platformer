using MainGame.UI.Unified;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Builds the new-game intro cutscene into the HomeScreen scene: a full-screen canvas that sits
/// above the menu, one VideoPlayer per clip, the RawImage they draw into, and the press-right hint.
///
/// It lives in HomeScreen rather than a scene of its own because every level is addressed by its
/// build index, and the build list is ordered Launcher (0), levels (1..N), HomeScreen (last) to keep
/// level number == build index — see <see cref="IntroCutsceneScreen"/> and
/// <see cref="CompanySplashScreen"/> for the same reasoning.
///
/// Re-running the menu item throws the old object away and rebuilds it, so this is also how you
/// re-tune the layout after editing the numbers below.
/// </summary>
public static class IntroCutsceneBuilder
{
    private const string k_MenuPath = "Tools/Intro/Build Intro Cutscene (HomeScreen)";

    private const string k_HomeScenePath = "Assets/MainGame/Scenes/HomeScreen.unity";
    private const string k_LoopClipPath = "Assets/MainGame/Video/Video1.mp4";
    private const string k_StoryClipPath = "Assets/MainGame/Video/Video2.mp4";
    private const string k_PromptFontPath = "Assets/MainGame/Font/pixel_noir/Pixel-Noir Caps SDF.asset";

    private const string k_RootName = "IntroCutscene";

    // The home screen's own canvases sort at 0 and 200, so the cutscene has to beat both.
    private const int k_SortingOrder = 500;

    private const string k_PromptText = "PRESS RIGHT ARROW TO CONTINUE";

    [MenuItem(k_MenuPath)]
    public static void Build()
    {
        VideoClip loopClip = AssetDatabase.LoadAssetAtPath<VideoClip>(k_LoopClipPath);
        VideoClip storyClip = AssetDatabase.LoadAssetAtPath<VideoClip>(k_StoryClipPath);

        if (loopClip == null || storyClip == null)
        {
            Debug.LogError($"[IntroCutscene] Missing clips. Expected '{k_LoopClipPath}' and '{k_StoryClipPath}'.");
            return;
        }

        if (!OpenHomeScreen(out Scene scene)) return;

        RemoveExisting(scene);

        GameObject root = BuildHierarchy(loopClip, storyClip);
        SceneManager.MoveGameObjectToScene(root, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[IntroCutscene] Built '{k_RootName}' in {scene.name} and saved the scene.", root);
    }

    // Plays the cutscene without clicking through the menu first, which is the only way to see the
    // two things a still Scene window cannot show: whether Video1 actually loops, and whether the
    // cut to Video2 lands without a black frame. Loads level 1 at the end exactly like New Game.
    [MenuItem("Tools/Intro/Preview Intro Cutscene (Play Mode)")]
    private static void Preview()
    {
        if (!IntroCutsceneScreen.TryPlay(1))
        {
            Debug.LogWarning("[IntroCutscene] Nothing to preview — build it into HomeScreen first, " +
                             "and make sure HomeScreen is the scene in play mode.");
        }
    }

    // Greyed out outside play mode: the cutscene runs on a coroutine, which only runs then.
    [MenuItem("Tools/Intro/Preview Intro Cutscene (Play Mode)", isValidateFunction: true)]
    private static bool ValidatePreview() => Application.isPlaying;

    // ─── Scene handling ─────────────────────────────────────────────────────────

    private static bool OpenHomeScreen(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (scene.path == k_HomeScenePath) return true;

        // Only prompts when something is genuinely unsaved; cancelling leaves the project untouched.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[IntroCutscene] Cancelled — HomeScreen was not opened.");
            return false;
        }

        scene = EditorSceneManager.OpenScene(k_HomeScenePath, OpenSceneMode.Single);
        return scene.IsValid();
    }

    private static void RemoveExisting(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name != k_RootName) continue;

            Object.DestroyImmediate(go);
            Debug.Log($"[IntroCutscene] Replaced the existing '{k_RootName}'.");
            return;
        }
    }

    // ─── Construction ───────────────────────────────────────────────────────────

    private static GameObject BuildHierarchy(VideoClip loopClip, VideoClip storyClip)
    {
        var root = new GameObject(k_RootName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = k_SortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        IntroCutsceneScreen screen = root.AddComponent<IntroCutsceneScreen>();

        // Letterbox fill behind the video, and a raycast target so nothing reaches the menu below.
        RectTransform background = NewUI("Background", root.transform);
        Stretch(background);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = Color.black;

        RectTransform surface = NewUI("VideoSurface", root.transform);
        Stretch(surface);
        RawImage rawImage = surface.gameObject.AddComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        AspectRatioFitter fitter = surface.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = SafeAspect(loopClip);

        VideoPlayer loopPlayer = BuildPlayer("LoopPlayer", root.transform, loopClip, isLooping: true);
        VideoPlayer storyPlayer = BuildPlayer("StoryPlayer", root.transform, storyClip, isLooping: false);

        GameObject prompt = BuildPrompt(root.transform);

        var so = new SerializedObject(screen);
        so.FindProperty("m_LoopClip").objectReferenceValue = loopClip;
        so.FindProperty("m_StoryClip").objectReferenceValue = storyClip;
        so.FindProperty("m_LoopPlayer").objectReferenceValue = loopPlayer;
        so.FindProperty("m_StoryPlayer").objectReferenceValue = storyPlayer;
        so.FindProperty("m_Surface").objectReferenceValue = rawImage;
        so.FindProperty("m_SurfaceFitter").objectReferenceValue = fitter;
        so.FindProperty("m_ContinuePrompt").objectReferenceValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static VideoPlayer BuildPlayer(string name, Transform parent, VideoClip clip, bool isLooping)
    {
        var go = new GameObject(name, typeof(VideoPlayer));
        go.transform.SetParent(parent, false);

        // IntroCutsceneScreen re-applies all of this at runtime; setting it here is so the inspector
        // shows what will actually happen rather than a default-looking player.
        VideoPlayer player = go.GetComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.isLooping = isLooping;
        player.renderMode = VideoRenderMode.APIOnly;

        // Off, never on — see IntroCutsceneScreen.ConfigurePlayer. Waiting for the first frame parks
        // the clock until the decoder produces it, which for these 1080p clips takes seconds.
        player.waitForFirstFrame = false;

        // Direct, never AudioSource output — see IntroCutsceneScreen.ConfigurePlayer for why routing
        // through an AudioSource parks the player at frame 0.
        player.audioOutputMode = VideoAudioOutputMode.Direct;

        if (clip != null)
        {
            for (ushort track = 0; track < clip.audioTrackCount; track++)
            {
                player.EnableAudioTrack(track, true);
            }
        }

        // Built disabled. IntroCutsceneScreen switches each player on only while its clip is wanted,
        // so a player left active here would hold a 1080p decoder open for the whole home screen.
        go.SetActive(false);

        return player;
    }

    private static GameObject BuildPrompt(Transform parent)
    {
        RectTransform rect = NewUI("ContinuePrompt", parent);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(1200f, 90f);

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = k_PromptText;
        text.fontSize = 42;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_PromptFontPath);
        if (font != null) text.font = font;

        // Dark backing so the hint stays legible over a bright frame.
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(3f, -3f);

        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static float SafeAspect(VideoClip clip)
    {
        if (clip == null || clip.height == 0) return 16f / 9f;
        return (float)clip.width / clip.height;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
