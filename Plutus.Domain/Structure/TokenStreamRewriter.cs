namespace Plutus.Domain.Structure;

/// <summary>
/// Applies text edits to a token stream instead of to a raw string. This is the same contract ANTLR's
/// <c>TokenStreamRewriter</c> offers — replace, delete and insert against token indices — and it is what makes a
/// structural rewrite safe: edits are expressed in terms of tokens that were actually parsed, so an edit can never
/// slice a string literal or a comment in half.
/// </summary>
public interface ITokenStreamRewriter
{
    int EditCount { get; }

    /// <summary>Replaces every token of <paramref name="span"/> (inclusive) with <paramref name="text"/>.</summary>
    void Replace(TokenSpan span, string text);

    /// <summary>Removes every token of <paramref name="span"/> (inclusive).</summary>
    void Delete(TokenSpan span);

    /// <summary>Inserts text immediately before the token at <paramref name="tokenIndex"/>.</summary>
    void InsertBefore(int tokenIndex, string text);

    /// <summary>Inserts text immediately after the token at <paramref name="tokenIndex"/>.</summary>
    void InsertAfter(int tokenIndex, string text);

    /// <summary>Produces the rewritten text.</summary>
    string GetRewrittenText();
}

/// <summary>
/// The default <see cref="ITokenStreamRewriter"/>: an edit list applied over the token stream with overlap
/// detection. Conflicts throw rather than being resolved arbitrarily, because a structural rewrite that cuts the
/// same tokens twice is a bug in the visitor, not something to paper over.
/// </summary>
public sealed class TokenStreamRewriter(CodeTokenStream stream) : ITokenStreamRewriter
{
    private readonly List<TextEdit> _edits = [];
    private readonly Dictionary<int, string> _replacementByStart = [];
    private readonly Dictionary<int, int> _replacementEndByStart = [];
    private readonly Dictionary<int, List<string>> _insertBefore = [];
    private readonly Dictionary<int, List<string>> _insertAfter = [];

    public CodeTokenStream Stream { get; } = stream;

    public int EditCount => _edits.Count;

    public IReadOnlyList<TextEdit> Edits => _edits;

    public void Replace(TokenSpan span, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var target = Validate(span);

        if (_replacementByStart.ContainsKey(target.StartIndex))
        {
            throw new InvalidOperationException($"Token {target.StartIndex} is already replaced.");
        }

        foreach (var edit in _edits)
        {
            if (edit.Span is { } existing && existing.Overlaps(target))
            {
                throw new InvalidOperationException(
                    $"The replacement {target} overlaps the edit {existing} registered for token {edit.TokenIndex}.");
            }
        }

        _replacementByStart[target.StartIndex] = text;
        _replacementEndByStart[target.StartIndex] = target.EndIndex;
        _edits.Add(new TextEdit(TextEditKind.Replace, target.StartIndex, text, target));
    }

    public void Delete(TokenSpan span) => Replace(span, string.Empty);

    public void InsertBefore(int tokenIndex, string text) => Insert(_insertBefore, tokenIndex, text);

    public void InsertAfter(int tokenIndex, string text) => Insert(_insertAfter, tokenIndex, text);

    public string GetRewrittenText()
    {
        if (_edits.Count == 0 && _insertBefore.Count == 0 && _insertAfter.Count == 0)
        {
            return Stream.Source;
        }

        var builder = new System.Text.StringBuilder(Stream.Source.Length);
        var index = 0;

        while (index < Stream.Count)
        {
            AppendAll(builder, _insertBefore, index);

            if (_replacementByStart.TryGetValue(index, out var replacement))
            {
                builder.Append(replacement);
                index = _replacementEndByStart[index] + 1;
                continue;
            }

            builder.Append(Stream[index].Text);
            AppendAll(builder, _insertAfter, index);
            index++;
        }

        AppendAll(builder, _insertBefore, Stream.Count);
        return builder.ToString();
    }

    private void Insert(Dictionary<int, List<string>> target, int tokenIndex, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (tokenIndex < 0 || tokenIndex > Stream.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenIndex), tokenIndex, $"The stream has {Stream.Count} tokens.");
        }

        if (_replacementByStart.TryGetValue(tokenIndex, out _))
        {
            throw new InvalidOperationException($"Token {tokenIndex} is being replaced, so text cannot be inserted at it.");
        }

        // An insert inside a replaced span would be silently dropped when the span is written out, so it is an
        // error here instead of a surprise in the output.
        foreach (var (start, end) in _replacementEndByStart)
        {
            if (tokenIndex > start && tokenIndex <= end)
            {
                throw new InvalidOperationException(
                    $"Token {tokenIndex} lies inside the replaced span [{start}..{end}], so text cannot be inserted at it.");
            }
        }

        if (!target.TryGetValue(tokenIndex, out var list))
        {
            list = [];
            target[tokenIndex] = list;
        }

        list.Add(text);
    }

    private TokenSpan Validate(TokenSpan span)
    {
        if (span.IsEmpty)
        {
            throw new ArgumentException("A token span must contain at least one token.", nameof(span));
        }

        if (span.StartIndex < 0 || span.EndIndex >= Stream.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(span),
                span,
                $"The stream has {Stream.Count} tokens, so the span must stay within [0..{Stream.Count - 1}].");
        }

        return span;
    }

    private static void AppendAll(System.Text.StringBuilder builder, Dictionary<int, List<string>> source, int tokenIndex)
    {
        if (!source.TryGetValue(tokenIndex, out var texts))
        {
            return;
        }

        foreach (var text in texts)
        {
            builder.Append(text);
        }
    }
}

/// <summary>The kinds of edit a rewriter accepts.</summary>
public enum TextEditKind
{
    Replace,
    InsertBefore,
    InsertAfter,
}

/// <summary>One registered edit, kept for diagnostics and for overlap reporting.</summary>
/// <param name="Kind">What the edit does.</param>
/// <param name="TokenIndex">The token the edit is anchored at.</param>
/// <param name="Text">The text the edit contributes.</param>
/// <param name="Span">The replaced token span, or <c>null</c> for an insertion.</param>
public sealed record TextEdit(TextEditKind Kind, int TokenIndex, string Text, TokenSpan? Span);
