using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Domain.Minifier;
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
        });

        services.AddScoped<IReadOnlyCollection<IRemoteRepository>>(sp => sp.GetRequiredService<IEnumerable<IRemoteRepository>>().ToList());

        services.AddScoped<BundleAppRunner>();
        services.AddScoped<PlutusBundler>();
        services.AddScoped<BundleWriter>(e =>
        {
            var writer = ActivatorUtilities.CreateInstance<BundleWriter>(e);
            writer.MaxConcurrentFileReads = options.MaxConcurrency;
            return writer;
        });
        services.AddScoped<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IFileInfoDetailProvider, PhysicianPlutusFileInfo>();
        services.AddSingleton<IPathService, FileSystemPathService>();
        services.AddSingleton<IMarkdownLanguageProvider, MarkdownLanguageProvider>();
        services.AddSingleton<IAdditionalIgnoreMatcherProvider, AdditionalIgnoreMatcherProvider>();
        services.AddSingleton(options);
        services.AddSingleton(options.TargetEncoding);

        services.AddSingleton<ICodeMinifier>(provider =>
        {
            var op = provider.GetRequiredService<RunAppOptions>();
            return op.Minify ? CompositeCodeMinifier.CreateFull() : IdentityCodeMinifier.Instance;
        });

        return services;
    }
}
