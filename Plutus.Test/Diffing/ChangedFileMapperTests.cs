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
        var result = Map(RepositoryRoot(), [Root("/repo")], File("src/Core.cs"));

        result.Files.Should().ContainSingle().Which.Path.Should().Be("src/Core.cs");
        result.PathsFor("/repo").Should().Equal("src/Core.cs");
    }

    [Fact]
    public void Resolve_WhenRootIsNested_ShouldStripTheRootPrefix()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], File("src/Core.cs"), File("tests/CoreTests.cs"));

        result.Files.Should().ContainSingle().Which.Path.Should().Be("Core.cs");
        result.PathsFor("/repo/src").Should().Equal("Core.cs");
    }

    [Fact]
    public void Resolve_WhenMultipleRootsShareOneRepository_ShouldMapEachRootIndependently()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src"), Root("/repo/tests")], File("src/Core.cs"), File("tests/CoreTests.cs"));

        result.PathsFor("/repo/src").Should().Equal("Core.cs");
        result.PathsFor("/repo/tests").Should().Equal("CoreTests.cs");
        result.Files.Should().HaveCount(2);
    }

    [Fact]
    public void Resolve_WhenAChangedFileIsOutsideEveryRoot_ShouldDropIt()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src")], File("docs/README.md"));

        result.Files.Should().BeEmpty();
        result.PathsFor("/repo/src").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldPreserveTheRawDiffAndComparisonBase()
    {
        var diff = CreateContext(File("src/Core.cs")) with { RawText = "RAW" };

        var result = new ChangedFileMapper(RepositoryRoot()).Resolve(diff, [Root("/repo/src")]);

        result.RawText.Should().Be("RAW");
        result.ComparisonBase.Should().Be("basesha");
    }

    [Fact]
    public void Resolve_WhenPathCasingDiffersAndCaseIsIgnored_ShouldStillRelativize()
    {
        var result = MapIgnoringCase("/Repo", [Root("/repo/src")], File("SRC/Core.cs"));

        result.Files.Should().ContainSingle().Which.Path.Should().Be("Core.cs");
    }

    [Fact]
    public void Resolve_WhenPathCasingDiffersAndCaseMatters_ShouldDropTheFile()
    {
        var result = Map("/Repo", [Root("/repo/src")], File("SRC/Core.cs"));

        result.Files.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenRootIsUnknownToTheDiff_ShouldProduceAnEmptyGroup()
    {
        var result = Map(RepositoryRoot(), [Root("/repo/src"), Root("/repo/other")], File("src/Core.cs"));

        result.PathsFor("/repo/other").Should().BeEmpty();
        result.FileGroups.Should().HaveCount(2);
    }

    [Fact]
    public void Resolve_WhenRootsOverlap_ShouldReportEachRootView()
    {
        var result = Map(RepositoryRoot(), [Root("/repo"), Root("/repo/src")], File("src/Core.cs"));

        // Each root expresses the same change in its own path space; the flat list is their union.
        result.PathsFor("/repo").Should().Equal("src/Core.cs");
        result.PathsFor("/repo/src").Should().Equal("Core.cs");
        result.FileGroups.Should().HaveCount(2);
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
    public void Resolve_WithNullDiff_ShouldThrow()
    {
        var mapper = new ChangedFileMapper(RepositoryRoot());

        var act = () => mapper.Resolve(null!, [Root("/repo")]);

        act.Should().Throw<ArgumentNullException>();
    }

    private static string RepositoryRoot() => "/repo";

    private static GitDiffContext Map(string repositoryRoot, IReadOnlyList<IFileInfo> roots, params ChangedFile[] files) =>
        new ChangedFileMapper(repositoryRoot).Resolve(CreateContext(files), roots);

    private static GitDiffContext MapIgnoringCase(string repositoryRoot, IReadOnlyList<IFileInfo> roots, params ChangedFile[] files) =>
        new ChangedFileMapper(repositoryRoot, ignorePathCase: true).Resolve(CreateContext(files), roots);

    private static GitDiffContext CreateContext(params ChangedFile[] files) =>
        new("basesha", files, string.Empty);

    private static ChangedFile File(string path) => new(path, ChangeKind.Modified);

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
