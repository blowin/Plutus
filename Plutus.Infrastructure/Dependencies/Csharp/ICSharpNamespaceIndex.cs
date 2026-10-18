using Microsoft.Extensions.FileProviders;

namespace Plutus.Infrastructure.Dependencies.Csharp;

/// <summary>
/// Builds and caches a mapping of C# namespaces to the virtual paths of the files that declare them.
/// Scanning happens once per file provider to avoid quadratic parsing costs during dependency resolution.
/// </summary>
public interface ICSharpNamespaceIndex
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetMapAsync(
        IFileProvider provider,
        CancellationToken ct);
}
