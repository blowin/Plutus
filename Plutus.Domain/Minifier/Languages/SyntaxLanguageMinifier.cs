namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// A processor for languages whose behavior is fully described by the comment syntax table: it declares the
/// languages it serves and has no hooks of its own. The catalog creates one per language that no maintained
/// processor claims.
/// </summary>
public class SyntaxLanguageMinifier(
    IReadOnlyCollection<LanguageId> languages,
    string description,
    SourceProcessingContext? context = null) : BaseLanguageMinifier(context)
{
    public SyntaxLanguageMinifier(LanguageId language, string description, SourceProcessingContext? context = null)
        : this([language], description, context)
    {
    }

    public override IReadOnlyCollection<LanguageId> Languages { get; } = languages;

    public override string Description { get; } = description;
}
