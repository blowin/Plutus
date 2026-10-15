namespace Plutus.Domain.Comments;

/// <summary>
/// How comments are located in a source file.
/// <para>
/// The distinction is deliberate and user-visible: <see cref="Lexical"/> is the project's own character scanner
/// and <see cref="AntlrTokenChannel"/> is real grammar work — a generated ANTLR lexer whose comment token types
/// are dropped from the hidden channel. The two are not interchangeable, and the selected one is reported.
/// </para>
/// </summary>
public enum CommentStripStrategy
{
    /// <summary>No comment processing at all.</summary>
    None = 0,

    /// <summary>A language-aware lexical scanner. No grammar is involved.</summary>
    Lexical = 1,

    /// <summary>
    /// Tokens produced by a project-maintained ANTLR grammar are filtered by channel and comment token type.
    /// Unavailable until such a grammar exists; requesting it without one is an error, never a silent fallback.
    /// </summary>
    AntlrTokenChannel = 2,
}

/// <summary>The value of <c>--comment-strategy</c> before it is resolved against the maintained grammars.</summary>
public enum CommentStrategyRequest
{
    /// <summary>Use ANTLR channels where a maintained grammar exists, lexical scanning everywhere else.</summary>
    Auto = 0,

    /// <summary>Always use the lexical scanner.</summary>
    Lexical = 1,

    /// <summary>Always use ANTLR token channels; fails when no maintained grammar covers a language.</summary>
    Antlr = 2,
}
