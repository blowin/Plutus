using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class StyleLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "css";

    public override string Description => "Style Sheets Minifier (Removes CSS/SCSS standard and inline comment blocks)";

    // Group 1: Protects single-quoted and double-quoted strings (crucial for font-families and url() assets)
    // Group 2: Identifies and strips standard block style comments /* ... */
    // Group 3: Identifies and strips modern preprocessor inline comments // ...
    protected override Regex CommentReplacePattern { get; } = new Regex(
        @"(""[^""]*""|'[^']*')|(/\*[^*]*\*+(?:[^/*][^*]*\*+)*/)|(//[^\x0D\x0A]*)",
        RegexOptions.Compiled);
}
