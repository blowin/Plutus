using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Plutus.Domain;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class ChangedFileMapperTests
{
    [Fact]
    public void Resolve_WhenRootIsTheRepositoryRoot_ShouldKeepRepositoryRelativePaths()
    {
        var result = Map(RepositoryRoot(), [Root("/repo")], Chunk("src/Core.cs"));

        result.TrackedChunksFor("/repo").Should().ContainSingle().Which.Path.Should().Be("src/Core.cs");
        result.TrackedPathsFor("/repo").Should().Equal("src/Core.cs");
    }

    [Fact]
    public void Resolve_WhenRootIsNested_ShouldStripTheRootPrefix()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], Chunk("src/Core.cs"), Chunk("tests/CoreTests.cs"));

        result.TrackedChunksFor("/repo/src").Should().ContainSingle().Which.Path.Should().Be("Core.cs");
    }

    [Fact]
    public void Resolve_WhenMultipleRootsShareOneRepository_ShouldMapEachRootIndependently()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src"), Root("/repo/tests")], Chunk("src/Core.cs"), Chunk("tests/CoreTests.cs"));

        result.TrackedPathsFor("/repo/src").Should().Equal("Core.cs");
        result.TrackedPathsFor("/repo/tests").Should().Equal("CoreTests.cs");
    }

    [Fact]
    public void Resolve_WhenAChangedFileIsOutsideEveryRoot_ShouldDropIt()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], Chunk("docs/README.md"));

        result.TrackedChunksFor("/repo/src").Should().BeEmpty();
        result.TrackedPathsFor("/repo/src").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldPreserveTheComparisonBase()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], Chunk("src/Core.cs"));

        result.ComparisonBase.Should().Be("basesha");
    }

    [Fact]
    public void Resolve_WhenRootIsNested_ShouldRewritePathsInsideTheDiffText()
    {
        var chunk = new DiffChunk(
            "src/Core.cs",
            "src/Core.cs",
            ChangeKind.Modified,
            "diff --git a/src/Core.cs b/src/Core.cs\n--- a/src/Core.cs\n+++ b/src/Core.cs\n@@ -1 +1 @@\n-old\n+new",
            1,
            1);

        var result = Map(RepositoryRoot(), [Root("/repo/src")], chunk);

        var mapped = result.TrackedChunksFor("/repo/src").Should().ContainSingle().Subject;
        mapped.Text.Should().Contain("diff --git a/Core.cs b/Core.cs");
        mapped.Text.Should().Contain("--- a/Core.cs");
        mapped.Text.Should().Contain("+++ b/Core.cs");
        mapped.Text.Should().NotContain("a/src/Core.cs");
        mapped.Text.Should().Contain("+new");
    }

    [Fact]
    public void Resolve_WhenRootIsTheRepository_ShouldLeaveTheDiffTextUntouched()
    {
        var chunk = new DiffChunk(
            "src/Core.cs",
            "src/Core.cs",
            ChangeKind.Modified,
            "diff --git a/src/Core.cs b/src/Core.cs\n+new",
            1,
            0);

        var result = Map(RepositoryRoot(), [Root("/repo")], chunk);

        result.TrackedChunksFor("/repo").Should().ContainSingle().Which.Text.Should().Be(chunk.Text);
    }

    [Fact]
    public void Resolve_WhenPathCasingDiffersAndCaseIsIgnored_ShouldStillRelativize()
    {
        var result = MapIgnoringCase("/Repo", [Root("/repo/src")], Chunk("SRC/Core.cs"));

        result.TrackedChunksFor("/repo/src").Should().ContainSingle().Which.Path.Should().Be("Core.cs");
    }

    [Fact]
    public void Resolve_WhenPathCasingDiffersAndCaseMatters_ShouldDropTheChunk()
    {
        var result = Map("/Repo", [Root("/repo/src")], Chunk("SRC/Core.cs"));

        result.TrackedChunksFor("/repo/src").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenRootIsUnknownToTheDiff_ShouldProduceAnEmptyGroup()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src"), Root("/repo/other")], Chunk("src/Core.cs"));

        result.TrackedPathsFor("/repo/other").Should().BeEmpty();
        result.FileGroups.Should().HaveCount(2);
    }

    [Fact]
    public void Resolve_WhenRootsOverlap_ShouldReportEachRootView()
    {
        var result = Map(RepositoryRoot(), [Root("/repo"), Root("/repo/src")], Chunk("src/Core.cs"));

        // Each root expresses the same change in its own path space.
        result.TrackedPathsFor("/repo").Should().Equal("src/Core.cs");
        result.TrackedPathsFor("/repo/src").Should().Equal("Core.cs");
    }

    [Fact]
    public void Resolve_WithUntrackedFiles_ShouldMapThemToTheOwningRoot()
    {
        var result = new ChangedFileMapper(RepositoryRoot()).Resolve(
            "basesha",
            [Chunk("src/Core.cs")],
            [Chunk("src/New.cs"), Chunk("tests/NewTests.cs")],
            [Root("/repo/src")]);

        result.UntrackedPathsFor("/repo/src").Should().Equal("New.cs");
        result.UntrackedChunks.Should().ContainSingle().Which.Path.Should().Be("New.cs");
    }

    [Fact]
    public void Resolve_WhenAnUntrackedChunkIsOutsideTheRoot_ShouldDropIt()
    {
        var result = new ChangedFileMapper(RepositoryRoot()).Resolve(
            "basesha",
            [],
            [Chunk("docs/notes.md")],
            [Root("/repo/src")]);

        result.UntrackedChunks.Should().BeEmpty();
        result.UntrackedPathsFor("/repo/src").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WithoutUntrackedChunks_ShouldLeaveTheUntrackedViewEmpty()
    {
        var result = Map(RepositoryRoot(), [Root("/repo")], Chunk("src/Core.cs"));

        result.UntrackedChunks.Should().BeEmpty();
        result.UntrackedPathsFor("/repo").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenRootIsUnknown_ShouldReportNoPathsForThatRoot()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], Chunk("src/Core.cs"));

        result.TrackedPathsFor("/repo/other").Should().BeEmpty();
        result.UntrackedPathsFor("/repo/other").Should().BeEmpty();
    }

    [Theory]
    [InlineData("/repo", true)]
    [InlineData("/repo/", true)]
    [InlineData("/repo/src", true)]
    [InlineData("/repo/src/Deep", true)]
    [InlineData("/other", false)]
    [InlineData("/repo-extra", false)]
    public void IsInsideRepository_ShouldCompareWholePathSegments(string rootPath, bool expected)
    {
        new ChangedFileMapper(RepositoryRoot()).IsInsideRepository(rootPath).Should().Be(expected);
    }

    [Fact]
    public void Resolve_WithNullRoots_ShouldThrow()
    {
        var mapper = new ChangedFileMapper(RepositoryRoot());

        var act = () => mapper.Resolve("basesha", [Chunk("src/Core.cs")], [], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static string RepositoryRoot() => "/repo";

    private static GitDiffContext Map(string repositoryRoot, IReadOnlyList<IFileInfo> roots, params DiffChunk[] chunks) =>
        new ChangedFileMapper(repositoryRoot).Resolve("basesha", chunks, [], roots);

    private static GitDiffContext MapIgnoringCase(string repositoryRoot, IReadOnlyList<IFileInfo> roots, params DiffChunk[] chunks) =>
        new ChangedFileMapper(repositoryRoot, ignorePathCase: true).Resolve("basesha", chunks, [], roots);

    private static DiffChunk Chunk(string repositoryPath) =>
        new(repositoryPath, repositoryPath, ChangeKind.Modified, $"diff --git a/{repositoryPath} b/{repositoryPath}", 1, 0);

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
