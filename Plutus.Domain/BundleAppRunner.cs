using System.Threading;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class BundleAppRunner(
    IProjectContextResolver contextResolver,
    IFileInfoDetailProvider detailProvider,
    PlutusBundler bundler,
    IAdditionalIgnoreMatcherProvider additionalIgnoreMatcherProvider,
    IGitDiffContextProvider gitDiffContextProvider)
{
    public async Task<int> RunAsync(RunAppOptions options, CancellationToken cancellationToken = default)
    {
        var details = await contextResolver.ResolveContextAsync(options.ProjectPath, options.OutputPath, options.Format, options.Branch, options.Commit, cancellationToken);
        if (details is null)
        {
            return 1;
        }

        var (roots, output, explicitFiles) = details;
        if (explicitFiles is { Count: > 0 })
        {
            options = options with { ExplicitInitialFiles = explicitFiles };
        }

        cancellationToken.ThrowIfCancellationRequested();

        DiffScopeResult? diffScope = null;
        if (options.Diff is { } diffOptions)
        {
            diffScope = await gitDiffContextProvider.ResolveAsync(diffOptions, roots, cancellationToken);
            if (diffScope is null)
            {
                return 1;
            }
        }

        var additionalMatchers = additionalIgnoreMatcherProvider.GetMatchers(roots.ConvertAll(detailProvider.CreateFileProvider), options).ToList();
        var ignoreMatcherFactory = new IgnoreMatcherFactory(additionalMatchers);
        var ignoreMatcher = ignoreMatcherFactory.CreateIgnoreMatcher(options.UseDefaultExcludes, options.AllowDangerous);

        var projectScanner = new ProjectScanner(detailProvider, ignoreMatcher);
        return await bundler.RunBundleAsync(projectScanner, roots, output, options, diffScope?.Context, cancellationToken);
    }
}
