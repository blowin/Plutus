namespace Plutus.Domain;

public interface IRemoteRepositoryProvider
{
    bool IsSupportedPath(string path);

    ValueTask<string> DownloadAsync(string path);

    string NormalizeOutputPath(string path);
}
