namespace Plutus.Domain.Minifier.Languages;

/// <summary>PHP processor, including heredoc and nowdoc bodies.</summary>
public sealed class PhpLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier([LanguageId.Php], "PHP Minifier (Removes //, # and /*...*/ comments)", context),
      IMaintainedLanguageProcessor;
