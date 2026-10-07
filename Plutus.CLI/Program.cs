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
                var maxFileSize = FileSize.Parse(maxSizeStr);
                return await RunAppAsync(projectPath, outputPath, noGitignore, extraIgnores, useDefaultExcludes, allowDangerous, maxFileSize);
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

    private static async Task<int> RunAppAsync(
        List<string> projectPath,
        string? outputPath,
        bool noGitignore,
        List<string> extraIgnores,
        bool useDefaultExcludes,
        bool allowDangerous,
        FileSize maxFileSize)
    {
        using var githubRemoteRepository = new GitHubRemoteRepository();
        using var gitlabRemoteRepository = new GitLabRemoteRepository();
        using var bitbucketRemoteRepository = new BitbucketRemoteRepository();
        IRemoteRepository[] remoteRepositoryProviders = [githubRemoteRepository, gitlabRemoteRepository, bitbucketRemoteRepository];
        var physicianPlutusFileInfo = new PhysicianPlutusFileInfo();
        var projectContextResolver = new ProjectContextResolver(physicianPlutusFileInfo, remoteRepositoryProviders, new FileSystemPathService());
        var details = await projectContextResolver.ResolveContextAsync(projectPath, outputPath);
        if (details is null)
        {
            return 1;
        }

        var (roots, output) = details;
        var additionalIgnoreMatchers = CreateAdditionalIgnoreMatchers(
            roots.ConvertAll(physicianPlutusFileInfo.CreateFileProvider),
            noGitignore,
            extraIgnores).ToList();
        var ignoreMatcherFactory = new IgnoreMatcherFactory(additionalIgnoreMatchers);
        var ignoreMatcher = ignoreMatcherFactory.CreateIgnoreMatcher(useDefaultExcludes, allowDangerous);
        var markdownLanguageProvider = new MarkdownLanguageProvider();
        var app = new PlutusBundler(physicianPlutusFileInfo, ignoreMatcher, markdownLanguageProvider);
        return await app.RunBundleAsync(roots, output, maxFileSize);
    }

    private static IEnumerable<IIgnoreMatcher> CreateAdditionalIgnoreMatchers(List<IFileProvider> roots, bool noGitignore, List<string> extraIgnorePatterns)
    {
        if (!noGitignore)
        {
            yield return GitIgnoreMatcher.CreateFromFolders(roots, extraIgnorePatterns);
        }
        else
        {
            yield return GitIgnoreMatcher.CreateFromLines(extraIgnorePatterns);
        }
    }
}
