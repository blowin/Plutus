namespace Plutus.Domain.IgnoreMatcher;

public interface IIgnoreMatcher
{
    bool IsIgnoredFile(FileInfo fileInfo);

    bool IsIgnoredFile(string relativePath) => IsIgnoredFile(new FileInfo(relativePath.TrimEnd('/')));

    bool IsIgnoredDirectory(DirectoryInfo directoryInfo);

    bool IsIgnoredDirectory(string relativePath) => IsIgnoredDirectory(new DirectoryInfo(relativePath.TrimEnd('/') + "/"));
}
