using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;

namespace Plutus.Test.Diffing;

public class DiffScopeLimiterTests
{
    [Fact]
    public void Apply_ShouldEstimateTokensForEveryChunk()
    {
        var result = CreateLimiter().Apply("/repo", "basesha", [Chunk("src/A.cs")], [], [Root("/repo")]);

        result.Context.TrackedChunks.Should().ContainSingle().Which.EstimatedTokens.Should().BeGreaterThan(0);
        result.Summary.Should().BeNull();
    }

    [Fact]
    public void Apply_WhenAFileExceedsTheSizeLimit_ShouldExcludeOnlyThatDiff()
    {
        var sizes = new Dictionary<string, long> { ["src/Small.cs"] = 100, ["src/Large.cs"] = 5000 };

        var result = CreateLimiter(maxFileSize: 1024).Apply(
            "/repo",
            "basesha",
            [Chunk("src/Small.cs"), Chunk("src/Large.cs")],
            [],
            [Root("/repo")],
            path => sizes[path]);

        var kept = result.Context.TrackedChunks.Where(chunk => !chunk.IsSkipped).ToArray();
        var omitted = result.Context.TrackedChunks.Where(chunk => chunk.IsSkipped).ToArray();

        kept.Should().ContainSingle().Which.Path.Should().Be("src/Small.cs");
        omitted.Should().ContainSingle().Which.Path.Should().Be("src/Large.cs");
        omitted[0].SkipReason.Should().Contain("max-file-size");
        result.Excluded.Should().Equal("src/Large.cs");
    }

    [Fact]
    public void Apply_WhenAFileNoLongerExists_ShouldKeepItsDiff()
    {
        var result = CreateLimiter(maxFileSize: 1).Apply(
            "/repo",
            "basesha",
            [Chunk("src/Gone.cs")],
            [],
            [Root("/repo")],
            _ => 0);

        // A deletion has nothing left to weigh, so it must survive a size limit of one byte.
        result.Context.TrackedChunks.Should().ContainSingle().Which.IsSkipped.Should().BeFalse();
    }

    [Fact]
    public void Apply_WithoutAFileSizeResolver_ShouldNotApplyTheSizeLimit()
    {
        var result = CreateLimiter(maxFileSize: 1).Apply("/repo", "basesha", [Chunk("src/A.cs")], [], [Root("/repo")]);

        result.Context.TrackedChunks.Should().ContainSingle().Which.IsSkipped.Should().BeFalse();
        result.Excluded.Should().BeEmpty();
    }

    [Fact]
    public void Apply_WhenTheBudgetFits_ShouldReportTheSummaryWithoutOmissions()
    {
        var result = CreateLimiter(budget: new TokenBudgetOptions(10_000, TokenOverflowPolicy.Select))
            .Apply("/repo", "basesha", [Chunk("src/A.cs")], [], [Root("/repo")]);

        result.Context.TrackedChunks.Should().ContainSingle().Which.IsSkipped.Should().BeFalse();
        result.Summary.Should().NotBeNull();
        result.Summary!.MaxTokens.Should().Be(10_000);
        result.Excluded.Should().BeEmpty();
    }

    [Fact]
    public void Apply_WhenSelectingByLargestFirst_ShouldKeepTheSmallerDiff()
    {
        var budget = new TokenBudgetOptions(60, TokenOverflowPolicy.Select, TokenSelectionStrategy.LargestFirst);

        var result = CreateLimiter(budget: budget).Apply(
            "/repo",
            "basesha",
            [Chunk("src/Small.cs"), Chunk("src/Large.cs", padding: 60)],
            [],
            [Root("/repo")]);

        result.Context.TrackedChunks.Where(chunk => !chunk.IsSkipped).Should().ContainSingle().Which.Path.Should().Be("src/Small.cs");
        result.Excluded.Should().Equal("src/Large.cs");
    }

    [Fact]
    public void Apply_WhenTheBudgetIsExceededUnderFailPolicy_ShouldThrow()
    {
        var limiter = CreateLimiter(budget: new TokenBudgetOptions(1, TokenOverflowPolicy.Fail));

        var act = () => limiter.Apply("/repo", "basesha", [Chunk("src/A.cs")], [], [Root("/repo")]);

        act.Should().Throw<TokenBudgetExceededException>().WithMessage("*exceeds --max-tokens*");
    }

    [Fact]
    public void Apply_ShouldLimitUntrackedChunksToo()
    {
        var result = CreateLimiter(budget: new TokenBudgetOptions(5, TokenOverflowPolicy.Select))
            .Apply("/repo", "basesha", [], [Chunk("src/New.cs", padding: 40)], [Root("/repo")]);

        result.Context.UntrackedChunks.Should().ContainSingle().Which.IsSkipped.Should().BeTrue();
        result.Excluded.Should().Equal("src/New.cs");
    }

    [Fact]
    public void Apply_WhenTheRootIsWrittenWithForwardSlashes_ShouldStillFindItsChunks()
    {
        // git reports the repository root with forward slashes, the file provider with backslashes.
        var limiter = new DiffScopeLimiter(new BundleLimits(FileSize.Parse("1M"), null));

        var result = limiter.Apply(@"D:\repo", "basesha", [Chunk("src/A.cs")], [], [Root("D:/repo")]);

        result.Context.TrackedPathsFor("D:/repo").Should().Equal("src/A.cs");
        result.Context.TrackedPathsFor(@"D:\repo").Should().Equal("src/A.cs");
    }

    [Fact]
    public void Apply_WhenTheRepositoryRootIsNotTheWorkingDirectory_ShouldStillMapChunks()
    {
        // The repository root is a parameter, so bundling another project cannot relativize against the
        // process working directory and silently drop every file.
        var limiter = new DiffScopeLimiter(new BundleLimits(FileSize.Parse("1M"), null));

        var result = limiter.Apply(
            @"D:\elsewhere\other-repo",
            "basesha",
            [Chunk("src/A.cs")],
            [],
            [Root(@"D:\elsewhere\other-repo")]);

        result.Context.TrackedPathsFor(@"D:\elsewhere\other-repo").Should().Equal("src/A.cs");
        result.Excluded.Should().BeEmpty();
    }

    [Fact]
    public void Apply_WithNullRoots_ShouldThrow()
    {
        var act = () => CreateLimiter().Apply("/repo", "basesha", [], [], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static DiffScopeLimiter CreateLimiter(long maxFileSize = long.MaxValue, TokenBudgetOptions? budget = null) =>
        new(new BundleLimits(FileSize.FromBytes(maxFileSize), budget));

    private static DiffChunk Chunk(string path, int padding = 0) =>
        new(path, path, ChangeKind.Modified, $"diff --git a/{path} b/{path}\n-old\n+new{string.Concat(Enumerable.Repeat("\n+pad", padding))}", 1, 1);

    private static IFileInfo Root(string physicalPath)
    {
        var root = Substitute.For<IFileInfo>();
        root.Name.Returns(Path.GetFileName(physicalPath));
        root.PhysicalPath.Returns(physicalPath);
        root.IsDirectory.Returns(true);
        root.Exists.Returns(true);
        return root;
    }
}
