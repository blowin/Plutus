using System.Collections.Frozen;
using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Dispatches source processing by language. A language with no entry is returned unchanged, which is what keeps
/// an unknown language from being stripped with another language's comment rules.
/// </summary>
public sealed class CompositeCodeMinifier(IEnumerable<ILanguageMinifier> minifiers) : ICodeMinifier
{
    private readonly FrozenDictionary<string, ILanguageMinifier> _minifiers = minifiers.ToFrozenDictionary(
            m => m.Language,
            m => m,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ILanguageMinifier> SupportedMinifiers => _minifiers.Values;

    /// <summary>
    /// Builds the full language set. <paramref name="mode"/> decides whether only comments are removed
    /// (<c>--strip-comments</c>) or whitespace is collapsed as well (<c>--minify</c>).
    /// </summary>
    public static CompositeCodeMinifier CreateFull(
        CommentStripOptions? options = null,
        SourceProcessingMode mode = SourceProcessingMode.Minify,
        ICommentStripObserver? observer = null,
        CommentStrippingPlan? plan = null,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<string, ICommentChannelLexer>? channelLexers = null) =>
        new(LanguageMinifierCatalog.Create(options, mode, observer, plan, inventory, channelLexers));

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
