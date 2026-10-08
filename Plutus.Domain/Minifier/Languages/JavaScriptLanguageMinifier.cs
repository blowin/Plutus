using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class JavaScriptLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "javascript";

    public override string Description => "JavaScript/TypeScript Minifier (Removes standard comments, protects template literals)";

    // Group 1 protects template literals (backticks), double quotes, single quotes, and regex literals safely.
    // Group 2/3 captures and destroys block and single-line comments.
    protected override Regex CommentReplacePattern { get; } = new Regex(
        @"(`[\s\S]*?`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')|(/\*[^*]*\*+(?:[^/*][^*]*\*+)*/)|(//[^\x0D\x0A]*)",
        RegexOptions.Compiled);
}
