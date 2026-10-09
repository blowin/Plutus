using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class PhpLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "php";
    public override string Description => "PHP Minifier (Removes //, # and /*...*/ comments)";

    protected override Regex CommentReplacePattern { get; } = new(@"(?x)
            ( "" (?: [^""\\] | \\. )* ""                 # 1. Double quoted strings

            | ' (?: [^'\\] | \\. )* ' )                  # 2. Single quoted strings
            | ( /\* [^*]* \*+ (?: [^/*] [^*]* \*+ )* / ) # Block comments
            | ( // [^\r\n]* )                            # Line comments type 1
            | ( \# [^\r\n]* )                            # Line comments type 2 (#)",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
