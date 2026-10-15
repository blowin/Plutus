using Plutus.Domain.Tokens;

namespace Plutus.Domain.Diffing;

/// <summary>
/// Selects the revision range used to isolate bundle context to changed files, and carries the limits that
/// apply to the resulting diffs.
/// </summary>
public sealed record GitDiffOptions
{
    /// <summary>Base revision to compare against. Equivalent to <c>HEAD</c> when <see cref="Target"/> is not set.</summary>
    public string? Target { get; init; }

    /// <summary>
    /// Upper revision of an explicit range. Without it the comparison runs from the merge base of
    /// <see cref="Target"/> to the working tree, so uncommitted work is still reviewed.
    /// </summary>
    public string? TargetTo { get; init; }

    /// <summary>
    /// Also treat files git does not track yet as part of the change. They are invisible to
    /// <c>git diff</c>, so without this a newly created file is silently absent from the bundle.
    /// </summary>
    public bool IncludeUntracked { get; init; }

    /// <summary>
    /// Largest diff allowed for one file. A bigger diff is left out on its own rather than dropping the
    /// whole change set, which is what makes the limit usable on a diff.
    /// </summary>
    public FileSize MaxFileSize { get; init; } = FileSize.FromBytes(long.MaxValue);

    /// <summary>
    /// Optional token budget applied to the diffs, using the same policy and selection strategy as the
    /// content path. Ignored when <c>null</c>.
    /// </summary>
    public TokenBudgetOptions? TokenBudget { get; init; }
}
