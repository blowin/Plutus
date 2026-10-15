using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class GitDiffParserTests
{
    [Fact]
    public void ParseNullSeparatedPaths_ShouldSplitEveryEntryAndDropTheTrailingTerminator()
    {
        var paths = GitDiffParser.ParseNullSeparatedPaths("src/New.cs\0docs/Release Notes.md\0assets/logo.png\0");

        paths.Should().Equal("src/New.cs", "docs/Release Notes.md", "assets/logo.png");
    }

    [Fact]
    public void ParseNullSeparatedPaths_WithEmptyOutput_ShouldReturnNoPaths()
    {
        GitDiffParser.ParseNullSeparatedPaths(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ParseNullSeparatedPaths_ShouldNormalizeWindowsSeparators()
    {
        GitDiffParser.ParseNullSeparatedPaths("src\\New.cs\0").Should().Equal("src/New.cs");
    }

    [Fact]
    public void ParseNullSeparatedPaths_WithNullArgument_ShouldThrow()
    {
        var act = () => GitDiffParser.ParseNullSeparatedPaths(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
