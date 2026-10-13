using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class CSharpLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "csharp";

    public override string Description => "C# Minifier (Removes standard/XML comments and collapses blank lines)";

    protected override Regex CommentReplacePattern { get; } = new(@"(?x)
        (
            """"""[\s\S]*?""""""         # 1. Correctly matches raw string literals (including newlines and quotes)
            | @""[^""]*"" (?:""""[^""]*"")* "" # 2. Verbatim strings

            | "" (?: [^""\\] | \\. )* ""       # 3. Regular strings
            | ' (?: [^'\\] | \\. )* '          # 4. Character literals
        )
        | (/\*[\s\S]*?\*/) # Multi-line comments
        | ( // [^\r\n]* )                            # Single-line comments",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));


    private static readonly Regex RegionReplacePattern = new(@"^[ \t]*\#(?:region|endregion).*", RegexOptions.Compiled | RegexOptions.Multiline);

    protected override string MinifyInternal(string sourceCode)
    {
        // Add specific C# rule to drop region headers
        return RegionReplacePattern.Replace(sourceCode, string.Empty);
    }
}
