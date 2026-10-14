namespace Plutus.Domain.Diffing;

/// <summary>Changed files discovered under one bundle root, with paths relative to that root.</summary>
public sealed record DiffFileGroup(string RootPath, IReadOnlyList<ChangedFile> Files, IReadOnlyList<string> UntrackedPaths);

/// <summary>
/// Discovery result of a diff scoped bundle run: the files that changed, the untracked files that were
/// requested, and the raw unified diff that documents the tracked change structurally. A <c>null</c> context
/// means diff scoping was not requested.
/// </summary>
public sealed record GitDiffContext(
    string ComparisonBase,
    IReadOnlyList<ChangedFile> Files,
    string RawText)
{
    /// <summary>Paths git does not track yet, ordered as reported; empty unless untracked files were requested.</summary>
    public IReadOnlyList<string> UntrackedFiles { get; init; } = [];

    /// <summary>
    /// Generated new-file diffs for <see cref="UntrackedFiles"/>, kept apart from <see cref="RawText"/> so the
    /// tracked diff stays a verbatim <c>git diff</c> and <see cref="Files"/> keeps describing tracked changes
    /// only. Writers render both.
    /// </summary>
    public string UntrackedRawText { get; init; } = string.Empty;

    /// <summary>Per-root views resolved by <see cref="Plutus.Domain.ChangedFileMapper"/>; empty until mapping ran.</summary>
    public IReadOnlyList<DiffFileGroup> FileGroups { get; init; } = [];

    /// <summary>Changed files under one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<string> PathsFor(string rootPath, bool ignorePathCase = false) =>
        GroupFor(rootPath, ignorePathCase)?.Files.Select(file => file.Path).ToArray() ?? [];

    /// <summary>Untracked files under one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<string> UntrackedPathsFor(string rootPath, bool ignorePathCase = false) =>
        GroupFor(rootPath, ignorePathCase)?.UntrackedPaths ?? [];

    private DiffFileGroup? GroupFor(string rootPath, bool ignorePathCase)
    {
        var comparison = ignorePathCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var group in FileGroups)
        {
            if (group.RootPath.Equals(rootPath, comparison))
            {
                return group;
            }
        }

        return null;
    }
}
