using Plutus.Domain.Diffing;

namespace Plutus.Domain.BundleWriters;

/// <summary>
/// Renders the diff sections. Each file gets its own fenced block instead of one solid patch, because that is
/// what makes the size and token limits meaningful. A file that a limit excluded is reported in the directory
/// tree rather than as its own empty block, so the section stays a list of the changes it actually carries.
/// </summary>
public static class BundleDiff
{
    public static void WriteSection(
        TextWriter writer,
        GitDiffContext diff,
        string changedFilesHeading,
        bool includeFileContents)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(diff);

        writer.WriteLine($"## Changes against `{diff.ComparisonBase}`");
        writer.WriteLine();

        var tracked = diff.TrackedChunks.Where(chunk => !chunk.IsSkipped).ToArray();
        var untracked = diff.UntrackedChunks.Where(chunk => !chunk.IsSkipped).ToArray();

        // When the contents are omitted the diff is the artifact, so the scope line always appears. When the
        // contents are present the "## File List" block has already stated the count.
        if (!includeFileContents || tracked.Length > 0)
        {
            writer.WriteLine($"{changedFilesHeading} {tracked.Length} file(s).");
            writer.WriteLine();
        }

        WriteUntrackedNote(writer, untracked);

        if (tracked.Length == 0 && untracked.Length == 0)
        {
            writer.WriteLine("No changed files were reported for this scope.");
            return;
        }

        WriteChunks(writer, tracked);
        WriteChunks(writer, untracked);
    }

    /// <summary>
    /// Records on the tree that a change was left out, which is where a reader looks for the file. An omitted
    /// change needs no diff block: its content would be nothing but the reason, already visible as a marker.
    /// </summary>
    public static void MarkExcluded(IReadOnlyList<DiffChunk> chunks, IReadOnlyList<ProjectNode> rootNodes)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(rootNodes);

        var excluded = BuildExcludedLookup(chunks);
        if (excluded.Count == 0)
        {
            return;
        }

        foreach (var root in rootNodes)
        {
            MarkExcluded(root, excluded);
        }
    }

    /// <summary>
    /// Marker for one changed path, or <c>null</c> when the change was kept. The path list uses this so an
    /// exclusion is visible in the diff-only bundle too, where no directory tree is rendered.
    /// </summary>
    public static string? DescribeExclusion(IReadOnlyList<DiffChunk> chunks, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(relativePath);

        return BuildExcludedLookup(chunks).TryGetValue(relativePath, out var status)
            ? Marker(status)
            : null;
    }

    private static Dictionary<string, NodeStatus> BuildExcludedLookup(IReadOnlyList<DiffChunk> chunks)
    {
        var excluded = new Dictionary<string, NodeStatus>(StringComparer.Ordinal);
        foreach (var chunk in chunks.Where(chunk => chunk.IsSkipped))
        {
            excluded[chunk.Path] = chunk.SkipReason?.Contains("max-file-size", StringComparison.Ordinal) == true
                ? NodeStatus.OversizedFile
                : NodeStatus.TokenBudgetOmitted;
        }

        return excluded;
    }

    private static void MarkExcluded(ProjectNode node, Dictionary<string, NodeStatus> excluded)
    {
        if (!node.IsDirectory)
        {
            if (excluded.TryGetValue(node.RelativePath, out var status))
            {
                node.Status = status;
            }

            return;
        }

        foreach (var child in node.Children)
        {
            MarkExcluded(child, excluded);
        }
    }

    private static string Marker(NodeStatus status) => status switch
    {
        NodeStatus.OversizedFile => " [LARGE]",
        NodeStatus.TokenBudgetOmitted => " [OMITTED BY BUDGET]",
        _ => string.Empty
    };

    /// <summary>
    /// Untracked files are called out because <c>git diff</c> cannot describe them; their diffs are generated
    /// separately and follow below.
    /// </summary>
    private static void WriteUntrackedNote(TextWriter writer, IReadOnlyList<DiffChunk> untracked)
    {
        if (untracked.Count == 0)
        {
            return;
        }

        writer.WriteLine($"New files git does not track yet ({untracked.Count}), generated diffs included below:");
        writer.WriteLine();
    }

    private static void WriteChunks(TextWriter writer, IReadOnlyList<DiffChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"### DIFF: `{chunk.Path}`");
            writer.WriteLine();

            writer.WriteLine($"- Change: `{chunk.Kind.ToString().ToLowerInvariant()}`; lines: `+{chunk.AddedLines}/-{chunk.DeletedLines}`; tokens: `~{chunk.EstimatedTokens}`");
            writer.WriteLine();

            var fence = BuildFence(chunk.Text);
            writer.WriteLine($"{fence}diff");
            writer.Write(chunk.Text);
            if (!chunk.Text.EndsWith('\n'))
            {
                writer.WriteLine();
            }

            writer.WriteLine(fence);
            writer.WriteLine();
        }
    }

    /// <summary>Uses a fence longer than any backtick run inside the diff, so content cannot close it early.</summary>
    private static string BuildFence(string content)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in content)
        {
            currentRun = character == '`' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }
}
