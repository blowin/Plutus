using System.IO.Compression;
using System.Net;
using Plutus.Domain;

namespace Plutus.Infrastructure.RemoteRepository;

public abstract class BaseRemoteRepository : IRemoteRepository, IDisposable
{
    private readonly List<string> _tempDirectories = [];
    protected readonly HttpClient HttpClient;

    protected BaseRemoteRepository()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        HttpClient = new HttpClient(handler);
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public abstract bool IsSupportedPath(string path);

    protected abstract string BuildZipUrl(string path);

    protected abstract string PlatformPrefix { get; }

    public async ValueTask<string> DownloadAsync(string path)
    {
        string zipUrl = BuildZipUrl(path);

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
        var repoName = path.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s))
                       ?? $"{PlatformPrefix}_project_" + Guid.CreateVersion7().ToString("N");
        return Path.Combine(Directory.GetCurrentDirectory(), $"{repoName}_bundle.md");
    }

    public void Dispose()
    {
        HttpClient.Dispose();
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
        GC.SuppressFinalize(this);
    }
}
