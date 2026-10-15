using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens.Applier;
using Plutus.Infrastructure.Git;
using Plutus.Infrastructure.RemoteRepository;

namespace Plutus.Infrastructure;

public static class ServiceCollectionExt
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, RunAppOptions options)
    {
        services.AddScoped<HttpClient>(_ => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            DefaultRequestHeaders = { UserAgent = { System.Net.Http.Headers.ProductInfoHeaderValue.Parse("Mozilla/5.0...") } }
        });

        services.Scan(scan =>
        {
            scan.FromAssemblyOf<GitLabRemoteRepository>()
                .AddClasses(classes => classes.AssignableTo<IRemoteRepository>())
                .AsImplementedInterfaces()
                .WithScopedLifetime();

            scan.FromAssemblyOf<MarkdownBundleWriter>()
                .AddClasses(classes => classes.AssignableTo<IBundleWriter>())
                .AsImplementedInterfaces()
                .WithScopedLifetime();
        });

        services.AddScoped<IReadOnlyCollection<IRemoteRepository>>(sp => sp.GetRequiredService<IEnumerable<IRemoteRepository>>().ToList());

        services.AddScoped<BundleAppRunner>();
        services.AddScoped<PlutusBundler>();
        services.AddScoped<IReadOnlyCollection<IBundleWriter>>(e =>
        {
            var res = e.GetRequiredService<IEnumerable<IBundleWriter>>().ToList();
            foreach (IBundleWriter bundleWriter in res)
            {
                bundleWriter.MaxConcurrentFileReads = options.MaxConcurrency;
            }

            return res;
        });
        services.AddScoped<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IFileInfoDetailProvider, PhysicianPlutusFileInfo>();
        services.AddSingleton<IPathService, FileSystemPathService>();
        services.AddSingleton<IMarkdownLanguageProvider, MarkdownLanguageProvider>();
        services.AddSingleton<IAdditionalIgnoreMatcherProvider, AdditionalIgnoreMatcherProvider>();
        services.AddSingleton<IGitCommandExecutor, ProcessGitCommandExecutor>();
        services.AddScoped<BundleLimits>(_ => new BundleLimits(options.MaxFileSize, options.Diff?.TokenBudget));
        services.AddScoped<DiffScopeLimiter>(provider => new DiffScopeLimiter(
            new ChangedFileMapper(Directory.GetCurrentDirectory()),
            provider.GetRequiredService<BundleLimits>()));
        services.AddScoped<IGitDiffContextProvider>(provider => options.Diff is null
            ? new NullGitDiffContextProvider()
            : ActivatorUtilities.CreateInstance<GitDiffContextProvider>(provider));
        services.AddSingleton(options);
        services.AddSingleton<ITokenBudgetApplier>(_ => options.TokenBudget is null
            ? new NoOpTokenBudgetApplier()
            : new TokenBudgetApplier(options.TokenBudget));
        services.AddSingleton(options.TargetEncoding);

        services.AddSingleton<ICodeMinifier>(provider =>
        {
            var op = provider.GetRequiredService<RunAppOptions>();
            return op.Minify ? CompositeCodeMinifier.CreateFull() : IdentityCodeMinifier.Instance;
        });

        return services;
    }
}
