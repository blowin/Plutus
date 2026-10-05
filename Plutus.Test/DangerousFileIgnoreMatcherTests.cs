using FluentAssertions;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Test;

public class DangerousFileIgnoreMatcherTests
{
    private readonly DangerousFileIgnoreMatcher _matcher = new();

    [Theory]
    [InlineData(".env")]
    [InlineData("src/credentials.json")]
    [InlineData("id_rsa")]
    [InlineData("secrets/secret.json")]
    [InlineData("subfolder/.env.production")]
    public void IsIgnoredFile_ShouldReturnTrue_ForDangerousFiles(string path)
    {
        // Act
        var isIgnored = _matcher.IsIgnoredFile(path);

        // Assert
        isIgnored.Should().BeTrue();
    }

    [Theory]
    [InlineData(".env.example")]
    [InlineData(".env.sample")]
    [InlineData(".env.template")]
    [InlineData(".env.dist")]
    [InlineData("src/Program.cs")]
    [InlineData("appsettings.json")]
    public void IsIgnoredFile_ShouldReturnFalse_ForSafeFiles(string path)
    {
        // Act
        var isIgnored = _matcher.IsIgnoredFile(path);

        // Assert
        isIgnored.Should().BeFalse();
    }

    [Fact]
    public void IsIgnoredDirectory_ShouldAlwaysReturnFalse()
    {
        // Act & Assert
        _matcher.IsIgnoredDirectory("any/path/").Should().BeFalse();
    }
}
