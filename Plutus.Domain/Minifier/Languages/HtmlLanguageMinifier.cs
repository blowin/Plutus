using Plutus.Domain.Comments;

namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// Markup minifier. Only markup comments are removed; attribute values are copied verbatim, and the contents of
/// a <c>&lt;script&gt;</c> or <c>&lt;style&gt;</c> block are left alone so a script's own <c>//</c> is not touched.
/// </summary>
public sealed class HtmlLanguageMinifier(
    string language = "html",
    CommentStripOptions? options = null,
    SourceProcessingMode mode = SourceProcessingMode.Minify,
    ICommentStripper? stripper = null,
    ICommentStripObserver? observer = null)
    : SyntaxLanguageMinifier(language, "HTML Markup Minifier (Removes HTML <!-- ... --> comment blocks)", options, mode, stripper, observer);
