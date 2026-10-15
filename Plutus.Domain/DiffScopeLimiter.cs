using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Domain;

/// <summary>Outcome of limiting a diff: the finished context plus the paths that were left out.</summary>
public sealed record DiffScopeResult(
    GitDiffContext Context,
    TokenBudgetSummary? Summary,
    IReadOnlyList<string> Excluded);

/// <summary>
/// Turns a raw diff into the bundle's diff scope: maps repository paths onto the bundle roots, reduces every
/// chunk to a limitable item and runs it through <see cref="BundleLimits"/>. Because each diff is its own
/// item, one oversized or over-budget change is excluded on its own instead of discarding the change set.
/// No I/O happens here; the caller supplies the repository root and the file sizes.
/// </summary>
public sealed class DiffScopeLimiter(BundleLimits limits, bool ignorePathCase = true)
{
    /// <summary>Reason recorded on a change a size limit excluded.</summary>
    public const string SizeExclusionReason = "file is larger than max-file-size";

    /// <summary>Reason recorded on a change the token budget excluded.</summary>
    public const string BudgetExclusionReason = "omitted by token budget";

    public DiffScopeResult Apply(
        string repositoryRoot,
        string comparisonBase,
        IReadOnlyList<DiffChunk> trackedChunks,
        IReadOnlyList<DiffChunk> untrackedChunks,
        IReadOnlyList<IFileInfo> roots,
        Func<string, long>? fileSize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(comparisonBase);
        ArgumentNullException.ThrowIfNull(trackedChunks);
        ArgumentNullException.ThrowIfNull(untrackedChunks);
        ArgumentNullException.ThrowIfNull(roots);

        // The mapper is built from the repository the caller actually found, never from the process working
        // directory: bundling another project would otherwise relativize paths against the wrong root and
        // silently exclude every file.
        var mapper = new ChangedFileMapper(repositoryRoot, ignorePathCase);

        // Map first: limiting must only consider chunks this bundle will actually contain.
        var mapped = mapper.Resolve(comparisonBase, trackedChunks, untrackedChunks, roots);
        var all = mapped.TrackedChunks.Concat(mapped.UntrackedChunks).ToArray();

        // The repository path identifies a chunk across mapping, so an exclusion can be reported back.
        var items = all
            .Select(chunk => new BundleLimitItem(
                chunk.RepositoryPath,
                chunk.Path,
                fileSize?.Invoke(chunk.RepositoryPath) ?? 0,
                TokenEstimator.Estimate(chunk.Text)))
            .ToArray();

        var result = limits.Apply(items);
        var included = result.IncludedKeys.ToHashSet(StringComparer.Ordinal);
        var statuses = result.Exclusions.ToDictionary(
            exclusion => exclusion.Key,
            exclusion => exclusion.IsSizeLimit ? NodeStatus.OversizedFile : NodeStatus.TokenBudgetOmitted,
            StringComparer.Ordinal);

        var limited = all
            .Select(chunk => chunk with
            {
                EstimatedTokens = TokenEstimator.Estimate(chunk.Text),

                // The reason doubles as the signal behind the tree marker, so it stays short and stable.
                SkipReason = included.Contains(chunk.RepositoryPath)
                    ? null
                    : statuses.TryGetValue(chunk.RepositoryPath, out var status) && status == NodeStatus.OversizedFile
                        ? SizeExclusionReason
                        : BudgetExclusionReason
            })
            .ToArray();

        var context = Rebuild(mapped, [.. limited.Take(mapped.TrackedChunks.Count)], [.. limited.Skip(mapped.TrackedChunks.Count)]);
        return new DiffScopeResult(context, limits.CreateSummary(result), result.Exclusions.Select(exclusion => exclusion.DisplayPath).ToArray());
    }

    /// <summary>
    /// Merges the limited chunks back into the per-root groups. The groups were produced from these same
    /// chunks, so the repository path is a stable way back to the owning root.
    /// </summary>
    private static GitDiffContext Rebuild(
        GitDiffContext mapped,
        IReadOnlyList<DiffChunk> tracked,
        IReadOnlyList<DiffChunk> untracked)
    {
        var groups = mapped.FileGroups.Select(group => group with
        {
            TrackedChunks = tracked.Where(chunk => group.TrackedChunks.Any(original => original.RepositoryPath == chunk.RepositoryPath)).ToArray(),
            UntrackedChunks = untracked.Where(chunk => group.UntrackedChunks.Any(original => original.RepositoryPath == chunk.RepositoryPath)).ToArray()
        }).ToArray();

        return mapped with { FileGroups = groups };
    }
}
