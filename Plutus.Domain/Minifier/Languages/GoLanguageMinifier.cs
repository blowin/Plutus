namespace Plutus.Domain.Minifier.Languages;

/// <summary>Go processor. Go has no distinct documentation comment form, so <c>//</c> is always an ordinary comment.</summary>
public sealed class GoLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier([LanguageId.Go], "Go Minifier (Removes comments, preserves standard and raw strings)", context),
      IMaintainedLanguageProcessor;
