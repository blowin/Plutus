using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Infrastructure.Git;

/// <summary>
/// Discovers the per-file diffs to bundle by running git, then hands the raw chunks to
/// <see cref="DiffScopeLimiter"/>, which maps them onto the bundle roots and applies the limits.
/// </summary>
/// <remarks>
/// Two comparisons are supported. With only a target, the merge base of that target and <c>HEAD</c> is
/// compared with the working tree, so committed and uncommitted work are reviewed together. With a target
/// and <see cref="GitDiffOptions.TargetTo"/>, the two revisions are compared directly, which is how a
/// specific commit range is bundled.
/// </remarks>
public sealed class GitDiffContextProvider(
    IGitCommandExecutor gitCommandExecutor,
    DiffScopeLimiter diffScopeLimiter,
    bool ignorePathCase = true,
    IRunOutput? runOutput = null) : IGitDiffContextProvider
{
    private const string Head = "HEAD";

    private readonly IRunOutput _output = runOutput ?? NullRunOutput.Instance;

    public async Task<DiffScopeResult?> ResolveAsync(
        GitDiffOptions options,
        IReadOnlyList<IFileInfo> roots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(roots);

        if (roots.Count == 0)
        {
            _output.Error("Diff scoping requires at least one project path.");
            return null;
        }

        // Discovery starts at the bundle root, so a path outside a repository is reported as such instead of
        // silently inheriting whichever repository the process happens to run in.
        var discoveryDirectory = ChangedFileMapper.RootPath(roots[0]);
        var repositoryRoot = await TryRunAsync(discoveryDirectory, ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (repositoryRoot?.Succeeded != true || string.IsNullOrWhiteSpace(repositoryRoot.Value.StandardOutput))
        {
            _output.Error("Diff scoping requires a local git repository; no repository was found for the given project path.");
            return null;
        }

        var workingDirectory = repositoryRoot.Value.StandardOutput;
        var mapper = new ChangedFileMapper(workingDirectory, ignorePathCase);

        var outsideRoot = roots.FirstOrDefault(root => !mapper.IsInsideRepository(ChangedFileMapper.RootPath(root)));
        if (outsideRoot is not null)
        {
            _output.Error($"Diff scoping requires every project path to be inside the git repository '{workingDirectory}'. Outside: '{ChangedFileMapper.RootPath(outsideRoot)}'.");
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
            _output.Error($"git diff failed: {diff.StandardError}");
            return null;
        }

        var trackedChunks = GitDiffChunker.Split(diff.StandardOutput);
        var untrackedChunks = options.IncludeUntracked
            ? await ResolveUntrackedChunksAsync(workingDirectory, cancellationToken).ConfigureAwait(false)
            : [];

        try
        {
            return diffScopeLimiter.Apply(
                workingDirectory,
                comparisonBase,
                trackedChunks,
                untrackedChunks,
                roots,
                path => FileSizeOf(workingDirectory, path));
        }
        catch (TokenBudgetExceededException exception)
        {
            _output.Error(exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Size of a file as it exists now. A path with no file behind it reports zero, because a deletion has
    /// nothing left to weigh.
    /// </summary>
    private static long FileSizeOf(string workingDirectory, string repositoryRelativePath)
    {
        var fullPath = Path.Combine(workingDirectory, repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return new FileInfo(fullPath) is { Exists: true } file ? file.Length : 0;
    }

    /// <summary>
    /// Builds one generated diff per untracked file, so an uncommitted new file is not silently invisible.
    /// </summary>
    private async Task<IReadOnlyList<DiffChunk>> ResolveUntrackedChunksAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var listed = await TryRunAsync(workingDirectory, GitDiffArguments.ListUntrackedFiles, cancellationToken).ConfigureAwait(false);
        if (listed?.Succeeded != true)
        {
            _output.Error($"git ls-files failed: {listed?.StandardError ?? "no output"}");
            return [];
        }

        var paths = GitDiffParser.ParseNullSeparatedPaths(listed.Value.StandardOutput);
        var chunks = new List<DiffChunk>(paths.Count);
        foreach (var path in paths)
        {
            var text = await TryRunUntrackedDiffAsync(workingDirectory, path, cancellationToken).ConfigureAwait(false);
            if (text is not null)
            {
                chunks.AddRange(GitDiffChunker.Split(text));
            }
        }

        return chunks;
    }

    private async Task<string?> TryRunUntrackedDiffAsync(
        string workingDirectory,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunAsync(workingDirectory, GitDiffArguments.BuildUntrackedFileDiff(path), cancellationToken).ConfigureAwait(false);

            // "git diff --no-index" exits with 1 when differences exist, which is the expected outcome here.
            if (result.ExitCode is 0 or 1 && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return result.StandardOutput;
            }

            _output.Warning($"unable to generate a diff for untracked file '{path}': {result.StandardError}");
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _output.Warning($"unable to generate a diff for untracked file '{path}': {exception.Message}");
            return null;
        }
    }

    private async Task<string?> ResolveComparisonBaseAsync(string workingDirectory, GitDiffOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            // The ref itself is kept rather than the commit it points at, because the comparison semantics
            // are evaluated by git when the diff runs.
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

        _output.Error(failureMessage);
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
            _output.Error($"Unable to run git. Install git and ensure it is available on PATH. ({exception.Message})");
            return null;
        }
    }

    private Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        gitCommandExecutor.RunAsync(workingDirectory ?? Directory.GetCurrentDirectory(), arguments, cancellationToken);
}
