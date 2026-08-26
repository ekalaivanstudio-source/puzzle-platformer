using System.Collections.Generic;

/// <summary>
/// The one place that answers "what is this level's solution?".
///
/// The solution is read from the level's <see cref="LevelConfig"/>
/// (<c>sequence.autoPlaySequence</c>). When that is empty it falls back to the correct
/// sequence registered on <see cref="SequenceManager"/>, which is usable only if that
/// sequence has no <see cref="ActionTypeEnum.Any"/> wildcards — a wildcard says "any input
/// is accepted here", which is neither playable nor teachable.
///
/// Used by <see cref="AutoPlayTester"/> to play the level and by
/// <see cref="TutorialSequenceGuide"/> to teach it, so both read the same authored data.
/// </summary>
public static class LevelSolution
{
    /// <summary>
    /// Resolves the level's solution. Returns false with a <paramref name="reason"/> the
    /// designer can act on, rather than a silent no-op.
    /// </summary>
    public static bool TryResolve(out ActionTypeEnum[] solution, out string reason)
    {
        solution = null;
        reason = null;

        LevelConfig config = LevelContext.Instance != null ? LevelContext.Instance.Config : null;
        ActionTypeEnum[] authored = config != null ? config.sequence.autoPlaySequence : null;

        if (authored != null && authored.Length > 0)
        {
            int wildcard = System.Array.IndexOf(authored, ActionTypeEnum.Any);
            if (wildcard >= 0)
            {
                reason = $"'{config.name}' has Any at index {wildcard} of its Auto Play Sequence. " +
                         "A wildcard is not a concrete action — replace it with the action the " +
                         "solution actually uses.";
                return false;
            }

            solution = authored;
            return true;
        }

        // No authored solution — fall back to the level's correct sequence.
        IReadOnlyList<ActionTypeEnum> correct =
            SequenceManager.Instance != null ? SequenceManager.Instance.CorrectSequence : null;
        string configName = config != null ? config.name : "this level's LevelConfig";

        if (correct == null || correct.Count == 0)
        {
            reason = $"No solution available. Fill in Sequence ▸ Auto Play Sequence on {configName}.";
            return false;
        }

        var resolved = new ActionTypeEnum[correct.Count];
        for (int i = 0; i < correct.Count; i++)
        {
            if (correct[i] == ActionTypeEnum.Any)
            {
                reason = $"The correct sequence has Any at index {i}, so it cannot be used. " +
                         $"Fill in Sequence ▸ Auto Play Sequence on {configName} instead.";
                return false;
            }
            resolved[i] = correct[i];
        }

        solution = resolved;
        return true;
    }
}
