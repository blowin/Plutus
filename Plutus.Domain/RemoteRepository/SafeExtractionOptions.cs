namespace Plutus.Domain.RemoteRepository;

public readonly record struct SafeExtractionOptions()
{
    public FileSize MaxDownloadSize { get; init; } = FileSize.Parse("100MB");
    public FileSize MaxExtractedSize { get; init; } = FileSize.Parse("500MB");
    public int MaxFileCount { get; init; } = 10_000;
    public int MaxDepth { get; init; } = 16;
    public bool RejectDuplicatePaths { get; init; } = true;
    public bool RejectSymlinks { get; init; } = true;

    public static SafeExtractionOptions Default { get; } = new();
}
