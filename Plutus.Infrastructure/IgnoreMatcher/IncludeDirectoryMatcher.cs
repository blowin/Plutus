using MAB.DotIgnore;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Infrastructure.IgnoreMatcher;

internal sealed class IncludeDirectoryMatcher : IIgnoreMatcher
{
    private readonly List<string> _normalizedPatterns;
    private readonly IgnoreList _ignoreList;

    public IncludeDirectoryMatcher(IgnoreList ignoreList)
    {
        _ignoreList = ignoreList;
        _normalizedPatterns = new List<string>(ignoreList.Rules.Count);

        // Normalize masks once at startup
        foreach (var rule in ignoreList.Rules)
        {
            var pattern = rule.Pattern.Replace('\\', '/').TrimStart('!', '/').TrimEnd('/');
            if (pattern.Length > 0)
            {
                _normalizedPatterns.Add(pattern + "/");
            }
        }
    }

    public bool IsIgnoredFile(string relativePath)
    {
        var dirPath = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrEmpty(dirPath))
        {
            return true;
        }

        // Instead of Replace + TrimEnd + "/", perform a single delimiter replacement.
        var normalizedDir = dirPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Use Span to check the suffix, to avoid creating extra strings via + "/"
        ReadOnlySpan<char> dirSpan = normalizedDir.AsSpan();
        bool hasTrailingSlash = dirSpan.EndsWith("/");

        foreach (var pattern in _normalizedPatterns)
        {
            // If dirSpan does not end with "/", compare it with pattern as a folder prefix
            if (hasTrailingSlash)
            {
                if (normalizedDir.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else
            {
                // If the path being checked does not have a trailing slash, we can check using MemoryExtensions.
                if (dirSpan.StartsWith(pattern.AsSpan().Slice(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase))
                {
                    // Check that the match aligns strictly with the folder boundary
                    if (dirSpan.Length == pattern.Length - 1 || dirSpan[pattern.Length - 1] == '/')
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public bool IsIgnoredDirectory(string relativePath)
    {
        if (_ignoreList.IsIgnored(relativePath, true))
        {
            return false;
        }

        var normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ReadOnlySpan<char> pathSpan = normalizedPath.AsSpan().TrimEnd('/');

        foreach (var pattern in _normalizedPatterns)
        {
            ReadOnlySpan<char> patternSpan = pattern.AsSpan().TrimEnd('/');

            // Lookahead: check if the current folder leads to the target pattern
            if (patternSpan.StartsWith(pathSpan, StringComparison.OrdinalIgnoreCase))
            {
                // We guarantee a match strictly along folder segment boundaries.
                if (patternSpan.Length == pathSpan.Length || patternSpan[pathSpan.Length] == '/')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
