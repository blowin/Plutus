using FluentAssertions;
using Plutus.Domain.Structure;

namespace Plutus.Test.Structure;

/// <summary>
/// The token rewriter is the safety mechanism of the whole structural pass: edits are expressed as token spans, so
/// they cannot slice a literal or a comment, and conflicting edits are refused instead of resolved arbitrarily.
/// </summary>
public class TokenStreamRewriterTests
{
    [Fact]
    public void Replace_ReplacesOnlyTheSpan()
    {
        // The stream keeps whitespace as tokens of its own, so "one two three" is [one, ' ', two, ' ', three].
        var stream = Stream("one two three");
        var rewriter = new TokenStreamRewriter(stream);

        rewriter.Replace(new TokenSpan(2, 2), "TWO");

        rewriter.GetRewrittenText().Should().Be("one TWO three");
    }

    [Fact]
    public void Replace_MultipleDisjointSpans_AppliesAllOfThem()
    {
        var stream = Stream("one two three four");
        var rewriter = new TokenStreamRewriter(stream);

        rewriter.Replace(new TokenSpan(6, 6), "FOUR");
        rewriter.Replace(new TokenSpan(2, 2), "TWO");

        rewriter.GetRewrittenText().Should().Be("one TWO three FOUR");
        rewriter.EditCount.Should().Be(2);
    }

    [Fact]
    public void Delete_RemovesTheSpan()
    {
        var stream = Stream("one two three");
        var rewriter = new TokenStreamRewriter(stream);

        rewriter.Delete(new TokenSpan(2, 2));

        rewriter.GetRewrittenText().Should().Be("one  three");
    }

    [Fact]
    public void InsertBeforeAndAfter_AddTextAroundAToken()
    {
        var stream = Stream("one two");
        var rewriter = new TokenStreamRewriter(stream);

        rewriter.InsertBefore(2, "<");
        rewriter.InsertAfter(2, ">");

        rewriter.GetRewrittenText().Should().Be("one <two>");
    }

    [Fact]
    public void Replace_OverlappingSpans_AreRefused()
    {
        var stream = Stream("one two three");
        var rewriter = new TokenStreamRewriter(stream);
        rewriter.Replace(new TokenSpan(0, 2), "X");

        var act = () => rewriter.Replace(new TokenSpan(2, 4), "Y");

        act.Should().Throw<InvalidOperationException>().WithMessage("*overlaps*");
    }

    [Fact]
    public void Insert_InsideAReplacedSpan_IsRefused()
    {
        var stream = Stream("one two three");
        var rewriter = new TokenStreamRewriter(stream);
        rewriter.Replace(new TokenSpan(0, 1), "X");

        var act = () => rewriter.InsertBefore(1, "!");

        act.Should().Throw<InvalidOperationException>().WithMessage("*inside the replaced span*");
    }

    [Fact]
    public void Replace_SpanOutsideTheStream_IsRefused()
    {
        var stream = Stream("one two");
        var rewriter = new TokenStreamRewriter(stream);

        var act = () => rewriter.Replace(new TokenSpan(0, 5), ";");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Replace_EmptySpan_IsRefused()
    {
        var stream = Stream("one two");
        var rewriter = new TokenStreamRewriter(stream);

        var act = () => rewriter.Replace(new TokenSpan(2, 1), ";");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetRewrittenText_WithoutEdits_ReturnsTheSource()
    {
        var stream = Stream("one two");

        new TokenStreamRewriter(stream).GetRewrittenText().Should().Be("one two");
    }

    [Fact]
    public void TryCreate_StreamThatDoesNotTileTheSource_IsRefusedWithAReason()
    {
        var tokens = new[]
        {
            new CodeToken(0, TokenKind.Identifier, "one ", 0),
            new CodeToken(1, TokenKind.Identifier, "two", 10),
        };

        CodeTokenStream.TryCreate("one two", tokens, out var stream, out var problem).Should().BeFalse();

        stream.Should().BeNull();
        problem.Should().Contain("starts at");
        problem.Should().Contain("expected");
    }

    [Fact]
    public void TryCreate_StreamCoveringLessThanTheSource_IsRefused()
    {
        var tokens = new[] { new CodeToken(0, TokenKind.Identifier, "one", 0) };

        CodeTokenStream.TryCreate("one two", tokens, out _, out var problem).Should().BeFalse();

        problem.Should().Contain("cover 3 of 7");
    }

    [Fact]
    public void TryCreate_StreamThatTiles_RebuildsTheSource()
    {
        const string source = "a b\nc";

        CodeTokenStream.TryCreate(source, Tokens(source), out var stream, out var problem).Should().BeTrue(problem);

        stream.Reconstruct().Should().Be(source);
        stream.GetText(new TokenSpan(0, stream.Count - 1)).Should().Be(source);
    }

    /// <summary>Splits text into whitespace and non-whitespace runs, which tile the source by construction.</summary>
    private static CodeTokenStream Stream(string source)
    {
        CodeTokenStream.TryCreate(source, Tokens(source), out var stream, out var problem).Should().BeTrue(problem);
        return stream;
    }

    private static IReadOnlyList<CodeToken> Tokens(string source)
    {
        var tokens = new List<CodeToken>();
        var position = 0;

        while (position < source.Length)
        {
            var start = position;
            var whitespace = char.IsWhiteSpace(source[position]);
            while (position < source.Length && char.IsWhiteSpace(source[position]) == whitespace)
            {
                position++;
            }

            var text = source[start..position];
            tokens.Add(new CodeToken(tokens.Count, TokenKind.Identifier, text, start));
        }

        return tokens;
    }
}
