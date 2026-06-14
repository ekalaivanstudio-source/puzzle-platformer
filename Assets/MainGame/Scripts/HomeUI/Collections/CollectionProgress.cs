using System;
using System.Collections.Generic;

namespace HomeUI
{
    /// <summary>
    /// Runtime progress for a single Robo: whether it is unlocked and which of its parts have been
    /// collected (stored by part id, so reordering/renaming the authored parts list never corrupts
    /// a player's collection).
    /// </summary>
    [Serializable]
    public class RoboProgress
    {
        public string RoboId;
        public bool Unlocked;
        public List<string> CollectedPartIds = new List<string>();

        public RoboProgress() { }
        public RoboProgress(string roboId, bool unlocked)
        {
            RoboId = roboId;
            Unlocked = unlocked;
        }

        public bool HasPart(string partId) => CollectedPartIds.Contains(partId);

        /// <summary>Adds a part id if not already collected. Returns true if it was newly added.</summary>
        public bool AddPart(string partId)
        {
            if (string.IsNullOrEmpty(partId) || CollectedPartIds.Contains(partId)) return false;
            CollectedPartIds.Add(partId);
            return true;
        }
    }

    /// <summary>The full collections save payload — one <see cref="RoboProgress"/> per touched Robo.</summary>
    [Serializable]
    public class CollectionSaveData
    {
        public List<RoboProgress> Robos = new List<RoboProgress>();
    }
}
