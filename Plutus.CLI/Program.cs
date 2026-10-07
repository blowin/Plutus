using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MAB.DotIgnore;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure;
using Plutus.Infrastructure.IgnoreMatcher;
using Plutus.Infrastructure.RemoteRepository;

namespace Plutus.CLI;

/// <summary>
/// Project bundler: collects source files into a single Markdown document.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var projectPathsArgument = new Argument<List<string>>("project-path")
        {
            Description = "Path to the target project directory or GitHub repository URL.",
            Arity = ArgumentArity.OneOrMore,
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Output bundle file. Default: <folder-name>_bundle.md"
        };
        outputOption.Aliases.Add("-o");

        var maxSizeOption = new Option<string>("--max-file-size")
        {
            Description = "Max file size to include (e.g., 512K, 1M, 2MB).",
            DefaultValueFactory = _ => "1M"
        };

        var noGitignoreOption = new Option<bool>("--no-gitignore")
        {
            Description = "Do not read root .gitignore"
        };

        var ignoreOption = new Option<List<string>>("--ignore")
        {
            Description = "Additional gitignore-style pattern (can be repeated).",
            AllowMultipleArgumentsPerToken = true
        };

        var useDefaultExcludesOption = new Option<bool>("--use-default-excludes")
        {
            Description = "Use default built-in junk exclusions.",
            DefaultValueFactory = _ => true
        };

        var allowDangerousFilesOption = new Option<bool>("--allow-dangerous-files")
        {
            Description = "Include files normally treated as dangerous (.env, keys, etc.)."
        };

        var rootCommand = new RootCommand("Plutus — Project Bundler. Collects source files into a single Markdown document.")
        {
            projectPathsArgument,
            outputOption,
            maxSizeOption,
            noGitignoreOption,
            ignoreOption,
            useDefaultExcludesOption,
            allowDangerousFilesOption,
        };

        rootCommand.SetAction(async parseResult =>
        {
            var projectPath = parseResult.GetValue(projectPathsArgument)!;
            var outputPath = parseResult.GetValue(outputOption);
            var maxSizeStr = parseResult.GetValue(maxSizeOption)!;
            var noGitignore = parseResult.GetValue(noGitignoreOption);
            var extraIgnores = parseResult.GetValue(ignoreOption) ?? new List<string>();
            var useDefaultExcludes = parseResult.GetValue(useDefaultExcludesOption);
            var allowDangerous = parseResult.GetValue(allowDangerousFilesOption);

            try
            {
                var maxFileSize = ParseSize(maxSizeStr);
                using var githubRemoteRepository = new GitHubRemoteRepository();
                using var gitlabRemoteRepository = new GitLabRemoteRepository();
                using var bitbucketRemoteRepository = new BitbucketRemoteRepository();
                IRemoteRepository[] remoteRepositoryProviders = [githubRemoteRepository, gitlabRemoteRepository, bitbucketRemoteRepository];
                var physicianPlutusFileInfo = new PhysicianPlutusFileInfo();
                var details = await GetProcessDetail(physicianPlutusFileInfo, remoteRepositoryProviders, projectPath, outputPath);
                if (details is null)
                {
                    return 1;
                }

                var (roots, output) = details.Value;
                var ignoreMatcher = CreateIgnoreMatcher(
                    roots.ConvertAll(physicianPlutusFileInfo.CreateFileProvider),
                    !noGitignore,
                    extraIgnores,
                    useDefaultExcludes,
                    allowDangerous);
                var app = new PlutusBundler(physicianPlutusFileInfo, ignoreMatcher);
                return await app.RunBundleAsync(roots, output, maxFileSize);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        var result = rootCommand.Parse(args);
        return await Task.FromResult(await result.InvokeAsync()).ConfigureAwait(false);
    }

    private static long ParseSize(string value)
    {
        var raw = value.Trim().ToUpperInvariant();
        var match = Regex.Match(raw, "^([0-9]+(?:\\.[0-9]+)?)\\s*(B|KB|MB|GB|TB|K|M|G|T)?$");

        if (!match.Success)
        {
            throw new ArgumentException($"Invalid size: '{value}'. Examples: 512K, 1M, 2MB, 1024.");
        }

        var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value;

        if (string.IsNullOrEmpty(unit))
        {
            unit = "B";
        }

        var multiplier = unit switch
        {
            "B" => 1L,
            "KB" or "K" => 1024L,
            "MB" or "M" => 1024L * 1024L,
            "GB" or "G" => 1024L * 1024L * 1024L,
            "TB" or "T" => 1024L * 1024L * 1024L * 1024L,
            _ => throw new ArgumentException($"Unsupported size unit: {unit}")
        };

        var size = (long)(number * multiplier);

        if (size < 0)
        {
            throw new ArgumentException("Size cannot be negative.");
        }

        return size;
    }

    private static IIgnoreMatcher CreateIgnoreMatcher(
        List<IFileProvider> roots,
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

    private static async Task<(List<IFileInfo> Roots, IFileInfo Output)?> GetProcessDetail(
        IFileInfoDetailProvider provider,
        IReadOnlyCollection<IRemoteRepository> remoteRepositories,
        List<string>? projectPaths,
        string? outputPath)
    {
        if (projectPaths == null || projectPaths.Count == 0)
        {
            Console.Error.WriteLine("Error: No project paths specified.");
            return null;
        }

        var processedPaths = await GetPathForProcessAsync(remoteRepositories, projectPaths);
        var roots = processedPaths?.Select(e => ResolveDirectory(provider, e)).ToList();
        if (roots is null)
        {
            return null;
        }

        var primaryRoot = roots.First();
        var normalizeOutputPath = NormalizeOutputPath(remoteRepositories, projectPaths, outputPath);
        var output = ResolveOutput(provider, primaryRoot, normalizeOutputPath);

        if (output.IsDirectory)
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.PhysicalPath ?? output.Name}");
            return null;
        }

        return (roots, output);
    }

    private static async Task<List<string>?> GetPathForProcessAsync(IReadOnlyCollection<IRemoteRepository> remoteRepositories, List<string> projectPaths)
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
            foreach (var remoteRepositoryProvider in remoteRepositories)
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

    private static string? NormalizeOutputPath(IReadOnlyCollection<IRemoteRepository> remoteRepositories, List<string> projectPaths, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return outputPath;
        }

        var firstProjectPath = projectPaths.First();
        return remoteRepositories
            .Where(r => r.IsSupportedPath(firstProjectPath))
            .Select(r => r.NormalizeOutputPath(firstProjectPath))
            .FirstOrDefault();
    }

    private static IFileInfo ResolveDirectory(IFileInfoDetailProvider provider, string path)
    {
        var full = path.GetExpandPath();
        var dir = provider.CreateDirectory(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return dir;
    }

    private static IFileInfo ResolveOutput(IFileInfoDetailProvider provider, IFileInfo root, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return provider.CreateFile(outputPath.GetExpandPath());
        }

        var folderName = string.IsNullOrWhiteSpace(root.Name) ? "project" : root.Name;
        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), $"{folderName}_bundle.md");
        return provider.CreateFile(defaultPath.GetExpandPath());
    }
}
