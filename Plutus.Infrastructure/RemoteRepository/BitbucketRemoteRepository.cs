namespace Plutus.Infrastructure.RemoteRepository;

public sealed class BitbucketRemoteRepository : BaseRemoteRepository
{
    protected override string PlatformPrefix => "bitbucket";

    public override bool IsSupportedPath(string path) =>
        path.StartsWith("http://bitbucket.org", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://bitbucket.org", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path)
    {
        // 1. Convert to an indexable absolute path URI
        var uri = new Uri(path.TrimEnd('/'));

        // 2. Split path segments: ["/", "atlassian", "atlassian-plugins", "src", "master"]
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid Bitbucket repository URL layout: {path}");
        }

        // 3. Extract only the workspace (owner) and repository name segments
        var workspace = segments[0];
        var repoName = segments[1];

        // 4. Construct the clean public zip archive download endpoint
        return $"https://bitbucket.org/{workspace}/{repoName}/get/master.zip";
    }
}
