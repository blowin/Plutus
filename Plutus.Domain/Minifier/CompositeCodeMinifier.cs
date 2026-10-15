using System.Collections.Frozen;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Dispatches source processing by language. The map is built from the languages the processors declare, so a
/// language with no processor is returned unchanged — which is what keeps an unknown language from being stripped
/// with another language's comment rules.
/// </summary>
public sealed class CompositeCodeMinifier : ICodeMinifier
{
    private readonly FrozenDictionary<LanguageId, ILanguageMinifier> _minifiers;

    public CompositeCodeMinifier(IEnumerable<ILanguageMinifier> minifiers)
    {
        var map = new Dictionary<LanguageId, ILanguageMinifier>();
        foreach (var minifier in minifiers)
        {
            foreach (var language in minifier.Languages)
            {
                if (!map.TryAdd(language, minifier))
                {
                    throw new ArgumentException(
                        $"Two language processors claim '{language}': '{minifier.Description}'.",
                        nameof(minifiers));
                }
            }
        }

        _minifiers = map.ToFrozenDictionary();
    }

    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => _minifiers.Values.Distinct().ToList();

    /// <summary>The languages this minifier can dispatch, which is the keys of the composition.</summary>
    public IReadOnlyCollection<LanguageId> SupportedLanguages => _minifiers.Keys;

    public string Minify(string sourceCode, LanguageId language)
    {
        if (string.IsNullOrEmpty(sourceCode))
        {
            return sourceCode;
        }

        return _minifiers.TryGetValue(language, out var minifier)
            ? minifier.Minify(sourceCode, language)
            : sourceCode;
    }

    public string Minify(string sourceCode, string? language) => Minify(sourceCode, LanguageId.From(language));
}
