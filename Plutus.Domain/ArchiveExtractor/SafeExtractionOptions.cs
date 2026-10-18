namespace Plutus.Domain.ArchiveExtractor;

public record SafeExtractionOptions()
{
    public FileSize MaxDownloadSize { get; init; } = FileSize.Parse("100MB");
    public FileSize MaxExtractedSize { get; init; } = FileSize.Parse("500MB");
    public int MaxFileCount { get; init; } = 10_000;
    public int MaxDepth { get; init; } = 16;
    public bool RejectDuplicatePaths { get; init; } = true;
    public bool RejectSymlinks { get; init; } = true;

    public static SafeExtractionOptions Default { get; } = From(ArchiveExtractionOptions.Default);

    /// <summary>Creates a SafeExtractionOptions from ArchiveExtractionOptions, using defaults for null values.</summary>
    public static SafeExtractionOptions From(ArchiveExtractionOptions source)
    {
        var defaults = ArchiveExtractionOptions.Default;
        // Extract non-nullable values from defaults (the properties are nullable but defaults are always set)
        var defaultMaxDownloadSize = defaults.MaxDownloadSize!.Value;
        var defaultMaxExtractedSize = defaults.MaxExtractedSize!.Value;

        return new SafeExtractionOptions
        {
            MaxDownloadSize = source.MaxDownloadSize ?? defaultMaxDownloadSize,
            MaxExtractedSize = source.MaxExtractedSize ?? defaultMaxExtractedSize,
            MaxFileCount = source.MaxFileCount ?? defaults.MaxFileCount.GetValueOrDefault(),
            MaxDepth = source.MaxDepth ?? defaults.MaxDepth.GetValueOrDefault(),
            RejectDuplicatePaths = source.RejectDuplicatePaths ?? defaults.RejectDuplicatePaths.GetValueOrDefault(),
            RejectSymlinks = source.RejectSymlinks ?? defaults.RejectSymlinks.GetValueOrDefault(),
        };
    }
}
