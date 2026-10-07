using System.Text;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.CLI;

public class App(IRemoteRepository[] remoteRepositoryProviders, IFileInfoDetailProvider fileInfoDetailProvider)
{
    public async Task<int> RunBundleAsync(List<string>? projectPaths,
        string? outputPath,
        long maxFileSize,
        bool useGitIgnore,
        List<string> extraIgnorePatterns,
        bool useDefaultExcludes,
        bool allowDangerousFiles)
    {
        if (projectPaths == null || projectPaths.Count == 0)
        {
            Console.Error.WriteLine("Error: No project paths specified.");
            return 1;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var processedPaths = await GetPathForProcessAsync(projectPaths);
        var roots = processedPaths?.Select(ResolveDirectory).ToList();
        if (roots is null)
        {
            return 1;
        }

        var primaryRoot = roots.First();
        var normalizeOutputPath = NormalizeOutputPath(projectPaths, outputPath);
        var output = ResolveOutput(primaryRoot, normalizeOutputPath);

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

        IIgnoreMatcher ignoreMatcher = CreateIgnoreMatcher(
            roots.ConvertAll(fileInfoDetailProvider.CreateFileProvider),
            useGitIgnore,
            extraIgnorePatterns,
            useDefaultExcludes,
            allowDangerousFiles);
        var projectScanner = new ProjectScanner(fileInfoDetailProvider, ignoreMatcher);
        var bundleWriterEntries = new List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)>(roots.Count);
        foreach (var root in roots)
        {
            var provider = fileInfoDetailProvider.CreateFileProvider(root);
            var projectNode = projectScanner.Scan(provider, root.Name, physicalPath, maxFileSize);
            bundleWriterEntries.Add((provider, root.Name, root.PhysicalPath ?? root.Name, projectNode));
        }

        var bundleWriter = new BundleWriter(fileInfoDetailProvider);

        var allEntries = await bundleWriter.WriteAsync(bundleWriterEntries, output, maxFileSize).ConfigureAwait(false);

        Console.WriteLine($"Bundle created: {physicalPath}");
        Console.WriteLine($"Files included: {allEntries.Count}");
        Console.WriteLine($"Total size: {allEntries.Sum(e => e.File.Length).HumanSize()}");
        return 0;
    }

    private string? NormalizeOutputPath(List<string> projectPaths, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return outputPath;
        }

        var firstProjectPath = projectPaths.First();
        return remoteRepositoryProviders
            .Where(r => r.IsSupportedPath(firstProjectPath))
            .Select(r => r.NormalizeOutputPath(firstProjectPath))
            .FirstOrDefault();
    }

    private async Task<List<string>?> GetPathForProcessAsync(List<string> projectPaths)
    {
        var processedPaths = new List<string>();
        foreach (var path in projectPaths)
        {
            try
            {
                var remotePath = await ExtractRemotePathAsync(path);
                processedPaths.Add(remotePath ?? path);
            }
            catch
            {
                return null;
            }
        }

        return processedPaths;

        async ValueTask<string?> ExtractRemotePathAsync(string path)
        {
            foreach (var remoteRepositoryProvider in remoteRepositoryProviders)
            {
                if (!remoteRepositoryProvider.IsSupportedPath(path))
                {
                    continue;
                }

                Console.WriteLine($"Downloading remote repository: {path}...");
                try
                {
                    return await remoteRepositoryProvider.DownloadAsync(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error downloading repository {path}: {ex.Message}");
                    throw;
                }
            }

            return null;
        }
    }

    private static IIgnoreMatcher CreateIgnoreMatcher(List<IFileProvider> roots,
        bool useGitIgnore,
        List<string> extraIgnorePatterns,
        bool useDefaultExcludes,
        bool allowDangerousFiles)
    {
        var ignoreMatchBuilder = new IgnoreMatcherBuilder();
        var ignoreLines = useGitIgnore ? roots.SelectMany(LoadIgnoreLines).ToHashSet() : [];
        foreach (var extraIgnorePattern in extraIgnorePatterns)
        {
            ignoreLines.Add(extraIgnorePattern);
        }
        ignoreLines.RemoveWhere(string.IsNullOrWhiteSpace);

        if (ignoreLines.Count > 0)
        {
            ignoreMatchBuilder.Add(new GitIgnoreMatcher(new IgnoreList(ignoreLines)));
        }

        if (useDefaultExcludes)
        {
            ignoreMatchBuilder.IgnoreJunk();
        }

        if (!allowDangerousFiles)
        {
            ignoreMatchBuilder.IgnoreDangerousFile();
        }

        return ignoreMatchBuilder.Build();
    }

    private static List<string> LoadIgnoreLines(IFileProvider provider)
    {
        var lines = new List<string>();
        var gitIgnoreFile = provider.GetFileInfo(".gitignore");
        if (!gitIgnoreFile.Exists)
        {
            return lines;
        }

        try
        {
            using var stream = gitIgnoreFile.CreateReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to read {gitIgnoreFile.PhysicalPath ?? gitIgnoreFile.Name}: {ex.Message}");
        }

        return lines;
    }

    private IFileInfo ResolveDirectory(string path)
    {
        var full = path.GetExpandPath();
        var dir = fileInfoDetailProvider.CreateDirectory(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return dir;
    }

    private IFileInfo ResolveOutput(IFileInfo root, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return fileInfoDetailProvider.CreateFile(outputPath.GetExpandPath());
        }

        var folderName = string.IsNullOrWhiteSpace(root.Name) ? "project" : root.Name;
        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), $"{folderName}_bundle.md");
        return fileInfoDetailProvider.CreateFile(defaultPath.GetExpandPath());
    }
}
