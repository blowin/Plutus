using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Dependencies;

namespace Plutus.Infrastructure.Dependencies.Csharp;

/// <summary>
/// Analyzes C# source files to discover dependencies via 'using' directives and shared namespaces.
/// Uses Roslyn for accurate parsing and a cached namespace index for efficient resolution.
/// </summary>
public sealed class CSharpSourceAnalyzer(ICSharpNamespaceIndex namespaceIndex) : IDependencyAnalyzer
{
    public bool CanAnalyze(string virtualPath) =>
        virtualPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    public async Task<DependencyAnalysisResult> AnalyzeAsync(
        string virtualPath,
        IFileProvider fileProvider,
        CancellationToken ct)
    {
        var content = await ReadAllTextAsync(fileProvider, virtualPath, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DependencyAnalysisResult([], []);
        }

        var tree = CSharpSyntaxTree.ParseText(content, path: virtualPath, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct);

        // 1. Extract explicit using directives
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(e => e!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Extract the file's own namespaces (defaults to "<global>" if none)
        var ownNamespaces = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .DefaultIfEmpty("<global>")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var namespaceMap = await namespaceIndex.GetMapAsync(fileProvider, ct);
        var dependencies = new List<DiscoveredDependency>();

        // Combine all unique target namespaces to check
        var targetNamespaces = usings.Union(ownNamespaces).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var ns in targetNamespaces.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!namespaceMap.TryGetValue(ns, out var files))
            {
                continue;
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
            {
                // Do not include the file itself as its own dependency
                if (file.Equals(virtualPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isSharedNamespace = ownNamespaces.Contains(ns);
                var reason = isSharedNamespace
                    ? $"C# shared namespace '{ns}'"
                    : $"C# using directive '{ns}'";

                dependencies.Add(new DiscoveredDependency(ns, file, reason));
            }
        }

        return new DependencyAnalysisResult(dependencies, []);
    }

    private static async Task<string> ReadAllTextAsync(IFileProvider provider, string path, CancellationToken ct)
    {
        var fileInfo = provider.GetFileInfo(path);
        if (!fileInfo.Exists)
        {
            return string.Empty;
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
