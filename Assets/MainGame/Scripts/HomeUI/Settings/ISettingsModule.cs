namespace HomeUI
{
    /// <summary>
    /// Implemented by every settings category manager (Graphics, Audio, Input, …).
    ///
    /// This is the seam that makes the settings system open for extension but closed for
    /// modification: <see cref="SettingsManager"/> discovers all modules and calls
    /// <see cref="Apply"/> on each, so a brand-new category is added by writing one new
    /// MonoBehaviour that implements this interface — no edits to the coordinator.
    /// </summary>
    public interface ISettingsModule
    {
        /// <summary>Translates the relevant fields of <paramref name="data"/> into engine calls.</summary>
        void Apply(SettingsData data);
    }
}
