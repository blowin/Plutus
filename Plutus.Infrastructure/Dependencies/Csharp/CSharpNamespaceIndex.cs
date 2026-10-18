using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public sealed class CSharpNamespaceIndex : ICSharpNamespaceIndex
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
        var fileInfo = provider.GetFileInfo(path);
        if (!fileInfo.Exists)
        {
            return;
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        var tree = CSharpSyntaxTree.ParseText(
            content,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: path,
            cancellationToken: ct);

        var root = await tree.GetRootAsync(ct);

        // BaseNamespaceDeclarationSyntax covers both block and file-scoped namespaces
        var namespaces = root.DescendantNodes()
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
