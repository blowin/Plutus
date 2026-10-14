using System.Text;
using Plutus.Domain.Diffing;

namespace Plutus.Domain.BundleWriters;

/// <summary>
/// Renders the complementary diff section that documents the requested change structurally next to the
/// changed file contents. The stored diff text is already merge-base applied by git, so it is emitted
/// verbatim inside a fence that cannot be terminated by diff content.
/// </summary>
public static class BundleDiff
{
    public static string RenderSection(GitDiffContext diff, string changedFilesHeading)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var builder = new StringBuilder();
        builder.Append("## Changes against `").Append(diff.ComparisonBase).Append('`').Append('\n');
        builder.Append('\n');

        AppendUntrackedFiles(builder, diff);
        AppendRawDiff(builder, diff, changedFilesHeading);
        return builder.ToString();
    }

    /// <summary>
    /// Untracked files are listed separately because <c>git diff</c> cannot describe them; their generated
    /// new-file diffs follow in the same fenced block as the tracked diff.
    /// </summary>
    private static void AppendUntrackedFiles(StringBuilder builder, GitDiffContext diff)
    {
        if (diff.UntrackedFiles.Count == 0)
        {
            return;
        }

        builder.Append("New files git does not track yet (").Append(diff.UntrackedFiles.Count).Append("), generated diffs below:").Append('\n');
        builder.Append('\n');
        foreach (var path in diff.UntrackedFiles)
        {
            builder.Append("- `").Append(path).Append('`').Append('\n');
        }

        builder.Append('\n');
    }

    private static void AppendRawDiff(StringBuilder builder, GitDiffContext diff, string changedFilesHeading)
    {
        var combinedDiff = CombineDiffs(diff.RawText, diff.UntrackedRawText);

        if (diff.Files.Count == 0 && string.IsNullOrWhiteSpace(combinedDiff))
        {
            builder.Append("No changed files were reported for this scope.").Append('\n');
            return;
        }

        if (diff.Files.Count > 0)
        {
            builder.Append(changedFilesHeading).Append(' ').Append(diff.Files.Count).Append(" file(s).").Append('\n');
        }

        if (string.IsNullOrWhiteSpace(combinedDiff))
        {
            return;
        }

        builder.Append('\n');
        builder.Append("### Raw diff").Append('\n');
        builder.Append('\n');
        builder.Append(BuildFence(combinedDiff)).Append("diff").Append('\n');
        builder.Append(combinedDiff);
        if (!combinedDiff.EndsWith('\n'))
        {
            builder.Append('\n');
        }

        builder.Append(BuildFence(combinedDiff)).Append('\n');
    }

    /// <summary>Joins the tracked diff and the generated untracked diffs into one block for a single fence.</summary>
    private static string CombineDiffs(string trackedDiff, string untrackedDiff)
    {
        if (string.IsNullOrWhiteSpace(trackedDiff))
        {
            return untrackedDiff;
        }

        if (string.IsNullOrWhiteSpace(untrackedDiff))
        {
            return trackedDiff;
        }

        var separator = trackedDiff.EndsWith('\n') ? "\n" : "\n\n";
        return trackedDiff + separator + untrackedDiff;
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
