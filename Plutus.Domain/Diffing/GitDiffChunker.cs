namespace Plutus.Domain.Diffing;

/// <summary>
/// Splits a unified diff into one <see cref="DiffChunk"/> per file. Per-file chunks are what make the size
/// and token limits usable on a diff: a single oversized change can be dropped while the remaining changes
/// are still bundled.
/// </summary>
/// <remarks>
/// Chunk text is normalized to <c>\n</c> separators, matching how the bundle writers emit their own markup
/// (an explicitly encoded <see cref="StreamWriter"/> also uses <c>\n</c>). Git may hand us <c>\r\n</c> when
/// it runs on Windows, and keeping those would both mix separator styles in one artifact and break the
/// trailing-separator checks the renderers rely on.
/// </remarks>
public static class GitDiffChunker
{
    private const string GitHeaderPrefix = "diff --git ";
    private const string AddedFilePrefix = "+++";
    private const string DeletedFilePrefix = "---";
    private const string LineSeparator = "\n";

    public static IReadOnlyList<DiffChunk> Split(string diffText)
    {
        ArgumentNullException.ThrowIfNull(diffText);

        var chunks = new List<DiffChunk>();
        var lines = new List<string>();
        var added = 0;
        var deleted = 0;

        foreach (var line in EnumerateLines(diffText))
        {
            if (line.StartsWith(GitHeaderPrefix, StringComparison.Ordinal))
            {
                Flush(chunks, lines, added, deleted);
                lines.Clear();
                added = 0;
                deleted = 0;
            }

            lines.Add(line);

            if (IsAddedContent(line))
            {
                added++;
            }
            else if (IsDeletedContent(line))
            {
                deleted++;
            }
        }

        Flush(chunks, lines, added, deleted);
        return chunks;
    }

    /// <summary>A file header line (+++/---) names a file, so it is not a changed line of code.</summary>
    private static bool IsAddedContent(string line) =>
        line.StartsWith('+') && !line.StartsWith(AddedFilePrefix, StringComparison.Ordinal);

    private static bool IsDeletedContent(string line) =>
        line.StartsWith('-') && !line.StartsWith(DeletedFilePrefix, StringComparison.Ordinal);

    private static void Flush(List<DiffChunk> chunks, List<string> lines, int added, int deleted)
    {
        if (lines.Count == 0)
        {
            return;
        }

        // A block without a "diff --git" header names no file, so nothing can attribute or limit it.
        var header = lines.Find(line => line.StartsWith(GitHeaderPrefix, StringComparison.Ordinal));
        var path = header is null ? null : DiffPathNormalizer.ParseDiffHeaderDestination(header[GitHeaderPrefix.Length..]);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var text = string.Join(LineSeparator, lines);

        // Path starts repository-relative; ChangedFileMapper rewrites it to a bundle root later.
        chunks.Add(new DiffChunk(path, path, ResolveKind(lines), text, added, deleted));
    }

    private static ChangeKind ResolveKind(List<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                return ChangeKind.Added;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                return ChangeKind.Deleted;
            }

            if (line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                return ChangeKind.Renamed;
            }

            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                return ChangeKind.TypeChanged;
            }
        }

        return ChangeKind.Modified;
    }

    /// <summary><see cref="StringReader.ReadLine"/> strips both separators, so both input styles normalize.</summary>
    private static IEnumerable<string> EnumerateLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
