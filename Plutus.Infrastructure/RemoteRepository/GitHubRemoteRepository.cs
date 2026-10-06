namespace Plutus.Infrastructure.RemoteRepository;

public class GitHubRemoteRepository : BaseRemoteRepository
{
    protected override string PlatformPrefix => "github";

    public override bool IsSupportedPath(string path) =>
        path.StartsWith("http://github.com", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path)
    {
        var cleanUrl = path.TrimEnd('/');
        return $"{cleanUrl}/archive/refs/heads/master.zip";
    }
}
