using Plutus.Domain.ArchiveExtractor;
using Plutus.Infrastructure.Retry;

namespace Plutus.Infrastructure.RemoteRepository;

public sealed class BitbucketRemoteRepository(
    HttpClient client,
    ISafeArchiveExtractor safeArchiveExtractor,
    RetryPolicy retryPolicy) : BaseRemoteRepository(client, safeArchiveExtractor, retryPolicy)
{
    protected override string PlatformPrefix => "bitbucket";

    public override bool IsSupportedPath(string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.Host.Equals("bitbucket.org", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path, string? branch, string? commit)
    {
        // 1. Convert to an indexable absolute path URI
        var uri = new Uri(path.TrimEnd('/'));

        // 2. Split path segments: ["/", "atlassian", "atlassian-plugins", "src", "master"]
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid Bitbucket repository URL: {path}", nameof(path));
        }

        // 3. Extract only the workspace (owner) and repository name segments
        var workspace = segments[0];
        var repoName = segments[1];

        // 4. Construct the clean public zip archive download endpoint
        var target = !string.IsNullOrWhiteSpace(commit)
            ? commit.Trim()
            : !string.IsNullOrWhiteSpace(branch) ? branch.Trim() : "master";
        return $"https://bitbucket.org/{workspace}/{repoName}/get/{EscapeRef(target)}.zip";
    }
}
