using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class JavaLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "java";
    public override string Description => "Java Minifier (Removes standard block and inline comments)";

    // We reuse the exact same core state machine concept here
    protected override Regex CommentReplacePattern { get; } = new(@"(?x)
            ( "" (?: [^""\\] | \\. )* ""

            | ' (?: [^'\\] | \\. )* ' )
            | ( /\* [^*]* \*+ (?: [^/*] [^*]* \*+ )* / )
            | ( // [^\r\n]* )",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
