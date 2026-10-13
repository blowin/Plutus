using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

public sealed class HtmlLanguageMinifier : BaseLanguageMinifier
{
    public override string Language => "html";

    public override string Description => "HTML Markup Minifier (Removes HTML <!-- ... --> comment blocks)";

    // Matches standard HTML comment blocks safely across single or multiple lines.
    protected override Regex CommentReplacePattern { get; } = new Regex(
        @"(""[^""]*""|'[^']*')|(<!--[\s\S]*?-->)",
        RegexOptions.Compiled, TimeSpan.FromSeconds(2));
}
