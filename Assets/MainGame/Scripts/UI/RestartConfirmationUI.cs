using UnityEngine;

/// <summary>
/// Runtime restart confirmation used by the Restart input action.
/// It installs itself automatically, so existing level scenes need no changes.
/// </summary>
public sealed class RestartConfirmationUI : MonoBehaviour
{
    private enum ConfirmationAction
    {
        RestartLevel,
        GoToHomeScreen
    }

    private static RestartConfirmationUI s_Instance;

    private bool m_IsOpen;
    private bool m_WasGameplayInputEnabled;
    private ConfirmationAction m_Action;
    private GUIStyle m_BackdropStyle;
    private GUIStyle m_DialogStyle;
    private GUIStyle m_MessageStyle;
    private GUIStyle m_ButtonStyle;

    public static bool IsOpen => s_Instance != null && s_Instance.m_IsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (s_Instance != null) return;

        GameObject host = new GameObject(nameof(RestartConfirmationUI));
        s_Instance = host.AddComponent<RestartConfirmationUI>();
        DontDestroyOnLoad(host);
    }

    public static void ShowRestart()
    {
        Show(ConfirmationAction.RestartLevel);
    }

    public static void ShowHomeScreen()
    {
        Show(ConfirmationAction.GoToHomeScreen);
    }

    private static void Show(ConfirmationAction action)
    {
        if (s_Instance == null) Install();
        if (s_Instance.m_IsOpen) return;

        s_Instance.m_IsOpen = true;
        s_Instance.m_Action = action;
        DeviceInputProvider input = DeviceInputProvider.Instance;
        s_Instance.m_WasGameplayInputEnabled = input != null && input.IsEnabled;
        input?.SetEnabled(false);
    }

    private void OnGUI()
    {
        if (!m_IsOpen) return;

        BuildStyles();

        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, m_BackdropStyle);

        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 1.35f);
        float width = 520f * scale;
        float height = 220f * scale;
        Rect dialog = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(dialog, GUIContent.none, m_DialogStyle);
        GUI.Label(
            new Rect(dialog.x + 35f * scale, dialog.y + 28f * scale, width - 70f * scale, 90f * scale),
            m_Action == ConfirmationAction.RestartLevel
                ? "Are you sure you want to restart the level?"
                : "Are you sure you want to return to the home screen?",
            m_MessageStyle);

        float buttonWidth = 145f * scale;
        float buttonHeight = 50f * scale;
        float buttonY = dialog.y + height - 75f * scale;

        if (GUI.Button(new Rect(dialog.x + 75f * scale, buttonY, buttonWidth, buttonHeight), "Yes", m_ButtonStyle))
            Confirm();

        if (GUI.Button(new Rect(dialog.x + width - 75f * scale - buttonWidth, buttonY, buttonWidth, buttonHeight), "No", m_ButtonStyle))
            Close();
    }

    private void Confirm()
    {
        m_IsOpen = false;
        if (m_Action == ConfirmationAction.RestartLevel)
            GameManager.Instance?.RestartLevel();
        else
            GameManager.Instance?.GoToMainMenu();
    }

    private void Close()
    {
        m_IsOpen = false;
        if (m_WasGameplayInputEnabled)
            DeviceInputProvider.Instance?.SetEnabled(true);
    }

    private void BuildStyles()
    {
        if (m_DialogStyle != null) return;

        Texture2D backdrop = MakeTexture(new Color(0f, 0f, 0f, 0.72f));
        Texture2D dialog = MakeTexture(new Color(0.075f, 0.09f, 0.13f, 0.98f));

        m_BackdropStyle = new GUIStyle { normal = { background = backdrop } };
        m_DialogStyle = new GUIStyle(GUI.skin.box) { normal = { background = dialog } };
        m_MessageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        m_ButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
