using System.CommandLine;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Infrastructure;

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

        var excludeOption = new Option<List<string>>("--exclude")
        {
            Description = "Global gitignore-style wildcard patterns to exclude BOTH files and directories. Can be repeated",
            AllowMultipleArgumentsPerToken = true
        };
        excludeOption.Aliases.Add("-e");

        var excludeFilesOption = new Option<List<string>>("--exclude-files")
        {
            Description = "Gitignore-style wildcard patterns targeting strictly FILES for exclusion. Useful for ignoring specific file names or extensions across the entire project graph",
            AllowMultipleArgumentsPerToken = true
        };

        var excludeDirsOption = new Option<List<string>>("--exclude-dirs")
        {
            Description = "Gitignore-style wildcard patterns targeting strictly DIRECTORIES for exclusion. Matches the target directory name or absolute tree path at any nesting level",
            AllowMultipleArgumentsPerToken = true
        };

        var includeFilesOption = new Option<List<string>>("--include-files")
        {
            Description = "Inverted gitignore-style filters. Explicitly isolates and restricts processing strictly to FILES that match these patterns. When active, all other unmatched files are omitted by default",
            AllowMultipleArgumentsPerToken = true
        };

        var includeDirsOption = new Option<List<string>>("--include-dirs")
        {
            Description = "Inverted gitignore-style filters. Explicitly restricts scanning paths strictly to DIRECTORIES that match these patterns. Allows you to whitelist and isolate processing to specific module trees",
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

        var minifyOption = new Option<bool>("--minify")
        {
            Description = "Minify source code (remove comments and extra whitespace) for supported languages before bundling."
        };
        minifyOption.Aliases.Add("-m");

        var encodingOption = new Option<string>("--encoding")
        {
            Description = "Target text encoding for reading, writing, and logging (e.g., utf-8, windows-1251, koi8-r).",
            DefaultValueFactory = _ => "utf-8"
        };

        var rootCommand = new RootCommand("Plutus — Project Bundler. Collects source files into a single Markdown document.")
        {
            projectPathsArgument,
            outputOption,
            maxSizeOption,
            noGitignoreOption,
            excludeOption,
            excludeFilesOption,
            excludeDirsOption,
            includeFilesOption,
            includeDirsOption,
            useDefaultExcludesOption,
            allowDangerousFilesOption,
            minifyOption,
            encodingOption,
        };

        rootCommand.SetAction(async parseResult =>
        {
            var projectPath = parseResult.GetValue(projectPathsArgument)!;
            var outputPath = parseResult.GetValue(outputOption);
            var maxSizeStr = parseResult.GetValue(maxSizeOption)!;
            var noGitignore = parseResult.GetValue(noGitignoreOption);
            var excludePatterns = parseResult.GetValue(excludeOption) ?? new List<string>();
            var excludeDirPatterns = parseResult.GetValue(excludeDirsOption) ?? new List<string>();
            var excludeFilePatterns = parseResult.GetValue(excludeFilesOption) ?? new List<string>();
            var includeDirPatterns = parseResult.GetValue(includeDirsOption) ?? new List<string>();
            var includeFilePatterns = parseResult.GetValue(includeFilesOption) ?? new List<string>();
            var useDefaultExcludes = parseResult.GetValue(useDefaultExcludesOption);
            var allowDangerous = parseResult.GetValue(allowDangerousFilesOption);
            var minify = parseResult.GetValue(minifyOption);
            var encodingName = parseResult.GetValue(encodingOption)!;

            Encoding targetEncoding;
            try
            {
                targetEncoding = Encoding.GetEncoding(encodingName);
            }
            catch (ArgumentException)
            {
                Console.Error.WriteLine($"Error: Unsupported encoding name '{encodingName}'. Examples: utf-8, windows-1251.");
                return 1;
            }

            try
            {
                var maxFileSize = FileSize.Parse(maxSizeStr);
                var options = new RunAppOptions
                {
                    ProjectPath = projectPath,
                    OutputPath = outputPath,
                    UseGitignore = !noGitignore,
                    ExcludePatterns = excludePatterns,
                    UseDefaultExcludes = useDefaultExcludes,
                    AllowDangerous = allowDangerous,
                    MaxFileSize = maxFileSize,
                    ExcludeDirPatterns = excludeDirPatterns,
                    ExcludeFilePatterns = excludeFilePatterns,
                    IncludeDirPatterns = includeDirPatterns,
                    IncludeFilePatterns = includeFilePatterns,
                    Minify = minify,
                    TargetEncoding = targetEncoding,
                };

                return await RunAppAsync(options);
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

    private static async Task<int> RunAppAsync(RunAppOptions options)
    {
        Console.OutputEncoding = options.TargetEncoding;
        Console.InputEncoding = options.TargetEncoding;

        var sc = new ServiceCollection();
        sc.AddInfrastructureServices(options);
        await using var serviceProvider = sc.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using var scope = serviceProvider.CreateAsyncScope();

        var runner = scope.ServiceProvider.GetRequiredService<BundleAppRunner>();
        return await runner.RunAsync(options);
    }
}
