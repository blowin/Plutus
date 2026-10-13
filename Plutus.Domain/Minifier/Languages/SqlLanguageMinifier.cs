using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class SqlLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "sql";
    public override string Description => "SQL Minifier (Removes inline -- and block /*...*/ comments)";

    protected override Regex CommentReplacePattern { get; } = new(@"(?x)
            ( ' (?: [^'\\] | \\. | '' )* ' )             # 1. SQL string literals (handles escaped '')
            | ( /\* [^*]* \*+ (?: [^/*] [^*]* \*+ )* / ) # Block comments
            | ( -- [^\r\n]* )                            # Single-line -- comments",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
