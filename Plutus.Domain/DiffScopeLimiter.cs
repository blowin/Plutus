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
/// No I/O happens here; the caller supplies file sizes.
/// </summary>
public sealed class DiffScopeLimiter(ChangedFileMapper changedFileMapper, BundleLimits limits)
{
    public DiffScopeResult Apply(
        string comparisonBase,
        IReadOnlyList<DiffChunk> trackedChunks,
        IReadOnlyList<DiffChunk> untrackedChunks,
        IReadOnlyList<IFileInfo> roots,
        Func<string, long>? fileSize = null)
    {
        ArgumentNullException.ThrowIfNull(comparisonBase);
        ArgumentNullException.ThrowIfNull(trackedChunks);
        ArgumentNullException.ThrowIfNull(untrackedChunks);
        ArgumentNullException.ThrowIfNull(roots);

        // Map first: limiting must only consider chunks this bundle will actually contain.
        var mapped = changedFileMapper.Resolve(comparisonBase, trackedChunks, untrackedChunks, roots);
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
        var reasons = result.Exclusions.ToDictionary(exclusion => exclusion.Key, exclusion => exclusion.Reason, StringComparer.Ordinal);

        var limited = all
            .Select(chunk => chunk with
            {
                EstimatedTokens = TokenEstimator.Estimate(chunk.Text),
                SkipReason = included.Contains(chunk.RepositoryPath)
                    ? null
                    : reasons.GetValueOrDefault(chunk.RepositoryPath, "excluded by bundle limits")
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
