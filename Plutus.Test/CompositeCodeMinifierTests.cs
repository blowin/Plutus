using FluentAssertions;
using Plutus.Domain.Minifier;

namespace Plutus.Test;

public class CompositeCodeMinifierTests
{
    [Fact]
    public void Minify_ShouldReturnOriginalContentForUnknownLanguage()
    {
        const string source = "some   arbitrary   text";

        CompositeCodeMinifier.CreateFull().Minify(source, "unknown-language")
            .Should().Be(source);
    }

    [Theory]
    [InlineData("javascript")]
    [InlineData("typescript")]
    [InlineData("tsx")]
    [InlineData("jsx")]
    public void Minify_ShouldSupportJavaScriptAliases(string language)
    {
        const string source = "const value = 1; // remove me";

        var result = CompositeCodeMinifier.CreateFull().Minify(source, language);

        result.Should().NotContain("remove me");
        result.Should().Contain("const");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Minify_ShouldReturnEmptyOrNullInputUnchanged(string? source)
    {
        CompositeCodeMinifier.CreateFull().Minify(source!, "javascript").Should().Be(source);
    }

    [Fact]
    public void Constructor_ShouldRejectDuplicateLanguageNames()
    {
        var first = new StubLanguageMinifier("test");
        var second = new StubLanguageMinifier("TEST");

        var act = () => new CompositeCodeMinifier([first, second]);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class StubLanguageMinifier(string language) : ILanguageMinifier
    {
        public string Language => language;
        public string Description => "test minifier";
        public string Minify(string sourceCode) => sourceCode;
    }
}
