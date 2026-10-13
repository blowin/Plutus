using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.CLI;

public class BundleAppRunner(
    IProjectContextResolver contextResolver,
    IFileInfoDetailProvider detailProvider,
    IServiceProvider serviceProvider)
{
    public async Task<int> RunAsync(RunAppOptions options)
    {
        var details = await contextResolver.ResolveContextAsync(options.ProjectPath, options.OutputPath);
        if (details is null) return 1;

        var (roots, output) = details;

        var additionalMatchers = CreateAdditionalIgnoreMatchers(roots.ConvertAll(detailProvider.CreateFileProvider), options).ToList();
        var ignoreMatcherFactory = new IgnoreMatcherFactory(additionalMatchers);
        var ignoreMatcher = ignoreMatcherFactory.CreateIgnoreMatcher(options.UseDefaultExcludes, options.AllowDangerous);

        var app = ActivatorUtilities.CreateInstance<PlutusBundler>(serviceProvider, ignoreMatcher);
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
}

public record RunAppOptions
{
    public required List<string> ProjectPath { get; init; }
    public required string? OutputPath { get; init; }
    public required bool UseGitignore { get; init; }
    public required List<string> ExcludePatterns { get; init; }
    public required bool UseDefaultExcludes { get; init; }
    public required bool AllowDangerous { get; init; }
    public required FileSize MaxFileSize { get; init; }
    public required List<string> ExcludeDirPatterns { get; init; }
    public required List<string> ExcludeFilePatterns { get; init; }
    public required List<string> IncludeDirPatterns { get; init; }
    public required List<string> IncludeFilePatterns { get; init; }
    public bool Minify { get; init; }
}
