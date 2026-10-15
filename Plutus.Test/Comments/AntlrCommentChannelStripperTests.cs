using FluentAssertions;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// Exercises the ANTLR token-channel path with a stub lexer, because the project maintains no grammar today. What
/// is tested here is the contract a real generated lexer would have to satisfy, plus the failure modes that must
/// degrade to "leave the file alone" instead of "rewrite it from an incomplete token stream".
/// </summary>
public class AntlrCommentChannelStripperTests
{
    private static readonly GrammarInventoryEntry Grammar = new()
    {
        Language = "sql",
        GrammarFile = "SqlLexer.g4",
        LexerClass = "SqlLexer",
        CommentTokenTypes = ["SIMPLE_COMMENT", "BRACKETED_COMMENT"],
        DocumentationTokenTypes = ["DOC_COMMENT"],
        CommentChannels = [new GrammarChannelRef("HIDDEN", null, "HIDDEN")],
    };

    [Fact]
    public void Strategy_IsReportedAsTokenChannelProcessing()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());

        stripper.Strategy.Should().Be(CommentStripStrategy.AntlrTokenChannel);
        stripper.Grammar.Should().BeSameAs(Grammar);
    }

    [Fact]
    public void Strip_DropsCommentTokensAndKeepsTheRest()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());
        var source = "SELECT 1; -- comment\nSELECT 2;";

        var result = stripper.Strip(source, "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1; \nSELECT 2;");
        result.Strategy.Should().Be(CommentStripStrategy.AntlrTokenChannel);
        result.RemovedInlineComments.Should().Be(1);
        result.FallbackReason.Should().BeNull();
    }

    [Fact]
    public void Strip_CommentTokenOnTheCommentChannel_IsDroppedEvenWithoutATokenType()
    {
        var grammar = Grammar with { CommentTokenTypes = [] };
        var stripper = new AntlrCommentChannelStripper(grammar, new StubLexer());

        stripper.Strip("SELECT 1; -- comment", "sql", CommentStripOptions.Default).Content.Should().Be("SELECT 1; ");
    }

    [Fact]
    public void Strip_PreserveDocumentationComments_KeepsTheDocumentationToken()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());
        var source = "/** docs */\nSELECT 1;";

        var result = stripper.Strip(source, "sql", new CommentStripOptions { PreserveDocumentationComments = true });

        result.Content.Should().Be(source);
        result.PreservedDocumentationComments.Should().Be(1);
        result.RemovedDocumentationComments.Should().Be(0);
    }

    [Fact]
    public void Strip_DocumentationCommentsAreRemovedByDefault()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());

        var result = stripper.Strip("/** docs */\nSELECT 1;", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("\nSELECT 1;");
        result.RemovedDocumentationComments.Should().Be(1);
    }

    [Fact]
    public void Strip_LineEndingsInsideARemovedComment_AreReEmitted()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());

        // The block comment token text contains two line breaks, which must survive the removal.
        var result = stripper.Strip("SELECT 1;/* a\nb\nc */SELECT 2;", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1;\n\nSELECT 2;");
    }

    [Fact]
    public void Strip_LexerThatDoesNotTileTheInput_ReturnsTheSourceUnchangedWithAReason()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer { DropLastToken = true });

        var result = stripper.Strip("SELECT 1; -- comment", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1; -- comment");
        result.FallbackReason.Should().Contain("does not tile");
    }

    [Fact]
    public void Strip_LexerThatReportsWrongOffsets_ReturnsTheSourceUnchanged()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer { ShiftOffsets = true });

        var result = stripper.Strip("SELECT 1; -- comment", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1; -- comment");
        result.FallbackReason.Should().Contain("does not tile");
    }

    [Fact]
    public void Strip_LexerThatThrows_ReturnsTheSourceUnchangedWithTheFailureReason()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer { ThrowOnTokenize = true });

        var result = stripper.Strip("SELECT 1;", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1;");
        result.FallbackReason.Should().Contain("failed");
    }

    [Fact]
    public void Strip_UnresolvedChannel_ReturnsTheSourceUnchanged()
    {
        var grammar = Grammar with { CommentChannels = [new GrammarChannelRef("NOWHERE", null, "NOWHERE")] };
        var stripper = new AntlrCommentChannelStripper(grammar, new StubLexer());

        var result = stripper.Strip("SELECT 1; -- comment", "sql", CommentStripOptions.Default);

        result.Content.Should().Be("SELECT 1; -- comment");
        result.FallbackReason.Should().Contain("NOWHERE");
    }

    [Fact]
    public void Strip_EmptySource_IsReturnedUnchanged()
    {
        var stripper = new AntlrCommentChannelStripper(Grammar, new StubLexer());

        stripper.Strip(string.Empty, "sql", CommentStripOptions.Default).Content.Should().BeEmpty();
    }

    /// <summary>
    /// Tokenizes <c>--</c> to end of line as a hidden-channel comment and everything else as code, with a lexical
    /// approximation of doc comments. The offsets are real, so the tiling check can be exercised.
    /// </summary>
    private sealed class StubLexer : ICommentChannelLexer
    {
        public bool DropLastToken { get; init; }

        public bool ShiftOffsets { get; init; }

        public bool ThrowOnTokenize { get; init; }

        public string Language => "sql";

        public IReadOnlyList<CommentChannelToken> Tokenize(string source)
        {
            if (ThrowOnTokenize)
            {
                throw new InvalidOperationException("lexer exploded");
            }

            var tokens = new List<CommentChannelToken>();
            var position = 0;

            while (position < source.Length)
            {
                if (source.AsSpan(position).StartsWith("/*"))
                {
                    var close = source.IndexOf("*/", position + 2, StringComparison.Ordinal);
                    var end = close < 0 ? source.Length : close + 2;
                    var text = source[position..end];
                    var isDocumentation = text.StartsWith("/**", StringComparison.Ordinal);
                    tokens.Add(new CommentChannelToken(
                        3,
                        isDocumentation ? "DOC_COMMENT" : "BRACKETED_COMMENT",
                        text,
                        1,
                        position));
                    position = end;
                    continue;
                }

                if (source.AsSpan(position).StartsWith("--"))
                {
                    var lineEnd = source.IndexOf('\n', position);
                    var end = lineEnd < 0 ? source.Length : lineEnd;
                    tokens.Add(new CommentChannelToken(2, "SIMPLE_COMMENT", source[position..end], 1, position));
                    position = end;
                    continue;
                }

                var nextComment = NextCommentStart(source, position + 1);
                var runEnd = nextComment < 0 ? source.Length : nextComment;
                tokens.Add(new CommentChannelToken(1, "CODE", source[position..runEnd], 0, position));
                position = runEnd;
            }

            if (DropLastToken && tokens.Count > 0)
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

            return ShiftOffsets
                ? tokens.Select(token => token with { StartIndex = token.StartIndex + 1 }).ToList()
                : tokens;
        }

        private static int NextCommentStart(string source, int from)
        {
            var block = source.IndexOf("/*", from, StringComparison.Ordinal);
            var line = source.IndexOf("--", from, StringComparison.Ordinal);

            if (block < 0)
            {
                return line;
            }

            return line < 0 ? block : Math.Min(block, line);
        }
    }
}
