using System.Text;
using Plutus.Domain.Diffing;

namespace Plutus.Domain.BundleWriters;

/// <summary>
/// Renders the complementary diff section that documents the requested change structurally next to the
/// changed file contents. The stored text is already merge-base applied by git, so it is emitted verbatim
/// inside a fence that cannot be terminated by diff content.
/// </summary>
public static class BundleDiff
{
    public static string RenderSection(GitDiffContext diff, string changedFilesHeading)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var builder = new StringBuilder();
        builder.Append("## Changes against `").Append(diff.ComparisonBase).Append('`').Append('\n');
        builder.Append('\n');

        if (diff.Files.Count == 0)
        {
            builder.Append("No changed files were reported for this scope.").Append('\n');
            return builder.ToString();
        }

        builder.Append(changedFilesHeading).Append(' ').Append(diff.Files.Count).Append(" file(s).").Append('\n');

        if (string.IsNullOrWhiteSpace(diff.RawText))
        {
            return builder.ToString();
        }

        builder.Append('\n');
        builder.Append("### Raw diff").Append('\n');
        builder.Append('\n');
        builder.Append(BuildFence(diff.RawText)).Append("diff").Append('\n');
        builder.Append(diff.RawText);
        if (!diff.RawText.EndsWith('\n'))
        {
            builder.Append('\n');
        }

        builder.Append(BuildFence(diff.RawText)).Append('\n');
        return builder.ToString();
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
