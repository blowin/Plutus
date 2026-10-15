using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;

namespace Plutus.Test;

public class CompositeCodeMinifierTests
{
    [Fact]
    public void Minify_ShouldReturnOriginalContentForUnknownLanguage()
    {
        const string source = "some   arbitrary   text";

        Full().Minify(source, "unknown-language")
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

        var result = Full().Minify(source, language);

        result.Should().NotContain("remove me");
        result.Should().Contain("const");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Minify_ShouldReturnEmptyOrNullInputUnchanged(string? source)
    {
        Full().Minify(source!, "javascript").Should().Be(source);
    }

    [Fact]
    public void Constructor_ShouldRejectDuplicateLanguageNames()
    {
        var first = new StubLanguageMinifier(LanguageId.Parse("test"));
        var second = new StubLanguageMinifier(LanguageId.Parse("TEST"));

        var act = () => new CompositeCodeMinifier([first, second]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SupportedLanguages_ShouldCoverTheSyntaxRegistryExactly()
    {
        var catalog = Full();

        catalog.SupportedLanguages.Should().BeEquivalentTo(TestSourceProcessing.Syntax.Languages);
    }

    [Fact]
    public void Composite_IsBuiltFromTheLanguagesEachProcessorDeclares()
    {
        // One processor serving several languages must dispatch by the language it was called with, not by its
        // first language: SCSS allows '//' while CSS does not.
        var catalog = Full();

        catalog.Minify("// comment\nbody {}", LanguageId.Scss).Should().NotContain("// comment");
        catalog.Minify("// comment\nbody {}", LanguageId.Css).Should().Contain("// comment");
    }

    /// <summary>The full pipeline, built the way a host builds it: maintained processors plus the generic ones.</summary>
    private static CompositeCodeMinifier Full() =>
        new(TestSourceProcessing.Minifiers(SourceProcessingContext.Default));

    private sealed class StubLanguageMinifier(LanguageId language) : ILanguageMinifier
    {
        public IReadOnlyCollection<LanguageId> Languages => [language];

        public string Description => "test minifier";

        public string Minify(string sourceCode) => sourceCode;

        public string Minify(string sourceCode, LanguageId requested) => sourceCode;
    }
}
