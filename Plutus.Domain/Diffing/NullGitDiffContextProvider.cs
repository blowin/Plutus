using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.Diffing;

/// <summary>Provider used when diff scoping is not requested; the bundle keeps every scanned path.</summary>
public sealed class NullGitDiffContextProvider : IGitDiffContextProvider
{
    public Task<DiffScopeResult?> ResolveAsync(GitDiffOptions options, IReadOnlyList<IFileInfo> roots, CancellationToken cancellationToken = default) =>
        Task.FromResult<DiffScopeResult?>(null);
}
