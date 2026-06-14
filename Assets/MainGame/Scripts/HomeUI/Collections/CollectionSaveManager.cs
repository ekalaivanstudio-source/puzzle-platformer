using System;

namespace HomeUI
{
    /// <summary>
    /// Persistence-only layer for collections (mirrors the level system's SaveManager): it loads,
    /// holds, mutates and writes <see cref="CollectionSaveData"/> and nothing else. Unlock rules
    /// live in <see cref="CollectionManager"/>; the UI subscribes to <see cref="OnChanged"/>.
    /// </summary>
    public static class CollectionSaveManager
    {
        private const string FileName = "collections.json";

        /// <summary>Raised after every save so the Collections UI can refresh live.</summary>
        public static event Action OnChanged;

        private static CollectionSaveData s_Data;

        public static CollectionSaveData Data
        {
            get { if (s_Data == null) Load(); return s_Data; }
        }

        public static void Load() => s_Data = JsonSaveUtility.Load(FileName, new CollectionSaveData());

        public static void Save()
        {
            JsonSaveUtility.Save(FileName, Data);
            OnChanged?.Invoke();
        }

        public static RoboProgress GetRobo(string roboId)
        {
            foreach (RoboProgress r in Data.Robos)
                if (r.RoboId == roboId) return r;
            return null;
        }

        public static RoboProgress GetOrCreateRobo(string roboId, bool unlockedIfNew = false)
        {
            RoboProgress existing = GetRobo(roboId);
            if (existing != null) return existing;

            var created = new RoboProgress(roboId, unlockedIfNew);
            Data.Robos.Add(created);
            return created;
        }

        public static void ResetProgress()
        {
            s_Data = new CollectionSaveData();
            Save();
        }
    }
}
