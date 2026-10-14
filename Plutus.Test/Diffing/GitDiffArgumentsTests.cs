using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class GitDiffArgumentsTests
{
    [Fact]
    public void Build_WhenNoTargetIsConfigured_ShouldCompareWorkingTreeWithHead()
    {
        var arguments = GitDiffArguments.Build(new GitDiffOptions(), comparisonBase: null);

        arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames");
    }

    [Fact]
    public void Build_WhenTargetIsConfigured_ShouldCompareTheMergeBaseWithTheWorkingTree()
    {
        var arguments = GitDiffArguments.Build(new GitDiffOptions { Target = "main" }, comparisonBase: "abc123");

        // "--merge-base" keeps the destination at the working tree, so uncommitted work is part of the review.
        arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames", "--merge-base", "abc123");
    }

    [Fact]
    public void Build_WhenComparisonBaseIsMissing_ShouldFallBackToTheRawTarget()
    {
        var arguments = GitDiffArguments.Build(new GitDiffOptions { Target = "origin/main" }, comparisonBase: null);

        arguments.Should().Equal("diff", "--no-color", "--no-ext-diff", "--no-renames", "--merge-base", "origin/main");
    }

    [Fact]
    public void Build_WhenTargetIsBlank_ShouldBehaveLikeNoTarget()
    {
        var arguments = GitDiffArguments.Build(new GitDiffOptions { Target = "   " }, comparisonBase: "abc123");

        arguments.Should().NotContain("--merge-base");
    }

    [Fact]
    public void ListUntrackedFiles_ShouldAskGitForNullSeparatedNonIgnoredFiles()
    {
        GitDiffArguments.ListUntrackedFiles.Should().Equal("ls-files", "-z", "--others", "--exclude-standard");
    }
}
