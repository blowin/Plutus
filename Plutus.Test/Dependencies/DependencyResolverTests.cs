using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain.Dependencies;
using Plutus.Domain.Diagnostics;

namespace Plutus.Test.Dependencies;

public class DependencyResolverTests
{
    [Fact]
    public async Task Resolve_DeterministicAndTracksReasons()
    {
        var analyzer = Substitute.For<IDependencyAnalyzer>();
        analyzer.CanAnalyze(Arg.Any<string>()).Returns(true);

        analyzer.AnalyzeAsync("A.csproj", Arg.Any<IFileProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DependencyAnalysisResult(
                [new DiscoveredDependency("B.csproj", "B.csproj", "ProjectRef")],
                [])));

        analyzer.AnalyzeAsync("B.csproj", Arg.Any<IFileProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DependencyAnalysisResult([], [])));

        var output = Substitute.For<IRunOutput>();
        var resolver = new DependencyResolver([analyzer], output);

        var result = await resolver.ResolveAsync(["A.csproj"], Substitute.For<IFileProvider>(), CancellationToken.None);

        result.Should().HaveCount(2);
        var depFile = result.Single(f => f.VirtualPath == "B.csproj");
        depFile.IsDirect.Should().BeFalse();
        depFile.InclusionReasons.Should().ContainSingle("A.csproj -> ProjectRef");
    }

    [Fact]
    public async Task Resolve_UnresolvedReference_LogsWarningAndSkips()
    {
        var analyzer = Substitute.For<IDependencyAnalyzer>();
        analyzer.CanAnalyze(Arg.Any<string>()).Returns(true);
        analyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<IFileProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DependencyAnalysisResult(
                [new DiscoveredDependency("Missing.csproj", null, "ProjectRef")],
                [])));

        var output = Substitute.For<IRunOutput>();
        var resolver = new DependencyResolver([analyzer], output);

        var result = await resolver.ResolveAsync(["A.csproj"], Substitute.For<IFileProvider>(), CancellationToken.None);

        result.Should().HaveCount(1);
        output.Received().Warning(Arg.Is<string>(s => s.Contains("Unresolved reference")));
    }
}
