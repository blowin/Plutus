namespace Plutus.Domain.Minifier;

/// <summary>
/// Marks a language processor the project maintains. The dependency container registers every implementation of
/// this interface by scanning, and the composition step maps the languages they declare to the processor that
/// serves them — so a language is added by declaring it, not by editing a dispatch table.
/// </summary>
public interface IMaintainedLanguageProcessor : ILanguageMinifier;
