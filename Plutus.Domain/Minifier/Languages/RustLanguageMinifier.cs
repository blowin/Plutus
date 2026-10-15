namespace Plutus.Domain.Minifier.Languages;

/// <summary>Rust processor. Block comments nest, and lifetimes are not mistaken for character literals.</summary>
public sealed class RustLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier([LanguageId.Rust], "Rust Minifier (Removes standard/doc comments)", context),
      IMaintainedLanguageProcessor;
