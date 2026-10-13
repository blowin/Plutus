using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class GoLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "go";
    public override string Description => "Go Minifier (Removes comments, preserves standard and raw strings)";

    protected override Regex CommentReplacePattern { get; } = new Regex(
        @"(`[\s\S]*?`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')|(/\*[^*]*\*+(?:[^/*][^*]*\*+)*/)|(//[^\x0D\x0A]*)",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
