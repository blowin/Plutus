namespace Plutus.Domain.Comments;

/// <summary>
/// The comment and literal delimiters the syntax table and the scanner are written in terms of. Keeping them here
/// means a delimiter is spelled once, so a rule and the scanner that consumes it cannot drift apart.
/// </summary>
internal static class CommentMarkers
{
    // Line comments.
    public const string SlashSlash = "//";
    public const string SlashSlashSlash = "///";
    public const string SlashSlashBang = "//!";
    public const string Hash = "#";
    public const string DashDash = "--";
    public const string Semicolon = ";";
    public const string Apostrophe = "'";

    // Block comments.
    public const string SlashStar = "/*";
    public const string StarSlash = "*/";
    public const string SlashStarStar = "/**";
    public const string SlashStarBang = "/*!";
    public const string ParenthesisStar = "(*";
    public const string StarParenthesis = "*)";
    public const string ParenthesisStarStar = "(**";
    public const string MarkupOpen = "<!--";
    public const string MarkupClose = "-->";
    public const string CdataOpen = "<![CDATA[";
    public const string CdataClose = "]]>";
    public const string PowerShellBlockOpen = "<#";
    public const string PowerShellBlockClose = "#>";
    public const string RubyBlockOpen = "=begin";
    public const string RubyBlockClose = "=end";

    // Literals.
    public const string Quote = "\"";
    public const string Backtick = "`";
    public const string TripleQuote = "\"\"\"";
    public const string TripleApostrophe = "'''";
    public const string VerbatimQuote = "@\"";
    public const string InterpolatedQuote = "$\"";
    public const string InterpolatedVerbatimQuote = "$@\"";
    public const string VerbatimInterpolatedQuote = "@$\"";
    public const string ShellAnsiQuote = "$'";
    public const string ShellLocalizedQuote = "$\"";
    public const string BracketOpen = "[";
    public const string BracketClose = "]";

    // Heredocs and here-strings.
    public const string HeredocMarker = "<<";
    public const string PhpHeredocMarker = "<<<";
    public const string PowerShellHereDoubleOpen = "@\"";
    public const string PowerShellHereDoubleClose = "\"@";
    public const string PowerShellHereSingleOpen = "@'";
    public const string PowerShellHereSingleClose = "'@";

    // Interpolation holes.
    public const string BraceHole = "{";
    public const string TemplateHole = "${";
    public const string SwiftHole = "\\(";
    public const string RubyHole = "#{";
    public const string DollarTag = "$";
}
