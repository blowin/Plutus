using System.IO.Compression;
using Plutus.Domain;

namespace Plutus.Infrastructure;

public class GitHubRemoteRepositoryProvider : IRemoteRepositoryProvider, IDisposable
{
    private readonly List<string> _tempDirectories = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    public bool IsSupportedPath(string path) =>
        path.StartsWith("http://github.com", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase);

    public async ValueTask<string> DownloadAsync(string githubUrl)
    {
        // Convert https://github.com to https://github.com/zipball/main
        string zipUrl = githubUrl.TrimEnd('/') + "/zipball/main";

        var tempZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        var targetDir = Path.Combine(Path.GetTempPath(), $"plutus_{Guid.NewGuid()}");

        // Download
        // ReSharper disable once ShortLivedHttpClient
        using (var client = new HttpClient())
        using (var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        await ZipFile.ExtractToDirectoryAsync(tempZip, targetDir);
        File.Delete(tempZip);

        _tempDirectories.Add(targetDir);

        // GitHub archives with a root folder named "user-repo-hash",
        // so we need to return the path to this inner subdirectory
        var subDirs = Directory.GetDirectories(targetDir);
        return subDirs.Length > 0 ? subDirs[0] : targetDir;
    }

    public string NormalizeOutputPath(string path)
    {
        var repoName = path.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s)) ?? "remote_project_" + Guid.CreateVersion7().ToString("N");
        return Path.Combine(Directory.GetCurrentDirectory(), $"{repoName}_bundle.md");
    }
}
