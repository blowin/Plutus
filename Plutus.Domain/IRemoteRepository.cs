namespace Plutus.Domain;

public interface IRemoteRepository
{
    bool IsSupportedPath(string path);

    ValueTask<string> DownloadAsync(string path, string? branch = null, string? commit = null);

    string NormalizeOutputPath(string path);
}