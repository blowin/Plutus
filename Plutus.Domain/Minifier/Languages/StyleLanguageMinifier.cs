namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Style-sheet processor for the whole CSS family. Plain CSS only has <c>/* */</c>; the preprocessor dialects add
/// <c>//</c>, and the per-call language decides which forms apply.
/// </summary>
public sealed class StyleLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier(
        [LanguageId.Css, LanguageId.Scss, LanguageId.Sass, LanguageId.Less],
        "Style Sheets Minifier (Removes CSS/SCSS standard and inline comment blocks)",
        context),
      IMaintainedLanguageProcessor;
