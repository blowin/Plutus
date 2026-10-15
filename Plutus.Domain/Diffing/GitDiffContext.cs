namespace Plutus.Domain.Diffing;

/// <summary>
/// The diff scoped view of one bundle root: the tracked file diffs and the generated diffs for untracked
/// files, both with paths relative to that root. <see cref="ChangedFileMapper"/> produces one group per root
/// and drops paths that belong to no root.
/// </summary>
public sealed record DiffFileGroup(
    string RootPath,
    IReadOnlyList<DiffChunk> TrackedChunks,
    IReadOnlyList<DiffChunk> UntrackedChunks)
{
    public IReadOnlyList<string> TrackedPaths => TrackedChunks.Select(chunk => chunk.Path).ToArray();

    public IReadOnlyList<string> UntrackedPaths => UntrackedChunks.Select(chunk => chunk.Path).ToArray();
}

/// <summary>
/// Discovery result of a diff scoped bundle run: the revision the comparison was made against and the
/// per-root diff views that the bundle is limited to. A <c>null</c> context means diff scoping was not
/// requested.
/// </summary>
public sealed record GitDiffContext(string ComparisonBase)
{
    /// <summary>Per-root views resolved by <see cref="Plutus.Domain.ChangedFileMapper"/>; empty until mapping ran.</summary>
    public IReadOnlyList<DiffFileGroup> FileGroups { get; init; } = [];

    /// <summary>Tracked chunks for every root, used for the top-level diff section.</summary>
    public IReadOnlyList<DiffChunk> TrackedChunks => FileGroups.SelectMany(group => group.TrackedChunks).ToArray();

    /// <summary>Untracked chunks for every root, used for the top-level diff section.</summary>
    public IReadOnlyList<DiffChunk> UntrackedChunks => FileGroups.SelectMany(group => group.UntrackedChunks).ToArray();

    /// <summary>Tracked paths visible from one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<string> TrackedPathsFor(string rootPath) => GroupFor(rootPath)?.TrackedPaths ?? [];

    /// <summary>Untracked paths visible from one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<string> UntrackedPathsFor(string rootPath) => GroupFor(rootPath)?.UntrackedPaths ?? [];

    /// <summary>Tracked chunks visible from one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<DiffChunk> TrackedChunksFor(string rootPath) => GroupFor(rootPath)?.TrackedChunks ?? [];

    /// <summary>Untracked chunks visible from one root, or an empty list when that root has none.</summary>
    public IReadOnlyList<DiffChunk> UntrackedChunksFor(string rootPath) => GroupFor(rootPath)?.UntrackedChunks ?? [];

    /// <summary>
    /// Finds the view for one bundle root. Both sides are normalized before comparing, because the same
    /// directory reaches here as <c>D:\dir</c> from the file provider and as <c>D:/dir</c> from git; an exact
    /// string comparison silently produced an empty set, which excluded every file from the bundle.
    /// </summary>
    private DiffFileGroup? GroupFor(string rootPath)
    {
        var normalized = VcsPathNormalizer.RootPrefix(rootPath);

        return FileGroups.FirstOrDefault(group =>
            VcsPathNormalizer.RootPrefix(group.RootPath).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
