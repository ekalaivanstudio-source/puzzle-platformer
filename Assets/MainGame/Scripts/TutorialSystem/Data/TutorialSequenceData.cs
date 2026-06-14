using System.Collections.Generic;
using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// An ordered list of <see cref="TutorialStepData"/> that make up one complete tutorial
    /// ("Onboarding", "Boost Shop Intro", "How To Push Bricks", ...).
    ///
    /// Designers create and reorder these in the Tutorial Creator window. The steps are stored as
    /// an ordered list (the list order IS the play order — sub-asset order in the project is
    /// irrelevant), so reordering never touches code.
    ///
    /// Identity: <see cref="SequenceId"/> is the stable save key. Never change it once players have
    /// completed the tutorial, or they'll see it again.
    /// </summary>
    [CreateAssetMenu(fileName = "Tutorial_", menuName = "Tutorial System/Sequence", order = 0)]
    public class TutorialSequenceData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, unique id. Used as the save key for 'already completed' tracking. " +
                 "Never change once shipped.")]
        [SerializeField] private string m_SequenceId = "tutorial_id";

        [Tooltip("Human-readable description for designers. Not used at runtime.")]
        [TextArea(1, 3)]
        [SerializeField] private string m_Description = "";

        [Header("Playback Rules")]
        [Tooltip("If true, this tutorial plays only once ever (tracked by the save system). " +
                 "Untick for tutorials you want to replay every time (e.g. while iterating).")]
        [SerializeField] private bool m_PlayOnce = true;

        [Tooltip("If true and a step is interrupted (scene change / app quit), resume from the last " +
                 "incomplete step next time instead of restarting from step 0.")]
        [SerializeField] private bool m_Resumable = false;

        [Header("Steps")]
        [Tooltip("The steps, in play order. Reorder via the Tutorial Creator window.")]
        [SerializeField] private List<TutorialStepData> m_Steps = new List<TutorialStepData>();

        /// <summary>Stable, unique save key for this tutorial.</summary>
        public string SequenceId => m_SequenceId;

        /// <summary>Designer-facing description.</summary>
        public string Description => m_Description;

        /// <summary>If true, the tutorial is skipped once it has been completed.</summary>
        public bool PlayOnce => m_PlayOnce;

        /// <summary>If true, resume from the last incomplete step rather than restarting.</summary>
        public bool Resumable => m_Resumable;

        /// <summary>Number of steps in the sequence.</summary>
        public int StepCount => m_Steps.Count;

        /// <summary>Read-only view of the ordered steps.</summary>
        public IReadOnlyList<TutorialStepData> Steps => m_Steps;

        /// <summary>Returns the step at <paramref name="index"/>, or null if out of range / missing.</summary>
        public TutorialStepData GetStep(int index)
        {
            if (index < 0 || index >= m_Steps.Count) return null;
            return m_Steps[index];
        }
    }
}
