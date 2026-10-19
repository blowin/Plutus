using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.BundleWriters.CacheReadProviders;

public sealed class CacheReadProvider(Encoding encoding) : ICacheReadProvider
{
    private readonly ConcurrentDictionary<IFileInfo, Task<ReadCacheEntry>> _cache = new(new Comparer());

    public ValueTask<ReadCacheEntry> GetCachedContentAsync(IFileInfo fileInfo, CancellationToken cancellationToken)
    {
        var task = _cache.GetOrAdd(fileInfo, async (_) => await CreateAsync(fileInfo, cancellationToken));
        return new ValueTask<ReadCacheEntry>(task);
    }

    public async ValueTask<ReadCacheEntry> CreateAsync(IFileInfo fileInfo, CancellationToken cancellationToken)
    {
        if (!fileInfo.Exists)
        {
            return ReadCacheEntry.Empty("File does not exist");
        }

        if (fileInfo.Length == 0)
        {
            return new ReadCacheEntry(string.Empty);
        }

        try
        {
            await using var fs = fileInfo.CreateReadStream();

            // OPTIMIZATION: Rent a shared block-sized chunk buffer from ArrayPool to avoid allocations
            var buffer = ArrayPool<byte>.Shared.Rent(81920);

            // Pre-size the memory stream if length is known to prevent continuous resizing allocations
            await using var ms = fileInfo.Length > 0
                ? new MemoryStream((int)fileInfo.Length)
                : new MemoryStream();

            try
            {
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    // ON-THE-FLY BINARY CHECK: Scan the chunk buffer before writing it to our memory block
                    for (var i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == 0)
                        {
                            return ReadCacheEntry.Empty("binary file");
                        }
                    }

                    await ms.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            var bytes = ms.ToArray();
            return bytes.Length == 0
                ? new ReadCacheEntry(string.Empty)
                : new ReadCacheEntry(encoding.GetString(bytes));
        }
        catch (Exception ex)
        {
            return ReadCacheEntry.Empty($"unreadable: {ex.Message}");
        }
    }

    private sealed class Comparer : IEqualityComparer<IFileInfo>
    {
        public bool Equals(IFileInfo? x, IFileInfo? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(x.PhysicalPath) && !string.IsNullOrWhiteSpace(y.PhysicalPath))
            {
                return string.Equals(
                    Path.GetFullPath(x.PhysicalPath),
                    Path.GetFullPath(y.PhysicalPath),
                    StringComparison.OrdinalIgnoreCase);
            }

            return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase) && x.IsDirectory == y.IsDirectory;
        }

        public int GetHashCode(IFileInfo obj)
        {
            if (obj.PhysicalPath is not null)
            {
                try
                {
                    return Path.GetFullPath(obj.PhysicalPath).GetHashCode(StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return obj.PhysicalPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
                }
            }

            return HashCode.Combine(obj.Name.GetHashCode(StringComparison.OrdinalIgnoreCase), obj.IsDirectory);
        }
    }
}
