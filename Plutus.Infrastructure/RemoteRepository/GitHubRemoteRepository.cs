using Plutus.Domain.ArchiveExtractor;
using Plutus.Infrastructure.Retry;

namespace Plutus.Infrastructure.RemoteRepository;

public class GitHubRemoteRepository(
    HttpClient client,
    ISafeArchiveExtractor safeArchiveExtractor,
    RetryPolicy retryPolicy) : BaseRemoteRepository(client, safeArchiveExtractor, retryPolicy)
{
    protected override string PlatformPrefix => "github";

    public override bool IsSupportedPath(string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path, string? branch, string? commit)
    {
        var uri = ParseRepositoryUri(path);
        var segments = GetRepositoryPathSegments(uri);
        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid GitHub repository URL: {path}", nameof(path));
        }

        var repositoryUrl = $"https://github.com/{segments[0]}/{segments[1]}";
        var target = !string.IsNullOrWhiteSpace(commit)
            ? commit.Trim()
            : !string.IsNullOrWhiteSpace(branch)
                ? branch.Trim()
                : "master";

        // GitHub's generic archive endpoint accepts branch names, tag names and commit hashes.
        return $"{repositoryUrl}/archive/{EscapeRef(target)}.zip";
    }
}
