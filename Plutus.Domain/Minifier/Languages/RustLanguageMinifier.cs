using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class RustLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "rust";
    public override string Description => "Rust Minifier (Removes standard/doc comments)";

    protected override Regex CommentReplacePattern { get; } = new(@"(?x)
            ( "" (?: [^""\\] | \\. )* ""                    # 1. Standard string literals

            | ' (?: [^'\\] | \\. )* ' )                     # 2. Character literals / Lifetimes protection boundary
            | ( /\* [^*]* \*+ (?: [^/*] [^*]* \*+ )* / )    # Block comments
            | ( // [^\r\n]* )                               # Line and Doc comments",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
