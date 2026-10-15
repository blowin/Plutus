using FluentAssertions;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// Keeps the documentation honest and in step with the code: the supported-language list must match the registry,
/// and the text must describe lexical scanning as lexical scanning rather than as grammar or AST work.
/// </summary>
public class CommentStrippingDocumentationTests
{
    [Fact]
    public void CommentStrippingGuide_MentionsEveryCanonicalLanguage()
    {
        var guide = ReadDocument("COMMENT_STRIPPING.md");

        foreach (var definition in TestSourceProcessing.Syntax.Definitions)
        {
            guide.Should().Contain(
                $"`{definition.Language}`",
                "the supported-language table must list {0}",
                definition.Language);
        }
    }

    [Fact]
    public void CommentStrippingGuide_DescribesTheStrategyItActuallyImplements()
    {
        var guide = ReadDocument("COMMENT_STRIPPING.md");

        guide.Should().Contain("lexical scanner");
        guide.Should().Contain("ANTLR token channel");
        guide.Should().Contain("A parser, an AST pass");
        guide.Should().Contain("no token stream exists");
    }

    [Fact]
    public void CommentStrippingGuide_StatesTheDocumentationCommentDefault()
    {
        var guide = ReadDocument("COMMENT_STRIPPING.md");

        guide.Should().Contain("documentation comments are removed like any other comment");
        guide.Should().Contain("--preserve-doc-comments");
    }

    [Fact]
    public void GrammarInventory_RecordsThatNoGrammarIsMaintained()
    {
        var inventory = ReadDocument("ANTLR_GRAMMAR_INVENTORY.md");

        inventory.Should().Contain("Plutus maintains no ANTLR grammar today");
        inventory.Should().Contain("grammar-manifest.json");
        inventory.Should().Contain("commentChannels");
        inventory.Should().Contain("HIDDEN");
    }

    [Fact]
    public void CliReference_DocumentsTheCommentFlags()
    {
        var cli = ReadDocument("CLI.md");

        cli.Should().Contain("--strip-comments");
        cli.Should().Contain("--preserve-doc-comments");
        cli.Should().Contain("--comment-strategy");
    }

    [Fact]
    public void Readme_LinksToTheCommentStrippingGuide()
    {
        var readme = TestFiles.ReadFromRepository("README.md");

        readme.Should().Contain("docs/COMMENT_STRIPPING.md");
    }

    private static string ReadDocument(string fileName) => TestFiles.ReadFromRepository($"docs/{fileName}");
}
