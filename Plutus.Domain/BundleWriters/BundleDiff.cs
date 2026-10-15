using System.Text;
using Plutus.Domain.Diffing;

namespace Plutus.Domain.BundleWriters;

/// <summary>
/// Renders the diff sections. Each file gets its own fenced block instead of one solid patch, because that is
/// what makes the size and token limits meaningful: a change that is left out is reported in place, and the
/// rest of the change set is still readable.
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

        var tracked = diff.TrackedChunks;
        var untracked = diff.UntrackedChunks;

        // When the contents are omitted the diff is the artifact, so the scope line always appears. When the
        // contents are present the "## File List" block has already stated the count.
        if (!includeFileContents || tracked.Count > 0)
        {
            writer.WriteLine($"{changedFilesHeading} {tracked.Count} file(s).");
            writer.WriteLine();
        }

        WriteUntrackedNote(writer, untracked);

        if (tracked.Count == 0 && untracked.Count == 0)
        {
            writer.WriteLine("No changed files were reported for this scope.");
            return;
        }

        WriteChunks(writer, tracked);
        WriteChunks(writer, untracked);
    }

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

            if (chunk.IsSkipped)
            {
                writer.WriteLine($"Diff omitted: {chunk.SkipReason}");
                writer.WriteLine();
                continue;
            }

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
