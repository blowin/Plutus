using System.Text.RegularExpressions;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// C# processor. Comment and structural handling come from the shared services; the extra hook drops region
/// directives, which are boilerplate rather than comments and are therefore only removed when minifying.
/// </summary>
public sealed class CSharpLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier(
        [LanguageId.CSharp],
        "C# Minifier (Removes standard/XML comments and collapses blank lines)",
        context),
      IMaintainedLanguageProcessor
{
    private static readonly Regex RegionReplacePattern = new(@"^[ \t]*\#(?:region|endregion).*", RegexOptions.Compiled | RegexOptions.Multiline);

    protected override string MinifyWhitespace(string sourceCode) => RegionReplacePattern.Replace(sourceCode, string.Empty);
}
