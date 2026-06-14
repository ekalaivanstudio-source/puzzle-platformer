using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Designer-authored default settings. Used on first launch and whenever the player resets
    /// settings. Keeping defaults in a ScriptableObject (rather than hard-coded) lets each project
    /// ship its own sensible starting point — e.g. a low-end mobile build defaults to Low quality —
    /// without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "SettingsDefaults", menuName = "Home UI/Settings Defaults", order = 0)]
    public class SettingsDefaults : ScriptableObject
    {
        [Tooltip("The values applied on first run / after a reset.")]
        [SerializeField] private SettingsData m_Defaults = new SettingsData();

        /// <summary>A fresh copy of the defaults (the asset itself is never mutated at runtime).</summary>
        public SettingsData CreateDefaultData() => m_Defaults.Clone();
    }
}
