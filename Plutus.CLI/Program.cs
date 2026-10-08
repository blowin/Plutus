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
        using var githubRemoteRepository = new GitHubRemoteRepository();
        using var gitlabRemoteRepository = new GitLabRemoteRepository();
        using var bitbucketRemoteRepository = new BitbucketRemoteRepository();
        IRemoteRepository[] remoteRepositoryProviders = [githubRemoteRepository, gitlabRemoteRepository, bitbucketRemoteRepository];
        var physicianPlutusFileInfo = new PhysicianPlutusFileInfo();
        var projectContextResolver = new ProjectContextResolver(physicianPlutusFileInfo, remoteRepositoryProviders, new FileSystemPathService());
        var details = await projectContextResolver.ResolveContextAsync(options.ProjectPath, options.OutputPath);
        if (details is null)
        {
            return 1;
        }

        var (roots, output) = details;
        var additionalIgnoreMatchers = CreateAdditionalIgnoreMatchers(
            roots.ConvertAll(physicianPlutusFileInfo.CreateFileProvider),
            options).ToList();
        var ignoreMatcherFactory = new IgnoreMatcherFactory(additionalIgnoreMatchers);
        var ignoreMatcher = ignoreMatcherFactory.CreateIgnoreMatcher(options.UseDefaultExcludes, options.AllowDangerous);
        var markdownLanguageProvider = new MarkdownLanguageProvider();
        var app = new PlutusBundler(physicianPlutusFileInfo, ignoreMatcher, markdownLanguageProvider);
        return await app.RunBundleAsync(roots, output, options.MaxFileSize);
    }

    private static IEnumerable<IIgnoreMatcher> CreateAdditionalIgnoreMatchers(List<IFileProvider> roots, RunAppOptions options)
    {
        if (options.UseGitignore)
        {
            yield return GitIgnoreMatcher.FromFolders(roots, options.ExcludePatterns);
        }
        else
        {
            yield return GitIgnoreMatcher.FromLines(options.ExcludePatterns);
        }

        if (options.ExcludeFilePatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromExcludeFiles(options.ExcludeFilePatterns);
        }

        if (options.ExcludeDirPatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromExcludeDirs(options.ExcludeDirPatterns);
        }

        if (options.IncludeFilePatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromIncludeFiles(options.IncludeFilePatterns);
        }

        if (options.IncludeDirPatterns.Count > 0)
        {
            yield return GitIgnoreMatcher.FromIncludeDirs(options.IncludeDirPatterns);
        }
    }

    private record RunAppOptions
    {
        public required List<string> ProjectPath { get; init; }
        public required string? OutputPath { get; init; }
        public required bool UseGitignore { get; init; }
        public required List<string> ExcludePatterns { get; init; }
        public required bool UseDefaultExcludes { get; init; }
        public required bool AllowDangerous { get; init; }
        public required FileSize MaxFileSize { get; init; }
        public required List<string> ExcludeDirPatterns { get; set; }
        public required List<string> ExcludeFilePatterns { get; set; }
        public required List<string> IncludeDirPatterns { get; set; }
        public required List<string> IncludeFilePatterns { get; set; }
    }
}
