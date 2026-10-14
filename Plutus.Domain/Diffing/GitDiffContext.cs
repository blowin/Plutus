namespace Plutus.Domain.Diffing;

/// <summary>Changed files discovered under one bundle root, with paths relative to that root.</summary>
public sealed record DiffFileGroup(string RootPath, IReadOnlyList<ChangedFile> Files);

/// <summary>
/// Discovery result of a diff scoped bundle run: the files that changed and the raw unified diff that
/// documents the change structurally. A <c>null</c> context means diff scoping was not requested.
/// </summary>
public sealed record GitDiffContext(
    string ComparisonBase,
    IReadOnlyList<ChangedFile> Files,
    string RawText)
{
    /// <summary>Per-root views resolved by <see cref="Plutus.Domain.ChangedFileMapper"/>; empty until mapping ran.</summary>
    public IReadOnlyList<DiffFileGroup> FileGroups { get; init; } = [];

    /// <summary>Paths of the changed files under one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<string> PathsFor(string rootPath, bool ignorePathCase = false)
    {
        var comparison = ignorePathCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var group in FileGroups)
        {
            if (group.RootPath.Equals(rootPath, comparison))
            {
                return group.Files.Select(f => f.Path).ToArray();
            }
        }

        return [];
    }
}
