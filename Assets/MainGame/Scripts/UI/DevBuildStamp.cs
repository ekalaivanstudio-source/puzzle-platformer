// Development-only build watermark. The whole file compiles to nothing in a
// production (non-development) player, so no stamp code or strings ship to users.
#if DEVELOPMENT_BUILD || UNITY_EDITOR

using UnityEngine;

/// <summary>
/// Draws a one-line build stamp along the bottom of the screen in development
/// builds and in the editor — e.g. "DEVELOPMENT BUILD  •  v0.1.0  •  build 01".
/// It installs itself, so existing scenes need no changes.
///
/// The version/build number are read from the Resources/BuildInfo.txt asset that
/// <c>BuildStampGenerator</c> regenerates on every build. Press F9 to hide the
/// stamp (for clean screenshots) — the toggle is not persisted.
/// </summary>
public sealed class DevBuildStamp : MonoBehaviour
{
    /// <summary>Name of the generated TextAsset inside a Resources folder (no extension).</summary>
    public const string ResourceName = "BuildInfo";

    private const KeyCode ToggleKey = KeyCode.F9;

    private static DevBuildStamp s_Instance;

    private string m_Text;
    private GUIStyle m_LabelStyle;
    private GUIStyle m_BackdropStyle;
    private Texture2D m_BackdropTexture;
    private bool m_IsVisible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (s_Instance != null) return;

        // Belt and braces: even if this assembly somehow ends up in a release
        // player, never draw the stamp there.
        if (!Debug.isDebugBuild && !Application.isEditor) return;

        GameObject host = new GameObject(nameof(DevBuildStamp));
        s_Instance = host.AddComponent<DevBuildStamp>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        m_Text = BuildStampText();

        // Also goes to the player log, so a tester's log file identifies the build
        // even when they cannot screenshot the stamp.
        Debug.Log("[BuildStamp] " + m_Text);
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == ToggleKey)
        {
            m_IsVisible = !m_IsVisible;
            Event.current.Use();
        }

        if (!m_IsVisible || string.IsNullOrEmpty(m_Text)) return;

        BuildStyles();

        // Sit above the mobile gesture bar / notch cut-outs. safeArea is in screen
        // space with y growing upwards; GUI space grows downwards, hence the flip.
        Rect safe = Screen.safeArea;
        float barHeight = m_LabelStyle.fontSize + 10f;
        float bottomInset = Screen.height - (safe.y + safe.height);
        Rect bar = new Rect(safe.x, Screen.height - bottomInset - barHeight, safe.width, barHeight);

        GUI.depth = -1000; // draw on top of any other IMGUI in the scene
        GUI.Box(bar, GUIContent.none, m_BackdropStyle);
        GUI.Label(bar, m_Text, m_LabelStyle);
    }

    private void OnDestroy()
    {
        if (m_BackdropTexture != null) Destroy(m_BackdropTexture);
        if (s_Instance == this) s_Instance = null;
    }

    /// <summary>
    /// Composes the stamp line from the generated build info, falling back to
    /// <see cref="Application.version"/> when the asset has not been generated yet.
    /// </summary>
    private static string BuildStampText()
    {
        var info = BuildInfo.Load();
        string version = string.IsNullOrEmpty(info.Version) ? Application.version : info.Version;

        string line = "DEVELOPMENT BUILD  •  v" + version + "  •  build " + info.BuildLabel;
        if (!string.IsNullOrEmpty(info.Date)) line += "  •  " + info.Date;
        if (Application.isEditor) line += "  •  EDITOR";

        return line;
    }

    private void BuildStyles()
    {
        if (m_LabelStyle != null) return;

        // Same clamped 1080p-relative scale the restart dialog uses, so the stamp
        // stays readable on phones without dominating a desktop window.
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 1.35f);

        m_BackdropTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        m_BackdropTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
        m_BackdropTexture.Apply();

        m_BackdropStyle = new GUIStyle { normal = { background = m_BackdropTexture } };
        m_LabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(18f * scale),
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            normal = { textColor = new Color(1f, 0.82f, 0.25f, 1f) }
        };
    }
}

/// <summary>
/// Parsed contents of the generated Resources/BuildInfo.txt asset.
/// Written by <c>BuildStampGenerator</c> before every build.
/// </summary>
public struct BuildInfo
{
    public string Version;
    public int Build;
    public string Date;
    public string Platform;

    /// <summary>Zero-padded build number for display — "01", "07", "142".</summary>
    public string BuildLabel => Build.ToString("D2");

    /// <summary>
    /// Reads the build info resource. Returns an empty struct (Build 0) when the
    /// asset is missing, e.g. in a project that has never been built.
    /// </summary>
    public static BuildInfo Load()
    {
        var info = new BuildInfo();
        var asset = Resources.Load<TextAsset>(DevBuildStamp.ResourceName);
        if (asset == null) return info;

        foreach (string rawLine in asset.text.Split('\n'))
        {
            string line = rawLine.Trim();
            int split = line.IndexOf('=');
            if (split <= 0) continue;

            string key = line.Substring(0, split).Trim();
            string value = line.Substring(split + 1).Trim();

            switch (key)
            {
                case "version":  info.Version = value; break;
                case "build":    int.TryParse(value, out info.Build); break;
                case "date":     info.Date = value; break;
                case "platform": info.Platform = value; break;
            }
        }

        return info;
    }
}

#endif
