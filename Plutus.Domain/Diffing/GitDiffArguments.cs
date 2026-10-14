namespace Plutus.Domain.Diffing;

/// <summary>
/// Builds the git command line for the supported diff scopes. An explicit base uses merge-base semantics
/// with the working tree as the destination, so the commits made on the branch and any uncommitted work are
/// reviewed together. Without a base the working tree is compared with <c>HEAD</c>, which keeps the diff
/// limited to uncommitted work.
/// </summary>
public static class GitDiffArguments
{
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
