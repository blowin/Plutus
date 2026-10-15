namespace Plutus.Domain.Minifier;

/// <summary>
/// Presents another processor under an additional language identifier. The comment syntax registry already maps
/// aliases to a shared description, so this exists for callers that need a distinct processor entry rather than a
/// distinct syntax.
/// </summary>
public sealed class AliasLanguageMinifier(LanguageId alias, ILanguageMinifier original) : ILanguageMinifier
{
    public IReadOnlyCollection<LanguageId> Languages => [alias];

    public string Description => $"{original.Description} (alias for {original.Languages.First()})";

    public string Minify(string sourceCode) => original.Minify(sourceCode, alias);

    public string Minify(string sourceCode, LanguageId language) => original.Minify(sourceCode, language);
}
