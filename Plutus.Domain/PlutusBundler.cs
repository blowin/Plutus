using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.BundleWriters;

namespace Plutus.Domain;

public class PlutusBundler(IFileInfoDetailProvider fileInfoDetailProvider, IReadOnlyCollection<IBundleWriter> bundleWriters)
{
    public async Task<int> RunBundleAsync(
        ProjectScanner projectScanner,
        IReadOnlyCollection<IFileInfo> roots,
        IFileInfo output,
        RunAppOptions options)
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
            var projectNode = projectScanner.Scan(provider, root.Name, physicalPath, options.MaxFileSize);
            bundleWriterEntries.Add((provider, root.Name, root.PhysicalPath ?? root.Name, projectNode));
        }

        var allEntries = await bundleWriter.WriteAsync(bundleWriterEntries, output, options.MaxFileSize).ConfigureAwait(false);

        stopwatch.Stop();

        Console.WriteLine($"Bundle created: {physicalPath}");
        Console.WriteLine($"Files included: {allEntries.Count}");
        Console.WriteLine($"Total size: {allEntries.Sum(e => e.File.Length).HumanSize()}");
        Console.WriteLine($"Elapsed time:   {stopwatch.Elapsed.TotalSeconds:F2}s ({stopwatch.ElapsedMilliseconds} ms)");
        return 0;
    }
}
