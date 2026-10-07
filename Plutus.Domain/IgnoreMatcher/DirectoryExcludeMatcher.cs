using System.Text.RegularExpressions;

namespace Plutus.Domain.IgnoreMatcher;

/// <summary>
/// Excludes directories based on a wildcard pattern.
/// Supports the characters '*' (any number of characters) and '?' (a single character).
/// The check is performed against the folder name at any nesting level.
/// </summary>
public class DirectoryExcludeMatcher : IIgnoreMatcher
{
    private readonly List<Regex> _excludedPatterns;

    public DirectoryExcludeMatcher(IEnumerable<string> excludedMasks)
    {
        _excludedPatterns = excludedMasks
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(CompileWildcardToRegex)
            .ToList();
    }

    public bool IsIgnoredFile(string relativePath) => false;

    public bool IsIgnoredDirectory(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || _excludedPatterns.Count == 0)
        {
            return false;
        }

        // Extract the current folder name from the path (e.g., "src/build_release" -> "build_release")
        var cleanedPath = relativePath.TrimEnd('/', '\\');
        var lastSlashIndex = cleanedPath.LastIndexOfAny(['/', '\\']);
        var name = lastSlashIndex >= 0 ? cleanedPath[(lastSlashIndex + 1)..] : cleanedPath;

        // Check the folder name against any of the compiled patterns
        return _excludedPatterns.Any(regex => regex.IsMatch(name));
    }

    /// <summary>
    /// Converts a wildcard mask into a compiled regular expression.
    /// </summary>
    private static Regex CompileWildcardToRegex(string wildcard)
    {
        // Escape all special regex characters, then replace the escaped * and ? with their regex equivalents
        var regexPattern = "^" + Regex.Escape(wildcard)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
