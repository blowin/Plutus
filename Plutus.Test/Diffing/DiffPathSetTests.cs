using FluentAssertions;
using Plutus.Domain.Diffing;

namespace Plutus.Test.Diffing;

public class DiffPathSetTests
{
    [Fact]
    public void ContainsFile_ForDiscoveredPath_ShouldBeTrue()
    {
        var set = DiffPathSet.Create(["src/Core.cs"]);

        set.ContainsFile("src/Core.cs").Should().BeTrue();
        set.Count.Should().Be(1);
    }

    [Fact]
    public void ContainsFile_ForUndiscoveredPath_ShouldBeFalse()
    {
        var set = DiffPathSet.Create(["src/Core.cs"]);

        set.ContainsFile("src/Other.cs").Should().BeFalse();
    }

    [Theory]
    [InlineData("./src/Core.cs")]
    [InlineData(@".\src\Core.cs")]
    [InlineData("/src/Core.cs")]
    [InlineData("src//Core.cs")]
    public void ContainsFile_ShouldNormalizeBothSidesBeforeMatching(string queriedPath)
    {
        var set = DiffPathSet.Create([@"\src\Core.cs"]);

        set.ContainsFile(queriedPath).Should().BeTrue();
    }

    [Fact]
    public void ContainsDirectory_ForAncestorOfChangedFile_ShouldBeTrue()
    {
        var set = DiffPathSet.Create(["src/Deep/Nested/Core.cs"]);

        set.ContainsDirectory("src").Should().BeTrue();
        set.ContainsDirectory("src/Deep").Should().BeTrue();
        set.ContainsDirectory("src/Deep/Nested").Should().BeTrue();
    }

    [Fact]
    public void ContainsDirectory_ForUnrelatedDirectory_ShouldBeFalse()
    {
        var set = DiffPathSet.Create(["src/Core.cs"]);

        set.ContainsDirectory("tests").Should().BeFalse();
        set.ContainsDirectory("src/Nested").Should().BeFalse();
    }

    [Fact]
    public void ContainsDirectory_ForRootPath_ShouldAlwaysBeTrue()
    {
        DiffPathSet.Create(["src/Core.cs"]).ContainsDirectory(string.Empty).Should().BeTrue();
        DiffPathSet.Create([]).ContainsDirectory(string.Empty).Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutAnyPaths_ShouldExcludeEveryFile()
    {
        var set = DiffPathSet.Create([]);

        set.Count.Should().Be(0);
        set.ContainsFile("src/Core.cs").Should().BeFalse();
    }

    [Fact]
    public void Create_WithDuplicateAndBlankPaths_ShouldCountEachFileOnce()
    {
        var set = DiffPathSet.Create(["src/Core.cs", "src/Core.cs", "//", "src/Other.cs"]);

        set.Count.Should().Be(2);
    }

    [Fact]
    public void Create_WhenComparerIgnoresCase_ShouldMatchDifferentCasing()
    {
        var set = DiffPathSet.Create(["src/Core.cs"]);

        set.ContainsFile("SRC/core.CS").Should().BeTrue();
    }
}
