using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class VcsPathNormalizerTests
{
    [Theory]
    [InlineData("/repo", "src/Core.cs", "/repo/src/Core.cs")]
    [InlineData("/repo", "/src/Core.cs", "/repo/src/Core.cs")]
    [InlineData("/repo/", "src/Core.cs", "/repo/src/Core.cs")]
    [InlineData(@"/repo\", @"src\Core.cs", "/repo/src/Core.cs")]
    [InlineData(@"C:\work\repo", @"src\Core.cs", "C:/work/repo/src/Core.cs")]
    public void Anchor_ShouldJoinRootAndPathWithForwardSlashes(string repositoryRoot, string path, string expected)
    {
        VcsPathNormalizer.Anchor(repositoryRoot, path).Should().Be(expected);
    }

    [Theory]
    [InlineData("/repo", "/repo/")]
    [InlineData("/repo/", "/repo/")]
    [InlineData(@"/repo\", "/repo/")]
    [InlineData("/", "/")]
    public void RootPrefix_ShouldEndWithExactlyOneSeparator(string root, string expected)
    {
        VcsPathNormalizer.RootPrefix(root).Should().Be(expected);
    }

    [Fact]
    public void Anchor_ShouldKeepTheLeadingSeparatorOfAnAbsoluteRoot()
    {
        var rootPrefix = VcsPathNormalizer.RootPrefix("/repo");

        rootPrefix.Should().StartWith("/");
        VcsPathNormalizer.Anchor("/repo", "src/Core.cs").Should().StartWith(rootPrefix);
    }

    [Fact]
    public void Anchor_WithAPathBesideTheRoot_ShouldNotMatchTheRootPrefix()
    {
        // "/other/repo/..." must not be treated as living under "/repo".
        var anchored = VcsPathNormalizer.Anchor("/other", "repo/Core.cs");

        anchored.Should().NotStartWith(VcsPathNormalizer.RootPrefix("/repo"));
    }
}
