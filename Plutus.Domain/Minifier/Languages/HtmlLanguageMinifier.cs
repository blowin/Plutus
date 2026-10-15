namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Markup processor. Only markup comments are removed; attribute values are copied verbatim, and the contents of
/// a <c>&lt;script&gt;</c> or <c>&lt;style&gt;</c> block are left alone so a script's own <c>//</c> is not touched.
/// </summary>
public sealed class HtmlLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier(
        [LanguageId.Html, LanguageId.Htm],
        "HTML Markup Minifier (Removes HTML <!-- ... --> comments)",
        context),
      IMaintainedLanguageProcessor;
