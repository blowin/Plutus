using Plutus.Domain;

namespace Plutus.Infrastructure.RemoteRepository;

/// <summary>
/// Shared behavior for the platform-specific remote repositories: it downloads the archive a platform builds for a
/// branch or commit, and hands it to <see cref="SecureArchiveExtractor"/> rather than to the framework's
/// <c>ZipFile.ExtractToDirectory</c>.
/// <para>
/// A remote archive is untrusted input: its entry names are attacker-controlled and its declared sizes are claims.
/// Extraction therefore happens only through the hardened path, and every temporary file and directory this class
/// creates is removed on success, on every failure, and on cancellation.
/// </para>
/// </summary>
public abstract class BaseRemoteRepository : IRemoteRepository, IDisposable
{
    /// <summary>22 bytes is the smallest possible ZIP end-of-central-directory record.</summary>
    private const int MinimumZipLength = 22;

    /// <summary>Where the download is streamed before it is validated and extracted.</summary>
    private const string TempFilePrefix = "plutus_remote_download_";

    /// <summary>Where an archive is extracted.</summary>
    internal const string TempDirectoryPrefix = "plutus_remote_";

    private readonly List<string> _tempDirectories = [];
    private readonly SecureArchiveExtractor _extractor;
    private bool _disposed;

    protected readonly HttpClient HttpClient;

    /// <param name="client">The HTTP client archive downloads use.</param>
    /// <param name="extractionLimits">The limits extraction enforces; defaults to <see cref="ArchiveExtractionLimits.Default"/>.</param>
    protected BaseRemoteRepository(HttpClient client, ArchiveExtractionLimits? extractionLimits = null)
    {
        HttpClient = client;
        _extractor = extractionLimits is null
            ? SecureArchiveExtractor.Default
            : new SecureArchiveExtractor(extractionLimits);
    }

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
            segments = segments.Take(treeIndex).ToList();
        }

        if (segments.Count > 0 && segments[^1].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] = segments[^1][..^4];
        }

        return segments.ToArray();
    }

    protected static string EscapeRef(string value) =>
        string.Join("/", value.Split('/').Select(Uri.EscapeDataString));

    public async ValueTask<string> DownloadAsync(
        string path,
        string? branch = null,
        string? commit = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(branch) && !string.IsNullOrWhiteSpace(commit))
        {
            throw new ArgumentException("Specify either a branch/tag or a commit, not both.");
        }

        string zipUrl = BuildZipUrl(path, branch, commit);

        var limits = _extractor.Limits;
        var tempZip = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}{Guid.NewGuid():N}.zip");
        var targetDir = Path.Combine(Path.GetTempPath(), $"{TempDirectoryPrefix}{Guid.NewGuid():N}");

        try
        {
            await DownloadToFileAsync(zipUrl, tempZip, limits, cancellationToken);

            await _extractor.ExtractAsync(tempZip, targetDir, cancellationToken);
        }
        catch
        {
            // This failure path must not leave the extraction directory behind: a later run could otherwise treat a
            // partial tree as a downloaded repository. The extractor has already removed its own partial output.
            SecureArchiveExtractor.DeleteDirectoryIfExists(targetDir);
            throw;
        }
        finally
        {
            // Runs for success, failure and cancellation alike. The download is always ours, and is never the returned
            // value, so it never outlives this call.
            DeleteTempFile(tempZip);
        }

        _tempDirectories.Add(targetDir);

        // Common logic for GitHub/GitLab: they archive the root into a subfolder like: "user-repo-hash"
        var subDirs = Directory.GetDirectories(targetDir);
        return subDirs.Length > 0 ? subDirs[0] : targetDir;
    }

    /// <summary>
    /// Streams the archive to a file, refusing anything larger than the download limit. The limit is enforced while
    /// copying rather than from <c>Content-Length</c>, because a server may understate it or send a chunked response.
    /// </summary>
    private async Task DownloadToFileAsync(
        string zipUrl,
        string tempZip,
        ArchiveExtractionLimits limits,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // A zero-length body is a definitive "nothing to extract", and saying so is more useful than a generic
        // malformed-archive message.
        if (response.Content.Headers.ContentLength == 0)
        {
            throw new InvalidOperationException("The remote repository returned an empty archive. It might be empty or private.");
        }

        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > limits.MaxDownloadBytes)
        {
            throw new ArchiveRejectedException(
                ArchiveRejectionReasons.DownloadSizeExceeded,
                $"The remote archive declares {declaredLength.HumanSize()}, beyond the download limit of " +
                $"{limits.MaxDownloadBytes.HumanSize()}.",
                null);
        }

        await using (var file = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > limits.MaxDownloadBytes)
                {
                    throw new ArchiveRejectedException(
                        ArchiveRejectionReasons.DownloadSizeExceeded,
                        $"The remote archive is larger than the download limit of {limits.MaxDownloadBytes.HumanSize()}. " +
                        "The download was stopped.",
                        null);
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        var downloaded = new FileInfo(tempZip).Length;
        if (downloaded < MinimumZipLength)
        {
            throw new InvalidDataException(
                "Downloaded archive file is corrupted or empty. Verify branch name or repository access permissions.");
        }
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var dir in _tempDirectories)
        {
            SecureArchiveExtractor.DeleteDirectoryIfExists(dir);
        }

        _tempDirectories.Clear();

        if (disposing)
        {
            // The client is created per scope by the container and owned by it; a subclass that is handed a
            // caller-owned client does not get to dispose it, matching the previous behavior.
        }
    }

    private static void DeleteTempFile(string tempZip)
    {
        try
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }
        catch
        {
            // Best effort: a leftover temp file is preferable to masking the failure that caused it.
        }
    }
}