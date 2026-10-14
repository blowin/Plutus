namespace Plutus.Domain.Diffing;

/// <summary>
/// Extracts changed paths from unified git diff text. Parsing the diff that is bundled anyway keeps the
/// discovered subset and the structural <c>.diff</c> section guaranteed consistent, and avoids a second
/// process invocation whose output could drift from the first.
/// </summary>
public static class GitDiffParser
{
    private const string GitHeaderPrefix = "diff --git ";
    private const string NoNewlineMarker = "\\ No newline at end of file";

    public static IReadOnlyList<ChangedFile> ParseChangedFiles(string diffText)
    {
        ArgumentNullException.ThrowIfNull(diffText);

        var files = new List<ChangedFile>();
        string? currentPath = null;

        foreach (var line in EnumerateLines(diffText))
        {
            if (line.StartsWith(GitHeaderPrefix, StringComparison.Ordinal))
            {
                currentPath = DiffPathNormalizer.ParseDiffHeaderDestination(line[GitHeaderPrefix.Length..]);
                if (!string.IsNullOrEmpty(currentPath))
                {
                    files.Add(new ChangedFile(currentPath, ChangeKind.Modified));
                }

                continue;
            }

            if (string.IsNullOrEmpty(currentPath))
            {
                continue;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                SetKind(files, ChangeKind.Added);
            }
            else if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                SetKind(files, ChangeKind.Deleted);
            }
            else if (line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                SetKind(files, ChangeKind.Renamed);
            }
            else if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                SetKind(files, ChangeKind.TypeChanged);
            }
        }

        return files;
    }

    private static void SetKind(List<ChangedFile> files, ChangeKind kind) =>
        files[^1] = files[^1] with { Kind = kind };

    private static IEnumerable<string> EnumerateLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
