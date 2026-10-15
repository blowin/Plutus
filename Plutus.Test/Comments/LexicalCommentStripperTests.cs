using FluentAssertions;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// The guarantees the lexical scanner makes, stated as tests: comment-like text inside literals survives, removing
/// a comment never merges tokens, and line endings do not move.
/// </summary>
public class LexicalCommentStripperTests
{
    private static CommentStripResult Strip(string source, string language = "csharp", CommentStripOptions? options = null) =>
        new LexicalCommentStripper(TestSourceProcessing.Syntax).Strip(source, language, options ?? CommentStripOptions.Default);

    private static string StripText(string source, string language = "csharp", CommentStripOptions? options = null) =>
        Strip(source, language, options).Content;

    // ---- token separation ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("a/* comment */b", "a b")]
    [InlineData("a/*1*//*2*/b", "a b")]
    [InlineData("var x=1/*c*/+2;", "var x=1 +2;")]
    [InlineData("x/*c*/.y", "x .y")]
    [InlineData("/*c*/a", "a")]
    [InlineData("a/*c*/;", "a ;")]
    public void Strip_BlockCommentBetweenTokens_InsertsOneSpaceInsteadOfMergingThem(string source, string expected)
    {
        StripText(source).Should().Be(expected);
    }

    [Fact]
    public void Strip_BlockCommentSurroundedByWhitespace_DoesNotAddASeparator()
    {
        StripText("a /* c */ b").Should().Be("a  b");
    }

    [Fact]
    public void Strip_LineComment_KeepsTheTerminatorAndNeedsNoSeparator()
    {
        StripText("a// comment\nb").Should().Be("a\nb");
    }

    [Fact]
    public void Strip_CommentOnlyLine_LeavesAnEmptyLineBehind()
    {
        StripText("// comment\ncode();").Should().Be("\ncode();");
    }

    // ---- line endings -------------------------------------------------------------------------------------

    [Fact]
    public void Strip_CrLfLineComment_KeepsTheCrLfTerminator()
    {
        StripText("a; // c\r\nb;").Should().Be("a; \r\nb;");
    }

    [Fact]
    public void Strip_LoneCrLineComment_KeepsTheCrTerminator()
    {
        StripText("a; // c\rb;").Should().Be("a; \rb;");
    }

    [Fact]
    public void Strip_BlockCommentSpanningLines_ReEmitsEveryLineBreak()
    {
        StripText("a /* first\r\nsecond\nthird */ b").Should().Be("a \r\n\n b");
    }

    [Fact]
    public void Strip_LfBlockComment_KeepsTheLineCountStable()
    {
        const string source = "one();\n/* two\nthree\nfour */\nfive();\n";

        var result = StripText(source);

        result.Split('\n').Length.Should().Be(source.Split('\n').Length);
        result.Should().Be("one();\n\n\n\nfive();\n");
    }

    // ---- literal protection -------------------------------------------------------------------------------

    [Fact]
    public void Strip_UrlInsideAString_KeepsTheDoubleSlash()
    {
        StripText("var endpoint = \"https://example.test/api//v1\"; // comment")
            .Should().Be("var endpoint = \"https://example.test/api//v1\"; ");
    }

    [Fact]
    public void Strip_EscapedQuoteBeforeACommentMarker_KeepsTheStringIntact()
    {
        const string source = "var s = \"a\\\"//b\"; // comment";

        StripText(source).Should().Be("var s = \"a\\\"//b\"; ");
    }

    [Fact]
    public void Strip_CommentMarkersInsideABlockCommentLikeString_AreKept()
    {
        StripText("var s = \"/* not a comment */\"; // comment").Should().Be("var s = \"/* not a comment */\"; ");
    }

    [Fact]
    public void Strip_VerbatimStringWithDoubledQuotes_IsKeptIncludingMarkers()
    {
        const string source = "var s = @\"a \"\" // b\"; // comment";

        StripText(source).Should().Be("var s = @\"a \"\" // b\"; ");
    }

    [Fact]
    public void Strip_RawStringLiteral_KeepsMarkersAndNewlines()
    {
        const string source = "var json = \"\"\"\n{ \"note\": \"// not a comment\" }\n\"\"\"; // comment";

        StripText(source).Should().Be("var json = \"\"\"\n{ \"note\": \"// not a comment\" }\n\"\"\"; ");
    }

    [Fact]
    public void Strip_InterpolationHole_RemovesACommentInsideTheHole()
    {
        StripText("var label = $\"count={count /* inline */}\";").Should().Be("var label = $\"count={count }\";");
    }

    [Fact]
    public void Strip_CharacterLiteralContainingSlash_IsNotAComment()
    {
        StripText("char c = '/'; // comment").Should().Be("char c = '/'; ");
    }

    // A prefixed literal closes on the quote alone; treating the whole opener as the closer would let the literal
    // run to the end of the file and silently stop removing every later comment.
    [Theory]
    [InlineData("csharp", "var s = @\"a\"; // comment", "var s = @\"a\"; ")]
    [InlineData("csharp", "var s = $\"a\"; // comment", "var s = $\"a\"; ")]
    [InlineData("csharp", "var s = $@\"a\"; // comment", "var s = $@\"a\"; ")]
    [InlineData("csharp", "var s = @$\"a\"; // comment", "var s = @$\"a\"; ")]
    [InlineData("fsharp", "let s = @\"a\" // comment", "let s = @\"a\" ")]
    [InlineData("bash", "s=$'a' # comment", "s=$'a' ")]
    [InlineData("bash", "s=$\"a\" # comment", "s=$\"a\" ")]
    public void Strip_PrefixedLiteral_ClosesOnTheQuoteAndTheFollowingCommentIsStillRemoved(
        string language,
        string source,
        string expected)
    {
        StripText(source, language).Should().Be(expected);
    }

    [Fact]
    public void Strip_LiteralAtEndOfFile_WithoutAClosingDelimiter_IsReportedAsUnterminated()
    {
        var result = Strip("var s = @\"never closed");

        result.Content.Should().Be("var s = @\"never closed");
        result.UnterminatedSequences.Should().Be(1);
    }

    [Fact]
    public void Strip_JavaScriptTemplateLiteral_KeepsMarkers()
    {
        const string source = "const t = `\n  // data\n  /* data */\n`; // comment";

        StripText(source, "javascript").Should().Be("const t = `\n  // data\n  /* data */\n`; ");
    }

    [Fact]
    public void Strip_JavaScriptTemplateHole_RemovesACommentInsideTheHole()
    {
        StripText("const t = `${/* c */value}`;", "javascript").Should().Be("const t = `${ value}`;");
    }

    [Fact]
    public void Strip_JavaScriptRegexLiteralWithCommentMarkers_IsKept()
    {
        StripText("const re = /[/*]/; // comment", "javascript").Should().Be("const re = /[/*]/; ");
    }

    [Fact]
    public void Strip_JavaScriptEscapedSlashesInRegex_AreKept()
    {
        StripText(@"const re = /a\/\/b/; // comment", "javascript").Should().Be(@"const re = /a\/\/b/; ");
    }

    [Fact]
    public void Strip_JavaScriptDivision_IsNotTreatedAsARegex()
    {
        StripText("const half = total / 2; // comment", "javascript").Should().Be("const half = total / 2; ");
    }

    [Fact]
    public void Strip_GoRawString_KeepsMarkers()
    {
        const string source = "var help = `\n// data\n` // comment";

        StripText(source, "go").Should().Be("var help = `\n// data\n` ");
    }

    [Fact]
    public void Strip_RustNestedBlockComment_IsRemovedAsOneComment()
    {
        var result = Strip("/* outer /* inner */ still */ code();", "rust");

        result.Content.Should().Be(" code();");
        result.RemovedBlockComments.Should().Be(1);
    }

    [Fact]
    public void Strip_RustLifetime_IsNotMistakenForAString()
    {
        StripText("fn f<'a>(s: &'a str) {} // comment", "rust").Should().Be("fn f<'a>(s: &'a str) {} ");
    }

    [Fact]
    public void Strip_RustRawStringWithHashes_KeepsMarkers()
    {
        StripText("let s = r#\"a // b\"#; // comment", "rust").Should().Be("let s = r#\"a // b\"#; ");
    }

    [Fact]
    public void Strip_SqlDollarQuotedBody_KeepsItsCommentMarkers()
    {
        const string source = "SELECT $body$ -- data /* data */ $body$; -- comment";

        StripText(source, "sql").Should().Be("SELECT $body$ -- data /* data */ $body$; ");
    }

    [Fact]
    public void Strip_SqlDoubledQuoteEscape_IsKept()
    {
        StripText("SELECT 'it''s -- fine'; -- comment", "sql").Should().Be("SELECT 'it''s -- fine'; ");
    }

    [Fact]
    public void Strip_PhpHeredoc_KeepsCommentMarkersInsideTheBody()
    {
        const string source = "<<<EOT\n// data\nEOT;\n// comment";

        StripText(source, "php").Should().Be("<<<EOT\n// data\nEOT;\n");
    }

    [Fact]
    public void Strip_BashHashInsideAWord_IsNotAComment()
    {
        StripText("echo a#b # comment", "bash").Should().Be("echo a#b ");
    }

    [Fact]
    public void Strip_BashHeredocBody_KeepsHashLines()
    {
        const string source = "cat <<EOF\n# data\nEOF\n# comment";

        StripText(source, "bash").Should().Be("cat <<EOF\n# data\nEOF\n");
    }

    [Fact]
    public void Strip_MarkupAttributeWithCommentMarkers_IsKept()
    {
        const string source = "<input value=\"<!-- kept -->\"> <!-- removed -->";

        StripText(source, "html").Should().Be("<input value=\"<!-- kept -->\"> ");
    }

    [Fact]
    public void Strip_MarkupCdataSection_IsKept()
    {
        const string source = "<root><![CDATA[<!-- data -->]]></root><!-- removed -->";

        StripText(source, "xml").Should().Be("<root><![CDATA[<!-- data -->]]></root>");
    }

    [Fact]
    public void Strip_MarkupScriptBody_IsNotScannedForMarkupComments()
    {
        const string source = "<script>var re = /a/; // stays</script><!-- removed -->";

        StripText(source, "html").Should().Be("<script>var re = /a/; // stays</script>");
    }

    [Fact]
    public void Strip_MarkupLessThanInText_IsNotATag()
    {
        StripText("a < b <!-- removed -->", "html").Should().Be("a < b ");
    }

    [Fact]
    public void Strip_SqlServerBracketIdentifier_KeepsItsDashes()
    {
        StripText("SELECT [a--b] FROM t; -- comment", "sql").Should().Be("SELECT [a--b] FROM t; ");
    }

    // ---- unknown languages --------------------------------------------------------------------------------

    [Fact]
    public void Strip_LanguageWithoutSyntax_ReturnsTheSourceUnchanged()
    {
        const string source = "x = 1 # not stripped\n/* neither is this */\n";

        var result = Strip(source, "lua");

        result.Content.Should().Be(source);
        result.LanguageSupported.Should().BeFalse();
        result.RemovedComments.Should().Be(0);
        result.FallbackReason.Should().Contain("lua");
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("text")]
    [InlineData("yaml")]
    [InlineData("batch")]
    [InlineData("")]
    public void Strip_UnsupportedLanguage_IsReportedRatherThanGuessed(string language)
    {
        Strip("value # text", language).LanguageSupported.Should().BeFalse();
    }

    // ---- malformed input ----------------------------------------------------------------------------------

    [Fact]
    public void Strip_UnterminatedBlockComment_RemovesItAndReportsTheCount()
    {
        var result = Strip("code(); /* never closed");

        result.Content.Should().Be("code(); ");
        result.UnterminatedSequences.Should().Be(1);
    }

    [Fact]
    public void Strip_StrayQuote_DoesNotSwallowTheRestOfTheFile()
    {
        // A lone apostrophe opens and closes nothing; a compiler resynchronises at the line end and so does this.
        const string source = "key: don't\n# comment\nnext: 1\n";

        var result = StripText(source, "toml");

        result.Should().Contain("next: 1");
        result.Should().NotContain("# comment");
        Strip(source, "toml").UnterminatedSequences.Should().Be(1);
    }

    [Fact]
    public void Strip_EmptySource_ReturnsItUnchanged()
    {
        StripText(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Strip_RemovalIsIdempotent()
    {
        const string source = "a/* one */b // two\nc/** three */d\n";

        var first = Strip(source);
        var second = Strip(first.Content);

        second.RemovedComments.Should().Be(0);
        second.Content.Should().Be(first.Content);
    }

    // ---- documentation comments ---------------------------------------------------------------------------

    [Fact]
    public void Strip_DocumentationCommentsAreRemovedByDefault()
    {
        var result = Strip("/// <summary>docs</summary>\npublic void M() {}\n");

        result.Content.Should().Be("\npublic void M() {}\n");
        result.RemovedDocumentationComments.Should().Be(1);
    }

    [Fact]
    public void Strip_PreservedDocumentationComments_AreByteIdentical()
    {
        const string source = "/// <summary>docs</summary>\npublic void M() {}\n";
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var result = Strip(source, "csharp", options);

        result.Content.Should().Be(source);
        result.PreservedDocumentationComments.Should().Be(1);
        result.RemovedDocumentationComments.Should().Be(0);
    }

    [Theory]
    [InlineData("csharp", "/** docs */")]
    [InlineData("csharp", "/// docs")]
    [InlineData("java", "/** docs */")]
    [InlineData("javascript", "/** @param x */")]
    [InlineData("rust", "//! crate docs")]
    [InlineData("rust", "/*! module docs */")]
    [InlineData("php", "/** docs */")]
    [InlineData("css", "/*! keep me */")]
    public void Strip_PreserveDocumentationComments_AppliesToEveryDocumentationForm(string language, string comment)
    {
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var result = Strip(comment + "\ncode();\n", language, options);

        result.Content.Should().StartWith(comment);
        result.PreservedDocumentationComments.Should().Be(1);
    }

    [Fact]
    public void Strip_SlashSlashSlashSlash_IsAnOrdinaryComment()
    {
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var result = Strip("//// separator\ncode();\n", "csharp", options);

        result.Content.Should().Be("\ncode();\n");
        result.RemovedInlineComments.Should().Be(1);
        result.PreservedDocumentationComments.Should().Be(0);
    }

    [Fact]
    public void Strip_EmptyBlockComment_IsNotDocumentation()
    {
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var result = Strip("/**/code();", "csharp", options);

        result.Content.Should().Be("code();");
        result.PreservedDocumentationComments.Should().Be(0);
    }

    [Fact]
    public void Strip_GoHasNoDistinctDocumentationForm_SoNothingIsPreserved()
    {
        var options = new CommentStripOptions { PreserveDocumentationComments = true };

        var result = Strip("// Package docs\npackage main\n", "go", options);

        result.Content.Should().Be("\npackage main\n");
        result.PreservedDocumentationComments.Should().Be(0);
    }
}
