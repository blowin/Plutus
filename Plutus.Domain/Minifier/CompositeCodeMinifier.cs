using System.Collections.Frozen;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Domain.Minifier;

public sealed class CompositeCodeMinifier(IEnumerable<ILanguageMinifier> minifiers) : ICodeMinifier
{
    private readonly FrozenDictionary<string, ILanguageMinifier> _minifiers = minifiers.ToFrozenDictionary(
            m => m.Language,
            m => m,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => _minifiers.Values;

    public static CompositeCodeMinifier CreateFull()
    {
        var jsMinifier = new JavaScriptLanguageMinifier();
        var styleMinifier = new StyleLanguageMinifier();
        return new CompositeCodeMinifier([
            new CSharpLanguageMinifier(),
            new GoLanguageMinifier(),
            new JavaLanguageMinifier(),
            new PhpLanguageMinifier(),
            new RustLanguageMinifier(),
            new SqlLanguageMinifier(),

            // JavaScript & TypeScript stack registration
            jsMinifier, // Handles "javascript" identifier from MarkdownLanguageProvider
            new AliasLanguageMinifier("typescript", jsMinifier),
            new AliasLanguageMinifier("tsx", jsMinifier),
            new AliasLanguageMinifier("jsx", jsMinifier),

            // Style sheets engine registrations
            styleMinifier, // Handles "css" identifier mapping rules
            new AliasLanguageMinifier("scss", styleMinifier),
            new AliasLanguageMinifier("sass", styleMinifier),
            new AliasLanguageMinifier("less", styleMinifier),

            new HtmlLanguageMinifier(),
        ]);
    }

    public string Minify(string sourceCode, string language)
    {
        if (string.IsNullOrEmpty(sourceCode))
        {
            return sourceCode;
        }

        // If a minifier exists for the given language (e.g., "csharp"), apply it.
        return _minifiers.TryGetValue(language, out var minifier)
            ? minifier.Minify(sourceCode)
            : sourceCode;
    }
}
