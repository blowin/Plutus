using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.BundleWriters;
using Plutus.Domain.Diffing;
using Plutus.Domain.IgnoreMatcher;
using Plutus.Domain.Tokens;

namespace Plutus.Test;

public class BundleAppRunnerTests
{
    private readonly IProjectContextResolver _contextResolver = Substitute.For<IProjectContextResolver>();
    private readonly IFileInfoDetailProvider _detailProvider = Substitute.For<IFileInfoDetailProvider>();
    private readonly IAdditionalIgnoreMatcherProvider _matcherProvider = Substitute.For<IAdditionalIgnoreMatcherProvider>();
    private readonly IGitDiffContextProvider _diffProvider = Substitute.For<IGitDiffContextProvider>();
    private readonly RecordingBundler _bundler = new();
    private readonly IFileInfo _root;
    private readonly IFileInfo _output;

    public BundleAppRunnerTests()
    {
        _root = Substitute.For<IFileInfo>();
        _root.Name.Returns("Root");
        _root.PhysicalPath.Returns("/repo");
        _root.IsDirectory.Returns(true);

        _output = Substitute.For<IFileInfo>();
        _output.Name.Returns("out.md");
        _output.PhysicalPath.Returns("/repo/out.md");

        _contextResolver.ResolveContextAsync(Arg.Any<List<string>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new ProjectResolveResponse([_root], _output));
        _matcherProvider.GetMatchers(Arg.Any<List<IFileProvider>>(), Arg.Any<RunAppOptions>()).Returns([]);
    }

    [Fact]
    public async Task RunAsync_WithoutDiffOptions_ShouldNotAskForADiffContext()
    {
        var result = await CreateRunner().RunAsync(CreateOptions(diff: null));

        result.Should().Be(0);
        await _diffProvider.DidNotReceive().ResolveAsync(Arg.Any<GitDiffOptions>(), Arg.Any<IReadOnlyList<IFileInfo>>(), Arg.Any<CancellationToken>());
        _bundler.Invocations.Should().ContainSingle();
        _bundler.LastDiffContext.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_WhenDiffDiscoveryFails_ShouldStopBeforeBundling()
    {
        _diffProvider.ResolveAsync(Arg.Any<GitDiffOptions>(), Arg.Any<IReadOnlyList<IFileInfo>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DiffScopeResult?>(null));

        var result = await CreateRunner().RunAsync(CreateOptions(new GitDiffOptions()));

        result.Should().Be(1);
        _bundler.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithResolvedDiffScope_ShouldHandTheContextAndItsBudgetToTheBundler()
    {
        var context = CreateContext();
        var summary = new TokenBudgetSummary(100, TokenOverflowPolicy.Select, TokenSelectionStrategy.PathOrder, 120, 90, "estimator");
        var scope = new DiffScopeResult(context, summary, ["src/Large.cs"]);
        _diffProvider.ResolveAsync(Arg.Any<GitDiffOptions>(), Arg.Any<IReadOnlyList<IFileInfo>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DiffScopeResult?>(scope));

        var result = await CreateRunner().RunAsync(CreateOptions(new GitDiffOptions { Target = "main" }));

        result.Should().Be(0);
        await _diffProvider.Received(1).ResolveAsync(
            Arg.Is<GitDiffOptions>(options => options.Target == "main"),
            Arg.Is<IReadOnlyList<IFileInfo>>(roots => roots.Count == 1 && roots[0] == _root),
            Arg.Any<CancellationToken>());
        _bundler.LastDiffContext.Should().BeSameAs(context);
    }

    [Fact]
    public async Task RunAsync_WhenContextCannotBeResolved_ShouldFailWithoutRunningAnything()
    {
        _contextResolver.ResolveContextAsync(Arg.Any<List<string>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(Task.FromResult<ProjectResolveResponse?>(null));

        var result = await CreateRunner().RunAsync(CreateOptions(new GitDiffOptions()));

        result.Should().Be(1);
        await _diffProvider.DidNotReceive().ResolveAsync(Arg.Any<GitDiffOptions>(), Arg.Any<IReadOnlyList<IFileInfo>>(), Arg.Any<CancellationToken>());
        _bundler.Invocations.Should().BeEmpty();
    }

    private BundleAppRunner CreateRunner() =>
        new(_contextResolver, _detailProvider, _bundler, _matcherProvider, _diffProvider);

    private static GitDiffContext CreateContext() =>
        new("basesha")
        {
            FileGroups =
            [
                new DiffFileGroup("/repo", [new DiffChunk("src/Core.cs", "src/Core.cs", ChangeKind.Modified, "patch", 1, 0)], [])
            ]
        };

    private static RunAppOptions CreateOptions(GitDiffOptions? diff) => new()
    {
        ProjectPath = ["."],
        OutputPath = "out.md",
        UseGitignore = true,
        ExcludePatterns = [],
        UseDefaultExcludes = true,
        AllowDangerous = false,
        MaxFileSize = FileSize.FromBytes(1024),
        ExcludeDirPatterns = [],
        ExcludeFilePatterns = [],
        IncludeDirPatterns = [],
        IncludeFilePatterns = [],
        TargetEncoding = System.Text.Encoding.UTF8,
        MaxConcurrency = 1,
        Format = "md",
        Branch = null,
        Commit = null,
        Diff = diff
    };

    /// <summary>Captures the arguments of the bundling call the runner makes, without starting a real scan.</summary>
    private sealed class RecordingBundler : PlutusBundler
    {
        public RecordingBundler()
            : base(Substitute.For<IFileInfoDetailProvider>(), [])
        {
        }

        public List<int> Invocations { get; } = [];

        public GitDiffContext? LastDiffContext { get; private set; }


        public override Task<int> RunBundleAsync(
            ProjectScanner projectScanner,
            IReadOnlyCollection<IFileInfo> roots,
            IFileInfo output,
            RunAppOptions options,
            GitDiffContext? gitDiff = null)
        {
            Invocations.Add(1);
            LastDiffContext = gitDiff;
            return Task.FromResult(0);
        }
    }
}
