using System.Threading;

namespace Plutus.Domain;

public interface IRemoteRepository
{
    bool IsSupportedPath(string path);

    ValueTask<string> DownloadAsync(string path, string? branch = null, string? commit = null, CancellationToken cancellationToken = default);

    string NormalizeOutputPath(string path);
}
