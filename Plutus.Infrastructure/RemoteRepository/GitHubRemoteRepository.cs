namespace Plutus.Infrastructure.RemoteRepository;

public class GitHubRemoteRepository(HttpClient client) : BaseRemoteRepository(client)
{
    protected override string PlatformPrefix => "github";

    public override bool IsSupportedPath(string path) =>
        path.StartsWith("http://github.com", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase);

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
