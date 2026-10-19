using System.Text;
using FluentAssertions;
using NSubstitute;
using Plutus.Domain.BundleWriters.CacheReadProviders;
using Plutus.Domain.Dependencies;
using Plutus.Domain.Diagnostics;
using Plutus.Infrastructure.Dependencies.Csharp;
using Plutus.Test.Fakes;

namespace Plutus.Test.Dependencies;

public sealed class DependencyCyclicIntegrationTests
{
    [Fact]
    public async Task ResolveAsync_WithDeepCyclicDependencies_ResolvesSuccessfullyWithoutStackOverflow()
    {
        // Arrange
        // We set up a deep loop circular dependency graph:
        // Alpha.cs uses Beta namespace
        // Beta.cs uses Gamma namespace
        // Gamma.cs uses Alpha namespace
        var provider = new InMemoryFileProvider()
            .AddFile("src/Alpha.cs", """
                                    using Project.ModuleB;
                                    namespace Project.ModuleA;
                                    public class AlphaClass {}
                                    """)
            .AddFile("src/Beta.cs", """
                                    using Project.ModuleC;
                                    namespace Project.ModuleB;
                                    public class BetaClass {}
                                    """)
            .AddFile("src/Gamma.cs", """
                                    using Project.ModuleA;
                                    namespace Project.ModuleC;
                                    public class GammaClass {}
                                    """);

        // Set up the actual infrastructure pipeline engine
        var encoding = Encoding.UTF8;
        var cacheReadProvider = new CacheReadProvider(encoding);
        var roslynTreeCache = new RoslynTreeCache(cacheReadProvider);
        var normalizer = new UsingNormalizer();
        var index = new CSharpNamespaceIndex(roslynTreeCache, normalizer);

        var analyzer = new CSharpSourceAnalyzer(index, roslynTreeCache, normalizer);
        var runOutput = Substitute.For<IRunOutput>();

        // Target system under test
        var resolver = new DependencyResolver(new[] { analyzer }, runOutput);

        // Act
        // We kick off resolution starting only from Alpha.cs as the explicit root file
        var act = async () => await resolver.ResolveAsync(
            new[] { "src/Alpha.cs" },
            provider,
            CancellationToken.None);

        // Assert
        // 1. Ensure it completes in a timely fashion without hitting a StackOverflowException loop
        var result = await act.Should().NotThrowAsync();

        // 2. The resolver must collect all 3 files since they are completely bound together
        result.Subject.Should().HaveCount(3);

        // 3. Confirm target path evaluations and file-level entry classifications
        var alphaEntry = result.Subject.Single(f => f.VirtualPath == "src/Alpha.cs");
        var betaEntry = result.Subject.Single(f => f.VirtualPath == "src/Beta.cs");
        var gammaEntry = result.Subject.Single(f => f.VirtualPath == "src/Gamma.cs");

        alphaEntry.IsDirect.Should().BeTrue("Alpha was our starting root bundle entry file pointer selection");
        betaEntry.IsDirect.Should().BeFalse("Beta was discovered downstream through Alpha's usings code layout");
        gammaEntry.IsDirect.Should().BeFalse("Gamma was discovered downstream through Beta's usings code layout");

        // 4. Validate that inclusion chains tracking reasons remain clear and deterministic
        betaEntry.InclusionReasons.Should().ContainSingle()
            .Which.Should().Be("src/Alpha.cs -> C# using directive 'Project.ModuleB'");

        gammaEntry.InclusionReasons.Should().ContainSingle()
            .Which.Should().Be("src/Beta.cs -> C# using directive 'Project.ModuleC'");

        alphaEntry.InclusionReasons.Should().ContainSingle()
            .Which.Should().Be("src/Gamma.cs -> C# using directive 'Project.ModuleA'");
    }
}

