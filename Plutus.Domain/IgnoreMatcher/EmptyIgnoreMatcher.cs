namespace Plutus.Domain.IgnoreMatcher;

public sealed class EmptyIgnoreMatcher : IIgnoreMatcher
{
    public bool IsIgnoredFile(string relativePath) => false;

    public bool IsIgnoredDirectory(string relativePath) => false;
}
