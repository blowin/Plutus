using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.Diffing;

/// <summary>
/// Resolves the diff a bundle should be limited to. Implementations return <c>null</c> when no diff scoping
/// applies, so callers can treat "diff mode off" and "diff discovery unavailable" alike.
/// </summary>
public interface IGitDiffContextProvider
{
    Task<DiffScopeResult?> ResolveAsync(GitDiffOptions options, IReadOnlyList<IFileInfo> roots, CancellationToken cancellationToken = default);
}
