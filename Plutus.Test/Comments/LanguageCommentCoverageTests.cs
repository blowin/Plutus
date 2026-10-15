using FluentAssertions;
using Plutus.Domain.Comments;
using Plutus.Domain.Minifier;

namespace Plutus.Test.Comments;

/// <summary>
/// Enforces that the language matrix is real: every language the registry claims is exercised here, and every
/// entry proves both halves of the contract — its own comment form is removed, and its literal form is left
/// byte-identical. Adding a language without a sample fails the test rather than shipping an untested claim.
/// </summary>
public class LanguageCommentCoverageTests
{
    /// <summary>One language: the line that must disappear and the line that must survive untouched.</summary>
    private static readonly (string Language, string CommentLine, string LiteralLine)[] Samples =
    [
        ("csharp", "// COMMENT_MARKER", "var s = \"LITERAL_KEEP // COMMENT_MARKER\";"),
        ("java", "// COMMENT_MARKER", "String s = \"LITERAL_KEEP /* COMMENT_MARKER */\";"),
        ("kotlin", "// COMMENT_MARKER", "val s = \"\"\"LITERAL_KEEP // COMMENT_MARKER\"\"\""),
        ("scala", "// COMMENT_MARKER", "val s = \"\"\"LITERAL_KEEP /* COMMENT_MARKER */\"\"\""),
        ("groovy", "// COMMENT_MARKER", "def s = '''LITERAL_KEEP // COMMENT_MARKER'''"),
        ("javascript", "// COMMENT_MARKER", "const s = `LITERAL_KEEP ${'// COMMENT_MARKER'}`;"),
        ("typescript", "// COMMENT_MARKER", "const s: string = \"LITERAL_KEEP // COMMENT_MARKER\";"),
        ("jsx", "// COMMENT_MARKER", "const s = 'LITERAL_KEEP /* COMMENT_MARKER */';"),
        ("tsx", "// COMMENT_MARKER", "const s = /LITERAL_KEEP\\/\\/COMMENT_MARKER/;"),
        ("go", "// COMMENT_MARKER", "var s = `LITERAL_KEEP // COMMENT_MARKER`"),
        ("rust", "// COMMENT_MARKER", "let s = r#\"LITERAL_KEEP // COMMENT_MARKER\"#;"),
        ("swift", "// COMMENT_MARKER", "let s = \"LITERAL_KEEP // COMMENT_MARKER\""),
        ("dart", "// COMMENT_MARKER", "var s = r'LITERAL_KEEP // COMMENT_MARKER';"),
        ("c", "// COMMENT_MARKER", "char *s = \"LITERAL_KEEP // COMMENT_MARKER\";"),
        ("cpp", "// COMMENT_MARKER", "auto s = \"LITERAL_KEEP /* COMMENT_MARKER */\";"),
        ("fsharp", "// COMMENT_MARKER", "let s = @\"LITERAL_KEEP // COMMENT_MARKER\""),
        ("vb", "' COMMENT_MARKER", "Dim s = \"LITERAL_KEEP ' COMMENT_MARKER\""),
        ("html", "<!-- COMMENT_MARKER -->", "<div data-x=\"LITERAL_KEEP <!-- COMMENT_MARKER -->\"></div>"),
        ("xml", "<!-- COMMENT_MARKER -->", "<node attr=\"LITERAL_KEEP <!-- COMMENT_MARKER -->\" />"),
        ("css", "/* COMMENT_MARKER */", "content: \"LITERAL_KEEP /* COMMENT_MARKER */\";"),
        ("scss", "// COMMENT_MARKER", "content: \"LITERAL_KEEP // COMMENT_MARKER\";"),
        ("sass", "// COMMENT_MARKER", "content: 'LITERAL_KEEP // COMMENT_MARKER'"),
        ("less", "// COMMENT_MARKER", "content: \"LITERAL_KEEP /* COMMENT_MARKER */\";"),
        ("json", "// COMMENT_MARKER", "{ \"k\": \"LITERAL_KEEP // COMMENT_MARKER\" }"),
        ("jsonc", "// COMMENT_MARKER", "{ \"k\": \"LITERAL_KEEP /* COMMENT_MARKER */\" }"),
        ("sql", "-- COMMENT_MARKER", "SELECT 'LITERAL_KEEP -- COMMENT_MARKER';"),
        ("toml", "# COMMENT_MARKER", "key = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("ini", "; COMMENT_MARKER", "key = \"LITERAL_KEEP ; COMMENT_MARKER\""),
        ("dockerfile", "# COMMENT_MARKER", "ENV X=\"LITERAL_KEEP # COMMENT_MARKER\""),
        ("makefile", "# COMMENT_MARKER", "X = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("dotenv", "# COMMENT_MARKER", "KEY=\"LITERAL_KEEP # COMMENT_MARKER\""),
        ("hcl", "# COMMENT_MARKER", "x = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("tf", "# COMMENT_MARKER", "x = \"LITERAL_KEEP // COMMENT_MARKER\""),
        ("graphql", "# COMMENT_MARKER", "field: \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("protobuf", "// COMMENT_MARKER", "option x = \"LITERAL_KEEP // COMMENT_MARKER\";"),
        ("python", "# COMMENT_MARKER", "s = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("py", "# COMMENT_MARKER", "s = r'LITERAL_KEEP # COMMENT_MARKER'"),
        ("bash", "# COMMENT_MARKER", "s=\"LITERAL_KEEP # COMMENT_MARKER\""),
        ("sh", "# COMMENT_MARKER", "s='LITERAL_KEEP # COMMENT_MARKER'"),
        ("zsh", "# COMMENT_MARKER", "s=\"LITERAL_KEEP // COMMENT_MARKER\""),
        ("powershell", "# COMMENT_MARKER", "$s = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("ps1", "# COMMENT_MARKER", "$s = 'LITERAL_KEEP /* COMMENT_MARKER */'"),
        ("ruby", "# COMMENT_MARKER", "s = \"LITERAL_KEEP # COMMENT_MARKER\""),
        ("php", "// COMMENT_MARKER", "$s = \"LITERAL_KEEP // COMMENT_MARKER\";"),
    ];

    public static TheoryData<string, string, string> SampleData()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (language, commentLine, literalLine) in Samples)
        {
            data.Add(language, commentLine, literalLine);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SampleData))]
    public void StripLanguageSample_RemovesItsCommentAndKeepsItsLiteral(string language, string commentLine, string literalLine)
    {
        var source = commentLine + "\n" + literalLine + "\n";

        var result = LexicalCommentStripper.Instance.Strip(source, language, CommentStripOptions.Default);

        result.LanguageSupported.Should().BeTrue($"{language} is advertised as supported");

        // Exact equality is the strongest form of both claims at once: the comment line is gone, and the literal
        // line — including any comment marker inside it — came through byte for byte.
        result.Content.Should().Be("\n" + literalLine + "\n", "{0} must remove its comment and keep its literal", language);
        result.RemovedComments.Should().Be(1, "{0} has exactly one comment in this sample", language);
    }

    [Theory]
    [MemberData(nameof(SampleData))]
    public void StripLanguageSample_StrippingIsIdempotent(string language, string commentLine, string literalLine)
    {
        var source = commentLine + "\n" + literalLine + "\n";

        var first = LexicalCommentStripper.Instance.Strip(source, language, CommentStripOptions.Default);
        var second = LexicalCommentStripper.Instance.Strip(first.Content, language, CommentStripOptions.Default);

        second.RemovedComments.Should().Be(0);
        second.Content.Should().Be(first.Content);
    }

    [Fact]
    public void Registry_EveryCanonicalLanguageHasASample()
    {
        var sampled = Samples.Select(sample => sample.Language).ToHashSet(StringComparer.OrdinalIgnoreCase);

        LanguageCommentSyntaxRegistry.Definitions
            .Select(definition => definition.Language)
            .Should().BeSubsetOf(sampled);
    }

    [Fact]
    public void Registry_EveryDefinitionDeclaresAtLeastOneCommentForm()
    {
        foreach (var syntax in LanguageCommentSyntaxRegistry.Definitions)
        {
            (syntax.LineComments.Count > 0 || syntax.BlockComments.Count > 0)
                .Should().BeTrue($"{syntax.Language} must declare a comment form or stop claiming support");
        }
    }

    [Fact]
    public void Registry_SupportedLanguagesAreOfferedByTheCatalog()
    {
        var catalog = CompositeCodeMinifier.CreateFull().SupportedMinifiers
            .Select(minifier => minifier.Language)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        LanguageCommentSyntaxRegistry.Languages.Should().BeSubsetOf(catalog);
    }

    [Fact]
    public void Registry_LanguagesWithoutSyntaxAreNotOfferedByTheCatalog()
    {
        var catalog = CompositeCodeMinifier.CreateFull();

        catalog.SupportedMinifiers.Should().NotContain(minifier => minifier.Language == "yaml");
        catalog.Minify("value # text", "yaml").Should().Be("value # text");
    }

    [Fact]
    public void Registry_UnknownLanguageIsReportedAsUnsupported()
    {
        LexicalCommentStripper.Supports("lua").Should().BeFalse();
        LexicalCommentStripper.Supports("csharp").Should().BeTrue();
        LexicalCommentStripper.Supports("CSHARP").Should().BeTrue();
    }
}
