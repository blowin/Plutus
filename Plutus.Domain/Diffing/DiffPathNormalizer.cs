namespace Plutus.Domain.Diffing;

/// <summary>
/// Normalizes the path shapes that reach a diff comparison: platform separators, diff prefixes such as
/// <c>a/</c>, repeated separators, leading <c>./</c> segments and absolute or trailing slashes.
/// </summary>
public static class DiffPathNormalizer
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();

        // Leading and trailing separators are dropped so absolute, trailing-slash and relative forms all
        // reduce to the same root-relative path.
        normalized = normalized.Trim('/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized;
    }

    /// <summary>
    /// Returns the destination path of a <c>diff --git</c> header: the <c>b/</c> side when git emits
    /// prefixes, otherwise the second path token. Returns <c>null</c> when the shape is unexpected.
    /// </summary>
    public static string? ParseDiffHeaderDestination(string header)
    {
        var separator = header.IndexOf(" b/", StringComparison.Ordinal);
        if (separator >= 0)
        {
            var destination = Normalize(header[(separator + 1)..]);

            // Git writes `b/path`; the marker is a prefix, not part of the tracked path.
            return destination.StartsWith("b/", StringComparison.Ordinal) ? destination[2..] : destination;
        }

        var tokens = header.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length < 2 ? null : Normalize(tokens[^1]);
    }
}
