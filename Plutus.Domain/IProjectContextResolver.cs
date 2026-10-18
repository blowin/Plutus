using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public interface IProjectContextResolver
{
    Task<ProjectResolveResponse?> ResolveContextAsync(List<string>? projectPaths, string? outputPath, string? format, string? branch, string? commit, CancellationToken cancellationToken = default);
}

public record ProjectResolveResponse(
    List<IFileInfo> Roots,
    IFileInfo Output,
    IReadOnlyList<string> ExplicitInitialFiles)
{
    public IReadOnlyList<string> ExplicitInitialFiles { get; init; } = [];
}
