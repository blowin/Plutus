using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.BundleWriters.CacheReadProviders;

public interface ICacheReadProvider
{
    ValueTask<ReadCacheEntry> GetCachedContentAsync(IFileInfo fileInfo, CancellationToken cancellationToken);

    ValueTask<ReadCacheEntry> GetCachedContentAsync(string virtualPath, IFileProvider fileProvider, CancellationToken cancellationToken)
    {
        IFileInfo fileInfo = fileProvider.GetFileInfo(virtualPath);
        return GetCachedContentAsync(fileInfo, cancellationToken);
    }
}

public readonly record struct ReadCacheEntry(string Content, string? EmptyReason = null)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

    public static ReadCacheEntry Empty(string? reason = null) => new(string.Empty, reason);
}
