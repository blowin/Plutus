using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileProviders;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public sealed class RoslynTreeCache
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

            await using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(content))
            {
                return CacheEntry.Empty;
            }

            var tree = CSharpSyntaxTree.ParseText(
                content,
                new CSharpParseOptions(LanguageVersion.Latest),
                path: path,
                cancellationToken: ct);

            var root = await tree.GetRootAsync(ct);

            return new CacheEntry(content, tree, root);
        }, ct));

    public readonly record struct CacheEntry(string Content, SyntaxTree Tree, SyntaxNode Root)
    {
        public static CacheEntry Empty { get; } = default;
        public bool IsEmpty => Content == null!;
    }
}
