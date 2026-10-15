namespace Plutus.Domain.Comments;

/// <summary>
/// Removes comments with a language-aware character scanner. It is <b>not</b> grammar-driven: there is no lexer
/// generator, no token stream and no parser, and the documentation says so. What it does have is an explicit
/// per-language description of comment forms and literal forms
/// (<see cref="LanguageCommentSyntaxRegistry"/>), which is what lets it keep comment-like text inside strings,
/// raw strings, template literals, regex literals and markup attributes.
/// <para>
/// Guarantees, both tested:
/// </para>
/// <list type="bullet">
/// <item><description>Removing a comment never merges the tokens around it: a space is inserted when the removal
/// would touch non-whitespace on both sides.</description></item>
/// <item><description>Line endings survive: terminators inside a removed block comment are re-emitted, and a
/// removed line comment keeps the terminator that ends its line.</description></item>
/// <item><description>A language that is not in the registry is passed through untouched.</description></item>
/// </list>
/// </summary>
public sealed class LexicalCommentStripper : ICommentStripper
{
    public static readonly LexicalCommentStripper Instance = new();

    public CommentStripStrategy Strategy => CommentStripStrategy.Lexical;

    public static bool Supports(LanguageId language) => LanguageCommentSyntaxRegistry.IsSupported(language);

    /// <summary>Boundary convenience for callers that already hold an identifier as text.</summary>
    public static bool Supports(string? language) => LanguageId.TryParse(language, out var id) && Supports(id);

    public CommentStripResult Strip(string source, LanguageId language, CommentStripOptions options)
    {
        if (!LanguageCommentSyntaxRegistry.TryGet(language, out var syntax))
        {
            // No language-specific rules for an unknown language: guessing here is exactly how a '#' comment in a
            // Ruby file or a '%' literal in an Erlang file would be mangled.
            return new CommentStripResult
            {
                Content = source,
                Strategy = Strategy,
                Language = language,
                LanguageSupported = false,
                FallbackReason = $"no comment syntax is maintained for '{language}'",
            };
        }

        if (string.IsNullOrEmpty(source))
        {
            return new CommentStripResult { Content = source, Strategy = Strategy, Language = language };
        }

        var scanner = new CommentScanner(source, syntax, options, language);
        return scanner.Run();
    }

    /// <summary>Boundary convenience for callers that already hold an identifier as text.</summary>
    public CommentStripResult Strip(string source, string? language, CommentStripOptions options) =>
        Strip(source, LanguageId.From(language), options);
}
