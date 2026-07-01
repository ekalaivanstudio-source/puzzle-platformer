using System.Collections.Generic;
using UnityEngine;

namespace LevelGenerationSystem
{
    /// <summary>
    /// Designer-authored mapping of "which character in a level text file spawns which prefab".
    ///
    /// One asset per tile set. Create via the project window:
    ///   Create → Level Generation → Tile Palette
    ///
    /// A level is described in a plain text file where each line is a row and each character is a
    /// single cell. <see cref="TextLevelGenerator"/> reads that file, looks every character up in
    /// this palette, and instantiates the matching prefab. Characters listed in
    /// <see cref="m_EmptySymbols"/> (and spaces/tabs) mean "nothing here"; any character with no
    /// entry is skipped with a warning.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TilePalette",
        menuName = "Level Generation/Tile Palette",
        order = 0)]
    public class TilePalette : ScriptableObject
    {
        /// <summary>A single character → prefab binding.</summary>
        [System.Serializable]
        public class TileDefinition
        {
            [Tooltip("The character used in the level text file for this tile (e.g. \"#\"). " +
                     "Only the FIRST character is used.")]
            public string Symbol = "#";

            [Tooltip("Prefab spawned wherever this symbol appears in the level text.")]
            public GameObject Prefab;

            [Tooltip("Optional note for designers. Not used at runtime.")]
            public string Description;

            /// <summary>The first character of <see cref="Symbol"/>, or '\0' if empty.</summary>
            public char SymbolChar => string.IsNullOrEmpty(Symbol) ? '\0' : Symbol[0];
        }

        [Header("Grid")]
        [Tooltip("World-space size of one tile cell, in units. The project grid unit is 1.")]
        [SerializeField] private float m_TileSize = 1f;

        [Header("Empty Cells")]
        [Tooltip("Characters that mean \"no tile here\" (empty space). These are skipped silently. " +
                 "Spaces and tabs are ALWAYS treated as empty. Default: \".\"")]
        [SerializeField] private string m_EmptySymbols = ".";

        [Header("Tiles")]
        [Tooltip("Maps each text symbol to the prefab spawned for it.")]
        [SerializeField] private List<TileDefinition> m_Tiles = new List<TileDefinition>();

        // Built lazily from m_Tiles; invalidated whenever the asset is enabled or edited.
        private Dictionary<char, GameObject> m_Lookup;

        /// <summary>World size of one cell. Drives spacing between generated tiles.</summary>
        public float TileSize => m_TileSize;

        /// <summary>True if <paramref name="c"/> should produce no tile (empty space).</summary>
        public bool IsEmptySymbol(char c)
        {
            if (c == ' ' || c == '\t') return true;
            return !string.IsNullOrEmpty(m_EmptySymbols) && m_EmptySymbols.IndexOf(c) >= 0;
        }

        /// <summary>
        /// Looks up the prefab for a character. Returns true and sets <paramref name="prefab"/>
        /// when a non-null mapping exists; false otherwise. Does NOT account for empty symbols —
        /// callers should check <see cref="IsEmptySymbol"/> first.
        /// </summary>
        public bool TryGetPrefab(char c, out GameObject prefab)
        {
            EnsureLookup();
            return m_Lookup.TryGetValue(c, out prefab) && prefab != null;
        }

        private void EnsureLookup()
        {
            if (m_Lookup != null) return;
            m_Lookup = new Dictionary<char, GameObject>();
            foreach (TileDefinition t in m_Tiles)
            {
                if (t == null || t.Prefab == null) continue;
                char c = t.SymbolChar;
                if (c == '\0') continue;
                m_Lookup[c] = t.Prefab; // last entry wins; duplicates warned in OnValidate
            }
        }

        // The cache must not survive a domain reload / re-edit, or it would go stale.
        private void OnEnable() => m_Lookup = null;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only sanity check: warns about a bad tile size, multi-character symbols,
        /// symbols that collide with the empty list, duplicates, and missing prefabs — so content
        /// mistakes surface while editing rather than as a broken level at runtime.
        /// </summary>
        private void OnValidate()
        {
            m_Lookup = null;

            if (m_TileSize <= 0f)
                Debug.LogWarning("[TilePalette] Tile Size should be greater than 0.", this);

            var seen = new HashSet<char>();
            for (int i = 0; i < m_Tiles.Count; i++)
            {
                TileDefinition t = m_Tiles[i];
                if (t == null) continue;

                if (string.IsNullOrEmpty(t.Symbol))
                {
                    Debug.LogWarning($"[TilePalette] Entry {i} has an empty symbol.", this);
                    continue;
                }
                if (t.Symbol.Length > 1)
                    Debug.LogWarning($"[TilePalette] Entry {i} symbol \"{t.Symbol}\" is longer than one " +
                                     $"character; only '{t.SymbolChar}' is used.", this);

                char c = t.SymbolChar;
                if (IsEmptySymbol(c))
                    Debug.LogWarning($"[TilePalette] Symbol '{c}' (entry {i}) is also an empty symbol; " +
                                     "it will be treated as EMPTY and never spawn.", this);
                if (!seen.Add(c))
                    Debug.LogWarning($"[TilePalette] Duplicate symbol '{c}' — the last entry wins.", this);
                if (t.Prefab == null)
                    Debug.LogWarning($"[TilePalette] Symbol '{c}' (entry {i}) has no prefab assigned.", this);
            }
        }
#endif
    }
}
