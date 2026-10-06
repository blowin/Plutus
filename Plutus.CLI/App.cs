using System.Text;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.CLI;

public class App(IRemoteRepositoryProvider[] remoteRepositoryProviders)
{
    public async Task<int> RunBundleAsync(
        List<string>? projectPaths,
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
        var output = ResolveOutput(primaryRoot.Directory, normalizeOutputPath);

        if (Directory.Exists(output.FullName))
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.FullName}");
            return 1;
        }

        var allEntries = new List<BundleEntry>();

        IIgnoreMatcher ignoreMatcher = CreateIgnoreMatcher(roots.ConvertAll(e => e.Provider), useGitIgnore, extraIgnorePatterns, useDefaultExcludes, allowDangerousFiles);
        var collector = new FileCollector(ignoreMatcher);
        foreach (var root in roots)
        {
            var entries = collector.CollectFiles(root.Provider, output.FullName);
            allEntries.AddRange(entries);
        }

        allEntries.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        var bundleWriter = new BundleWriter(new PhysicianPlutusFileInfo(new PhysicalDirectoryInfo(primaryRoot.Directory)), ignoreMatcher);

        await bundleWriter.WriteAsync(
            roots.ConvertAll(e => (e.Provider, e.RootName, e.OriginalPath)),
            output,
            allEntries,
            maxFileSize)
            .ConfigureAwait(false);

        Console.WriteLine($"Bundle created: {output.FullName}");
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
            var remotePath = await ExtractRemotePathAsync(path);
            processedPaths.Add(remotePath ?? path);
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
                    return null;
                }
            }

            return null;
        }
    }

    private static IIgnoreMatcher CreateIgnoreMatcher(
        List<IFileProvider> roots,
        bool useGitIgnore,
        List<string> extraIgnorePatterns,
        bool useDefaultExcludes,
        bool allowDangerousFiles)
    {
        var ignoreMatchBuilder = new IgnoreMatcherBuilder();
        var ignoreLines = roots.SelectMany(root => LoadIgnoreLines(root, useGitIgnore, extraIgnorePatterns)).ToList();
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

    private static List<string> LoadIgnoreLines(IFileProvider provider, bool useGitIgnore, List<string> extraIgnorePatterns)
    {
        var lines = new List<string>();

        if (useGitIgnore)
        {
            var gitIgnoreFile = provider.GetFileInfo(".gitignore");
            if (gitIgnoreFile.Exists)
            {
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
            }
        }

        lines.AddRange(extraIgnorePatterns);
        return lines;
    }

    private static (IFileProvider Provider, DirectoryInfo Directory, string RootName, string OriginalPath) ResolveDirectory(string path)
    {
        var full = ExpandPath(path);
        var dir = new DirectoryInfo(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return (new PhysicalFileProvider(dir.FullName), dir, dir.Name, full);
    }

    private static FileInfo ResolveOutput(DirectoryInfo root, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return new FileInfo(ExpandPath(outputPath));
        }

        var folderName = string.IsNullOrWhiteSpace(root.Name) ? "project" : root.Name;
        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), $"{folderName}_bundle.md");
        return new FileInfo(ExpandPath(defaultPath));
    }

    private static string ExpandPath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);

        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var rest = path.TrimStart('~').TrimStart('/', '\\');
            path = string.IsNullOrEmpty(rest) ? home : Path.Combine(home, rest);
        }

        return Path.GetFullPath(path);
    }
}
