using Plutus.Domain.Comments;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Composes the language processors of a run. Which processor answers for a language is decided by the processors
/// themselves — each declares its languages — so there is no switch over identifiers here and adding a language is
/// a matter of declaring it, not of editing a dispatch table.
/// </summary>
public static class LanguageMinifierCatalog
{
    /// <summary>
    /// The processors that carry language-specific behavior beyond the syntax table. The dependency container
    /// discovers the same set by scanning for <see cref="ILanguageMinifier"/> implementations.
    /// </summary>
    public static IReadOnlyList<ILanguageMinifier> Maintained(SourceProcessingContext? context = null) =>
    [
        new CSharpLanguageMinifier(context),
        new GoLanguageMinifier(context),
        new JavaLanguageMinifier(context),
        new JavaScriptLanguageMinifier(context),
        new PhpLanguageMinifier(context),
        new RustLanguageMinifier(context),
        new SqlLanguageMinifier(context),
        new StyleLanguageMinifier(context),
        new HtmlLanguageMinifier(context),
    ];

    /// <summary>
    /// Fills in every language declared by the syntax registry that no maintained processor claims. The result is
    /// exactly one processor per language, and a language claimed twice is an error rather than a silent winner.
    /// </summary>
    public static IReadOnlyList<ILanguageMinifier> Compose(
        IEnumerable<ILanguageMinifier> maintained,
        SourceProcessingContext? context = null)
    {
        var effective = context ?? SourceProcessingContext.Default;
        var result = new List<ILanguageMinifier>();
        var claimed = new HashSet<LanguageId>();

        foreach (var minifier in maintained)
        {
            foreach (var language in minifier.Languages)
            {
                if (!claimed.Add(language))
                {
                    throw new InvalidOperationException(
                        $"Two language processors claim '{language}': '{minifier.Description}'.");
                }
            }

            result.Add(minifier);
        }

        foreach (var language in LanguageCommentSyntaxRegistry.Languages)
        {
            if (claimed.Add(language))
            {
                result.Add(new SyntaxLanguageMinifier(language, Describe(language), effective));
            }
        }

        return result;
    }

    /// <summary>The complete processor set: maintained processors plus a generic one for every other language.</summary>
    public static IReadOnlyList<ILanguageMinifier> Create(SourceProcessingContext? context = null) =>
        Compose(Maintained(context), context);

    /// <summary>Description shown for a language that has no dedicated processor.</summary>
    public static string Describe(LanguageId language)
    {
        var syntax = LanguageCommentSyntaxRegistry.Find(language);
        var displayName = syntax?.DisplayName ?? language.Value;
        return $"{displayName} processor (lexical comment scanner)";
    }
}
