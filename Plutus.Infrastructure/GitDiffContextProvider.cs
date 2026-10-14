using System.Text;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.Diffing;
using Plutus.Infrastructure.Git;

namespace Plutus.Infrastructure;

/// <summary>
/// Discovers the changed-file subset and the raw diff text by running git. An explicit target uses
/// merge-base semantics with the working tree as the destination; otherwise the working tree is compared
/// with the upstream branch when one exists, falling back to <c>HEAD</c> so uncommitted work is included.
/// </summary>
public sealed class GitDiffContextProvider(
    IGitCommandExecutor gitCommandExecutor,
    bool ignorePathCase = true,
    long maxUntrackedFileDiffBytes = GitDiffContextProvider.DefaultMaxUntrackedFileDiffBytes) : IGitDiffContextProvider
{
    private const string Head = "HEAD";

    /// <summary>
    /// Generated diffs carry the whole file, so a large untracked file would otherwise be inlined verbatim.
    /// Such a file is still listed and still bundled as content; only its diff is skipped.
    /// </summary>
    public const long DefaultMaxUntrackedFileDiffBytes = 1024 * 1024;

    public async Task<GitDiffContext?> ResolveAsync(
        GitDiffOptions options,
        IReadOnlyList<IFileInfo> roots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(roots);

        if (roots.Count == 0)
        {
            await Console.Error.WriteLineAsync("Error: Diff scoping requires at least one project path.").ConfigureAwait(false);
            return null;
        }

        // Discovery starts at the bundle root, so a path outside a repository is reported as such instead of
        // silently inheriting whichever repository the process happens to run in.
        var discoveryDirectory = RootPath(roots[0]);
        var repositoryRoot = await TryRunAsync(discoveryDirectory, ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (repositoryRoot?.Succeeded != true || string.IsNullOrWhiteSpace(repositoryRoot.Value.StandardOutput))
        {
            await Console.Error.WriteLineAsync("Error: Diff scoping requires a local git repository; no repository was found for the given project path.").ConfigureAwait(false);
            return null;
        }

        var workingDirectory = repositoryRoot.Value.StandardOutput;
        var mapper = new ChangedFileMapper(workingDirectory, ignorePathCase);

        var outsideRoot = roots.FirstOrDefault(root => !mapper.IsInsideRepository(RootPath(root)));
        if (outsideRoot is not null)
        {
            await Console.Error.WriteLineAsync($"Error: Diff scoping requires every project path to be inside the git repository '{workingDirectory}'. Outside: '{RootPath(outsideRoot)}'.").ConfigureAwait(false);
            return null;
        }

        var comparisonBase = await ResolveComparisonBaseAsync(workingDirectory, options, cancellationToken).ConfigureAwait(false);
        if (comparisonBase is null)
        {
            return null;
        }

        var diff = await RunAsync(workingDirectory, GitDiffArguments.Build(options, comparisonBase), cancellationToken).ConfigureAwait(false);
        if (!diff.Succeeded)
        {
            await Console.Error.WriteLineAsync($"Error: git diff failed: {diff.StandardError}").ConfigureAwait(false);
            return null;
        }

        var context = new GitDiffContext(comparisonBase, GitDiffParser.ParseChangedFiles(diff.StandardOutput), diff.StandardOutput);
        if (options.IncludeUntracked)
        {
            var untracked = await ResolveUntrackedFilesAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
            if (untracked is null)
            {
                return null;
            }

            context = context with
            {
                UntrackedFiles = untracked,
                UntrackedRawText = await BuildUntrackedDiffsAsync(workingDirectory, untracked, cancellationToken).ConfigureAwait(false)
            };
        }

        return mapper.Resolve(context, roots);
    }

    /// <summary>
    /// Lists files git does not track yet, excluding anything <c>.gitignore</c> already covers. Without this
    /// a newly created file is invisible, because <c>git diff</c> only looks at tracked content.
    /// </summary>
    private async Task<IReadOnlyList<string>?> ResolveUntrackedFilesAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var result = await TryRunAsync(workingDirectory, GitDiffArguments.ListUntrackedFiles, cancellationToken).ConfigureAwait(false);
        if (result?.Succeeded != true)
        {
            await Console.Error.WriteLineAsync($"Error: git ls-files failed: {result?.StandardError ?? "no output"}").ConfigureAwait(false);
            return null;
        }

        return GitDiffParser.ParseNullSeparatedPaths(result.Value.StandardOutput);
    }

    /// <summary>
    /// Builds a generated diff for each untracked file, so the structural section stays complete. A diff
    /// that cannot be produced is skipped with a warning rather than failing the whole bundle.
    /// </summary>
    private async Task<string> BuildUntrackedDiffsAsync(
        string workingDirectory,
        IReadOnlyList<string> untrackedFiles,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        foreach (var path in untrackedFiles)
        {
            var fileDiff = await TryRunUntrackedDiffAsync(workingDirectory, path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fileDiff))
            {
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append(fileDiff).Append('\n');
        }

        return builder.ToString();
    }

    private async Task<string?> TryRunUntrackedDiffAsync(string workingDirectory, string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(workingDirectory, path.Replace('/', Path.DirectorySeparatorChar));
        if (new FileInfo(fullPath) is { Exists: true } file && file.Length > maxUntrackedFileDiffBytes)
        {
            await Console.Error.WriteLineAsync($"Warning: skipping the generated diff for untracked file '{path}' because it is larger than {maxUntrackedFileDiffBytes} bytes.").ConfigureAwait(false);
            return null;
        }

        try
        {
            var result = await RunAsync(workingDirectory, GitDiffArguments.BuildUntrackedFileDiff(path), cancellationToken).ConfigureAwait(false);

            // "git diff --no-index" exits with 1 when differences exist, which is the expected outcome here.
            if (result.ExitCode is 0 or 1 && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return result.StandardOutput;
            }

            await Console.Error.WriteLineAsync($"Warning: unable to generate a diff for untracked file '{path}': {result.StandardError}").ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync($"Warning: unable to generate a diff for untracked file '{path}': {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private async Task<string?> ResolveComparisonBaseAsync(string workingDirectory, GitDiffOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            // The ref itself is kept rather than the commit it points at, because merge-base semantics are
            // evaluated by git when the diff runs.
            return options.Target;
        }

        var upstream = await TryRunAsync(workingDirectory, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"], cancellationToken).ConfigureAwait(false);
        if (upstream is { Succeeded: true } upstreamResult && !string.IsNullOrWhiteSpace(upstreamResult.StandardOutput))
        {
            return upstreamResult.StandardOutput;
        }

        return await TryResolveRevisionAsync(workingDirectory, Head, "Cannot scope by diff because the repository has no commits yet.", cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryResolveRevisionAsync(string workingDirectory, string revision, string failureMessage, CancellationToken cancellationToken)
    {
        var resolved = await TryRunAsync(workingDirectory, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], cancellationToken).ConfigureAwait(false);
        if (resolved is { Succeeded: true } resolvedResult && !string.IsNullOrWhiteSpace(resolvedResult.StandardOutput))
        {
            return resolvedResult.StandardOutput;
        }

        await Console.Error.WriteLineAsync($"Error: {failureMessage}").ConfigureAwait(false);
        return null;
    }

    private async Task<GitCommandResult?> TryRunAsync(string? workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await RunAsync(workingDirectory, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync($"Error: Unable to run git. Install git and ensure it is available on PATH. ({exception.Message})").ConfigureAwait(false);
            return null;
        }
    }

    private Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        gitCommandExecutor.RunAsync(workingDirectory ?? Directory.GetCurrentDirectory(), arguments, cancellationToken);

    private static string RootPath(IFileInfo root) => string.IsNullOrEmpty(root.PhysicalPath) ? root.Name : root.PhysicalPath;
}
