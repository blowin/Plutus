namespace Plutus.Domain.Comments;

/// <summary>
/// One token as a grammar-backed lexer produced it. <see cref="TypeName"/> is the symbolic token name from the
/// grammar (<c>SIMPLE_COMMENT</c>) and <see cref="Channel"/> is the numeric channel, so both the comment token
/// types and the channels a manifest declares can be matched without guessing.
/// </summary>
public sealed record CommentChannelToken(int Type, string TypeName, string Text, int Channel, int StartIndex)
{
    /// <summary>True when the grammar marks this token as a documentation comment.</summary>
    public bool IsDocumentation { get; init; }
}

/// <summary>
/// The seam that keeps ANTLR support honest: a generated lexer is used only through this interface, and only for
/// a language listed in the <see cref="GrammarInventory"/>. The project maintains no grammar today, so no
/// production implementation exists — tests supply one to prove the channel path and its failure modes.
/// </summary>
public interface ICommentChannelLexer
{
    LanguageId Language { get; }

    /// <summary>
    /// Tokenizes <paramref name="source"/>. Tokens must tile the input: every character belongs to exactly one
    /// token and the tokens are ordered by <see cref="CommentChannelToken.StartIndex"/>. A lexer that does not
    /// tile its input is rejected by the stripper instead of producing a corrupted file.
    /// </summary>
    IReadOnlyList<CommentChannelToken> Tokenize(string source);
}
