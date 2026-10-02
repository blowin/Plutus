using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MAB.DotIgnore;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.CLI;

/// <summary>
/// Project bundler: collects source files into a single Markdown document.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var options = Options.Parse(args);
        if (options is null)
        {
            return 1;
        }

        var root = ResolveDirectory(options.ProjectPath);
        var output = ResolveOutput(root, options.OutputPath);
        if (Directory.Exists(output.FullName))
        {
            Console.Error.WriteLine($"Error: Output path is a directory: {output.FullName}");
            return 1;
        }

        IIgnoreMatcher ignoreList = CreateIgnoreMatcher(root, options);
        var fileService = new FileService();
        var collector = new FileCollector(fileService, ignoreList);
        var entries = collector.CollectFiles(root, output);
        var bundleWriter = new BundleWriter(fileService, ignoreList);
        await bundleWriter.WriteAsync(root, output, entries, options.MaxFileSize).ConfigureAwait(false);

        Console.WriteLine($"Bundle created: {output.FullName}");
        Console.WriteLine($"Files included: {entries.Count}");
        Console.WriteLine($"Total size: {entries.Sum(e => e.Size).HumanSize()}");
        return 0;
    }

    private static IIgnoreMatcher CreateIgnoreMatcher(DirectoryInfo root, Options options)
    {
        var ignoreMatchBuilder = new IgnoreMatcherBuilder();

        var ignoreLines = LoadIgnoreLines(root, options);
        if (ignoreLines.Count > 0)
        {
            ignoreMatchBuilder.Add(new GitIgnoreMatcher(new IgnoreList(ignoreLines)));
        }

        if (options.UseDefaultExcludes)
        {
            ignoreMatchBuilder.IgnoreJunk();
        }

        if (!options.AllowDangerousFiles)
        {
            ignoreMatchBuilder.IgnoreDangerousFile();
        }

        return ignoreMatchBuilder.Build();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Plutus — Project Bundler

            Collects source files into a single Markdown document for analysis or sharing.

            Usage:
              plutus <project-path> [options]

            Options:
              -o, --output <path>             Output bundle file. Default: <folder-name>_bundle.md
              --max-file-size <size>          Max file size to include (e.g., 512K, 1M, 2MB). Default: 1M
              --no-gitignore                  Do not read root .gitignore
              --ignore <pattern>              Additional gitignore-style pattern (can be repeated)
              --no-default-excludes           Disable built-in junk exclusions
              --allow-dangerous-files         Include files normally treated as dangerous (.env, keys, etc.)
              -h, --help                      Show this help

            Examples:
              plutus C:\projects\MyApp
              plutus /home/user/projects/MyApp -o /tmp/myapp_bundle.md
              plutus . --max-file-size 5M
              plutus . --ignore "vendor/" --ignore "*.min.js"
            """);
    }

    private static string RequireNext(string[] args, ref int index, string optionName)
    {
        index++;
        if (index >= args.Length)
        {
            throw new ArgumentException($"Option {optionName} requires a value.");
        }
        return args[index];
    }

    private static DirectoryInfo ResolveDirectory(string path)
    {
        var full = ExpandPath(path);
        var dir = new DirectoryInfo(full);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Project directory not found: {full}");
        }

        return dir;
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

    private static List<string> LoadIgnoreLines(DirectoryInfo root, Options options)
    {
        var lines = new List<string>();

        if (options.UseGitIgnore)
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

        lines.AddRange(options.ExtraIgnorePatterns);
        return lines;
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

    private sealed record Options(
        string ProjectPath,
        string? OutputPath,
        long MaxFileSize,
        bool UseGitIgnore,
        List<string> ExtraIgnorePatterns,
        bool UseDefaultExcludes,
        bool AllowDangerousFiles)
    {
        public static Options? Parse(string[] args)
        {
            var projectPath = "D:\\Program\\projects\\C#\\Plutus";
            string? outputPath = null;
            var maxFileSize = ParseSize("1M");
            var useGitIgnore = true;
            var useDefaultExcludes = true;
            var allowDangerousFiles = false;
            var extraIgnorePatterns = new List<string>();

            var positionalUsed = false;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return null;

                    case "-o":
                    case "--output":
                        outputPath = RequireNext(args, ref i, arg);
                        break;

                    case "--max-file-size":
                        maxFileSize = ParseSize(RequireNext(args, ref i, arg));
                        break;

                    case "--no-gitignore":
                        useGitIgnore = false;
                        break;

                    case "--ignore":
                        extraIgnorePatterns.Add(RequireNext(args, ref i, arg));
                        break;

                    case "--no-default-excludes":
                        useDefaultExcludes = false;
                        break;

                    case "--allow-dangerous-files":
                        allowDangerousFiles = true;
                        break;

                    default:
                        if (arg.StartsWith("-", StringComparison.Ordinal))
                        {
                            Console.Error.WriteLine($"Error: Unknown option: {arg}");
                            return null;
                        }

                        if (positionalUsed)
                        {
                            Console.Error.WriteLine($"Error: Unexpected positional argument: {arg}");
                            return null;
                        }

                        projectPath = arg;
                        positionalUsed = true;
                        break;
                }
            }

            return new Options(
                projectPath,
                outputPath,
                maxFileSize,
                useGitIgnore,
                extraIgnorePatterns,
                useDefaultExcludes,
                allowDangerousFiles);
        }
    }
}
