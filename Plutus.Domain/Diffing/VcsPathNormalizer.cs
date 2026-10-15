namespace Plutus.Domain.Diffing;

/// <summary>
/// Normalizes the path shapes that reach a diff comparison: platform separators, repository anchoring and
/// the trailing separator used for prefix matching.
/// </summary>
public static class VcsPathNormalizer
{
    /// <summary>Anchors a repository-relative path to the repository root, using forward slashes throughout.</summary>
    public static string Anchor(string repositoryRoot, string repositoryRelativePath) =>
        RootPrefix(repositoryRoot) + repositoryRelativePath.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// Returns the root with exactly one trailing separator, so a prefix match cannot end in the middle of a
    /// path segment. The leading separator of an absolute root is preserved.
    /// </summary>
    public static string RootPrefix(string root) => root.Replace('\\', '/').TrimEnd('/') + "/";
}
