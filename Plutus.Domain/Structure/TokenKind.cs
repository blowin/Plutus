namespace Plutus.Domain.Structure;

/// <summary>
/// What a token is, as far as structural rewriting is concerned. The classification is used for diagnostics and for
/// assertions in tests; the rewriting itself works on token positions, so a misclassified token can never move an
/// edit.
/// </summary>
public enum TokenKind
{
    /// <summary>A keyword of the language.</summary>
    Keyword,

    /// <summary>An identifier, including contextual keywords the grammar treats as names.</summary>
    Identifier,

    /// <summary>A string, character, numeric, or interpolated-text literal.</summary>
    Literal,

    /// <summary>Punctuation and operators.</summary>
    Punctuation,

    /// <summary>The end-of-file token.</summary>
    EndOfFile,
}
