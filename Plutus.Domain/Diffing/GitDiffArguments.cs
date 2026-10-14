namespace Plutus.Domain.Diffing;

/// <summary>
/// Builds the git command lines for the supported diff scopes. An explicit base uses merge-base semantics
/// with the working tree as the destination, so the commits made on the branch and any uncommitted work are
/// reviewed together. Without a base the working tree is compared with <c>HEAD</c>, which keeps the diff
/// limited to uncommitted work.
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

    /// <summary><c>/dev/null</c> is accepted by git on every platform, including Windows.</summary>
    private const string DevNullDevice = "/dev/null";

    public static IReadOnlyList<string> Build(GitDiffOptions options, string? comparisonBase)
    {
        ArgumentNullException.ThrowIfNull(options);

        var arguments = new List<string> { "diff", "--no-color", "--no-ext-diff", "--no-renames" };
        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            arguments.Add("--merge-base");
            arguments.Add(string.IsNullOrWhiteSpace(comparisonBase) ? options.Target : comparisonBase);
        }

        return arguments;
    }
}
