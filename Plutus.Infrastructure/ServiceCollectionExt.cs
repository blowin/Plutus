using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Domain.Minifier;
using Plutus.Domain.Minifier.Languages;
using Plutus.Domain.Structure;
using Plutus.Domain.Tokens.Applier;
using Plutus.Infrastructure.Diagnostics;
using Plutus.Infrastructure.Git;
using Plutus.Infrastructure.RemoteRepository;
using Plutus.Infrastructure.Structure;

namespace Plutus.Infrastructure;

public static class ServiceCollectionExt
{
    /// <summary>
    /// Registers the services of a run. <paramref name="runOutput"/> is the single target every service reports to;
    /// when it is omitted the run is silent, which is what a library caller or a test usually wants.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        RunAppOptions options,
        IRunOutput? runOutput = null)
    {
        var output = runOutput ?? NullRunOutput.Instance;
        services.AddSingleton(output);
        services.AddSingleton<BundleRunReport>();
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
        services.AddSingleton<IAdditionalIgnoreMatcherProvider>(_ => new AdditionalIgnoreMatcherProvider(output));
        services.AddSingleton<IGitCommandExecutor, ProcessGitCommandExecutor>();

        // One limits service per payload: the bundle both carries source files and, in diff mode, diffs.
        // The repository root is resolved per bundle by the provider, so it is passed to Apply rather than
        // captured here.
        services.AddScoped(_ => new BundleLimits(options.MaxFileSize, options.TokenBudget, output));
        services.AddScoped(_ => new DiffScopeLimiter(
            new BundleLimits(options.MaxFileSize, options.Diff?.TokenBudget, output)));
        services.AddScoped<IGitDiffContextProvider>(provider => options.Diff is null
            ? new NullGitDiffContextProvider()
            : ActivatorUtilities.CreateInstance<GitDiffContextProvider>(provider));
        services.AddSingleton(options);
        services.AddSingleton(options.TargetEncoding);

        // Source processing. The comment strategy was resolved by the CLI against the grammars the project
        // actually maintains, so this composition only has to honor it; the reports it writes are printed after the
        // run, which is what makes the selected strategy and the structural pass observable.
        services.AddSingleton(_ => new CommentStrippingReport(
            options.CommentStripPlan ?? DisabledCommentPlan));
        services.AddSingleton<ICommentStripObserver>(provider => provider.GetRequiredService<CommentStrippingReport>());
        services.AddSingleton<SignatureExtractionReport>();
        services.AddSingleton<ISignatureExtractionObserver>(provider => provider.GetRequiredService<SignatureExtractionReport>());
        services.AddSingleton(new GrammarInventoryLoader().Discover());

        // Discovery, not a dispatch table: each implementation declares the language it serves, and the container
        // registers it. Adding a structural provider or a maintained language processor is a matter of writing the
        // class — and because providers live in this assembly, the domain stays free of any parser.
        services.Scan(scan =>
        {
            scan.FromAssemblyOf<CSharpSignatureSkeletonProvider>()
                .AddClasses(classes => classes.AssignableTo<ISignatureSkeletonProvider>())
                .As<ISignatureSkeletonProvider>()
                .WithSingletonLifetime();

            // A grammar-backed lexer would be registered by the same rule; today the scan finds none.
            scan.FromAssemblyOf<CSharpSignatureSkeletonProvider>()
                .AddClasses(classes => classes.AssignableTo<ICommentChannelLexer>())
                .As<ICommentChannelLexer>()
                .WithSingletonLifetime();

            scan.FromAssemblyOf<CSharpLanguageMinifier>()
                .AddClasses(classes => classes.AssignableTo<IMaintainedLanguageProcessor>())
                .As<ILanguageMinifier>()
                .WithSingletonLifetime();
        });

        services.AddSingleton<IReadOnlyDictionary<LanguageId, ICommentChannelLexer>>(provider => provider
            .GetServices<ICommentChannelLexer>()
            .ToDictionary(lexer => lexer.Language));

        services.AddSingleton<ISignatureSkeletonExtractor>(provider => new SignatureSkeletonExtractor(
            provider.GetServices<ISignatureSkeletonProvider>(),
            provider.GetRequiredService<ISignatureExtractionObserver>()));

        // Source processing is composed from services, not from static helpers: the syntax table is a singleton, the
        // factory is a singleton that reads the options, and the context it builds is shared by every processor.
        services.AddSingleton<ILanguageCommentSyntaxTable, LanguageCommentSyntaxTable>();
        services.AddSingleton<LanguageMinifierCatalog>();
        services.AddSingleton<CodeMinifierFactory>();
        services.AddSingleton(provider => provider.GetRequiredService<CodeMinifierFactory>().Context);
        services.AddSingleton(provider => provider.GetRequiredService<CodeMinifierFactory>().Create());

        services.AddSingleton<IRunReportWriter, RunReportWriter>();

        return services;
    }

    /// <summary>The plan recorded when neither <c>--strip-comments</c> nor <c>--minify</c> was requested.</summary>
    private static readonly CommentStrippingPlan DisabledCommentPlan = new()
    {
        Request = CommentStrategyRequest.Auto,
        DefaultStrategy = CommentStripStrategy.None,
        Rationale = "comment processing is disabled",
    };
}
