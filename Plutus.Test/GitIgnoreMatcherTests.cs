using FluentAssertions;
using MAB.DotIgnore;
using Plutus.Infrastructure.IgnoreMatcher;

namespace Plutus.Test;

public class GitIgnoreMatcherTests
{
    [Theory]
    [InlineData("frontend/", "frontend", true)]
    [InlineData("frontend/", "src/frontend", true)]
    [InlineData("frontend/", "src/sub/frontend", true)]
    [InlineData("frontend", "src/frontend", true)]
    [InlineData("/frontend/", "frontend", true)]
    [InlineData("/frontend/", "src/frontend", false)]
    [InlineData("*/frontend", "src/frontend", true)]
    public void IsIgnoredDirectory_ShouldMatchNestedDirectories(string pattern, string path, bool expected)
    {
        // Arrange
        var ignoreList = new IgnoreList(new[] { pattern });
        var matcher = new GitIgnoreMatcher(ignoreList);

        // Act
        var result = matcher.IsIgnoredDirectory(path);

        // Assert
        result.Should().Be(expected);
    }
}
