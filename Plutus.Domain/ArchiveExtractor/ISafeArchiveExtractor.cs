namespace Plutus.Domain.ArchiveExtractor;

public interface ISafeArchiveExtractor
{
    ValueTask<SafeExtractionResult> ExtractAsync(
        Stream archiveStream,
        string targetDirectory,
        CancellationToken cancellationToken = default);
}
