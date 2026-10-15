namespace Plutus.Domain.Structure;

/// <summary>
/// One token of a token stream, carrying its own text. The text spans the token together with the trivia around it,
/// so concatenating every token of a stream reproduces the original source exactly — that invariant is what lets a
/// rewrite stay lossless outside the spans it intentionally replaces.
/// </summary>
/// <param name="Index">The position of the token in its stream.</param>
/// <param name="Kind">The token classification.</param>
/// <param name="Text">The token text, including leading and trailing trivia.</param>
/// <param name="Start">The absolute character offset of <paramref name="Text"/> in the source.</param>
public sealed record CodeToken(int Index, TokenKind Kind, string Text, int Start)
{
    public int Length => Text.Length;

    public int End => Start + Length;

    public bool IsEndOfFile => Kind == TokenKind.EndOfFile;

    public override string ToString() => $"{Index}:{Kind} '{Text}'";
}
