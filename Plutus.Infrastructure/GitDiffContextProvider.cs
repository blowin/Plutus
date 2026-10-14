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
    bool ignorePathCase = false) : IGitDiffContextProvider
{
    private const string Head = "HEAD";

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
        return mapper.Resolve(context, roots);
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
