using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single per-scene holder for a level's <see cref="LevelConfig"/>. Put one on a
/// manager object in each level scene and assign that level's config asset. Every other
/// system (camera, sequence, collectables) reads <c>LevelContext.Instance.Config</c>, so
/// the level's data is assigned in exactly one place.
///
/// Runs before other scripts (negative execution order) so consumers can safely read the
/// config in their own <c>Awake</c>.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class LevelContext : MonoBehaviour
{
    public static LevelContext Instance { get; private set; }

    [Tooltip("This level's config asset. Holds collectable counts, camera and sequence settings.")]
    [SerializeField] private LevelConfig config;

    /// <summary>The level's config, or null if none was assigned.</summary>
    public LevelConfig Config => config;

    /// <summary>
    /// Level number: the config's value when set, otherwise the scene build index
    /// (Home = 0, Level1 = 1, …).
    /// </summary>
    public int CurrentLevel =>
        config != null && config.levelNumber > 0
            ? config.levelNumber
            : SceneManager.GetActiveScene().buildIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[LevelContext] Another instance already exists in this scene.", this);
        Instance = this;

        if (config == null)
            Debug.LogWarning("[LevelContext] No LevelConfig assigned; systems will use their built-in defaults.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
