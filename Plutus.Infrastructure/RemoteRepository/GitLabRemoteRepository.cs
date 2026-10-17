using Plutus.Domain;

namespace Plutus.Infrastructure.RemoteRepository;

public sealed class GitLabRemoteRepository(HttpClient client, IFileInfoDetailProvider fileInfoDetailProvider) : BaseRemoteRepository(client, fileInfoDetailProvider)
{
    protected override string PlatformPrefix => "gitlab";

    public override bool IsSupportedPath(string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path, string? branch, string? commit)
    {
        var uri = ParseRepositoryUri(path);
        var segments = GetRepositoryPathSegments(uri);
        if (segments.Length < 2)
        {
            throw new ArgumentException($"Invalid GitLab project URL: {path}", nameof(path));
        }

        var projectPath = string.Join("/", segments);
        var target = !string.IsNullOrWhiteSpace(commit) ? commit.Trim() : branch?.Trim();
        var url = $"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString(projectPath)}/repository/archive.zip";
        return string.IsNullOrWhiteSpace(target) ? url : $"{url}?sha={Uri.EscapeDataString(target)}";
    }
}
