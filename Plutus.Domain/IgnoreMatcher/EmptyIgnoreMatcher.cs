namespace Plutus.Domain.IgnoreMatcher;

public sealed class EmptyIgnoreMatcher : IIgnoreMatcher
{
    public static readonly IIgnoreMatcher Instance = new EmptyIgnoreMatcher();

    public bool IsIgnoredFile(string relativePath) => false;

    public bool IsIgnoredDirectory(string relativePath) => false;
}
