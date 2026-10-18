using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Domain;

/// <summary>
/// Scans the roots and writes the bundle. It reports problems through <see cref="IRunOutput"/> and records the run
/// numbers in <see cref="BundleRunReport"/>; it does not format or print the summary itself.
/// </summary>
public class PlutusBundler(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IReadOnlyCollection<IBundleWriter> bundleWriters,
    IRunOutput? runOutput = null,
    BundleRunReport? runReport = null)
{
    private readonly IRunOutput _output = runOutput ?? NullRunOutput.Instance;
    private readonly BundleRunReport _report = runReport ?? new BundleRunReport();

    /// <summary>Scans the bundle roots and writes the requested format, restricted to the diff scope when one was resolved.</summary>
    public virtual async Task<int> RunBundleAsync(
        ProjectScanner projectScanner,
        IReadOnlyCollection<IFileInfo> roots,
        IFileInfo output,
        RunAppOptions options,
        GitDiffContext? gitDiff = null,
        CancellationToken cancellationToken = default)
    {
        if (output.IsDirectory)
        {
            _output.Error($"Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return 1;
        }

        var bundleWriter = bundleWriters.FirstOrDefault(e => e.FormatExtension.Equals(options.Format, StringComparison.InvariantCultureIgnoreCase));
        if (bundleWriter is null)
        {
            var list = string.Join(", ", bundleWriters.Select(e => e.FormatExtension));
            _output.Error($"Unsupported compilation format targeting style '{options.Format}'. Available: [{list}].");
            return 1;
        }

        var physicalPath = output.PhysicalPath;
        if (string.IsNullOrWhiteSpace(physicalPath))
        {
            _output.Error("Output path is empty.");
            return 1;
        }

        // Start core operational stopwatch benchmarking
        var stopwatch = Stopwatch.StartNew();

        var bundleWriterEntries = new List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)>(roots.Count);
        foreach (var root in roots)
        {
            var provider = fileInfoDetailProvider.CreateFileProvider(root);
            var projectNode = projectScanner.Scan(provider, root.Name, physicalPath, options.MaxFileSize, CreateDiffPaths(gitDiff, root));
            bundleWriterEntries.Add((provider, root.Name, root.PhysicalPath ?? root.Name, projectNode));
        }

        // Changes a limit left out are reported as tree markers, not as diff blocks with nothing but a reason.
        if (gitDiff is not null)
        {
            BundleDiff.MarkExcluded(gitDiff.TrackedChunks.Concat(gitDiff.UntrackedChunks).ToArray(), bundleWriterEntries.ConvertAll(entry => entry.ProjectNode));
        }

        List<BundleEntry> allEntries;
        try
        {
            bundleWriter.IncludeFileContents = options.IncludeFileContents;
            allEntries = await bundleWriter.WriteAsync(bundleWriterEntries, output, gitDiff, cancellationToken).ConfigureAwait(false);
        }
        catch (TokenBudgetExceededException ex)
        {
            _output.Error(ex.Message);
            return 1;
        }


        stopwatch.Stop();

        // The numbers are recorded, not printed: the host decides how a run summary reads.
        _report.Record(physicalPath, allEntries.Count, allEntries.Sum(e => e.File.Length), stopwatch.Elapsed);
        return 0;
    }

    /// <summary>
    /// Combines the tracked diff scope with any requested untracked files into one selection, so both kinds
    /// of change reach the bundle while everything else stays excluded.
    /// </summary>
    private static DiffPathSet? CreateDiffPaths(GitDiffContext? gitDiff, IFileInfo root)
    {
        if (gitDiff is null)
        {
            return null;
        }

        var rootPath = ChangedFileMapper.RootPath(root);
        var paths = gitDiff.TrackedPathsFor(rootPath).Concat(gitDiff.UntrackedPathsFor(rootPath));
        return DiffPathSet.Create(paths);
    }
}
