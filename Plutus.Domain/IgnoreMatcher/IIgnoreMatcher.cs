namespace Plutus.Domain.IgnoreMatcher;

public interface IIgnoreMatcher
{
    bool IsIgnoredFile(string relativePath);

    bool IsIgnoredDirectory(string relativePath);
}
