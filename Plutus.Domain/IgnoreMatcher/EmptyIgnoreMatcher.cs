namespace Plutus.Domain.IgnoreMatcher;

public sealed class EmptyIgnoreMatcher : IIgnoreMatcher
{
    public bool IsIgnoredFile(FileInfo fileInfo) => false;

    public bool IsIgnoredDirectory(DirectoryInfo directoryInfo) => false;
}
