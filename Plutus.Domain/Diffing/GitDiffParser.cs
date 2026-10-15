namespace Plutus.Domain.Diffing;

/// <summary>
/// Parses the path lists git prints for machine consumption. File changes themselves are read from unified
/// diff text by <see cref="GitDiffChunker"/>, because a diff has to be split per file to be limitable.
/// </summary>
public static class GitDiffParser
{
    /// <summary>
    /// Splits the null-terminated output of <c>git ls-files -z</c>. Null separation is used because a path
    /// may contain spaces, quotes or newlines, and git would otherwise escape it.
    /// </summary>
    public static IReadOnlyList<string> ParseNullSeparatedPaths(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var paths = new List<string>();
        foreach (var entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = DiffPathNormalizer.Normalize(entry);
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }

        return paths;
    }
}
