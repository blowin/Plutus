using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Minifier;
using Plutus.Infrastructure;
using Plutus.Infrastructure.Diagnostics;

namespace Plutus.Test;

/// <summary>
/// The composition root is part of the behavior: services are built by the container, so a service that cannot be
/// resolved — or a scope mismatch — has to fail here rather than in a run.
/// </summary>
public class ServiceCollectionTests
{
    [Fact]
    public void Container_BuildsTheServicesOfARun()
    {
        using var scope = CreateScope();

        scope.ServiceProvider.GetRequiredService<PlutusBundler>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICodeMinifier>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SourceProcessingContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ILanguageCommentSyntaxTable>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<LanguageMinifierCatalog>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<GrammarInventory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IRunReportWriter>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IRunOutput>().Should().BeOfType<RecordingRunOutput>();
    }

    [Fact]
    public void Container_SeesTheCommentStrategyTheHostResolved()
    {
        var plan = new CommentStrippingPlan
        {
            Request = CommentStrategyRequest.Auto,
            DefaultStrategy = CommentStripStrategy.Lexical,
            Rationale = "lexical scanning",
        };

        using var scope = CreateScope(plan);
        var context = scope.ServiceProvider.GetRequiredService<SourceProcessingContext>();

        context.Strippers.Should().NotBeNull();
        context.Mode.Should().Be(SourceProcessingMode.None);

        // The strategy the CLI resolved is the one the pipeline reports, not a default re-derived by the container.
        scope.ServiceProvider.GetRequiredService<CommentStrippingReport>().Plan.Should().BeSameAs(plan);
    }

    private static IServiceScope CreateScope(CommentStrippingPlan? plan = null)
    {
        var options = new RunAppOptions
        {
            ProjectPath = ["."],
            OutputPath = null,
            UseGitignore = true,
            ExcludePatterns = [],
            UseDefaultExcludes = true,
            AllowDangerous = false,
            MaxFileSize = FileSize.Parse("1M"),
            ExcludeDirPatterns = [],
            ExcludeFilePatterns = [],
            IncludeDirPatterns = [],
            IncludeFilePatterns = [],
            TargetEncoding = Encoding.UTF8,
            MaxConcurrency = 1,
            Format = "md",
            Branch = null,
            Commit = null,
            CommentStripPlan = plan,
        };

        var services = new ServiceCollection();
        services.AddInfrastructureServices(options, new RecordingRunOutput());

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        return provider.CreateScope();
    }
}
