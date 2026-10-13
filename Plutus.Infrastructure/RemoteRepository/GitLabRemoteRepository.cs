using System.Web;

namespace Plutus.Infrastructure.RemoteRepository;

public sealed class GitLabRemoteRepository(HttpClient client) : BaseRemoteRepository(client)
{
    protected override string PlatformPrefix => "gitlab";

    public override bool IsSupportedPath(string path) =>
        path.StartsWith("http://gitlab.com", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://gitlab.com", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path)
    {
        // Example path input: https://gitlab.com
        var cleanUrl = path.TrimEnd('/');
        var uri = new Uri(cleanUrl);

        // Extract "edu_yndx_mag/dz-catalog-analysis-karasev-m26-555"
        var projectPath = uri.AbsolutePath.TrimStart('/');

        // GitLab API requires the project path string to be URL-encoded (e.g., slashes become %2F)
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);

        // This endpoint automatically redirects and resolves the default branch archive stream
        return $"https://gitlab.com/api/v4/projects/{encodedProjectPath}/repository/archive.zip";
    }
}
