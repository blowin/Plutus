namespace Plutus.Domain.Structure;

/// <summary>
/// An inclusive range of token indices, the same convention ANTLR's <c>TokenStreamRewriter</c> uses: replacing a
/// span means replacing every token from <see cref="StartIndex"/> to <see cref="EndIndex"/>, both included.
/// </summary>
public readonly record struct TokenSpan(int StartIndex, int EndIndex)
{
    public int Length => EndIndex - StartIndex + 1;

    public bool IsEmpty => EndIndex < StartIndex;

    public static TokenSpan Single(int index) => new(index, index);

    public bool Contains(int index) => index >= StartIndex && index <= EndIndex;

    public bool Overlaps(TokenSpan other) => StartIndex <= other.EndIndex && other.StartIndex <= EndIndex;

    public override string ToString() => $"[{StartIndex}..{EndIndex}]";
}
