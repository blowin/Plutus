using Plutus.Domain.ArchiveExtractor;

namespace Plutus.Test.RemoteRepository;

public sealed class NullSafeArchiveExtractor : ISafeArchiveExtractor
{
    public ValueTask<SafeExtractionResult> ExtractAsync(
        Stream archiveStream,
        string targetDirectory,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new SafeExtractionResult(targetDirectory, 0, 0));
}
