using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Plutus.Infrastructure;
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
                var app = new App(
                    [githubRemoteRepository, gitlabRemoteRepository, bitbucketRemoteRepository],
                    new PhysicianPlutusFileInfo());
                return await app.RunBundleAsync(
                    projectPath,
                    outputPath,
                    maxFileSize,
                    !noGitignore,
                    extraIgnores,
                    useDefaultExcludes,
                    allowDangerous);
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
}
