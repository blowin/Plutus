namespace Plutus.Domain.Diffing;

/// <summary>
/// Builds the git command lines for the supported diff scopes: an explicit commit range, or an explicit base
/// compared with the working tree so uncommitted work is still reviewed, or the working tree alone.
/// </summary>
public static class GitDiffArguments
{
    /// <summary>Lists untracked files that are not ignored, null-terminated so paths cannot be split.</summary>
    public static IReadOnlyList<string> ListUntrackedFiles { get; } =
        ["ls-files", "-z", "--others", "--exclude-standard"];

    /// <summary>
    /// Builds a real "new file" diff for a path git does not track. <c>--no-index</c> is the only way to get
    /// structured hunks for such a file, because the index has nothing to compare it against.
    /// </summary>
    public static IReadOnlyList<string> BuildUntrackedFileDiff(string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);

        return ["diff", "--no-color", "--no-ext-diff", "--no-index", "--", DevNullDevice, repositoryRelativePath];
    }

    public static IReadOnlyList<string> Build(GitDiffOptions options, string? comparisonBase)
    {
        ArgumentNullException.ThrowIfNull(options);

        var arguments = new List<string> { "diff", "--no-color", "--no-ext-diff", "--no-renames" };

        if (!string.IsNullOrWhiteSpace(options.TargetTo))
        {
            // An explicit range compares the two revisions directly; the working tree is not involved.
            arguments.Add(string.IsNullOrWhiteSpace(comparisonBase) ? options.Target! : comparisonBase);
            arguments.Add(options.TargetTo);
            return arguments;
        }

        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            arguments.Add("--merge-base");
            arguments.Add(string.IsNullOrWhiteSpace(comparisonBase) ? options.Target : comparisonBase);
        }

        return arguments;
    }

    /// <summary><c>/dev/null</c> is accepted by git on every platform, including Windows.</summary>
    private const string DevNullDevice = "/dev/null";
}
