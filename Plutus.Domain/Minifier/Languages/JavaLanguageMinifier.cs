namespace Plutus.Domain.Minifier.Languages;

/// <summary>Java processor, including Javadoc and text blocks.</summary>
public sealed class JavaLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier([LanguageId.Java], "Java Minifier (Removes standard block and inline comments)", context),
      IMaintainedLanguageProcessor;
