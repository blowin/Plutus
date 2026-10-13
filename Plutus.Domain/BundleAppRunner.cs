using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class BundleAppRunner(
    IProjectContextResolver contextResolver,
    IFileInfoDetailProvider detailProvider,
    PlutusBundler bundler,
    IAdditionalIgnoreMatcherProvider additionalIgnoreMatcherProvider)
{
    public async Task<int> RunAsync(RunAppOptions options)
    {
        var details = await contextResolver.ResolveContextAsync(options.ProjectPath, options.OutputPath, options.Format, options.Branch, options.Commit);
        if (details is null)
        {
            return 1;
        }

        var (roots, output) = details;

        var additionalMatchers = additionalIgnoreMatcherProvider.GetMatchers(roots.ConvertAll(detailProvider.CreateFileProvider), options).ToList();
        var ignoreMatcherFactory = new IgnoreMatcherFactory(additionalMatchers);
        var ignoreMatcher = ignoreMatcherFactory.CreateIgnoreMatcher(options.UseDefaultExcludes, options.AllowDangerous);

        var projectScanner = new ProjectScanner(detailProvider, ignoreMatcher);
        return await bundler.RunBundleAsync(projectScanner, roots, output, options);
    }
}
