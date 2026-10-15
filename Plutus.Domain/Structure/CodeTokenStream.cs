namespace Plutus.Domain.Structure;

/// <summary>
/// A token stream that is known to tile its source: the tokens are ordered, contiguous, and cover every character
/// exactly once. Structural rewriting refuses to run without that property, because a stream that does not tile the
/// input cannot be rewritten back into it without losing text.
/// </summary>
public sealed class CodeTokenStream
{
    private readonly IReadOnlyList<CodeToken> _tokens;

    private CodeTokenStream(string source, IReadOnlyList<CodeToken> tokens)
    {
        Source = source;
        _tokens = tokens;
    }

    /// <summary>The original text the stream was produced from.</summary>
    public string Source { get; }

    public IReadOnlyList<CodeToken> Tokens => _tokens;

    public int Count => _tokens.Count;

    public CodeToken this[int index] => _tokens[index];

    /// <summary>
    /// Creates a stream, verifying the tiling invariant. The failure reason is returned instead of thrown so a
    /// caller can degrade to "leave the file alone and say why", which is always preferable to a mangled file.
    /// </summary>
    public static bool TryCreate(string source, IEnumerable<CodeToken> tokens, out CodeTokenStream stream, out string? problem)
    {
        var list = tokens.ToList();
        var expected = 0;

        for (var index = 0; index < list.Count; index++)
        {
            var token = list[index];
            if (token.Index != index)
            {
                problem = $"token {index} reports index {token.Index}";
                stream = null!;
                return false;
            }

            if (token.Start != expected)
            {
                problem = $"token {index} starts at {token.Start}, expected {expected}";
                stream = null!;
                return false;
            }

            if (token.End > source.Length)
            {
                problem = $"token {index} runs past the end of the source";
                stream = null!;
                return false;
            }

            if (!source.AsSpan(token.Start, token.Length).SequenceEqual(token.Text))
            {
                problem = $"token {index} text does not match the source at {token.Start}";
                stream = null!;
                return false;
            }

            expected = token.End;
        }

        if (expected != source.Length)
        {
            problem = $"tokens cover {expected} of {source.Length} characters";
            stream = null!;
            return false;
        }

        problem = null;
        stream = new CodeTokenStream(source, list);
        return true;
    }

    /// <summary>The text of an inclusive token span, exactly as it appears in the source.</summary>
    public string GetText(TokenSpan span)
    {
        if (span.IsEmpty || Count == 0)
        {
            return string.Empty;
        }

        var start = this[span.StartIndex].Start;
        var end = this[Math.Min(span.EndIndex, Count - 1)].End;
        return Source[start..end];
    }

    /// <summary>The source text of the whole stream, rebuilt from its tokens.</summary>
    public string Reconstruct() => string.Concat(_tokens.Select(token => token.Text));
}
