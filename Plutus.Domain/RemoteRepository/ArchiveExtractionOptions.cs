namespace Plutus.Domain.RemoteRepository;

/// <summary>
/// Configurable limits and behaviors for remote archive extraction.
/// These options control how downloaded archives (e.g., from GitHub, GitLab, Bitbucket) are validated and extracted.
/// </summary>
public sealed record ArchiveExtractionOptions
{
    /// <summary>
    /// Maximum allowed download size. Archives exceeding this limit are rejected before extraction.
    /// Default: 100 MB.
    /// </summary>
    public FileSize? MaxDownloadSize { get; init; }

    /// <summary>
    /// Maximum allowed total extracted size (sum of all file sizes after decompression).
    /// Protects against decompression bombs (zip bombs).
    /// Default: 500 MB.
    /// </summary>
    public FileSize? MaxExtractedSize { get; init; }

    /// <summary>
    /// Maximum number of files allowed in the archive.
    /// Default: 10,000.
    /// </summary>
    public int? MaxFileCount { get; init; }

    /// <summary>
    /// Maximum nesting depth of directories in the archive.
    /// Depth is counted from the extraction root (level 0).
    /// Default: 16.
    /// </summary>
    public int? MaxDepth { get; init; }

    /// <summary>
    /// Whether to reject archives containing duplicate entry paths.
    /// When true, the first duplicate entry causes the entire extraction to fail.
    /// Default: true.
    /// </summary>
    public bool? RejectDuplicatePaths { get; init; }

    /// <summary>
    /// Whether to reject archives containing symlinks (stored as directory entries with non-zero length).
    /// Default: true.
    /// </summary>
    public bool? RejectSymlinks { get; init; }

    /// <summary>
    /// Default values for archive extraction options.
    /// </summary>
    public static ArchiveExtractionOptions Default { get; } = new()
    {
        MaxDownloadSize = FileSize.Parse("100MB"),
        MaxExtractedSize = FileSize.Parse("500MB"),
        MaxFileCount = 10_000,
        MaxDepth = 16,
        RejectDuplicatePaths = true,
        RejectSymlinks = true,
    };
}
