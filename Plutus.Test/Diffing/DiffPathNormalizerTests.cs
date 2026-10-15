using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class DiffPathNormalizerTests
{
    [Theory]
    [InlineData("src/Core.cs", "src/Core.cs")]
    [InlineData("/src/Core.cs", "src/Core.cs")]
    [InlineData(@".\src\Core.cs", "src/Core.cs")]
    [InlineData("./src/Core.cs", "src/Core.cs")]
    [InlineData("src/", "src")]
    [InlineData("  src/Core.cs  ", "src/Core.cs")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_ShouldProduceRootRelativeForwardSlashPaths(string? input, string expected)
    {
        DiffPathNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("a/src/Core.cs b/src/Core.cs", "src/Core.cs")]
    [InlineData("a/docs/Release Notes.md b/docs/Release Notes.md", "docs/Release Notes.md")]
    [InlineData("src/Plain.cs src/Plain.cs", "src/Plain.cs")]
    public void ParseDiffHeaderDestination_ShouldReturnTheDestinationPath(string header, string expected)
    {
        DiffPathNormalizer.ParseDiffHeaderDestination(header).Should().Be(expected);
    }

    [Fact]
    public void ParseDiffHeaderDestination_WhenHeaderHasNoDestination_ShouldReturnNull()
    {
        DiffPathNormalizer.ParseDiffHeaderDestination("a/src/Core.cs").Should().BeNull();
    }
}
