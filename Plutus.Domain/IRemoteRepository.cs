namespace Plutus.Domain;

public interface IRemoteRepository
{
    bool IsSupportedPath(string path);

    ValueTask<string> DownloadAsync(string path);

    string NormalizeOutputPath(string path);
}
