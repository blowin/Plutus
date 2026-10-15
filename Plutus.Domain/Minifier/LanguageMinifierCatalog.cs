using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Composes the language processors of a run. Which processor answers for a language is decided by the processors
/// themselves — each declares its languages — so there is no switch over identifiers here and adding a language is
/// a matter of declaring it, not of editing a dispatch table.
/// <para>
/// The catalog is a service: it takes the comment syntax table, which is what tells it which languages still need a
/// generic processor, and the container decides whether the maintained processors or only the discovered ones are
/// used.
/// </para>
/// </summary>
public sealed class LanguageMinifierCatalog(ILanguageCommentSyntaxTable syntax)
{
    /// <summary>
    /// The processors that carry language-specific behavior beyond the syntax table, for a caller without a
    /// container. A run with a container gets the same set discovered by scanning for <see cref="ILanguageMinifier"/>
    /// implementations, so nothing has to be registered here.
    /// </summary>
    public IReadOnlyList<ILanguageMinifier> Maintained(SourceProcessingContext? context = null) =>
    [
        new Languages.CSharpLanguageMinifier(context),
        new Languages.GoLanguageMinifier(context),
        new Languages.JavaLanguageMinifier(context),
        new Languages.JavaScriptLanguageMinifier(context),
        new Languages.PhpLanguageMinifier(context),
        new Languages.RustLanguageMinifier(context),
        new Languages.SqlLanguageMinifier(context),
        new Languages.StyleLanguageMinifier(context),
        new Languages.HtmlLanguageMinifier(context),
    ];

    /// <summary>
    /// Fills in every language declared by the syntax table that no maintained processor claims. The result is
    /// exactly one processor per language, and a language claimed twice is an error rather than a silent winner.
    /// </summary>
    public IReadOnlyList<ILanguageMinifier> Compose(
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

        foreach (var language in syntax.Languages)
        {
            if (claimed.Add(language))
            {
                result.Add(new Languages.SyntaxLanguageMinifier(language, Describe(language), effective));
            }
        }

        return result;
    }

    /// <summary>The complete processor set: maintained processors plus a generic one for every other language.</summary>
    public IReadOnlyList<ILanguageMinifier> Create(SourceProcessingContext? context = null) =>
        Compose(Maintained(context), context);

    /// <summary>Description shown for a language that has no dedicated processor.</summary>
    public string Describe(LanguageId language)
    {
        var displayName = syntax.Find(language)?.DisplayName ?? language.Value;
        return $"{displayName} processor (lexical comment scanner)";
    }
}
