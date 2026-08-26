using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ModernLevelSelection;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [System.Serializable]
    public class MenuPanel
    {
        public string panelName;
        public CanvasGroup canvasGroup;
    }

    [Header("Panels")]
    [SerializeField] private MenuPanel[] panels;

    [Header("Settings")]
    [SerializeField] private float transitionTime = 0.25f;
    [Header("Buttons")]
    [SerializeField]
    private Button newGameButton, quitButton;
    private CanvasGroup currentPanel;
    private bool isTransitioning;

    private void Start()
    {
        // Hide all panels except the first one
        for (int i = 0; i < panels.Length; i++)
        {
            bool active = i == 0;

            panels[i].canvasGroup.alpha = active ? 1 : 0;
            panels[i].canvasGroup.interactable = active;
            panels[i].canvasGroup.blocksRaycasts = active;
            panels[i].canvasGroup.gameObject.SetActive(active);

            if (active)
                currentPanel = panels[i].canvasGroup;
        }
    }

    public void OpenPanel(string panelName)
    {
        if (isTransitioning)
            return;

        foreach (var panel in panels)
        {
            if (panel.panelName == panelName)
            {
                StartCoroutine(SwitchPanel(panel.canvasGroup));
                return;
            }
        }

        Debug.LogWarning($"Panel '{panelName}' not found.");
    }

    private IEnumerator SwitchPanel(CanvasGroup nextPanel)
    {
        isTransitioning = true;

        if (currentPanel != null)
        {
            yield return StartCoroutine(Fade(currentPanel, 1, 0));

            currentPanel.interactable = false;
            currentPanel.blocksRaycasts = false;
            currentPanel.gameObject.SetActive(false);
        }

        nextPanel.gameObject.SetActive(true);
        nextPanel.alpha = 0;
        nextPanel.interactable = true;
        nextPanel.blocksRaycasts = true;

        yield return StartCoroutine(Fade(nextPanel, 0, 1));

        currentPanel = nextPanel;
        isTransitioning = false;
    }

    private IEnumerator Fade(CanvasGroup group, float from, float to)
    {
        float t = 0;

        while (t < transitionTime)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / transitionTime);
            yield return null;
        }

        group.alpha = to;
    }

    private void RegisterButton()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(NewGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }
    /// <summary>
    /// Starts a new game by resetting progress and loading the first level.
    /// </summary>
    public void NewGame()
    {
        SaveManager.ResetProgress();

        // Wipe collectable progress (Robot Parts / Memory Shards) for a fresh start.
        Collectables.CollectableSaveSystem.ResetAll();

        // Make sure Level 1 is unlocked.
        SaveManager.SetHighestUnlocked(1);

        // The intro cutscene loads the level itself once it finishes. It declines when the home
        // screen has no cutscene built, in which case we go straight in as before.
        if (!MainGame.UI.Unified.IntroCutsceneScreen.TryPlay(1))
        {
            SceneManager.LoadScene(1);
        }
    }

    /// <summary>
    /// Quits the application.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (newGameButton != null)
            newGameButton.onClick.RemoveListener(NewGame);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }
    private void Awake()
    {
        RegisterButton();
    }
}