using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.Minifier;
using Plutus.Infrastructure.RemoteRepository;

namespace Plutus.Infrastructure;

public static class ServiceCollectionExt
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, bool minify)
    {
        services.AddScoped<HttpClient>(_ => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            DefaultRequestHeaders = { UserAgent = { System.Net.Http.Headers.ProductInfoHeaderValue.Parse("Mozilla/5.0...") } }
        });

        services.AddScoped<IRemoteRepository, GitHubRemoteRepository>();
        services.AddScoped<IRemoteRepository, GitLabRemoteRepository>();
        services.AddScoped<IRemoteRepository, BitbucketRemoteRepository>();
        services.AddScoped<IReadOnlyCollection<IRemoteRepository>>(sp => sp.GetRequiredService<IEnumerable<IRemoteRepository>>().ToList());

        services.AddScoped<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IFileInfoDetailProvider, PhysicianPlutusFileInfo>();
        services.AddSingleton<IPathService, FileSystemPathService>();
        services.AddSingleton<IMarkdownLanguageProvider, MarkdownLanguageProvider>();

        services.AddSingleton<ICodeMinifier>(minify ? CompositeCodeMinifier.CreateFull() : IdentityCodeMinifier.Instance);

        return services;
    }
}
