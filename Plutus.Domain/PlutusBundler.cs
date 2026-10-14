using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Domain;

public class PlutusBundler(IFileInfoDetailProvider fileInfoDetailProvider, IReadOnlyCollection<IBundleWriter> bundleWriters)
{
    /// <summary>Scans the bundle roots and writes the requested format, restricted to the diff subset when one was resolved.</summary>
    public virtual async Task<int> RunBundleAsync(
        ProjectScanner projectScanner,
        IReadOnlyCollection<IFileInfo> roots,
        IFileInfo output,
        RunAppOptions options,
        GitDiffContext? gitDiff = null)
    {
        if (output.IsDirectory)
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return 1;
        }

        var bundleWriter = bundleWriters.FirstOrDefault(e => e.FormatExtension.Equals(options.Format, StringComparison.InvariantCultureIgnoreCase));
        if (bundleWriter is null)
        {
            var list = string.Join(", ", bundleWriters.Select(e => e.FormatExtension));
            Console.Error.WriteLine($"Error: Unsupported compilation format targeting style '{options.Format}'. Available: [{list}].");
            return 1;
        }

        var physicalPath = output.PhysicalPath;
        if (string.IsNullOrWhiteSpace(physicalPath))
        {
            Console.Error.WriteLine("Error: Output path is empty.");
            return 1;
        }

        // Start core operational stopwatch benchmarking
        var stopwatch = Stopwatch.StartNew();

        var bundleWriterEntries = new List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)>(roots.Count);
        foreach (var root in roots)
        {
            var provider = fileInfoDetailProvider.CreateFileProvider(root);
            var diffPaths = gitDiff is null ? null : DiffPathSet.Create(gitDiff.PathsFor(root.PhysicalPath ?? root.Name), VcsPathComparer.Comparer);
            var projectNode = projectScanner.Scan(provider, root.Name, physicalPath, options.MaxFileSize, diffPaths);
            bundleWriterEntries.Add((provider, root.Name, root.PhysicalPath ?? root.Name, projectNode));
        }

        List<BundleEntry> allEntries;
        try
        {
            allEntries = await bundleWriter.WriteAsync(bundleWriterEntries, output, options.MaxFileSize, gitDiff).ConfigureAwait(false);
        }
        catch (TokenBudgetExceededException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }


        stopwatch.Stop();

        Console.WriteLine($"Bundle created: {physicalPath}");
        Console.WriteLine($"Files included: {allEntries.Count}");
        Console.WriteLine($"Total size: {allEntries.Sum(e => e.File.Length).HumanSize()}");
        Console.WriteLine($"Elapsed time:   {stopwatch.Elapsed.TotalSeconds:F2}s ({stopwatch.ElapsedMilliseconds} ms)");
        return 0;
    }
}
