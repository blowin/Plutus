using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public sealed class CSharpNamespaceIndex(RoslynTreeCache treeCache, UsingNormalizer normalizer) : ICSharpNamespaceIndex
{
    private readonly Dictionary<string, HashSet<string>> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _globalUsings = new(StringComparer.OrdinalIgnoreCase);
    private IFileProvider? _cachedProvider;
    private bool _isBuilt;

    private readonly IIgnoreMatcher _ignoreMatcher = new IgnoreMatcherBuilder()
        .IgnoreJunk()
        .Build();

    public async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> GetMapAsync(
        IFileProvider provider,
        CancellationToken ct)
    {
        await EnsureBuiltAsync(provider, ct);
        return _map.ToDictionary(e => e.Key, e => (IReadOnlySet<string>)e.Value.AsReadOnly());
    }

    public async Task<IReadOnlySet<string>> GetGlobalUsingsAsync(
        IFileProvider provider,
        CancellationToken ct)
    {
        await EnsureBuiltAsync(provider, ct);
        return _globalUsings.AsReadOnly();
    }

    private async Task EnsureBuiltAsync(IFileProvider provider, CancellationToken ct)
    {
        if (_isBuilt && ReferenceEquals(_cachedProvider, provider))
        {
            return;
        }

        _map.Clear();
        _globalUsings.Clear();
        _cachedProvider = provider;

        await ScanDirectoryAsync(provider, string.Empty, ct);
        _isBuilt = true;
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

        var root = cacheEntry.Root;

        var namespaces = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .DefaultIfEmpty("<global>")
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var ns in namespaces)
        {
            if (!_map.TryGetValue(ns, out var list))
            {
                list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _map[ns] = list;
            }

            list.Add(path);
        }

        var globalUsingDirectives = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.None));

        foreach (var u in globalUsingDirectives)
        {
            var normalizedNamespace = normalizer.Normalize(u);
            if (!string.IsNullOrWhiteSpace(normalizedNamespace))
            {
                _globalUsings.Add(normalizedNamespace);
            }
        }
    }
}
