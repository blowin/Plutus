using System.Text.RegularExpressions;
using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// C# minifier. Comment removal is lexical; the extra hook drops region directives, which are boilerplate rather
/// than comments and are therefore only removed when minifying.
/// </summary>
public sealed class CSharpLanguageMinifier(
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier("csharp", "C# Minifier (Removes standard/XML comments and collapses blank lines)", options, mode, stripper, observer)
{
    private static readonly Regex RegionReplacePattern = new(@"^[ \t]*\#(?:region|endregion).*", RegexOptions.Compiled | RegexOptions.Multiline);

    protected override string MinifyWhitespace(string sourceCode) => RegionReplacePattern.Replace(sourceCode, string.Empty);
}
