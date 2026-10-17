namespace Plutus.Domain.RemoteRepository;

public enum ArchiveExtractionErrorKind
{
    PathTraversal,
    SymlinkEscape,
    SizeLimitExceeded,
    FileCountLimitExceeded,
    DepthLimitExceeded,
    DuplicatePath,
    MalformedArchive,
    DownloadSizeLimitExceeded,
}

public sealed class ArchiveExtractionException(
    string message,
    ArchiveExtractionErrorKind errorKind,
    string? entryPath = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public ArchiveExtractionErrorKind ErrorKind { get; } = errorKind;
    public string? EntryPath { get; } = entryPath;
}
