namespace Plutus.Domain.Minifier;

public sealed class AliasLanguageMinifier(string aliasLanguage, ILanguageMinifier originalMinifier) : ILanguageMinifier
{
    public string Language => aliasLanguage;
    public string Description => $"{originalMinifier.Description} (Alias for {originalMinifier.Language})";
    public string Minify(string sourceCode) => originalMinifier.Minify(sourceCode);
}
