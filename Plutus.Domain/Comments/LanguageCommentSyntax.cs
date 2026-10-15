namespace Plutus.Domain.Comments;

/// <summary>One line-comment form of a language.</summary>
/// <param name="Prefix">The characters that start the comment, for example <c>//</c> or <c>#</c>.</param>
/// <param name="Kind">Whether this form is an ordinary or a documentation comment.</param>
/// <param name="RequiresTokenBoundary">
/// When true the prefix only starts a comment at a word boundary. Shell, YAML and Makefiles need this:
/// <c>url#fragment</c> is a value, while <c>value # comment</c> is a comment.
/// </param>
/// <param name="ForbiddenFollowingChar">
/// Rejects a match when the next character is this one, which is how <c>///</c> avoids swallowing <c>////</c>.
/// </param>
public sealed record LineCommentSyntax(
    string Prefix,
    CommentKind Kind = CommentKind.Inline,
    bool RequiresTokenBoundary = false,
    char? ForbiddenFollowingChar = null);

/// <summary>One delimited comment form of a language.</summary>
/// <param name="Open">The opening delimiter.</param>
/// <param name="Close">The closing delimiter.</param>
/// <param name="Kind">Whether this form is an ordinary or a documentation comment.</param>
/// <param name="Nested">True for languages whose block comments nest, such as Rust and Kotlin.</param>
/// <param name="OpenRequiresLineStart">True for line-anchored forms such as Ruby's <c>=begin</c>.</param>
/// <param name="ForbiddenFollowingChar">
/// Rejects a match when the next character is this one, which is how <c>/**</c> avoids claiming the empty
/// comment <c>/**/</c> as a documentation block.
/// </param>
public sealed record BlockCommentSyntax(
    string Open,
    string Close,
    CommentKind Kind = CommentKind.Block,
    bool Nested = false,
    bool OpenRequiresLineStart = false,
    char? ForbiddenFollowingChar = null);

/// <summary>The literal forms the scanner can protect from comment detection.</summary>
public enum StringLiteralKind
{
    /// <summary>An ordinary delimited literal, optionally with escapes, doubling and interpolation holes.</summary>
    Delimited,

    /// <summary>A run of quote characters, used for raw, triple-quoted and multi-line literals.</summary>
    QuoteRun,

    /// <summary>Rust <c>r#"..."#</c>, where the hashes make the closing delimiter unique.</summary>
    RustRaw,

    /// <summary><c>&lt;&lt;TAG</c> (shell, HCL) or <c>&lt;&lt;&lt;TAG</c> (PHP): the body ends at a line that starts with the tag.</summary>
    Heredoc,

    /// <summary>PowerShell <c>@" ... "@</c> and <c>@' ... '@</c> here-strings.</summary>
    HereString,

    /// <summary>PostgreSQL <c>$tag$ ... $tag$</c>, whose body may contain <c>--</c> and <c>/*</c> freely.</summary>
    DollarQuoted,
}

/// <summary>One string-literal form. Literal contents are copied verbatim, so comment-like text inside stays.</summary>
public sealed record StringLiteralSyntax
{
    public required StringLiteralKind Kind { get; init; }

    /// <summary>The opening delimiter, including any language prefix that is part of it.</summary>
    public required string Open { get; init; }

    /// <summary>The closing delimiter. Defaults to <see cref="Open"/>.</summary>
    public string? Close { get; init; }

    /// <summary>Escape character, or <c>null</c> for literals that have no escapes.</summary>
    public string? Escape { get; init; } = "\\";

    /// <summary>True when the delimiter is escaped by doubling it, as in SQL <c>''</c> or C# <c>@""</c>.</summary>
    public bool DoubledDelimiterEscape { get; init; }

    /// <summary>True when the literal may span lines.</summary>
    public bool AllowsNewlines { get; init; }

    /// <summary>The opening sequence of an interpolation hole, for example <c>${</c> or <c>{</c>.</summary>
    public string? InterpolationOpen { get; init; }

    public char HoleOpenChar { get; init; } = '{';

    public char HoleCloseChar { get; init; } = '}';

    /// <summary>Minimum number of quote characters that open a <see cref="StringLiteralKind.QuoteRun"/> literal.</summary>
    public int MinQuoteRun { get; init; } = 3;

    /// <summary>
    /// True when the opening run may be longer than <see cref="MinQuoteRun"/> and the closing run must match its
    /// length, which is how C# raw strings are delimited.
    /// </summary>
    public bool MaximalQuoteRun { get; init; }

    /// <summary>Language prefixes that may precede the delimiter, such as Python's <c>r</c> or Rust's <c>b</c>.</summary>
    public IReadOnlyList<string> Prefixes { get; init; } = [];

    /// <summary>True when an <c>r</c>-style prefix disables escape processing, as in Python <c>r"\d"</c>.</summary>
    public bool PrefixDisablesEscapes { get; init; }

    /// <summary>True when the closing delimiter is only recognised at the start of a line.</summary>
    public bool CloseRequiresLineStart { get; init; }

    /// <summary>True when a heredoc terminator may be indented (shell <c>&lt;&lt;-</c>, PHP 7.3+).</summary>
    public bool IndentedTerminator { get; init; }

    /// <summary>True when a heredoc tag may be wrapped in quotes, as in PHP <c>&lt;&lt;&lt;'EOT'</c>.</summary>
    public bool QuotedTag { get; init; }

    /// <summary>
    /// True when a language prefix must start a token, so <c>br"..."</c> is only a Rust byte string when
    /// <c>br</c> is not the tail of a longer identifier.
    /// </summary>
    public bool RequiresTokenStart { get; init; }

    /// <summary>
    /// True for single-character literals. Matching then requires a well-formed literal (quote, one character or
    /// escape sequence, quote), which keeps Rust lifetimes such as <c>&amp;'a str</c> from opening a string that
    /// would hide real comments.
    /// </summary>
    public bool CharLiteral { get; init; }
}

/// <summary>Every comment and literal form of one language.</summary>
public sealed record LanguageCommentSyntax
{
    public required string Language { get; init; }

    /// <summary>Name for user-facing messages, for example <c>C#</c>.</summary>
    public required string DisplayName { get; init; }

    public IReadOnlyList<LineCommentSyntax> LineComments { get; init; } = [];

    public IReadOnlyList<BlockCommentSyntax> BlockComments { get; init; } = [];

    public IReadOnlyList<StringLiteralSyntax> Strings { get; init; } = [];

    /// <summary>True for JavaScript-family languages, where <c>/.../</c> is a regular expression literal.</summary>
    public bool RegexLiterals { get; init; }

    /// <summary>True for markup languages, which are scanned as text plus tags rather than as code.</summary>
    public bool Markup { get; init; }

    /// <summary>True when <c>&lt;![CDATA[ ... ]]&gt;</c> sections must be copied verbatim.</summary>
    public bool CdataSections { get; init; }

    /// <summary>Other language identifiers that share this syntax, such as <c>tsx</c> for <c>javascript</c>.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
