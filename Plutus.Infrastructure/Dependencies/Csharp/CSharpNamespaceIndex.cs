using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public sealed class CSharpNamespaceIndex(RoslynTreeCache treeCache) : ICSharpNamespaceIndex
{
    private readonly Dictionary<string, List<string>> _map = new(StringComparer.OrdinalIgnoreCase);
    private IFileProvider? _cachedProvider;
    private bool _isBuilt;
    private readonly IIgnoreMatcher _ignoreMatcher = new IgnoreMatcherBuilder()
        .IgnoreJunk()
        .Build();

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetMapAsync(
        IFileProvider provider,
        CancellationToken ct)
    {
        if (_isBuilt && ReferenceEquals(_cachedProvider, provider))
        {
            return _map.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value.AsReadOnly());
        }

        _map.Clear();
        _cachedProvider = provider;

        await ScanDirectoryAsync(provider, string.Empty, ct);

        _isBuilt = true;
        return _map.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value.AsReadOnly());
    }

    private async Task ScanDirectoryAsync(IFileProvider provider, string subPath, CancellationToken ct)
    {
        var contents = provider.GetDirectoryContents(subPath);
        if (!contents.Exists)
        {
            return;
        }

        foreach (var entry in contents)
        {
            ct.ThrowIfCancellationRequested();
            var path = string.IsNullOrEmpty(subPath) ? entry.Name : $"{subPath}/{entry.Name}";

            if (entry.IsDirectory)
            {
                // Skip common junk directories to speed up the scan
                if (_ignoreMatcher.IsIgnoredDirectory(entry.Name))
                {
                    continue;
                }

                await ScanDirectoryAsync(provider, path, ct);
            }
            else if (entry.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                await IndexFileAsync(provider, path, ct);
            }
        }
    }

    private async Task IndexFileAsync(IFileProvider provider, string path, CancellationToken ct)
    {
        var cacheEntry = await treeCache.GetOrParseAsync(path, provider, ct);
        if (cacheEntry.IsEmpty)
        {
            return;
        }

        var namespaces = cacheEntry.Root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .DefaultIfEmpty("<global>")
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var ns in namespaces)
        {
            if (!_map.TryGetValue(ns, out var list))
            {
                list = [];
                _map[ns] = list;
            }
            list.Add(path);
        }
    }
}
