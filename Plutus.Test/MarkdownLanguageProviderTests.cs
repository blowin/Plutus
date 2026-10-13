using FluentAssertions;
using Plutus.Domain;

namespace Plutus.Test;

public class MarkdownLanguageProviderTests
{
    private readonly MarkdownLanguageProvider _provider = new();

    [Theory]
    [InlineData("Program.cs", "csharp")]
    [InlineData("component.TSX", "tsx")]
    [InlineData("settings.JSON", "json")]
    [InlineData("script.sh", "bash")]
    [InlineData("styles.scss", "scss")]
    [InlineData("project.sln", "text")]
    public void GetLanguage_ShouldMapKnownExtensionsCaseInsensitively(string fileName, string expected)
    {
        _provider.GetLanguage(fileName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Dockerfile", "dockerfile")]
    [InlineData("Dockerfile.dev", "dockerfile")]
    [InlineData("Makefile", "makefile")]
    [InlineData("Makefile.local", "makefile")]
    [InlineData("Jenkinsfile", "groovy")]
    [InlineData(".env.example", "dotenv")]
    [InlineData("unknown.extension", "text")]
    [InlineData("README", "text")]
    public void GetLanguage_ShouldHandleSpecialFileNames(string fileName, string expected)
    {
        _provider.GetLanguage(fileName).Should().Be(expected);
    }
}
