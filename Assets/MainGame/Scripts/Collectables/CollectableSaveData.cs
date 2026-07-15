using System;
using System.Collections.Generic;

namespace Collectables
{
    /// <summary>One collected collectable, remembered forever once picked up.</summary>
    [Serializable]
    public class CollectedRecord
    {
        /// <summary>Stable per-instance id of the collectable (see <see cref="Collectable.UniqueId"/>).</summary>
        public string id;

        /// <summary>Which collectable family this record belongs to.</summary>
        public CollectableType type;

        /// <summary>The level the collectable was picked up in (scene build index).</summary>
        public int level;
    }

    /// <summary>
    /// Root serialisable container written to disk as JSON by <see cref="CollectableSaveSystem"/>.
    /// </summary>
    [Serializable]
    public class CollectableSaveData
    {
        public List<CollectedRecord> records = new List<CollectedRecord>();
    }
}
