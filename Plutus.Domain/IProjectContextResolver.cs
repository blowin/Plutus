using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain;

public interface IProjectContextResolver
{
    Task<ProjectResolveResponse?> ResolveContextAsync(List<string>? projectPaths, string? outputPath);
}

public record ProjectResolveResponse(List<IFileInfo> Roots, IFileInfo Output);
