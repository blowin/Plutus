using System.IO.Compression;
using Plutus.Domain;

namespace Plutus.Infrastructure.RemoteRepository;

public abstract class BaseRemoteRepository(HttpClient client) : IRemoteRepository, IDisposable
{
    private readonly List<string> _tempDirectories = [];
    protected readonly HttpClient HttpClient = client;

    public abstract bool IsSupportedPath(string path);

    protected abstract string BuildZipUrl(string path, string? branch, string? commit);

    protected abstract string PlatformPrefix { get; }

    protected static Uri ParseRepositoryUri(string path)
    {
        if (!Uri.TryCreate(path.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Invalid remote repository URL: '{path}'. Expected an absolute HTTP(S) URL.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException($"Invalid remote repository URL: '{path}'.", nameof(path));
        }

        return uri;
    }

    protected static string[] GetRepositoryPathSegments(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToList();

        // Accept browser URLs such as /owner/repo/tree/feature%2Fbranch and
        // remove the navigation suffix; archive endpoints need only the repository path.
        var treeIndex = segments.FindIndex(segment =>
            segment.Equals("tree", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("blob", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("-", StringComparison.OrdinalIgnoreCase));

        if (treeIndex >= 0)
        {
            if (segments[treeIndex].Equals("-", StringComparison.OrdinalIgnoreCase))
            {
                segments = segments.Take(treeIndex).ToList();
            }
            else
            {
                segments = segments.Take(treeIndex).ToList();
            }
        }

        if (segments.Count > 0 && segments[^1].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] = segments[^1][..^4];
        }

        return segments.ToArray();
    }

    protected static string EscapeRef(string value) =>
        string.Join("/", value.Split('/').Select(Uri.EscapeDataString));

    public async ValueTask<string> DownloadAsync(string path, string? branch = null, string? commit = null)
    {
        if (!string.IsNullOrWhiteSpace(branch) && !string.IsNullOrWhiteSpace(commit))
        {
            throw new ArgumentException("Specify either a branch/tag or a commit, not both.");
        }

        string zipUrl = BuildZipUrl(path, branch, commit);

        var tempZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
        var targetDir = Path.Combine(Path.GetTempPath(), $"plutus_remote_{Guid.NewGuid()}");

        try
        {
            using (var response = await HttpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                // CRITICAL SAFETY CHECK: If Content-Length is zero or the server didn't send zip headers
                if (response.Content.Headers.ContentLength == 0)
                {
                    throw new InvalidOperationException("The remote repository returned an empty archive. It might be empty or private.");
                }

                await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }

            // Secondary validation: Check file size on disk before extraction
            if (new FileInfo(tempZip).Length < 22) // 22 bytes is the absolute minimum size for any valid empty ZIP
            {
                throw new InvalidDataException("Downloaded archive file is corrupted or empty. Verify branch name or repository access permissions.");
            }

            await ZipFile.ExtractToDirectoryAsync(tempZip, targetDir);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }

        _tempDirectories.Add(targetDir);

        // Common logic for GitHub/GitLab: they archive the root into a subfolder like: "user-repo-hash"
        var subDirs = Directory.GetDirectories(targetDir);
        return subDirs.Length > 0 ? subDirs[0] : targetDir;
    }

    public string NormalizeOutputPath(string path)
    {
        string repoName;
        try
        {
            var uri = ParseRepositoryUri(path);
            var segments = GetRepositoryPathSegments(uri);
            repoName = segments.LastOrDefault() ?? $"{PlatformPrefix}_project_" + Guid.CreateVersion7().ToString("N");
        }
        catch (ArgumentException)
        {
            repoName = $"{PlatformPrefix}_project_" + Guid.CreateVersion7().ToString("N");
        }
        return Path.Combine(Directory.GetCurrentDirectory(), $"{repoName}_bundle.md");
    }

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
}
