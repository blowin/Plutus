using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class PlutusBundler(IFileInfoDetailProvider fileInfoDetailProvider, IIgnoreMatcher ignoreMatcher, IMarkdownLanguageProvider markdownLanguageProvider)
{
    public async Task<int> RunBundleAsync(
        IReadOnlyCollection<IFileInfo> roots,
        IFileInfo output,
        FileSize maxFileSize)
    {
        if (output.IsDirectory)
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return 1;
        }

        var physicalPath = output.PhysicalPath;
        if (string.IsNullOrWhiteSpace(physicalPath))
        {
            Console.Error.WriteLine("Error: Output path is empty.");
            return 1;
        }

        var projectScanner = new ProjectScanner(fileInfoDetailProvider, ignoreMatcher);
        var bundleWriterEntries = new List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)>(roots.Count);
        foreach (var root in roots)
        {
            var provider = fileInfoDetailProvider.CreateFileProvider(root);
            var projectNode = projectScanner.Scan(provider, root.Name, physicalPath, maxFileSize);
            bundleWriterEntries.Add((provider, root.Name, root.PhysicalPath ?? root.Name, projectNode));
        }

        var bundleWriter = new BundleWriter(fileInfoDetailProvider, markdownLanguageProvider);

        var allEntries = await bundleWriter.WriteAsync(bundleWriterEntries, output, maxFileSize).ConfigureAwait(false);

        Console.WriteLine($"Bundle created: {physicalPath}");
        Console.WriteLine($"Files included: {allEntries.Count}");
        Console.WriteLine($"Total size: {allEntries.Sum(e => e.File.Length).HumanSize()}");
        return 0;
    }
}
