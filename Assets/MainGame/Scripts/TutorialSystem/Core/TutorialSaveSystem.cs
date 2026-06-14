using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// Persists tutorial progress between sessions.
    ///
    /// Two things are stored per sequence id:
    ///   • <b>Completed</b>     — has this tutorial fully finished? (used to skip PlayOnce tutorials)
    ///   • <b>Resume index</b>  — index of the next step to play (used by Resumable tutorials)
    ///
    /// Backed by <see cref="PlayerPrefs"/> so it works on every platform with zero setup. The keys
    /// are namespaced so they never collide with other game saves. Swap the four primitive
    /// Read*/Write* methods for your own JSON/cloud backend if you prefer — nothing else changes.
    /// </summary>
    public static class TutorialSaveSystem
    {
        private const string CompletedPrefix = "Tutorial.Completed.";
        private const string ResumePrefix    = "Tutorial.Resume.";

        /// <summary>True once <see cref="MarkCompleted"/> has been called for this sequence.</summary>
        public static bool IsCompleted(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return false;
            return PlayerPrefs.GetInt(CompletedPrefix + sequenceId, 0) == 1;
        }

        /// <summary>Records that a tutorial finished and clears its resume index.</summary>
        public static void MarkCompleted(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return;
            PlayerPrefs.SetInt(CompletedPrefix + sequenceId, 1);
            PlayerPrefs.DeleteKey(ResumePrefix + sequenceId);
            PlayerPrefs.Save();
        }

        /// <summary>Index of the next step to play for a resumable tutorial (0 if none saved).</summary>
        public static int GetResumeIndex(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return 0;
            return PlayerPrefs.GetInt(ResumePrefix + sequenceId, 0);
        }

        /// <summary>Stores the index of the next step to play (for resumable tutorials).</summary>
        public static void SetResumeIndex(string sequenceId, int nextStepIndex)
        {
            if (string.IsNullOrEmpty(sequenceId)) return;
            PlayerPrefs.SetInt(ResumePrefix + sequenceId, nextStepIndex);
            PlayerPrefs.Save();
        }

        /// <summary>Wipes all progress for a single tutorial (handy for QA / a "replay" button).</summary>
        public static void ResetSequence(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return;
            PlayerPrefs.DeleteKey(CompletedPrefix + sequenceId);
            PlayerPrefs.DeleteKey(ResumePrefix + sequenceId);
            PlayerPrefs.Save();
        }
    }
}
