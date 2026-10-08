using System.Collections.Frozen;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Domain.Minifier;

public sealed class CompositeCodeMinifier : ICodeMinifier
{
    private readonly FrozenDictionary<string, ILanguageMinifier> _minifiers;

    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => _minifiers.Values;

    public CompositeCodeMinifier(IEnumerable<ILanguageMinifier> minifiers)
    {
        _minifiers = minifiers.ToFrozenDictionary(
            m => m.Language,
            m => m,
            StringComparer.OrdinalIgnoreCase);
    }

    public static CompositeCodeMinifier CreateFull()
    {
        var jsMinifier = new JavaScriptLanguageMinifier();
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
            new AliasLanguageMinifier("jsx", jsMinifier)
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
