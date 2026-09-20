using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every <see cref="LevelConfig"/> in the game, in one asset, so a level's data can be read
/// <b>without that level's scene being open</b>.
///
/// <see cref="LevelContext"/> covers the normal case: a level scene holds its own config and
/// hands it to the systems running in it. The level selection map is the case it cannot
/// cover — it draws twenty levels while standing in the home screen, so there is no
/// <c>LevelContext</c> for any of them. This database is the answer: it lives in
/// <c>Resources/</c>, so <see cref="Collectables.LevelCollectableService"/> can load it with
/// no scene reference and ask "what does level 3 hide?" from anywhere.
///
/// It holds <b>references</b> to the config assets rather than a copy of their values, so it
/// can never disagree with them. The only thing that can go stale is the <i>list</i> — a
/// newly created config is not in it until the list is rebuilt, which
/// <c>Tools ▸ Level Collectables ▸ Rebuild Level Config Database</c> does, and which the
/// editor also does by itself whenever a LevelConfig asset is added, removed or renamed.
/// </summary>
[CreateAssetMenu(fileName = "LevelConfigDatabase", menuName = "Level/Level Config Database", order = 1)]
public class LevelConfigDatabase : ScriptableObject
{
    /// <summary>Path passed to <see cref="Resources.Load"/> — the asset must sit at Assets/Resources/LevelConfigDatabase.asset.</summary>
    public const string ResourcePath = "LevelConfigDatabase";

    [Tooltip("Every level's config, in levelNumber order. Rebuilt by " +
             "Tools ▸ Level Collectables ▸ Rebuild Level Config Database; editing by hand is " +
             "allowed but a rebuild will re-sort and re-fill it.")]
    [SerializeField] private List<LevelConfig> m_Configs = new List<LevelConfig>();

    private Dictionary<int, LevelConfig> m_ByLevelNumber;

    /// <summary>How many configs the database holds.</summary>
    public int Count => m_Configs != null ? m_Configs.Count : 0;

    /// <summary>The configs, in the order they were authored. Read-only.</summary>
    public IReadOnlyList<LevelConfig> Configs => m_Configs;

    /// <summary>
    /// The config for a level, or null when no level carries that number. The lookup is
    /// built once and cached, so the map can ask for twenty levels in a frame for free.
    /// </summary>
    public LevelConfig Get(int levelNumber)
    {
        if (m_ByLevelNumber == null) BuildLookup();
        return m_ByLevelNumber.TryGetValue(levelNumber, out LevelConfig config) ? config : null;
    }

    /// <summary>
    /// Drops the cached lookup so the next <see cref="Get"/> rebuilds it. Called after an
    /// editor rebuild; nothing at runtime needs it.
    /// </summary>
    public void InvalidateLookup() => m_ByLevelNumber = null;

#if UNITY_EDITOR
    /// <summary>Editor-only: replaces the contents wholesale. Used by the rebuild tool.</summary>
    public void SetConfigs(List<LevelConfig> configs)
    {
        m_Configs = configs ?? new List<LevelConfig>();
        InvalidateLookup();
    }
#endif

    private void BuildLookup()
    {
        m_ByLevelNumber = new Dictionary<int, LevelConfig>();
        if (m_Configs == null) return;

        for (int i = 0; i < m_Configs.Count; i++)
        {
            LevelConfig config = m_Configs[i];
            if (config == null) continue;

            // First entry wins. Two configs claiming the same level number is an authoring
            // mistake the rebuild tool reports; silently overwriting here would hide it.
            if (!m_ByLevelNumber.ContainsKey(config.levelNumber))
            {
                m_ByLevelNumber.Add(config.levelNumber, config);
            }
        }
    }

    private void OnDisable() => InvalidateLookup();
}
