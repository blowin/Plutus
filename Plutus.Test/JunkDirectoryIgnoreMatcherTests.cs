using FluentAssertions;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class JunkDirectoryIgnoreMatcherTests
{
    private readonly JunkDirectoryIgnoreMatcher _matcher = new();

    [Theory]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("src/bin")]
    [InlineData("project/.git")]
    [InlineData("test.egg-info")]
    public void IsIgnoredDirectory_ShouldReturnTrue_ForJunkFolders(string path)
    {
        // Act
        var result = _matcher.IsIgnoredDirectory(path);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("src/Core")]
    [InlineData("Plutus.Domain")]
    [InlineData("App/Assets")]
    public void IsIgnoredDirectory_ShouldReturnFalse_ForValidFolders(string path)
    {
        // Act
        var result = _matcher.IsIgnoredDirectory(path);

        // Assert
        result.Should().BeFalse();
    }
}
