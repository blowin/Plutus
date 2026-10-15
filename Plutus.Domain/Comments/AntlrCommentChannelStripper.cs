namespace Plutus.Domain.Comments;

/// <summary>
/// Removes comments by dropping the tokens a grammar puts on its comment channels. This is real
/// token-channel processing, not lexical scanning: what counts as a comment is the grammar's own token type or
/// channel, and the source is reconstructed from the tokens the parser would keep.
/// <para>
/// It refuses to guess. If the lexer's tokens do not tile the input, or if the grammar declares channels that do
/// not resolve, the file is returned unchanged with a <see cref="CommentStripResult.FallbackReason"/> instead of
/// being silently rewritten from an incomplete token stream.
/// </para>
/// </summary>
public sealed class AntlrCommentChannelStripper(GrammarInventoryEntry grammar, ICommentChannelLexer lexer) : ICommentStripper
{
    private readonly HashSet<string> _commentTokenTypes = new(grammar.CommentTokenTypes, StringComparer.Ordinal);
    private readonly HashSet<string> _documentationTokenTypes = new(grammar.DocumentationTokenTypes, StringComparer.Ordinal);

    public CommentStripStrategy Strategy => CommentStripStrategy.AntlrTokenChannel;

    public GrammarInventoryEntry Grammar { get; } = grammar;

    public CommentStripResult Strip(string source, LanguageId language, CommentStripOptions options)
    {
        if (string.IsNullOrEmpty(source))
        {
            return new CommentStripResult { Content = source, Strategy = Strategy, Language = language };
        }

        var channelNumbers = Grammar.ResolveCommentChannelNumbers(out var unresolved);
        if (unresolved.Count > 0)
        {
            return Fallback(source, $"grammar '{Grammar.Language}' has unresolved comment channel(s): {string.Join(", ", unresolved)}");
        }

        IReadOnlyList<CommentChannelToken> tokens;
        try
        {
            tokens = lexer.Tokenize(source);
        }
        catch (Exception exception)
        {
            return Fallback(source, $"lexer '{Grammar.LexerClass ?? Grammar.GrammarFile}' failed: {exception.Message}");
        }

        if (!Tiles(source, tokens, out var tilingProblem))
        {
            return Fallback(source, $"lexer output does not tile the input: {tilingProblem}");
        }

        var buffer = new CommentRemovalBuffer();
        var removedInline = 0;
        var removedBlock = 0;
        var removedDocumentation = 0;
        var preservedDocumentation = 0;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var isComment = IsCommentToken(token, channelNumbers);

            if (!isComment)
            {
                buffer.Append(token.Text);
                continue;
            }

            var isDocumentation = _documentationTokenTypes.Contains(token.TypeName) || token.IsDocumentation;
            if (isDocumentation && options.PreserveDocumentationComments)
            {
                buffer.Append(token.Text);
                preservedDocumentation++;
                continue;
            }

            if (isDocumentation)
            {
                removedDocumentation++;
            }
            else if (token.Text.AsSpan().ContainsAny('\r', '\n'))
            {
                removedBlock++;
            }
            else
            {
                removedInline++;
            }

            var nextChar = index + 1 < tokens.Count && tokens[index + 1].Text.Length > 0
                ? tokens[index + 1].Text[0]
                : (char?)null;
            buffer.RemoveComment(token.Text, nextChar);
        }

        return new CommentStripResult
        {
            Content = buffer.ToString(),
            Strategy = Strategy,
            Language = language,
            RemovedInlineComments = removedInline,
            RemovedBlockComments = removedBlock,
            RemovedDocumentationComments = removedDocumentation,
            PreservedDocumentationComments = preservedDocumentation,
        };
    }

    /// <summary>Boundary convenience for callers that already hold an identifier as text.</summary>
    public CommentStripResult Strip(string source, string? language, CommentStripOptions options) =>
        Strip(source, LanguageId.From(language), options);

    private bool IsCommentToken(CommentChannelToken token, IReadOnlyList<int> channelNumbers) =>
        _commentTokenTypes.Contains(token.TypeName)
        || _documentationTokenTypes.Contains(token.TypeName)
        || token.IsDocumentation
        || channelNumbers.Contains(token.Channel);

    private CommentStripResult Fallback(string source, string reason) => new()
    {
        Content = source,
        Strategy = Strategy,
        LanguageSupported = true,
        FallbackReason = reason,
    };

    /// <summary>
    /// Verifies that the tokens cover the source exactly once, in order. Token positions come from the lexer, so
    /// this is what keeps a mismatched or stale grammar from deleting text it never tokenized.
    /// </summary>
    private static bool Tiles(string source, IReadOnlyList<CommentChannelToken> tokens, out string problem)
    {
        var expected = 0;
        foreach (var token in tokens)
        {
            if (token.StartIndex != expected)
            {
                problem = $"token '{token.TypeName}' starts at {token.StartIndex}, expected {expected}";
                return false;
            }

            if (token.StartIndex + token.Text.Length > source.Length)
            {
                problem = $"token '{token.TypeName}' runs past the end of the input";
                return false;
            }

            if (!source.AsSpan(token.StartIndex, token.Text.Length).SequenceEqual(token.Text))
            {
                problem = $"token '{token.TypeName}' text does not match the source at {token.StartIndex}";
                return false;
            }

            expected = token.StartIndex + token.Text.Length;
        }

        if (expected != source.Length)
        {
            problem = $"tokens cover {expected} of {source.Length} characters";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
