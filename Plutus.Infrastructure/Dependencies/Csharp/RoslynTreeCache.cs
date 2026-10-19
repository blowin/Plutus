using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.BundleWriters.CacheReadProviders;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public sealed class RoslynTreeCache(ICacheReadProvider cacheReadProvider)
{
    private readonly ConcurrentDictionary<string, Task<CacheEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<CacheEntry> GetOrParseAsync(string virtualPath, IFileProvider fileProvider, CancellationToken ct) =>
        _cache.GetOrAdd(virtualPath, path => Task.Run(async () =>
        {
            var fileInfo = fileProvider.GetFileInfo(path);
            if (!fileInfo.Exists)
            {
                return CacheEntry.Empty;
            }

            var content = await cacheReadProvider.GetCachedContentAsync(virtualPath, fileProvider, ct);
            if (content.IsEmpty)
            {
                return CacheEntry.Empty;
            }

            var tree = CSharpSyntaxTree.ParseText(
                content.Content,
                new CSharpParseOptions(LanguageVersion.Latest),
                path: path,
                cancellationToken: ct);

            var root = await tree.GetRootAsync(ct);

            return new CacheEntry(content.Content, tree, root);
        }, ct));

    public readonly record struct CacheEntry(string Content, SyntaxTree Tree, SyntaxNode Root)
    {
        public static CacheEntry Empty { get; } = default;
        public bool IsEmpty => Content == null!;
    }
}
