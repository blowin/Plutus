using System.Text;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.CLI;

public class App
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

        var roots = projectPaths.Select(ResolveDirectory).ToList();
        var primaryRoot = roots.First();
        var output = ResolveOutput(primaryRoot.Directory, outputPath);

        if (Directory.Exists(output.FullName))
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.FullName}");
            return 1;
        }

        var allEntries = new List<BundleEntry>();

        IIgnoreMatcher ignoreMatcher = CreateIgnoreMatcher(roots.ConvertAll(e => e.Directory), useGitIgnore, extraIgnorePatterns, useDefaultExcludes, allowDangerousFiles);
        foreach (var root in roots)
        {
            var collector = new FileCollector(ignoreMatcher);
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

    private static IIgnoreMatcher CreateIgnoreMatcher(
        List<DirectoryInfo> roots,
        bool useGitIgnore,
        List<string> extraIgnorePatterns,
        bool useDefaultExcludes,
        bool allowDangerousFiles)
    {
        var ignoreMatchBuilder = new IgnoreMatcherBuilder();

        foreach (var root in roots)
        {
            var ignoreLines = LoadIgnoreLines(root, useGitIgnore, extraIgnorePatterns);
            if (ignoreLines.Count > 0)
            {
                ignoreMatchBuilder.Add(new GitIgnoreMatcher(new IgnoreList(ignoreLines)));
            }
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

    private static List<string> LoadIgnoreLines(DirectoryInfo root, bool useGitIgnore, List<string> extraIgnorePatterns)
    {
        var lines = new List<string>();

        if (useGitIgnore)
        {
            var gitIgnorePath = Path.Combine(root.FullName, ".gitignore");
            if (File.Exists(gitIgnorePath))
            {
                try
                {
                    lines.AddRange(File.ReadAllLines(gitIgnorePath, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: failed to read {gitIgnorePath}: {ex.Message}");
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
