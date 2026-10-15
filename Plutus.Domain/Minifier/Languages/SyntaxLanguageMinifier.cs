using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// A minifier for a language that has no language-specific hook: everything it does is described by the syntax
/// table in <see cref="LanguageCommentSyntaxRegistry"/>.
/// </summary>
public class SyntaxLanguageMinifier(
    string language,
    string description,
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : BaseLanguageMinifier(options, mode, stripper, observer)
{
    public override string Language => language;

    public override string Description => description;
}
